using System.Numerics;

namespace ProjectShockGL.Rendering;

/// <summary>A separate mesh layout for lighting. Primary texture UVs are copied unchanged.</summary>
public sealed class LightmapUvMesh
{
    private readonly float[] _vertices;
    private readonly uint[] _indices;
    
    private readonly Vector2[] _uv;
    public ReadOnlyMemory<float> Vertices => _vertices;
    public ReadOnlyMemory<uint> Indices => _indices;
    public ReadOnlyMemory<Vector2> LightmapUv => _uv;
    public int ChartCount { get; }

    private LightmapUvMesh(float[] vertices, uint[] indices, Vector2[] uv, int charts)
    {
        _vertices = vertices;
        _indices = indices;
        _uv = uv;
        
        ChartCount = charts;
    }

    internal static LightmapUvMesh Restore(float[] vertices, uint[] indices, Vector2[] uv)
    {
        return new LightmapUvMesh(vertices, indices, uv, 0);
    }

    public Mesh CreateMesh(Silk.NET.OpenGL.GL gl) => new(gl, _vertices, _indices, _uv);

    public static LightmapUvMesh Generate(float[] vertices, uint[] indices, int resolution = 128, int padding = 2)
    {
        return Generate(vertices, indices, resolution, padding, brushFaces: false);
    }

    public static LightmapUvMesh GenerateBrush(Vector3 size, Vector3 scale, float textureScale,
        IReadOnlyDictionary<Core.BrushFace, Core.BrushFaceData>? faces = null, int resolution = 128, int padding = 2)
    {
        var mesh = BrushBoxMesh.Build(size, scale, textureScale, faces);
        return Generate(mesh.Vertices, mesh.Indices, resolution, padding, brushFaces: true);
    }

    private sealed record Chart(int[] Source, uint[] Indices, Vector2[] Projected, Vector2 Size);

    private static LightmapUvMesh Generate(float[] vertices, uint[] indices, int resolution, int padding, bool brushFaces)
    {
        if (resolution < 8 || resolution > 8192 || padding < 1 || padding * 2 + 1 > resolution)
        {
            throw new ArgumentOutOfRangeException(nameof(resolution), "Invalid chart resolution or padding.");
        }

        if (vertices.Length == 0 || vertices.Length % 8 != 0 || indices.Length == 0 || indices.Length % 3 != 0 ||
            vertices.Any(value => !float.IsFinite(value)) || indices.Any(index => index >= vertices.Length / 8))
        {
            throw new InvalidDataException("Lightmap geometry needs finite position/normal/UV vertices and valid triangles.");
        }

        Vector3 Position(int index) => new(vertices[index * 8], vertices[index * 8 + 1], vertices[index * 8 + 2]);
        
        var charts = new List<Chart>();
        
        for (var at = 0; at < indices.Length; at += brushFaces ? 6 : 3)
        {
            var source = brushFaces ? Enumerable.Range(at / 6 * 4, 4).ToArray()
                : indices.Skip(at).Take(3).Select(index => (int)index).ToArray();
            
            var p = source.Select(Position).ToArray();
            
            var edge = p[1] - p[0];
            var cross = Vector3.Cross(edge, p[2] - p[0]);

            if (!float.IsFinite(edge.LengthSquared()) || !float.IsFinite(cross.LengthSquared()) ||
                edge.LengthSquared() < 1e-16f || cross.LengthSquared() < 1e-16f)
            {
                throw new InvalidDataException($"Degenerate lightmap triangle at index {at}.");
            }

            var u = Vector3.Normalize(edge);
            var v = Vector3.Normalize(Vector3.Cross(Vector3.Normalize(cross), u));
            
            var projected = p.Select(point => new Vector2(Vector3.Dot(point - p[0], u), Vector3.Dot(point - p[0], v))).ToArray();
            
            var min = projected.Aggregate(Vector2.Min);
            var max = projected.Aggregate(Vector2.Max);
            
            projected = projected.Select(point => point - min).ToArray();
            charts.Add(new Chart(source, brushFaces ? [0, 1, 2, 0, 2, 3] : [0, 1, 2], projected, max - min));
        }

        bool Pack(float scale, out (int X, int Y, int Width, int Height)[] rectangles)
        {
            rectangles = new (int, int, int, int)[charts.Count];
            
            var x = 0; var y = 0; var row = 0;
            
            for (var i = 0; i < charts.Count; i++)
            {
                var width = Math.Max(1, (int)MathF.Ceiling(charts[i].Size.X * scale)) + padding * 2;
                var height = Math.Max(1, (int)MathF.Ceiling(charts[i].Size.Y * scale)) + padding * 2;

                if (width > resolution || height > resolution)
                {
                    return false;
                }

                if (x + width > resolution)
                {
                    x = 0; 
                    y += row; 
                    row = 0;
                }

                if (y + height > resolution)
                {
                    return false;
                }
                
                rectangles[i] = (x, y, width, height);
                
                x += width;
                
                row = Math.Max(row, height);
            }
            
            return true;
        }

        if (!Pack(0, out _))
        {
            throw new InvalidDataException("Too many lightmap charts. Increase SurfaceSize or simplify the mesh.");
        }
        
        var largest = charts.Max(chart => MathF.Max(chart.Size.X, chart.Size.Y));
        
        var low = 0f;
        var high = resolution / largest;
        
        for (var i = 0; i < 32; i++)
        {
            var middle = (low + high) * .5f;

            if (Pack(middle, out _))
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }
        
        Pack(low, out var packed);

        var output = new List<float>();
        var outputIndices = new List<uint>();
        
        var uv = new List<Vector2>();
        
        for (var i = 0; i < charts.Count; i++)
        {
            var chart = charts[i];
            var first = (uint)uv.Count;
            
            for (var j = 0; j < chart.Source.Length; j++)
            {
                output.AddRange(vertices.Skip(chart.Source[j] * 8).Take(8));
                
                // Half-texel inset keeps all samples inside the chart's protected border.
                var point = chart.Projected[j] * low;
                
                uv.Add((new Vector2(packed[i].X + padding + .5f, packed[i].Y + padding + .5f) + point) / resolution);
            }
            
            outputIndices.AddRange(chart.Indices.Select(index => first + index));
        }
        
        return new LightmapUvMesh(output.ToArray(), outputIndices.ToArray(), uv.ToArray(), charts.Count);
    }
}