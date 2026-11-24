using System.Numerics;

namespace ProjectShockGL.Rendering;

internal readonly struct CameraFrustum
{
    private readonly Vector4 _left, _right, _bottom, _top, _near, _far;

    public CameraFrustum(Matrix4x4 matrix)
    {
        var x = new Vector4(matrix.M11, matrix.M21, matrix.M31, matrix.M41);
        var y = new Vector4(matrix.M12, matrix.M22, matrix.M32, matrix.M42);
        var z = new Vector4(matrix.M13, matrix.M23, matrix.M33, matrix.M43);
        var w = new Vector4(matrix.M14, matrix.M24, matrix.M34, matrix.M44);
        
        _left = Normalize(w + x);
        _right = Normalize(w - x);
        _bottom = Normalize(w + y);
        _top = Normalize(w - y);
        
        // Match the OpenGL clip volume (-w <= z <= w) used by the renderer.
        _near = Normalize(w + z);
        _far = Normalize(w - z);
    }

    public bool IntersectsSphere(Vector3 center, float radius)
    {
        return Inside(_left, center, radius) && Inside(_right, center, radius) &&
               Inside(_bottom, center, radius) && Inside(_top, center, radius) &&
               Inside(_near, center, radius) && Inside(_far, center, radius);
    }

    private static bool Inside(Vector4 plane, Vector3 center, float radius)
    {
        return Vector3.Dot(new Vector3(plane.X, plane.Y, plane.Z), center) + plane.W >= -radius;
    }

    private static Vector4 Normalize(Vector4 plane)
    {
        var length = new Vector3(plane.X, plane.Y, plane.Z).Length();
        // A degenerate plane must not reject otherwise visible geometry.
        return length > 0 && float.IsFinite(length) ? plane / length : Vector4.Zero;
    }
}
