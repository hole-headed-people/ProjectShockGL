using System.Numerics;

namespace ProjectShockGLEditor;

public static class GizmoMath
{
    public static bool Project(Vector3 point, Matrix4x4 viewProjection, Vector2 screenSize, out Vector2 screen)
    {
        var clip = Vector4.Transform(new Vector4(point, 1), viewProjection);
        screen = default;
        
        if (clip.W <= 0.0001f)
        {
            return false;
        }
        
        var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;

        if (ndc.Z < 0 || ndc.Z > 1)
        {
            return false;
        }
        
        screen = new Vector2((ndc.X + 1) * 0.5f * screenSize.X, (1 - ndc.Y) * 0.5f * screenSize.Y);
        
        return float.IsFinite(screen.X) && float.IsFinite(screen.Y);
    }

    public static bool ScreenRay(Vector2 screen, Vector2 size, Matrix4x4 viewProjection,
        out Vector3 origin, out Vector3 direction)
    {
        origin = direction = default;

        if (size.X <= 0 || size.Y <= 0 || !Matrix4x4.Invert(viewProjection, out var inverse))
        {
            return false;
        }
        
        var x = screen.X / size.X * 2 - 1;
        var y = 1 - screen.Y / size.Y * 2;
        
        var near = Vector4.Transform(new Vector4(x, y, 0, 1), inverse);
        var far = Vector4.Transform(new Vector4(x, y, 1, 1), inverse);

        if (MathF.Abs(near.W) < 1e-7f || MathF.Abs(far.W) < 1e-7f)
        {
            return false;
        }
        
        origin = new Vector3(near.X, near.Y, near.Z) / near.W;
        
        var end = new Vector3(far.X, far.Y, far.Z) / far.W;

        if (Vector3.DistanceSquared(origin, end) < 1e-10f)
        {
            return false;
        }
        
        direction = Vector3.Normalize(end - origin);
        
        return true;
    }

    public static bool RayPlane(Vector3 origin, Vector3 direction, Vector3 point, Vector3 normal, out Vector3 hit)
    {
        hit = default;
        
        var denominator = Vector3.Dot(direction, normal);

        if (MathF.Abs(denominator) < 0.0001f)
        {
            return false;
        }
        
        var distance = Vector3.Dot(point - origin, normal) / denominator;

        if (distance < 0 || !float.IsFinite(distance))
        {
            return false;
        }
        
        hit = origin + direction * distance;
        
        return true;
    }

    public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var length = ab.LengthSquared();
        var t = length > 1e-8f ? Math.Clamp(Vector2.Dot(point - a, ab) / length, 0, 1) : 0;
        
        return Vector2.Distance(point, a + ab * t);
    }

    public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
    {
        return MathF.Atan2(Vector3.Dot(axis, Vector3.Cross(from, to)), Vector3.Dot(from, to));
    }

    public static float Snap(float value, float step)
    {
        return step > 0 ? MathF.Round(value / step) * step : value;
    }
}
