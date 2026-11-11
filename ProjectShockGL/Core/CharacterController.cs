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
        if (IsTrigger || IsDestroyed || !IsEnabled || Owner.IsDestroyed || !Owner.IsActive)
        {
            return;
        }
        
        var radius = WorldRadius;

        if (!float.IsFinite(radius) || radius <= 0f)
        {
            return;
        }
        
        radius -= MathF.Min(SkinWidth, radius * 0.1f);
        
        var (a, b) = GetWorldSegment();
        if (!PhysicsHelper.IsFinite(a) || !PhysicsHelper.IsFinite(b))
        {
            return;
        }

        var obstacles = colliders.Where(other => other.Owner != Owner && !other.IsTrigger &&
            (CollisionMask & Owner.CollidesWith & other.Owner.Layer) != 0 &&
            (other.Owner.CollidesWith & Owner.Layer) != 0)
            .Select(other => (Collider: other, Bounds: other.GetWorldBounds())).ToArray();
        var overlaps = obstacles.Where(other => Intersects(other, Vector3.Zero)).ToArray();
        if (overlaps.Length == 0)
        {
            return;
        }

        var directions = new List<Vector3>();
        var min = Vector3.Min(a, b) - new Vector3(radius);
        var max = Vector3.Max(a, b) + new Vector3(radius);
        foreach (var obstacle in overlaps)
        {
            min = Vector3.Min(min, obstacle.Bounds.min);
            max = Vector3.Max(max, obstacle.Bounds.max);
            AddDirection(PhysicsHelper.CapsuleContactNormal(a, b, obstacle.Collider));
            var rotation = obstacle.Collider is BoxCollider
                ? obstacle.Collider.Owner.Transform.Rotation : Quaternion.Identity;
            foreach (var axis in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
            {
                var direction = Vector3.Transform(axis, rotation);
                AddDirection(direction);
                AddDirection(-direction);
            }
        }

        var limit = Vector3.Distance(min, max) + radius * 2f;
        if (!float.IsFinite(limit))
        {
            return;
        }

        var overlapSet = overlaps.Select(other => other.Collider).ToHashSet();
        var margin = MathF.Max(radius * 0.0001f, 0.000001f);
        var best = float.PositiveInfinity;
        var correction = Vector3.Zero;
        foreach (var direction in directions)
        {
            var low = 0f;
            var high = limit;
            if (OverlapsAt(direction * high))
            {
                continue;
            }

            // All these convex obstacles overlap the starting capsule, so their union
            // along this ray ends at the last exit. Search for that shared exit.
            for (var step = 0; step < 24; step++)
            {
                var mid = (low + high) * 0.5f;
                if (OverlapsAt(direction * mid))
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            var distance = high + margin;
            var candidate = direction * distance;
            if (distance >= best || !ClearPath(candidate))
            {
                continue;
            }

            best = distance;
            correction = candidate;
        }

        if (!float.IsFinite(best))
        {
            return;
        }

        Owner.Transform.Position += correction;
        _stairSupportPosition = null;
        var displacement = LastDisplacement;
        Move(Vector3.Zero);
        LastDisplacement = displacement;

        bool OverlapsAt(Vector3 offset)
        {
            foreach (var obstacle in overlaps)
            {
                if (Intersects(obstacle, offset))
                {
                    return true;
                }
            }

            return false;
        }

        bool Intersects((Collider Collider, (Vector3 min, Vector3 max) Bounds) obstacle, Vector3 offset)
        {
            var bounds = (Vector3.Min(a, b) + offset - new Vector3(radius),
                Vector3.Max(a, b) + offset + new Vector3(radius));
            return PhysicsHelper.AabbOverlap(bounds, obstacle.Bounds) &&
                PhysicsHelper.CapsuleIntersectsCollider(a + offset, b + offset, radius, obstacle.Collider);
        }

        void AddDirection(Vector3 candidate)
        {
            if (PhysicsHelper.TryNormalize(candidate, out var direction) &&
                !directions.Any(existing => Vector3.Dot(existing, direction) > 0.99999f))
            {
                directions.Add(direction);
            }
        }

        bool ClearPath(Vector3 offset)
        {
            var sweptBounds = (Vector3.Min(Vector3.Min(a, b), Vector3.Min(a, b) + offset) - new Vector3(radius),
                Vector3.Max(Vector3.Max(a, b), Vector3.Max(a, b) + offset) + new Vector3(radius));
            var blockers = obstacles.Where(other => !overlapSet.Contains(other.Collider) &&
                PhysicsHelper.AabbOverlap(sweptBounds, other.Bounds)).ToArray();
            if (blockers.Length == 0)
            {
                return true;
            }

            var steps = Math.Ceiling(offset.Length() / (radius * 0.25f));
            if (steps > MaxSubsteps)
            {
                return false;
            }

            for (var step = 1; step <= (int)steps; step++)
            {
                var at = offset * (step / (float)steps);
                foreach (var obstacle in blockers)
                {
                    if (Intersects(obstacle, at))
                    {
                        return false;
                    }
                }
            }

            return true;
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
        
        if (IsDestroyed || Owner.IsDestroyed || !IsEnabled || !Owner.IsActive)
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
        var start = Owner.Transform.Position;
        var allowStairEdge = displacement.Y <= 0f && _stairSupportPosition == start;
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

        var filter = new PhysicsQueryFilter { IgnoreObject = Owner, IgnoreTriggers = true };
        var mask = CollisionMask & Owner.CollidesWith;
        var searchMin = Vector3.Min(a, b) + Vector3.Min(displacement, Vector3.Zero) - new Vector3(queryRadius);
        var searchMax = Vector3.Max(a, b) + Vector3.Max(displacement, Vector3.Zero) + new Vector3(queryRadius);
        // Stair and slope probes can move vertically beyond the requested displacement.
        searchMin.Y = float.NegativeInfinity;
        searchMax.Y = float.PositiveInfinity;
        var candidates = Physics.GetQueryCandidates((searchMin, searchMax), mask, filter);
        var offset = Vector3.Zero;
        var step = displacement / (int)count;
        var movesHorizontally = step.X != 0f || step.Z != 0f;

        for (var i = 0; i < (int)count; i++)
        {
            var previousOffset = offset;
            var supported = movesHorizontally && displacement.Y <= 0 && GroundAt(offset, probe, out _, out _);
            
            if (step.Y != 0)
            {
                var clear = Travel(offset, new Vector3(0, step.Y, 0), out offset, out var normal);
                
                if (!clear)
                {
                    CollisionFlags |= step.Y > 0 ? CollisionFlags.Above :
                        Walkable(normal) ? CollisionFlags.Below : CollisionFlags.Sides;

                    if (!movesHorizontally)
                    {
                        break;
                    }
                }
            }
            
            MoveHorizontal(new Vector3(step.X, 0, 0));
            MoveHorizontal(new Vector3(0, 0, step.Z));
            
            if (supported &&
                GroundAt(offset, MathF.Max(StepOffset, probe), out var landing, out _, true))
            {
                offset = landing;
                allowStairEdge = true;
            }

            if (offset == previousOffset)
            {
                break;
            }
        }

        var ground = Vector3.Zero;
        
        IsGrounded = displacement.Y <= 0 && GroundAt(offset, probe, out _, out ground);
        
        if (IsGrounded)
        {
            GroundNormal = ground;
            if (allowStairEdge || displacement.Y < 0f)
            {
                _stairSupportPosition = start + offset;
            }
        }
        
        Owner.Transform.Position = start + offset;
        
        LastDisplacement = offset;
        
        return CollisionFlags;

        bool Walkable(Vector3 normal)
        {
            return normal.Y > 0 && normal.Y + 0.00001f >= slopeCos;
        }

        bool Blocked(Vector3 at, out Collider? hit)
        {
            var bounds = (Vector3.Min(a, b) + at - new Vector3(queryRadius),
                Vector3.Max(a, b) + at + new Vector3(queryRadius));
            foreach (var candidate in candidates)
            {
                if (PhysicsHelper.AabbOverlap(bounds, candidate.Bounds) &&
                    PhysicsHelper.CapsuleIntersectsCollider(a + at, b + at, queryRadius, candidate.Collider))
                {
                    hit = candidate.Collider;
                    return true;
                }
            }

            hit = null;
            return false;
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
                
                var precision = margin / (8f * delta.Length());
                for (var j = 0; j < 20 && high - low > precision; j++)
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

            if ((allowStairEdge || stairLanding || displacement.Y < 0f) &&
                !clear && normal.Y > 0 && !Walkable(normal) && StepOffset > 0 &&
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
            if (Travel(original, movement, out var stopped, out var normal))
            {
                offset = stopped;
                
                return;
            }

            var supported = displacement.Y <= 0 && GroundAt(original, probe, out _, out _);
            
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
