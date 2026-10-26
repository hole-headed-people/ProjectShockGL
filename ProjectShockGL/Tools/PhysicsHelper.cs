using System.Numerics;
using ProjectShockGL.Core;

namespace ProjectShockGL.Tools;

public static class PhysicsHelper
{
    private readonly record struct DVector(double X, double Y, double Z)
    {
        public DVector(Vector3 value) : this(value.X, value.Y, value.Z) { }
        public double LengthSquared => Dot(this, this);
        public double At(int axis) => axis == 0 ? X : axis == 1 ? Y : Z;
        public Vector3 ToVector() => new((float)X, (float)Y, (float)Z);
        public static double Dot(DVector a, DVector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static DVector operator +(DVector a, DVector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static DVector operator -(DVector a, DVector b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static DVector operator *(DVector value, double factor) => new(value.X * factor, value.Y * factor, value.Z * factor);
        public static DVector operator /(DVector value, double factor) => value * (1 / factor);
    }

    public static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    public static bool TryNormalize(Vector3 value, out Vector3 normalized)
    {
        normalized = default;

        if (!IsFinite(value))
        {
            return false;
        }
        
        var vector = new DVector(value);

        if (vector.LengthSquared == 0)
        {
            return false;
        }
        
        normalized = (vector / Math.Sqrt(vector.LengthSquared)).ToVector();
        
        return true;
    }

    public static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point)
    {
        var start = new DVector(a);
        var direction = new DVector(b) - start;

        if (direction.LengthSquared == 0)
        {
            return a;
        }
        
        var t = Math.Clamp(DVector.Dot(new DVector(point) - start, direction) / direction.LengthSquared, 0, 1);
        
        return (start + direction * t).ToVector();
    }

    public static (Vector3, Vector3) ClosestPointsBetweenSegments(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        var start1 = new DVector(p1);
        var start2 = new DVector(p2);
        
        var d1 = new DVector(q1) - start1;
        var d2 = new DVector(q2) - start2;
        
        var r = start1 - start2;
        
        var a = d1.LengthSquared;
        var e = d2.LengthSquared;
        
        var f = DVector.Dot(d2, r);

        double s;
        double t;

        if (a == 0 && e == 0)
        {
            return (p1, p2);
        }
        
        if (a == 0)
        {
            s = 0;
            t = Math.Clamp(f / e, 0, 1);
        }
        else
        {
            var c = DVector.Dot(d1, r);

            if (e == 0)
            {
                t = 0; 
                s = Math.Clamp(-c / a, 0, 1);
            }
            else
            {
                var b = DVector.Dot(d1, d2);
                var denominator = a * e - b * b;
                
                s = denominator > 1e-14 * a * e ? Math.Clamp((b * f - c * e) / denominator, 0, 1) : 0;
                t = (b * s + f) / e;

                if (t < 0)
                {
                    t = 0; 
                    s = Math.Clamp(-c / a, 0, 1);
                }
                else if (t > 1)
                {
                    t = 1; 
                    s = Math.Clamp((b - c) / a, 0, 1);
                }
            }
        }
        
        return ((start1 + d1 * s).ToVector(), (start2 + d2 * t).ToVector());
    }

    public static bool AabbOverlap((Vector3 min, Vector3 max) a, (Vector3 min, Vector3 max) b)
    {
        return a.min.X <= b.max.X && a.max.X >= b.min.X &&
               a.min.Y <= b.max.Y && a.max.Y >= b.min.Y &&
               a.min.Z <= b.max.Z && a.max.Z >= b.min.Z;
    }

    public static bool IntersectsSphereCollider(Vector3 center, float radius, Collider collider) => collider switch
    {
        SphereCollider sphere => SphereSphere(center, radius, sphere),
        BoxCollider box => SphereBox(center, radius, box),
        CapsuleCollider capsule => SphereCapsule(center, radius, capsule),
        _ => false
    };

    private static bool SphereSphere(Vector3 center, float radius, SphereCollider sphere)
    {
        var delta = new DVector(center) - new DVector(sphere.GetWorldCenter());
        var r = (double)radius + sphere.WorldRadius;
        
        return delta.LengthSquared <= r * r;
    }

    private static bool SphereBox(Vector3 center, float radius, BoxCollider box)
    {
        var local = Vector3.Transform(center - box.GetWorldCenter(), Quaternion.Inverse(box.Parent.Transform.Rotation));
        var closest = Vector3.Clamp(local, -box.WorldHalfExtents, box.WorldHalfExtents);
        
        return (new DVector(local) - new DVector(closest)).LengthSquared <= (double)radius * radius;
    }

    private static bool SphereCapsule(Vector3 center, float radius, CapsuleCollider capsule)
    {
        var (a, b) = capsule.GetWorldSegment();
        var closest = ClosestPointOnSegment(a, b, center);
        var r = (double)radius + capsule.WorldRadius;
        
        return (new DVector(center) - new DVector(closest)).LengthSquared <= r * r;
    }

    public static bool RayIntersectsCollider(Vector3 origin, Vector3 dir, float maxDist,
        Collider collider, out float distance, out Vector3 normal)
    {
        distance = 0;
        normal = default;

        if (!IsFinite(origin) || float.IsNaN(maxDist) || maxDist < 0 || !TryNormalize(dir, out dir))
        {
            return false;
        }
        
        return collider switch
        {
            SphereCollider sphere => RaySphereInternal(origin, dir, maxDist, sphere.GetWorldCenter(),
                sphere.WorldRadius, out distance, out normal),
            BoxCollider box => RayBox(origin, dir, maxDist, box, out distance, out normal),
            CapsuleCollider capsule => RayCapsule(origin, dir, maxDist, capsule, out distance, out normal),
            _ => false
        };
    }
    
    private static bool Roots(double a, double b, double c, out double near, out double far)
    {
        near = far = 0;

        if (a <= 0 || !double.IsFinite(a) || !double.IsFinite(b) || !double.IsFinite(c))
        {
            return false;
        }
        
        var discriminant = b * b - a * c;

        if (discriminant < 0)
        {
            return false;
        }
        
        var sqrt = Math.Sqrt(discriminant);
        var q = -b - Math.CopySign(sqrt, b);

        if (q == 0)
        {
            near = far = -b / a; 
            return true;
        }
        
        var t1 = q / a;
        var t2 = c / q;
        
        near = Math.Min(t1, t2);
        far = Math.Max(t1, t2);
        
        return true;
    }

    private static Vector3 Normal(DVector value, Vector3 fallback)
    {
        return value.LengthSquared > 0 ? (value / Math.Sqrt(value.LengthSquared)).ToVector() : fallback;
    }

    public static bool RaySphereInternal(Vector3 origin, Vector3 dir, float maxDist,
        Vector3 center, float radius, out float distance, out Vector3 normal)
    {
        distance = 0;
        normal = default;

        if (!IsFinite(origin) || !IsFinite(center) || !float.IsFinite(radius) || radius < 0 ||
            float.IsNaN(maxDist) || maxDist < 0 || !TryNormalize(dir, out dir))
        {
            return false;
        }
        
        var oc = new DVector(origin) - new DVector(center);
        var d = new DVector(dir);

        if (!Roots(d.LengthSquared, DVector.Dot(oc, d), oc.LengthSquared - (double)radius * radius,
                out var near, out var far))
        {
            return false;
        }
        
        var t = near >= 0 ? near : far;

        if (!double.IsFinite(t) || t < 0 || t > maxDist || t > float.MaxValue)
        {
            return false;
        }
        
        distance = (float)t;
        normal = Normal(oc + d * t, -dir);
        
        return true;
    }

    private static bool RayBox(Vector3 origin, Vector3 dir, float maxDist, BoxCollider box,
        out float distance, out Vector3 normal)
    {
        distance = 0;
        normal = default;
        
        var inverse = Quaternion.Inverse(box.Parent.Transform.Rotation);
        
        var localOrigin = new DVector(Vector3.Transform(origin - box.GetWorldCenter(), inverse));
        var localDirection = new DVector(Vector3.Transform(dir, inverse));
        
        var half = new DVector(box.WorldHalfExtents);
        
        var entry = double.NegativeInfinity;
        var exit = double.PositiveInfinity;
        
        var entryNormal = Vector3.Zero;
        var exitNormal = Vector3.Zero;
        
        for (var axis = 0; axis < 3; axis++)
        {
            var o = localOrigin.At(axis);
            var d = localDirection.At(axis);
            var h = half.At(axis);
            
            if (d == 0)
            {
                if (o < -h || o > h)
                {
                    return false;
                }
                
                continue;
            }
            
            var t1 = (-h - o) / d;
            var t2 = (h - o) / d;
            
            var n = axis == 0 ? Vector3.UnitX : axis == 1 ? Vector3.UnitY : Vector3.UnitZ;
            
            var n1 = -n;
            var n2 = n;

            if (t1 > t2)
            {
                (t1, t2) = (t2, t1); 
                (n1, n2) = (n2, n1);
            }

            if (t1 > entry)
            {
                entry = t1; 
                entryNormal = n1;
            }

            if (t2 < exit)
            {
                exit = t2; 
                exitNormal = n2;
            }

            if (entry > exit)
            {
                return false;
            }
        }
        
        var t = entry >= 0 ? entry : exit;
        
        if (!double.IsFinite(t) || t < 0 || t > maxDist || t > float.MaxValue)
        {
            return false;
        }
        
        distance = (float)t;
        normal = Vector3.Transform(entry >= 0 ? entryNormal : exitNormal, box.Parent.Transform.Rotation);
        
        return true;
    }

    private static bool RayCapsule(Vector3 origin, Vector3 dir, float maxDist, CapsuleCollider capsule,
        out float distance, out Vector3 normal)
    {
        distance = 0;
        normal = default;
        
        var (a, b) = capsule.GetWorldSegment();
        
        var start = new DVector(a);
        var segment = new DVector(b) - start;
        
        var length = Math.Sqrt(segment.LengthSquared);

        if (length == 0)
        {
            return RaySphereInternal(origin, dir, maxDist, a, capsule.WorldRadius, out distance, out normal);
        }
        
        var axis = segment / length;
        
        var d = new DVector(dir);
        var oc = new DVector(origin) - start;
        
        var alongDirection = DVector.Dot(d, axis);
        var alongOrigin = DVector.Dot(oc, axis);
        
        var perpendicularDirection = d - axis * alongDirection;
        var perpendicularOrigin = oc - axis * alongOrigin;
        
        var radiusSquared = (double)capsule.WorldRadius * capsule.WorldRadius;
        
        var best = double.PositiveInfinity;

        void Consider(double t, int part)
        {
            if (!double.IsFinite(t) || t < 0 || t > maxDist || t >= best || t > float.MaxValue)
            {
                return;
            }
            
            var along = alongOrigin + t * alongDirection;

            if (part == 0 && (along < 0 || along > length))
            {
                return;
            }
            
            if (part == 1 && along > 0) 
            {
                return; 
            }

            if (part == 2 && along < length)
            {
                return;
            }
            
            best = t;
        }

        if (Roots(perpendicularDirection.LengthSquared, DVector.Dot(perpendicularDirection, perpendicularOrigin),
                perpendicularOrigin.LengthSquared - radiusSquared, out var near, out var far))
        {
            Consider(near, 0);
            Consider(far, 0);
        }
        
        if (Roots(d.LengthSquared, DVector.Dot(d, oc), oc.LengthSquared - radiusSquared, out near, out far))
        {
            Consider(near, 1);
            Consider(far, 1);
        }
        
        var endOffset = oc - segment;
        
        if (Roots(d.LengthSquared, DVector.Dot(d, endOffset), endOffset.LengthSquared - radiusSquared, out near, out far))
        {
            Consider(near, 2);
            Consider(far, 2);
        }

        if (double.IsPositiveInfinity(best))
        {
            return false;
        }
        
        distance = (float)best;
        
        var hit = oc + d * best;
        normal = Normal(hit - axis * Math.Clamp(alongOrigin + best * alongDirection, 0, length), -dir);
        
        return true;
    }

    public static bool CapsuleIntersectsCollider(Vector3 a, Vector3 b, float radius, Collider collider) => collider switch
    {
        SphereCollider sphere => CapsuleSphere(a, b, radius, sphere),
        BoxCollider box => CapsuleIntersectsBox(a, b, radius, box),
        CapsuleCollider capsule => CapsuleCapsule(a, b, radius, capsule),
        _ => false
    };

    private static bool CapsuleSphere(Vector3 a, Vector3 b, float radius, SphereCollider sphere)
    {
        var closest = ClosestPointOnSegment(a, b, sphere.GetWorldCenter());
        var r = (double)radius + sphere.WorldRadius;
        
        return (new DVector(sphere.GetWorldCenter()) - new DVector(closest)).LengthSquared <= r * r;
    }

    public static bool CapsuleIntersectsBox(Vector3 a, Vector3 b, float radius, BoxCollider box)
    {
        var center = box.GetWorldCenter();
        
        var inverse = Quaternion.Inverse(box.Parent.Transform.Rotation);
        
        var localA = Vector3.Transform(a - center, inverse);
        var localB = Vector3.Transform(b - center, inverse);
        
        return SegmentAabbDistanceSquared(localA, localB, box.WorldHalfExtents) <= (double)radius * radius;
    }
    
    public static double SegmentAabbDistanceSquared(Vector3 a, Vector3 b, Vector3 halfExtents)
    {
        return SegmentAabbDistanceSquared(a, b, halfExtents, out _);
    }

    private static double SegmentAabbDistanceSquared(Vector3 a, Vector3 b, Vector3 halfExtents, out Vector3 closest)
    {
        var start = new DVector(a);
        var direction = new DVector(b) - start;
        
        var half = new DVector(halfExtents);
        
        Span<double> cuts = stackalloc double[8];
        
        cuts[0] = 0;
        cuts[1] = 1;
        
        var count = 2;
        
        for (var axis = 0; axis < 3; axis++)
        {
            var d = direction.At(axis);
            
            if (d == 0)
            {
                continue;
            }
            
            var t1 = (-half.At(axis) - start.At(axis)) / d;
            var t2 = (half.At(axis) - start.At(axis)) / d;

            if (t1 > 0 && t1 < 1)
            {
                cuts[count++] = t1;
            }

            if (t2 > 0 && t2 < 1)
            {
                cuts[count++] = t2;
            }
        }
        
        cuts[..count].Sort();
        
        double Distance(double t)
        {
            var point = start + direction * t;
            double result = 0;
            
            for (var axis = 0; axis < 3; axis++)
            {
                var delta = Math.Max(Math.Abs(point.At(axis)) - half.At(axis), 0);
                result += delta * delta;
            }
            
            return result;
        }

        var bestT = 0d;
        var best = Distance(0);
        void Consider(double t)
        {
            var distance = Distance(t);
            if (distance < best)
            {
                best = distance;
                bestT = t;
            }
        }
        Consider(1);
        
        for (var i = 0; i < count - 1; i++)
        {
            var low = cuts[i];
            var high = cuts[i + 1];
            
            var midpoint = start + direction * ((low + high) * 0.5);
            
            double quadratic = 0, linear = 0;
            
            for (var axis = 0; axis < 3; axis++)
            {
                var value = midpoint.At(axis);
                var h = half.At(axis);
                
                if (value >= -h && value <= h)
                {
                    continue;
                }
                
                var d = direction.At(axis);
                var offset = start.At(axis) - (value < -h ? -h : h);
                
                quadratic += d * d;
                linear += d * offset;
            }

            if (quadratic == 0)
            {
                Consider((low + high) * 0.5);
            }
            else
            {
                Consider(Math.Clamp(-linear / quadratic, low, high));
            }

            if (best == 0)
            {
                break;
            }
        }
        
        closest = (start + direction * bestT).ToVector();
        return best;
    }
    
    internal static Vector3 CapsuleContactNormal(Vector3 a, Vector3 b, Collider obstacle)
    {
        Vector3 separation;
        
        switch (obstacle)
        {
            case BoxCollider box:
                var rotation = box.Parent.Transform.Rotation;
                var inverse = Quaternion.Inverse(rotation);
                var center = box.GetWorldCenter();
                var localA = Vector3.Transform(a - center, inverse);
                var localB = Vector3.Transform(b - center, inverse);
                var half = box.WorldHalfExtents;
                
                SegmentAabbDistanceSquared(localA, localB, half, out var point);
                
                separation = Vector3.Transform(point - Vector3.Clamp(point, -half, half), rotation);
                
                break;
            case SphereCollider sphere:
                var sphereCenter = sphere.GetWorldCenter();
                separation = ClosestPointOnSegment(a, b, sphereCenter) - sphereCenter;
                
                break;
            case CapsuleCollider capsule:
                var (c, d) = capsule.GetWorldSegment();
                var (p, q) = ClosestPointsBetweenSegments(a, b, c, d);
                separation = p - q;
                
                break;
            default:
                return Vector3.Zero;
        }
        
        return TryNormalize(separation, out var normal) ? normal : Vector3.Zero;
    }
    
    internal static bool TryGetStairSupport(Vector3 a, Vector3 b, float radius,
        BoxCollider box, float maxRise, out Vector3 normal)
    {
        normal = Vector3.Zero;
        
        var center = box.GetWorldCenter();
        var rotation = box.Parent.Transform.Rotation;
        var inverse = Quaternion.Inverse(rotation);
        var half = box.WorldHalfExtents;
        
        SegmentAabbDistanceSquared(Vector3.Transform(a - center, inverse),
            Vector3.Transform(b - center, inverse), half, out var closest);
        
        var surface = Vector3.Transform(Vector3.Clamp(closest, -half, half) * 0.99999f, rotation) + center;
        var (_, max) = box.GetWorldBounds();
        var origin = new Vector3(surface.X, max.Y + MathF.Max(radius, 0.01f), surface.Z);

        if (!RayIntersectsCollider(origin, -Vector3.UnitY, float.PositiveInfinity, box,
                out var distance, out var treadNormal))
        {
            return false;
        }
        
        var height = origin.Y - distance;
        var bottom = MathF.Min(a.Y, b.Y) - radius;

        if (height < bottom - radius * 0.001f || height - bottom > maxRise + radius * 0.001f)
        {
            return false;
        }
        
        normal = treadNormal;
        
        return true;
    }

    private static bool CapsuleCapsule(Vector3 a1, Vector3 b1, float radius, CapsuleCollider capsule)
    {
        var (a2, b2) = capsule.GetWorldSegment();
        var (p1, p2) = ClosestPointsBetweenSegments(a1, b1, a2, b2);
        
        var r = (double)radius + capsule.WorldRadius;
        
        return (new DVector(p1) - new DVector(p2)).LengthSquared <= r * r;
    }
}

