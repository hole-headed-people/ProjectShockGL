using System.Numerics;

namespace ProjectShockGL.Core;

public class BoxCollider : Collider
{
    private Vector3 _halfExtents = Vector3.One;
    
    public Vector3 HalfExtents
    {
        get => _halfExtents;
        set => _halfExtents = new Vector3(NonNegative(value.X, nameof(HalfExtents)),
            NonNegative(value.Y, nameof(HalfExtents)), NonNegative(value.Z, nameof(HalfExtents)));
    }
    
    public Vector3 WorldHalfExtents
    {
        get
        {
            var scale = Parent.Transform.Scale;
            
            return new Vector3(
                HalfExtents.X * MathF.Abs(scale.X),
                HalfExtents.Y * MathF.Abs(scale.Y),
                HalfExtents.Z * MathF.Abs(scale.Z)
            );
        }
    }
    
    public override (Vector3 min, Vector3 max) GetLocalBounds()
    {
        return (Center - HalfExtents, Center + HalfExtents);
    }

    public override (Vector3 min, Vector3 max) CalculateWorldBounds()
    {
        var corners = GetCorners();
        
        var min = corners[0];
        var max = corners[0];

        for (var i = 1; i < 8; i++)
        {
            min = Vector3.Min(min, corners[i]);
            max = Vector3.Max(max, corners[i]);
        }

        return (min, max);
    }

    public override bool Intersects(Collider other)
    {
        return other switch
        {
            BoxCollider box => IntersectsBox(box),
            SphereCollider sphere => sphere.IntersectsBox(this),
            CapsuleCollider capsule => capsule.IntersectsBox(this),
            _ => false
        };
    }

