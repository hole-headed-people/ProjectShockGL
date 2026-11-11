using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using ProjectShockGL.Core;
using ProjectShockGL.Tools;

namespace ProjectShockGL;

internal sealed class BepuSimulation : IDisposable
{
    private const float Step = 1f / 60f;
    private const int MaxSteps = 8;
    
    private readonly BufferPool _pool = new();
    private readonly Simulation _simulation;
    
    private readonly Dictionary<Collider, BodyEntry> _entries = [];
    private readonly Dictionary<CollidableReference, Collider> _colliders = [];
    
    private float _elapsedTime;
    
    private int _dynamicCount;

    private readonly record struct ShapeSettings(Vector3 Dimensions, Quaternion LocalOrientation);

    private sealed class BodyEntry(CollidableReference reference, TypedIndex shape, ShapeSettings settings, RigidPose pose,
        Rigidbody? body, float mass)
    {
        public CollidableReference Reference { get; } = reference;
        public TypedIndex Shape { get; } = shape;
        public ShapeSettings Settings { get; } = settings;
        public RigidPose Pose { get; set; } = pose;
        public Rigidbody? Body { get; } = body;
        public float Mass { get; } = mass;
    }

    public bool HasDynamicBodies => _dynamicCount > 0;

    public BepuSimulation()
    {
        _simulation = Simulation.Create(_pool, new Contacts(_colliders), new Gravity(), new SolveDescription(8, 1));
    }

    private static bool IsSolid(Collider collider)
    {
        return collider.IsEnabled && !collider.IsDestroyed && !collider.IsTrigger && collider.Owner.IsActive && !collider.Owner.IsDestroyed;
    }

    public static bool IsDynamic(Collider collider)
    {
        return collider is BoxCollider or SphereCollider or CapsuleCollider &&
               !collider.Owner.IsStatic && IsSolid(collider) && GetBody(collider) is { IsEnabled: true };
    }

    private static Rigidbody? GetBody(Collider collider)
    {
        return collider.Owner.Collider == collider && collider.Owner.GetComponent<Rigidbody>() is { IsDestroyed: false } body
            ? body
            : null;
    }

    public void Update(IEnumerable<Collider> colliders, float deltaSeconds)
    {
        foreach (var collider in colliders)
        {
            Synchronize(collider);
        }

        if (!HasDynamicBodies)
        {
            return;
        }

        _elapsedTime = MathF.Min(_elapsedTime + deltaSeconds, MaxSteps * Step);
        
        for (var i = 0; i < MaxSteps && _elapsedTime >= Step; i++)
        {
            _simulation.Timestep(Step);
            _elapsedTime -= Step;
        }

        foreach (var (collider, entry) in _entries)
        {
            if (entry.Body is not { } body)
            {
                continue;
            }

            var reference = _simulation.Bodies[entry.Reference.BodyHandle];
            var pose = reference.Pose;
            
            body.LinearVelocity = reference.Velocity.Linear;
            body.AngularVelocity = reference.Velocity.Angular;
            
            var transform = collider.Owner.Transform;
            var rotation = Quaternion.Normalize(pose.Orientation * Quaternion.Conjugate(entry.Settings.LocalOrientation));
            
            transform.Rotation = rotation;
            transform.Position = pose.Position - Vector3.Transform(collider.Center * transform.Scale, rotation);
            
            entry.Pose = new RigidPose(collider.GetWorldCenter(), transform.Rotation);
        }
    }

