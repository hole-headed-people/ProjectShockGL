using System.Numerics;
using ProjectShockGL.Core;

namespace ProjectShockGL.Tools;

public static class Utils
{
    public static IEnumerable<(Vector3 From, Vector3 To)> GetRadiusLines(Vector3 position, float radius)
    {
        if (!float.IsFinite(radius) || radius <= 0)
        {
            yield break;
        }
        
        const int segments = 64;

        for (var plane = 0; plane < 3; plane++)
        {
            var u = plane == 2 ? Vector3.UnitY : Vector3.UnitX;
            var v = plane == 0 ? Vector3.UnitY : Vector3.UnitZ;
            for (var i = 0; i < segments; i++)
            {
                var a = i * MathF.Tau / segments;
                var b = (i + 1) * MathF.Tau / segments;
                yield return (position + radius * (u * MathF.Cos(a) + v * MathF.Sin(a)),
                    position + radius * (u * MathF.Cos(b) + v * MathF.Sin(b)));
            }
        }
    }
    
    public static IEnumerable<(Vector3 From, Vector3 To)> GetConeLines(Transform transform, float radius, float angle)
    {
        if (!float.IsFinite(radius) || radius <= 0)
        {
            yield break;
        }
        
        var origin = transform.Position;
        var halfAngle = angle * MathF.PI / 360;
        var forward = transform.Forward;
        var right = transform.Right;
        var up = transform.Up;
        var center = origin + forward * (radius * MathF.Cos(halfAngle));
        var radiusSin = radius * MathF.Sin(halfAngle);
        
        const int segments = 48;

        for (var i = 0; i < segments; i++)
        {
            var point = Rim(i);
            
            yield return (point, Rim(i + 1));

            if (i % (segments / 4) == 0)
            {
                yield return (origin, point);
            }
        }

        yield break;

        Vector3 Rim(int index)
        {
            var a = index * MathF.Tau / segments;
            
            return center + (right * MathF.Cos(a) + up * MathF.Sin(a)) * radiusSin;
        }
    }
}
