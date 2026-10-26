using System.Numerics;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;

namespace ProjectShockGLEditor;

public static class LightGizmo
{
    public static void Draw(Graphics graphics, Light light)
    {
        if (light.Type == LightType.Directional)
        {
            var origin = light.Transform.Position;
            
            var forward = light.Transform.Forward;
            var right = light.Transform.Right;
            var up = light.Transform.Up;
            
            var tip = origin + forward * 2f;
            var head = tip - forward * 0.45f;
            
            var texture = Content.GetWhitePixel();
            
            graphics.DrawLine(origin, tip, texture, Color.Yellow, 0.025f, ignoreDepth: true);
            graphics.DrawLine(tip, head + right * 0.22f, texture, Color.Yellow, 0.025f, ignoreDepth: true);
            graphics.DrawLine(tip, head - right * 0.22f, texture, Color.Yellow, 0.025f, ignoreDepth: true);
            graphics.DrawLine(tip, head + up * 0.22f, texture, Color.Yellow, 0.025f, ignoreDepth: true);
            graphics.DrawLine(tip, head - up * 0.22f, texture, Color.Yellow, 0.025f, ignoreDepth: true);
            
            return;
        }

        if (light.Type == LightType.Point)
        {
            foreach (var (from, to) in RadiusLines(light))
            {
                graphics.DrawLine(from, to, Content.GetWhitePixel(), Color.Yellow, 0.012f, ignoreDepth: true);
            }
            
            return;
        }

        if (light.Type != LightType.Spot)
        {
            return;
        }

        foreach (var (from, to) in ConeLines(light, light.OuterAngle))
        {
            graphics.DrawLine(from, to, Content.GetWhitePixel(), Color.Yellow, 0.012f, ignoreDepth: true);
        }

        foreach (var (from, to) in ConeLines(light, light.InnerAngle))
        {
            graphics.DrawLine(from, to, Content.GetWhitePixel(), Color.Cyan, 0.008f, ignoreDepth: true);
        }
        
        if (float.IsFinite(light.Radius) && light.Radius > 0)
        {
            graphics.DrawLine(light.Transform.Position, light.Transform.Position + light.Transform.Forward * light.Radius,
                Content.GetWhitePixel(), Color.White, 0.012f, ignoreDepth: true);
        }
    }
    
    public static IEnumerable<(Vector3 From, Vector3 To)> RadiusLines(Light light)
    {
        if (!float.IsFinite(light.Radius) || light.Radius <= 0)
        {
            yield break;
        }
        
        const int segments = 64;
        
        var center = light.Transform.Position;
        
        for (var plane = 0; plane < 3; plane++)
        {
            var u = plane == 2 ? Vector3.UnitY : Vector3.UnitX;
            var v = plane == 0 ? Vector3.UnitY : Vector3.UnitZ
                ;
            for (var i = 0; i < segments; i++)
            {
                var a = i * MathF.Tau / segments;
                var b = (i + 1) * MathF.Tau / segments;
                yield return (center + light.Radius * (u * MathF.Cos(a) + v * MathF.Sin(a)),
                    center + light.Radius * (u * MathF.Cos(b) + v * MathF.Sin(b)));
            }
        }
    }

    public static IEnumerable<(Vector3 From, Vector3 To)> ConeLines(Light light, float angle)
    {
        if (!float.IsFinite(light.Radius) || light.Radius <= 0)
        {
            yield break;
        }
        
        var origin = light.Transform.Position;
        var halfAngle = angle * MathF.PI / 360;
        var forward = light.Transform.Forward;
        var right = light.Transform.Right;
        var up = light.Transform.Up;
        var center = origin + forward * (light.Radius * MathF.Cos(halfAngle));
        var radius = light.Radius * MathF.Sin(halfAngle);
        
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
            
            return center + (right * MathF.Cos(a) + up * MathF.Sin(a)) * radius;
        }
    }
}