    private void Synchronize(Collider collider)
    {
        var body = GetBody(collider);
        var dynamicBody = !collider.Owner.IsStatic && body is { IsEnabled: true } ? body : null;
        
        var center = collider.GetWorldCenter();
        
        var rotation = collider.Owner.Transform.Rotation;
        var rotationLength = rotation.LengthSquared();
        
        if (!IsSolid(collider) || (!collider.Owner.IsStatic && body == null) ||
            !TryGetShapeSettings(collider, out var settings) ||
            !PhysicsHelper.IsFinite(center) || !float.IsFinite(rotationLength) || rotationLength < 1e-8f)
        {
            Remove(collider);
            
            return;
        }

        var mass = dynamicBody?.Mass ?? 1f;
        if (_entries.TryGetValue(collider, out var entry) && entry.Body == dynamicBody && entry.Mass == mass &&
            entry.Settings == settings && entry.Pose.Position == center && entry.Pose.Orientation == rotation)
        {
            if (dynamicBody != null)
            {
                var existing = _simulation.Bodies[entry.Reference.BodyHandle];
                if (existing.Velocity.Linear != dynamicBody.LinearVelocity ||
                    existing.Velocity.Angular != dynamicBody.AngularVelocity)
                {
                    existing.Awake = true;
                    existing.Velocity = new BodyVelocity(dynamicBody.LinearVelocity, dynamicBody.AngularVelocity);
                }
            }

            return;
        }

        var velocity = dynamicBody == null
            ? default
            : new BodyVelocity(dynamicBody.LinearVelocity, dynamicBody.AngularVelocity);
        Remove(collider);
        
        var pose = new RigidPose(center, Quaternion.Normalize(rotation * settings.LocalOrientation));
        var size = settings.Dimensions;
        
        var (reference, shape) = collider switch
        {
            BoxCollider => Add(new Box(size.X, size.Y, size.Z), pose, velocity, dynamicBody),
            SphereCollider => Add(new Sphere(size.X), pose, velocity, dynamicBody),
            CapsuleCollider => Add(new Capsule(size.X, size.Y), pose, velocity, dynamicBody),
            _ => throw new InvalidOperationException("Unsupported Bepu shape.")
        };

        _entries.Add(collider, new BodyEntry(reference, shape, settings, new RigidPose(center, rotation), dynamicBody, mass));
        _colliders.Add(reference, collider);
    }

    internal static BodyVelocity CalculatePointImpulse(Rigidbody body, Vector3 impulse, Vector3 worldPoint)
    {
        ObjectDisposedException.ThrowIf(body.IsDestroyed, body);
        if (body.Owner?.Collider is not { IsDestroyed: false } collider || !TryGetShapeSettings(collider, out var settings))
        {
            throw new InvalidOperationException("A point impulse requires a valid box, sphere or capsule collider.");
        }

        var center = collider.GetWorldCenter();
        var rotation = collider.Owner.Transform.Rotation;
        var rotationLength = rotation.LengthSquared();
        if (!PhysicsHelper.IsFinite(center) || !float.IsFinite(rotationLength) || rotationLength < 1e-8f)
        {
            throw new InvalidOperationException("A point impulse requires a valid collider pose.");
        }

        var pose = new RigidPose(center, Quaternion.Normalize(rotation * settings.LocalOrientation));
        var size = settings.Dimensions;
        var inertia = collider switch
        {
            BoxCollider => new Box(size.X, size.Y, size.Z).ComputeInertia(body.Mass),
            SphereCollider => new Sphere(size.X).ComputeInertia(body.Mass),
            CapsuleCollider => new Capsule(size.X, size.Y).ComputeInertia(body.Mass),
            _ => throw new InvalidOperationException("Unsupported Bepu shape.")
        };

        var velocity = new BodyVelocity(body.LinearVelocity, body.AngularVelocity);
        BodyReference.ApplyImpulse(impulse, worldPoint - center, ref inertia, ref pose, ref velocity);
        return velocity;
    }

