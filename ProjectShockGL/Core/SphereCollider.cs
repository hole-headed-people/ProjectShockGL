using System.Numerics;
using MathF = System.MathF;
using ProjectShockGL.Tools;

namespace ProjectShockGL.Core;

public class SphereCollider : Collider
{
    private float _radius = 0.5f;
    public float Radius
    {
        get => _radius;
        set => _radius = NonNegative(value, nameof(Radius));
    }

    public float WorldRadius
    {
        get
        {
            var scale = Vector3.Abs(Parent.Transform.Scale);
            return Radius * MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z));
        }
    }

    public override (Vector3 min, Vector3 max) GetLocalBounds()
    {
        var r = new Vector3(Radius);
        return (Center - r, Center + r);
    }

    public override (Vector3 min, Vector3 max) CalculateWorldBounds()
    {
        var center = GetWorldCenter();
        var r = new Vector3(WorldRadius);
        
        return (center - r, center + r);
    }

    public override bool Intersects(Collider other)
    {
        return PhysicsHelper.IntersectsSphereCollider(GetWorldCenter(), WorldRadius, other);
    }

    public bool IntersectsBox(BoxCollider box) => Intersects(box);
}

