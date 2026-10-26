using System.Numerics;
using ProjectShockGL.Tools;

namespace ProjectShockGL.Core;

public class CapsuleCollider : Collider
{
    private float _radius = 0.5f;
    private float _height = 2f;
    
    private Vector3 _localAxis = Vector3.UnitY;

    public float Radius
    {
        get => _radius;
        set => _radius = NonNegative(value, nameof(Radius));
    }

    public float Height
    {
        get => _height;
        set => _height = NonNegative(value, nameof(Height));
    }

    public Vector3 LocalAxis
    {
        get => _localAxis;
        set
        {
            if (!PhysicsHelper.TryNormalize(value, out var axis))
            {
                throw new ArgumentOutOfRangeException(nameof(LocalAxis), "Capsule axis must be finite and nonzero.");
            }
            
            _localAxis = axis;
        }
    }
    
    public float WorldRadius
    {
        get
        {
            var u = Vector3.Normalize(Vector3.Cross(LocalAxis,
                MathF.Abs(LocalAxis.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
            
            var v = Vector3.Cross(LocalAxis, u);
            
            u *= Parent.Transform.Scale;
            v *= Parent.Transform.Scale;
            
            var a = Dot(u, u);
            var b = Dot(u, v);
            var c = Dot(v, v);
            
            var maxStretchSquared = (a + c + Math.Sqrt((a - c) * (a - c) + 4 * b * b)) * 0.5;
            
            return (float)(Radius * Math.Sqrt(maxStretchSquared));
        }
    }

    public float WorldHeight =>
        MathF.Max(
            (float)(Height *
                    Math.Sqrt(Dot(LocalAxis * Parent.Transform.Scale, LocalAxis * Parent.Transform.Scale))),
            2 * WorldRadius);

    public (Vector3 a, Vector3 b) GetWorldSegment()
    {
        var center = GetWorldCenter();
        var scaledAxis = LocalAxis * Parent.Transform.Scale;

        if (!PhysicsHelper.TryNormalize(scaledAxis, out var axis))
        {
            axis = LocalAxis;
        }
        
        axis = Vector3.Transform(axis, Parent.Transform.Rotation);
        
        var halfCylinder = MathF.Max(0, WorldHeight * 0.5f - WorldRadius);
        
        return (center - axis * halfCylinder, center + axis * halfCylinder);
    }

    public override (Vector3 min, Vector3 max) GetLocalBounds()
    {
        var halfCylinder = MathF.Max(0, Height * 0.5f - Radius);
        var extent = Vector3.Abs(LocalAxis) * halfCylinder + new Vector3(Radius);
        
        return (Center - extent, Center + extent);
    }

    public override (Vector3 min, Vector3 max) CalculateWorldBounds()
    {
        var (a, b) = GetWorldSegment();
        var r = new Vector3(WorldRadius);
        
        return (Vector3.Min(a, b) - r, Vector3.Max(a, b) + r);
    }

    public override bool Intersects(Collider other)
    {
        var (a, b) = GetWorldSegment();
        
        return PhysicsHelper.CapsuleIntersectsCollider(a, b, WorldRadius, other);
    }

    public bool IntersectsBox(BoxCollider box)
    {
        var (a, b) = GetWorldSegment();
        
        return PhysicsHelper.CapsuleIntersectsBox(a, b, WorldRadius, box);
    }

    public bool IntersectsSphere(SphereCollider sphere)
    {
        return Intersects(sphere);
    }

    private static double Dot(Vector3 a, Vector3 b)
    {
        return (double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z;
    }
}

