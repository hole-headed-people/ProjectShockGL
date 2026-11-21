using System.Numerics;
using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

internal sealed class WorldLightmaps : IDisposable
{
    private readonly GL _gl;
    
    private readonly StoredLightmap _data;
    
    private readonly Dictionary<LightmapSurfaceId, StoredLightmapSurface> _surfaces;
    private readonly Dictionary<(int Template, Mesh Source, int Revision), Mesh?> _meshes = [];
    
    private readonly Texture[] _pages;
    
    internal string Asset { get; }

    internal WorldLightmaps(GL gl, string asset)
    {
        _gl = gl; Asset = asset;
        _data = LightmapStorage.Read(asset);
        _surfaces = _data.Surfaces.ToDictionary(surface => surface.Id);
        
        var pages = new List<Texture>();

        try
        {
            foreach (var page in _data.Pages)
            {
                pages.Add(Texture.FromLinearRgb(gl, _data.Size, page));
            }

            _pages = pages.ToArray();
            _data = _data with { Pages = [] };
        }
        catch
        {
            foreach (var page in pages)
            {
                page.Dispose();
            }

            throw;
        }
    }

    private readonly record struct Vertex(Vector3 Position, Vector3 Normal, Vector2 UV);
    private readonly record struct Triangle(Vertex A, Vertex B, Vertex C);
    private static Vertex VertexAt(ReadOnlySpan<float> data, int index)
    {
        var p = index * 8;
        return new Vertex(new(data[p],data[p+1],data[p+2]), new(data[p+3],data[p+4],data[p+5]), new(data[p+6],data[p+7]));
    }

    private Mesh? Resolve(StoredLightmapSurface surface, Mesh source)
    {
        var key = (surface.Template, source, source.Revision);

        if (_meshes.TryGetValue(key, out var cached))
        {
            return cached;
        }
        
        var template = _data.Templates[surface.Template];
        
        var input = source.Vertices.Span;
        var indices = source.Indices.Span;
        
        var reference = template.Vertices.Span;
        var referenceIndices = template.Indices.Span;
        var referenceUV = template.LightmapUv.Span;
        
        var lookup = new Dictionary<Triangle, (Vector2 A, Vector2 B, Vector2 C)>();
        
        for (var i = 0; i < referenceIndices.Length; i += 3)
        {
            var a = (int)referenceIndices[i]; var b = (int)referenceIndices[i+1]; var c = (int)referenceIndices[i+2];

            if (!lookup.TryAdd(new Triangle(VertexAt(reference, a), VertexAt(reference, b), VertexAt(reference, c)),
                    (referenceUV[a], referenceUV[b], referenceUV[c])))
            {
                _meshes[key] = null; 
                
                return null;
            }
        }
        var vertices = new float[indices.Length * 8];
        var uv = new Vector2[indices.Length];
        
        for (var i = 0; i < indices.Length; i += 3)
        {
            var a = (int)indices[i]; var b = (int)indices[i+1]; var c = (int)indices[i+2];

            if (!lookup.TryGetValue(new Triangle(VertexAt(input, a), VertexAt(input, b), VertexAt(input, c)),
                    out var mapping))
            {
                _meshes[key] = null; 
                return null;
            }

            for (var j = 0; j < 3; j++)
            {
                input.Slice((int)indices[i+j] * 8,8).CopyTo(vertices.AsSpan((i+j)*8,8));
            }
            
            uv[i] = mapping.A; uv[i+1] = mapping.B; uv[i+2] = mapping.C;
        }
        
        var mesh = new Mesh(_gl, vertices, Enumerable.Range(0,indices.Length).Select(i => (uint)i).ToArray(), uv);
        
        _meshes[key] = mesh;
        
        return mesh;
    }

    private static bool Matches(Matrix4x4 a, Matrix4x4 b)
    {
        var aa = new[] {a.M11,a.M12,a.M13,a.M14,a.M21,a.M22,a.M23,a.M24,a.M31,a.M32,a.M33,a.M34,a.M41,a.M42,a.M43,a.M44};
        var bb = new[] {b.M11,b.M12,b.M13,b.M14,b.M21,b.M22,b.M23,b.M24,b.M31,b.M32,b.M33,b.M34,b.M41,b.M42,b.M43,b.M44};
       
        return aa.Zip(bb).All(pair => MathF.Abs(pair.First - pair.Second) <= .00001f);
    }

    internal void Apply(IReadOnlyList<RenderCommand> commands)
    {
        foreach (var command in commands.OfType<MeshCommand>())
        {
            if (!command.StaticBatchEligible || !command.Lit || command.Shader != null ||
                command.SurfaceMode == SurfaceMode.Transparent || command.Mesh is not { } source ||
                !_surfaces.TryGetValue(command.LightmapSurface, out var surface))
            {
                continue;
            }
            
            var model = Matrix4x4.CreateScale(command.Scale) * Matrix4x4.CreateFromQuaternion(command.Rotation) * Matrix4x4.CreateTranslation(command.Position);

            if (!Matches(model, surface.Model) || Resolve(surface, source) is not { } mesh)
            {
                continue;
            }
            
            command.Mesh = mesh; command.LightmapTexture = _pages[surface.Page];
            command.LightmapPage = surface.Page; command.LightmapScaleOffset = surface.Region;
        }
    }

    internal bool Detail(Core.DetailPlacement detail, Mesh source, out Mesh mesh, out Texture? texture, out Vector4 region)
    {
        mesh = source; texture = null; region = new Vector4(1,1,0,0);

        if (!_surfaces.TryGetValue(new LightmapSurfaceId(detail.Id, 0, true), out var surface))
        {
            return false;
        }
        
        var model = Matrix4x4.CreateScale(detail.Scale) * Matrix4x4.CreateFromQuaternion(detail.Rotation) * Matrix4x4.CreateTranslation(detail.Position);

        if (!Matches(model, surface.Model) || Resolve(surface, source) is not { } prepared)
        {
            return false;
        }
        
        mesh = prepared; texture = _pages[surface.Page]; region = surface.Region;
        
        return true;
    }

    public void Dispose()
    {
        Cleanup.Run(_meshes.Values.OfType<Mesh>().Distinct().Select<Mesh,Action>(mesh => mesh.Dispose)
            .Concat(_pages.Select<Texture,Action>(texture => texture.Dispose)).ToArray());
        _meshes.Clear();
    }
}