    private static bool TryGetShapeSettings(Collider collider, out ShapeSettings settings)
    {
        settings = default;
        
        switch (collider)
        {
            case BoxCollider box:
                var size = 2f * box.WorldHalfExtents;
                settings = new ShapeSettings(size, Quaternion.Identity);
                
                return PhysicsHelper.IsFinite(size) && size.X > 0f && size.Y > 0f && size.Z > 0f;

            case SphereCollider sphere:
                var radius = sphere.WorldRadius;
                settings = new ShapeSettings(new Vector3(radius, 0f, 0f), Quaternion.Identity);
                
                return float.IsFinite(radius) && radius > 0f;

            case CapsuleCollider capsule:
                var capsuleRadius = capsule.WorldRadius;
                var length = capsule.WorldHeight - 2f * capsuleRadius;
                settings = new ShapeSettings(new Vector3(capsuleRadius, length, 0f), AlignCapsule(capsule.GetScaledLocalAxis()));
                
                return float.IsFinite(capsuleRadius) && capsuleRadius > 0f && float.IsFinite(length) && length >= 0f;

            default:
                return false;
        }
    }

    private static Quaternion AlignCapsule(Vector3 axis)
    {
        var cross = Vector3.Cross(Vector3.UnitY, axis);
        var sine = cross.Length();
        
        if (sine > 1e-8f)
        {
            return Quaternion.CreateFromAxisAngle(cross / sine, MathF.Atan2(sine, axis.Y));
        }

        return axis.Y < 0f ? new Quaternion(1f, 0f, 0f, 0f) : Quaternion.Identity;
    }

    private (CollidableReference Reference, TypedIndex Shape) Add<T>(T convex, RigidPose pose, BodyVelocity velocity,
        Rigidbody? body)
        where T : unmanaged, IConvexShape
    {
        var shape = _simulation.Shapes.Add(convex);
        CollidableReference reference;
        
        if (body != null)
        {
            var description = BodyDescription.CreateDynamic(pose, velocity, convex.ComputeInertia(body.Mass),
                new CollidableDescription(shape, 0.1f), new BodyActivityDescription(0.01f));
            
            reference = new CollidableReference(CollidableMobility.Dynamic, _simulation.Bodies.Add(description));
            
            _dynamicCount++;
        }
        else
        {
            reference = new CollidableReference(_simulation.Statics.Add(new StaticDescription(pose, shape)));
        }

        return (reference, shape);
    }

    public void Remove(Collider collider)
    {
        if (!_entries.Remove(collider, out var entry))
        {
            return;
        }

        if (entry.Body != null)
        {
            _simulation.Bodies.Remove(entry.Reference.BodyHandle);
            _dynamicCount--;
        }
        else
        {
            _simulation.Statics.Remove(entry.Reference.StaticHandle);
        }

        _colliders.Remove(entry.Reference);
        _simulation.Shapes.Remove(entry.Shape);
    }

    public void Dispose()
    {
        _simulation.Dispose();
        _pool.Clear();
    }

    private readonly struct Contacts(Dictionary<CollidableReference, Collider> colliders) : INarrowPhaseCallbacks
    {
        public void Initialize(Simulation simulation) { }
        public void Dispose() { }

        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
        {
            var first = colliders[a].Owner;
            var second = colliders[b].Owner;
            return first != second && (first.CollidesWith & second.Layer) != 0 && (second.CollidesWith & first.Layer) != 0;
        }

        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;

        public bool ConfigureContactManifold<T>(int workerIndex, CollidablePair pair, ref T manifold,
            out PairMaterialProperties material) where T : unmanaged, IContactManifold<T>
        {
            material = new PairMaterialProperties(0.8f, 2f, new SpringSettings(30f, 1f));
            return true;
        }

        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB,
            ref ConvexContactManifold manifold) => true;
    }

    private struct Gravity : IPoseIntegratorCallbacks
    {
        private Vector3Wide _gravityStep;

        public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
        
        public bool AllowSubstepsForUnconstrainedBodies => false;
        public bool IntegrateVelocityForKinematics => false;
        
        public void Initialize(Simulation simulation) { }

        public void PrepareForIntegration(float dt)
        {
            _gravityStep = Vector3Wide.Broadcast(new Vector3(0f, -9.81f * dt, 0f));
        }

        public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation,
            BodyInertiaWide inertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
        {
            velocity.Linear += _gravityStep;
        }
    }
}