    private bool IntersectsBox(BoxCollider other)
    {
        var a = this;

        var centerA = a.GetWorldCenter();
        var centerB = other.GetWorldCenter();

        var rotA = a.Parent.Transform.Rotation;
        var rotB = other.Parent.Transform.Rotation;

        var axesA = new Vector3[]
        {
            Vector3.Transform(Vector3.UnitX, rotA),
            Vector3.Transform(Vector3.UnitY, rotA),
            Vector3.Transform(Vector3.UnitZ, rotA),
        };

        var axesB = new Vector3[]
        {
            Vector3.Transform(Vector3.UnitX, rotB),
            Vector3.Transform(Vector3.UnitY, rotB),
            Vector3.Transform(Vector3.UnitZ, rotB),
        };
        
        var halfA = a.WorldHalfExtents;
        var halfB = other.WorldHalfExtents;
        
        var delta = centerB - centerA;
        
        var rotation = new float[3, 3];
        var absRotation = new float[3, 3];
        
        const float epsilon = 1e-6f;

        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                rotation[i, j] = Vector3.Dot(axesA[i], axesB[j]);
                absRotation[i, j] = MathF.Abs(rotation[i, j]) + epsilon;
            }
        }

        var deltaA = new float[3];
        
        for (var i = 0; i < 3; i++)
        {
            deltaA[i] = Vector3.Dot(delta, axesA[i]);
        }
        
        var deltaB = new float[3];
        
        for (var i = 0; i < 3; i++)
        {
            deltaB[i] = Vector3.Dot(delta, axesB[i]);
        }
        
        for (var i = 0; i < 3; i++)
        {
            var ra = halfA[i];
            var rb = halfB[0] * absRotation[i, 0]
                   + halfB[1] * absRotation[i, 1]
                   + halfB[2] * absRotation[i, 2];

            if (MathF.Abs(deltaA[i]) > ra + rb)
            {
                return false;
            }
        }
        
        for (var i = 0; i < 3; i++)
        {
            var ra = halfA[0] * absRotation[0, i]
                   + halfA[1] * absRotation[1, i]
                   + halfA[2] * absRotation[2, i];
            var rb = halfB[i];

            if (MathF.Abs(deltaB[i]) > ra + rb)
            {
                return false;
            }
        }
        
        {
            var ra = halfA[1] * absRotation[2, 0] + halfA[2] * absRotation[1, 0];
            var rb = halfB[1] * absRotation[0, 2] + halfB[2] * absRotation[0, 1];
            var d = MathF.Abs(deltaA[2] * rotation[1, 0] - deltaA[1] * rotation[2, 0]);
           
            if (d > ra + rb)
            {
                return false;
            }
        }
        
        {
            var ra = halfA[1] * absRotation[2, 1] + halfA[2] * absRotation[1, 1];
            var rb = halfB[0] * absRotation[0, 2] + halfB[2] * absRotation[0, 0];
            var d = MathF.Abs(deltaA[2] * rotation[1, 1] - deltaA[1] * rotation[2, 1]);

            if (d > ra + rb)
            {
                return false;
            }
        }
        
        {
            var ra = halfA[1] * absRotation[2, 2] + halfA[2] * absRotation[1, 2];
            var rb = halfB[0] * absRotation[0, 1] + halfB[1] * absRotation[0, 0];
            var d = MathF.Abs(deltaA[2] * rotation[1, 2] - deltaA[1] * rotation[2, 2]);

            if (d > ra + rb)
            {
                return false;
            }
        }
        
        {
            var ra = halfA[0] * absRotation[2, 0] + halfA[2] * absRotation[0, 0];
            var rb = halfB[1] * absRotation[1, 2] + halfB[2] * absRotation[1, 1];
            var d = MathF.Abs(deltaA[0] * rotation[2, 0] - deltaA[2] * rotation[0, 0]);

            if (d > ra + rb)
            {
                return false;
            }
        }
        
        {
            var ra = halfA[0] * absRotation[2, 1] + halfA[2] * absRotation[0, 1];
            var rb = halfB[0] * absRotation[1, 2] + halfB[2] * absRotation[1, 0];
            var d = MathF.Abs(deltaA[0] * rotation[2, 1] - deltaA[2] * rotation[0, 1]);

            if (d > ra + rb)
            {
                return false;
            }
        }
        
        {
            var ra = halfA[0] * absRotation[2, 2] + halfA[2] * absRotation[0, 2];
            var rb = halfB[0] * absRotation[1, 1] + halfB[1] * absRotation[1, 0];
            var d = MathF.Abs(deltaA[0] * rotation[2, 2] - deltaA[2] * rotation[0, 2]);

            if (d > ra + rb)
            {
                return false;
            }
        }

        {
            var ra = halfA[0] * absRotation[1, 0] + halfA[1] * absRotation[0, 0];
            var rb = halfB[1] * absRotation[2, 2] + halfB[2] * absRotation[2, 1];
            var d = MathF.Abs(deltaA[1] * rotation[0, 0] - deltaA[0] * rotation[1, 0]);

            if (d > ra + rb)
            {
                return false;
            }
        }

        {
            var ra = halfA[0] * absRotation[1, 1] + halfA[1] * absRotation[0, 1];
            var rb = halfB[0] * absRotation[2, 2] + halfB[2] * absRotation[2, 0];
            var d = MathF.Abs(deltaA[1] * rotation[0, 1] - deltaA[0] * rotation[1, 1]);

            if (d > ra + rb)
            {
                return false;
            }
        }

        {
            var ra = halfA[0] * absRotation[1, 2] + halfA[1] * absRotation[0, 2];
            var rb = halfB[0] * absRotation[2, 1] + halfB[1] * absRotation[2, 0];
            var d = MathF.Abs(deltaA[1] * rotation[0, 2] - deltaA[0] * rotation[1, 2]);

            if (d > ra + rb)
            {
                return false;
            }
        }
        
        return true;
    }
    
    public Vector3[] GetCorners()
    {
        var center = GetWorldCenter();
        var rotation = Parent.Transform.Rotation;
        var h = WorldHalfExtents;

        var localCorners = new Vector3[]
        {
            new(-h.X, -h.Y, -h.Z),
            new( h.X, -h.Y, -h.Z),
            new( h.X,  h.Y, -h.Z),
            new(-h.X,  h.Y, -h.Z),
            new(-h.X, -h.Y,  h.Z),
            new( h.X, -h.Y,  h.Z),
            new( h.X,  h.Y,  h.Z),
            new(-h.X,  h.Y,  h.Z),
        };
        
        var corners = new Vector3[8];
        
        for (var i = 0; i < 8; i++)
        {
            corners[i] = center + Vector3.Transform(localCorners[i], rotation);
        }

        return corners;
    }
}
