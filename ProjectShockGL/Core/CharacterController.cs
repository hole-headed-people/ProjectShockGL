using System.Numerics;
using ProjectShockGL.Tools;

namespace ProjectShockGL.Core;

[Flags]
public enum CollisionFlags
{
    None = 0,
    Sides = 1,
    Above = 2,
    Below = 4
}

public class CharacterController : CapsuleCollider
{
    private float _skinWidth = 0.01f;
    private float _stepOffset = 0.3f;
    private float _slopeLimit = 45f;
    
    private Vector3? _stairSupportPosition;
    
    private const int MaxSubsteps = 4096;

    public float SkinWidth
    {
        get => _skinWidth;
        set => _skinWidth = NonNegative(value, nameof(SkinWidth));
    }
    
    public float StepOffset
    {
        get => _stepOffset;
        set => _stepOffset = NonNegative(value, nameof(StepOffset));
    }

    public float SlopeLimit
    {
        get => _slopeLimit;
        set
        {
            if (!float.IsFinite(value) || value < 0 || value > 89)
                throw new ArgumentOutOfRangeException(nameof(SlopeLimit));
            _slopeLimit = value;
        }
    }

    public int CollisionMask { get; set; } = Layers.Everything;
    public CollisionFlags CollisionFlags { get; private set; }
    public bool IsGrounded { get; private set; }
    public Vector3 GroundNormal { get; private set; }
    public Vector3 LastDisplacement { get; private set; }
    
