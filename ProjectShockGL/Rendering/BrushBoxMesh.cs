using System.Numerics;

namespace ProjectShockGL.Rendering;

public static class BrushBoxMesh
{
    public static (float[] Vertices, uint[] Indices) Build(Vector3 size, Vector3 scale, float textureScale)
    {
        Validate(size, nameof(size));
        Validate(Vector3.Abs(scale), nameof(scale));

        if (!float.IsFinite(textureScale) || textureScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(textureScale));
        }
        
        var worldSize = Vector3.Abs(size * scale) * textureScale;
        
        Validate(worldSize, nameof(scale));
        
        var vertices = new float[24 * 8];
        var indices = new uint[36];
        
        (Vector3 N, Vector3 U, Vector3 V)[] faces =
        [
            (Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY),
            (-Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY),
            (Vector3.UnitY, Vector3.UnitX, -Vector3.UnitZ),
            (-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ),
            (Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY),
            (-Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitY)
        ];
        
        for (var f = 0; f < faces.Length; f++)
        {
            var (normal, u, v) = faces[f];
            var center = normal * size * 0.5f;
            var du = u * size * 0.5f;
            var dv = v * size * 0.5f;
            var repeatsU = Vector3.Dot(Vector3.Abs(u), worldSize);
            var repeatsV = Vector3.Dot(Vector3.Abs(v), worldSize);
            
            Vector3[] corners = [center - du - dv, center + du - dv, center + du + dv, center - du + dv];
            Vector2[] uv = [new(0, repeatsV), new(repeatsU, repeatsV), new(repeatsU, 0), Vector2.Zero];
           
            for (var c = 0; c < 4; c++)
            {
                var at = (f * 4 + c) * 8;
                
                vertices[at] = corners[c].X; vertices[at + 1] = corners[c].Y; vertices[at + 2] = corners[c].Z;
                vertices[at + 3] = normal.X; vertices[at + 4] = normal.Y; vertices[at + 5] = normal.Z;
                vertices[at + 6] = uv[c].X; vertices[at + 7] = uv[c].Y;
            }
            
            var first = (uint)(f * 4);
            
            uint[] faceIndices = [first, first + 1, first + 2, first, first + 2, first + 3];
            
            faceIndices.CopyTo(indices, f * 6);
        }
        
        return (vertices, indices);
    }

    internal static void Validate(Vector3 value, string name)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z) ||
            value.X <= 0 || value.Y <= 0 || value.Z <= 0)
        {
            throw new ArgumentOutOfRangeException(name, "Dimensions must be finite and positive.");
        }
    }
}
