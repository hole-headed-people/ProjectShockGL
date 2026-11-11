using System.Numerics;

namespace ProjectShockGL.Rendering;

public sealed class DebugDraw
{
    private readonly Graphics _graphics;

    public DebugDraw(Graphics graphics)
    {
        _graphics = graphics;
    }

    public void Line(Vector3 from, Vector3 to, Color color, bool ignoreDepth = false, float thickness = 0.02f)
    {
        _graphics.DrawLine(from, to, Content.GetWhitePixel(), color, thickness, ignoreDepth: ignoreDepth);
    }

    public void Box(Vector3 position, Quaternion rotation, Vector3 size, Color color, bool ignoreDepth = false)
    {
        var transform = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
        _graphics.DrawBoxWireframe(-size * 0.5f, size * 0.5f, transform, color, ignoreDepth: ignoreDepth);
    }

    public void Sphere(Vector3 position, float radius, Color color, bool ignoreDepth = false)
    {
        const int segments = 32;

        if (!float.IsFinite(radius) || radius <= 0f)
        {
            return;
        }

        for (var i = 0; i < segments; i++)
        {
            var from = i * MathF.Tau / segments;
            var to = (i + 1) * MathF.Tau / segments;
            var a = new Vector2(MathF.Cos(from), MathF.Sin(from)) * radius;
            var b = new Vector2(MathF.Cos(to), MathF.Sin(to)) * radius;

            Line(position + new Vector3(a.X, a.Y, 0f), position + new Vector3(b.X, b.Y, 0f), color, ignoreDepth);
            Line(position + new Vector3(a.X, 0f, a.Y), position + new Vector3(b.X, 0f, b.Y), color, ignoreDepth);
            Line(position + new Vector3(0f, a.X, a.Y), position + new Vector3(0f, b.X, b.Y), color, ignoreDepth);
        }
    }

    public void Billboard(Vector3 position, Vector2 size, Texture texture, Color color, bool ignoreDepth = false)
    {
        _graphics.DrawBillboard(position, size, texture, color, ignoreDepth: ignoreDepth);
    }
}