    internal void ResolveOverlaps(IEnumerable<Collider> colliders)
    {
        if (IsTrigger || IsDestroyed || !IsEnabled || Parent.IsDestroyed || !Parent.IsActive)
        {
            return;
        }
        
        var radius = WorldRadius;

        if (!float.IsFinite(radius) || radius <= 0f)
        {
            return;
        }
        
        radius -= MathF.Min(SkinWidth, radius * 0.1f);
        
        var obstacles = colliders.Where(other => other.Parent != Parent && !other.IsTrigger &&
            (CollisionMask & Parent.CollidesWith & other.Parent.Layer) != 0 &&
            (other.Parent.CollidesWith & Parent.Layer) != 0).ToArray();
        
        var margin = MathF.Max(radius * 0.0001f, 0.000001f);
        var corrected = false;
        
        for (var iteration = 0; iteration < 8; iteration++)
        {
            var moved = false;
            
            foreach (var obstacle in obstacles)
            {
                var (a, b) = GetWorldSegment();
                
                if (!PhysicsHelper.IsFinite(a) || !PhysicsHelper.IsFinite(b) ||
                    !PhysicsHelper.CapsuleIntersectsCollider(a, b, radius, obstacle))
                {
                    continue;
                }
                
                var bounds = obstacle.GetWorldBounds();
                var limit = Vector3.Distance(Vector3.Min(Vector3.Min(a, b), bounds.min),
                    Vector3.Max(Vector3.Max(a, b), bounds.max)) + radius * 2;

                if (!float.IsFinite(limit) || limit <= 0)
                {
                    continue;
                }
                
                var directions = new List<Vector3> { PhysicsHelper.CapsuleContactNormal(a, b, obstacle) };
                var rotation = obstacle is BoxCollider ? obstacle.Parent.Transform.Rotation : Quaternion.Identity;
                
                foreach (var axis in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
                {
                    var direction = Vector3.Transform(axis, rotation);
                    
                    directions.Add(direction);
                    directions.Add(-direction);
                }
                
                var best = float.PositiveInfinity;
                var correction = Vector3.Zero;
                
                foreach (var candidate in directions)
                {
                    if (!PhysicsHelper.TryNormalize(candidate, out var direction))
                    {
                        continue;
                    }

                    if (PhysicsHelper.CapsuleIntersectsCollider(a + direction * limit, b + direction * limit, radius,
                            obstacle))
                    {
                        continue;
                    }
                    
                    var low = 0f;
                    var high = limit;
                    
                    for (var j = 0; j < 24; j++)
                    {
                        var mid = (low + high) * 0.5f;
                        
                        if (PhysicsHelper.CapsuleIntersectsCollider(a + direction * mid, b + direction * mid, radius,
                                obstacle))
                        {
                            low = mid;
                        }

                        else
                        {
                            high = mid;
                        }
                    }

                    if (high >= best)
                    {
                        continue;
                    }
                    
                    best = high;
                    correction = direction * (high + margin);
                }

                if (!float.IsFinite(best))
                {
                    continue;
                }
                
                Parent.Transform.Position += correction;
                
                _stairSupportPosition = null;
                
                moved = true;
                corrected = true;
            }

            if (!moved)
            {
                break;
            }
        }
        
        if (corrected)
        {
            var displacement = LastDisplacement;
            
            Move(Vector3.Zero);
            LastDisplacement = displacement;
        }
    }

    public CollisionFlags Move(Vector3 displacement)
    {
        if (!PhysicsHelper.IsFinite(displacement))
        {
            throw new ArgumentOutOfRangeException(nameof(displacement));
        }

        CollisionFlags = CollisionFlags.None;
        LastDisplacement = Vector3.Zero;
        IsGrounded = false;
        GroundNormal = Vector3.Zero;
        
        if (IsDestroyed || Parent.IsDestroyed || !IsEnabled || !Parent.IsActive)
        {
            _stairSupportPosition = null;
            
            return CollisionFlags;
        }

        if (IsTrigger)
        {
            throw new InvalidOperationException("A CharacterController must be a solid capsule.");
        }

        var radius = WorldRadius;
        var (a, b) = GetWorldSegment();
        var start = Parent.Transform.Position;
        var allowStairEdge = displacement.Y <= 0 && _stairSupportPosition == start;
        
        _stairSupportPosition = null;

        if (!float.IsFinite(radius) || radius <= 0 || !PhysicsHelper.IsFinite(a) ||
            !PhysicsHelper.IsFinite(b) || !PhysicsHelper.IsFinite(start + displacement))
        {
            throw new InvalidOperationException("CharacterController requires a finite transform and positive world radius.");
        }

        var skin = MathF.Min(SkinWidth, radius * 0.1f);
        var queryRadius = radius - skin;
        var stride = queryRadius * 0.25f;
        var margin = MathF.Max(radius * 0.0001f, 0.000001f);
        var probe = MathF.Max(skin * 2, radius * 0.001f);
        var slopeCos = MathF.Cos(SlopeLimit * MathF.PI / 180);
        var maxComponent = Math.Max(Math.Abs((double)displacement.X),
            Math.Max(Math.Abs((double)displacement.Y), Math.Abs((double)displacement.Z)));
        
        var count = Math.Max(1, Math.Ceiling(maxComponent / stride));

        if (count > MaxSubsteps)
        {
            throw new ArgumentOutOfRangeException(nameof(displacement), "Move is too large; split it across updates or teleport explicitly.");
        }

        var filter = new PhysicsQueryFilter { IgnoreObject = Parent, IgnoreTriggers = true };
        var mask = CollisionMask & Parent.CollidesWith;
        var offset = Vector3.Zero;
        var step = displacement / (int)count;

        for (var i = 0; i < (int)count; i++)
        {
            var supported = displacement.Y <= 0 && GroundAt(offset, probe, out _, out _);
            
            if (step.Y != 0)
            {
                var clear = Travel(offset, new Vector3(0, step.Y, 0), out offset, out var normal);
                
                if (!clear)
                {
                    CollisionFlags |= step.Y > 0 ? CollisionFlags.Above :
                        Walkable(normal) ? CollisionFlags.Below : CollisionFlags.Sides;
                }
            }
            
            MoveHorizontal(new Vector3(step.X, 0, 0));
            MoveHorizontal(new Vector3(0, 0, step.Z));
            
            if (supported && displacement.Y <= 0 && (step.X != 0 || step.Z != 0) &&
                GroundAt(offset, MathF.Max(StepOffset, probe), out var landing, out _, true))
            {
                offset = landing;
                allowStairEdge = true;
            }
        }

        var ground = Vector3.Zero;
        
        IsGrounded = displacement.Y <= 0 && GroundAt(offset, probe, out _, out ground);
        
        if (IsGrounded)
        {
            GroundNormal = ground;
        }
        
        Parent.Transform.Position = start + offset;

        if (allowStairEdge && IsGrounded)
        {
            _stairSupportPosition = Parent.Transform.Position;
        }
        
        LastDisplacement = offset;
        
        return CollisionFlags;

        bool Walkable(Vector3 normal)
        {
            return normal.Y > 0 && normal.Y + 0.00001f >= slopeCos;
        }

        bool Blocked(Vector3 at, out Collider? hit)
        {
            return Physics.OverlapCapsule(a + at, b + at, queryRadius, mask, out hit, filter);
        }
        
        bool Travel(Vector3 from, Vector3 movement, out Vector3 destination, out Vector3 normal)
        {
            destination = from;
            normal = Vector3.Zero;
            
            var length = movement.Length();

            if (length == 0)
            {
                return !Blocked(from, out _);
            }
            
            var segments = Math.Max(1, Math.Ceiling((double)length / stride));

            if (segments > MaxSubsteps || !float.IsFinite(length))
            {
                return false;
            }
            
            var delta = movement / (int)segments;
            
            for (var n = 0; n < (int)segments; n++)
            {
                var next = from + movement * ((n + 1f) / (int)segments);
                
                if (!Blocked(next, out var obstacle))
                {
                    destination = next;
                    
                    continue;
                }

                if (Blocked(destination, out _))
                {
                    return false;
                }
                
                var low = 0f;
                var high = 1f;
                
                for (var j = 0; j < 20; j++)
                {
                    var mid = (low + high) * 0.5f;
                    
                    if (Blocked(destination + delta * mid, out var candidate))
                    {
                        high = mid;
                        obstacle = candidate;
                    }
                    else
                    {
                        low = mid;
                    }
                }
                
                var contact = destination + delta * high;

                if (obstacle != null)
                {
                    normal = PhysicsHelper.CapsuleContactNormal(a + contact, b + contact, obstacle);
                }
                
                destination += delta * MathF.Max(0, low - margin / delta.Length());
                
                return false;
            }
            
            return true;
        }

        bool GroundAt(Vector3 at, float distance, out Vector3 landing, out Vector3 normal, bool stairLanding = false)
        {
            var clear = Travel(at, -Vector3.UnitY * distance, out landing, out normal);

            if ((allowStairEdge || stairLanding) && !clear && normal.Y > 0 && !Walkable(normal) && StepOffset > 0 &&
                Blocked(landing - Vector3.UnitY * (margin * 3), out var obstacle) &&
                obstacle is BoxCollider box &&
                PhysicsHelper.TryGetStairSupport(a + landing, b + landing, queryRadius, box,
                    StepOffset, out var treadNormal) && Walkable(treadNormal))
            {
                normal = treadNormal;
            }
            
            return !clear && Walkable(normal);
        }

        void MoveHorizontal(Vector3 movement)
        {
            if (movement == Vector3.Zero)
            {
                return;
            }
            
            var original = offset;
            var supported = displacement.Y <= 0 && GroundAt(original, probe, out _, out _);
            
            if (Travel(original, movement, out var stopped, out var normal))
            {
                offset = stopped;
                
                return;
            }
            
            if (supported && Walkable(normal))
            {
                var rise = -Vector3.Dot(movement, normal) / normal.Y;
                
                if (rise > 0 && Travel(original, Vector3.UnitY * (rise + margin * 2), out var raised, out _) &&
                    Travel(raised, movement, out var forward, out _) &&
                    GroundAt(forward, rise + probe + margin * 2, out var landing, out _))
                {
                    offset = landing;
                    
                    CollisionFlags |= CollisionFlags.Below;
                    
                    return;
                }
            }
            
            if (supported && StepOffset > 0 &&
                Travel(original, Vector3.UnitY * StepOffset, out var above, out _) &&
                Travel(above, movement, out var across, out _) &&
                GroundAt(across, StepOffset + probe, out var stair, out _, true) &&
                stair.Y > original.Y + margin && stair.Y - original.Y <= StepOffset + margin)
            {
                offset = stair;
                allowStairEdge = true;
                
                CollisionFlags |= CollisionFlags.Below;
                
                return;
            }

            offset = stopped;
            
            CollisionFlags |= CollisionFlags.Sides;
        }
    }
}
