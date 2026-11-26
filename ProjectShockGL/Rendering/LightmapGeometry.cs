using System.Numerics;
using ProjectShockGL.Core;

namespace ProjectShockGL.Rendering;

public sealed class LightmapPreparationOptions
{
    public int AtlasSize { get; init; } = 1024;
    public int SurfaceSize { get; init; } = 128;
    public int Padding { get; init; } = 2;

    internal void Validate()
    {
        if (AtlasSize < 8 || AtlasSize > 8192 || SurfaceSize < 8 || SurfaceSize > AtlasSize ||
            Padding < 1 || Padding * 2 + 1 > SurfaceSize)
        {
            throw new ArgumentOutOfRangeException(nameof(AtlasSize), "Invalid lightmap atlas, surface size or padding.");
        }
    }
}

public readonly record struct LightmapSurfaceId(Guid ObjectId, int SurfaceIndex, bool IsDetail);
public sealed record LightmapMaterial(Texture? Texture, Color Color, SurfaceMode SurfaceMode, float AlphaCutoff,
    bool BackfaceCulling, bool ReceivesLight, bool CastBakedShadow = true);

public sealed record LightmapSurface(LightmapSurfaceId Id, LightmapUvMesh Mesh, Matrix4x4 Model,
    int Page, Vector4 ScaleOffset, IReadOnlyList<LightmapMaterial> Materials, IReadOnlyList<int> TriangleMaterials)
{
    public Vector2 AtlasUV(Vector2 local)
    {
        return local * new Vector2(ScaleOffset.X, ScaleOffset.Y) + new Vector2(ScaleOffset.Z, ScaleOffset.W);
    }

    public LightmapTriangle[] WorldTriangles()
    {
        if (!Matrix4x4.Invert(Model, out var inverse))
        {
            throw new InvalidDataException("Singular lightmap transform.");
        }
        
        var normalMatrix = Matrix4x4.Transpose(inverse);
        
        var vertices = Mesh.Vertices.Span;
        var indices = Mesh.Indices.Span;
        var uv = Mesh.LightmapUv.Span;
        
        var result = new LightmapTriangle[indices.Length / 3];
        
        for (var at = 0; at < indices.Length; at += 3)
        {
            var corners = new LightmapCorner[3];
            
            for (var j = 0; j < 3; j++)
            {
                var index = (int)indices[at + j];
                var offset = index * 8;
                var position = Vector3.Transform(new Vector3(vertices[offset], vertices[offset + 1], vertices[offset + 2]), Model);
                var normal = Vector3.TransformNormal(new Vector3(vertices[offset + 3], vertices[offset + 4], vertices[offset + 5]), normalMatrix);

                if (normal.LengthSquared() > 1e-16f)
                {
                    normal = Vector3.Normalize(normal);
                }
                
                corners[j] = new LightmapCorner(position, normal, new Vector2(vertices[offset + 6], vertices[offset + 7]), AtlasUV(uv[index]));
            }
            
            result[at / 3] = new LightmapTriangle(Id, Page, corners[0], corners[1], corners[2], Materials[TriangleMaterials[at / 3]]);
        }
        
        return result;
    }
}

public readonly record struct LightmapCorner(Vector3 Position, Vector3 Normal, Vector2 TextureUV, Vector2 LightmapUV);
public sealed record LightmapTriangle(LightmapSurfaceId Surface, int Page, LightmapCorner A, LightmapCorner B,
    LightmapCorner C, LightmapMaterial Material);
public sealed record LightmapInstanceGroup(LightmapUvMesh Mesh, int Page, LightmapMaterial Material, IReadOnlyList<LightmapSurface> Instances);

/// <summary>CPU preparation only. No light is calculated, no atlas texture is created, and the live world is not modified.</summary>
public sealed class LightmapGeometry
{
    public IReadOnlyList<LightmapSurface> Surfaces { get; }
    public int AtlasSize { get; }
    public int PageCount { get; }
    public IReadOnlyList<string> Warnings { get; }

    private LightmapGeometry(List<LightmapSurface> surfaces, int atlasSize, int pages, List<string> warnings)
    {
        Surfaces = surfaces.AsReadOnly();
        AtlasSize = atlasSize;
        PageCount = pages;
        Warnings = warnings.AsReadOnly();
    }

    public LightmapTriangle[] WorldTriangles()
    {
        return Surfaces.SelectMany(surface => surface.WorldTriangles()).ToArray();
    }

    // Instances share the UV layout and mesh, but each placement has its own atlas rectangle.
    public LightmapInstanceGroup[] DetailGroups()
    {
        return Surfaces.Where(surface => surface.Id.IsDetail)
            .GroupBy(surface => (surface.Mesh, surface.Page, Material: surface.Materials[0]))
            .Select(group => new LightmapInstanceGroup(group.Key.Mesh, group.Key.Page, group.Key.Material, group.ToArray())).ToArray();
    }

    public static LightmapGeometry Prepare(World world, LightmapPreparationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        
        options ??= new LightmapPreparationOptions();
        options.Validate();
        
        var surfaces = new List<LightmapSurface>();
        var warnings = new List<string>();
        
        var templates = new Dictionary<Mesh, LightmapUvMesh>();
        
        var seen = new HashSet<LightmapSurfaceId>();
        
        var perRow = options.AtlasSize / options.SurfaceSize;
        var perPage = perRow * perRow;

        LightmapUvMesh Template(Mesh mesh)
        {
            ObjectDisposedException.ThrowIf(mesh.IsDisposed, mesh);
            
            if (!templates.TryGetValue(mesh, out var template))
            {
                template = LightmapUvMesh.Generate(mesh.Vertices.ToArray(), mesh.Indices.ToArray(), options.SurfaceSize, options.Padding);
                templates.Add(mesh, template);
            }
            
            return template;
        }

        void Add(LightmapSurfaceId id, LightmapUvMesh mesh, Matrix4x4 model, LightmapMaterial[] materials, int[] triangleMaterials)
        {
            if (id.ObjectId == Guid.Empty || !seen.Add(id))
            {
                throw new InvalidDataException($"Duplicate or empty lightmap identity '{id}'.");
            }

            if (!Matrix4x4.Invert(model, out var inverse) || !float.IsFinite(inverse.M11) ||
                !float.IsFinite(inverse.M22) ||
                !float.IsFinite(inverse.M33) || !float.IsFinite(model.M41) || !float.IsFinite(model.M42) ||
                !float.IsFinite(model.M43))
            {
                throw new InvalidDataException($"Invalid lightmap transform for '{id}'.");
            }
            
            var tile = surfaces.Count % perPage;
            
            var ratio = (float)options.SurfaceSize / options.AtlasSize;
            var rect = new Vector4(ratio, ratio, (tile % perRow) * ratio, (tile / perRow) * ratio);
            
            surfaces.Add(new LightmapSurface(id, mesh, model, surfaces.Count / perPage, rect, materials, triangleMaterials));
        }

        Texture? TextureFor(string name, TextureFormat format)
        {
            return name == "WhitePixel"
                ? null
                : Content.LoadTexture(name, format) ??
                  throw new FileNotFoundException($"Lightmap surface texture '{name}' was not found.");
        }

        foreach (var obj in world.GetAllObjects(true).OrderBy(obj => obj.Guid))
        {
            if (!obj.IsStatic || !obj.IsActive || obj.IsDestroyed)
            {
                continue;
            }
            
            // Editor visibility is deliberately ignored: hidden folders are still part of the level.
            if (obj is BrushBox brush)
            {
                var renderer = brush.GetRenderComponents().OfType<MeshComponent>()
                    .FirstOrDefault(component => component.IsEnabled && !component.IsDebug);

                if (renderer == null)
                {
                    continue;
                }

                if (renderer.Shader != null)
                {
                    warnings.Add($"{obj.Name}: preparation uses the undeformed source mesh; custom shader deformation is not baked.");
                }
                
                var mesh = LightmapUvMesh.GenerateBrush(brush.Size, brush.Transform.Scale, brush.TextureScale,
                    brush.Faces, options.SurfaceSize, options.Padding);
                
                var materials = Enumerable.Range(0, 6).Select(index =>
                {
                    brush.Faces.TryGetValue((BrushFace)index, out var face);
                    
                    var texture = BrushFaceData.GetTexture(face, brush.TextureName, brush.TextureFormat);
                    var mode = face?.SurfaceMode ?? brush.SurfaceMode;
                    
                    return new LightmapMaterial(TextureFor(texture.Name, texture.Format), brush.Color, mode,
                        face?.AlphaCutoff ?? brush.AlphaCutoff, renderer.BackfaceCulling, brush.Lit && mode != SurfaceMode.Transparent);
                }).ToArray();
                
                var model = Matrix4x4.CreateScale(renderer.UseParentScale ? brush.Transform.Scale * renderer.Scale : new Vector3(renderer.Scale)) * Matrix4x4.CreateFromQuaternion(brush.Transform.Rotation * renderer.LocalRotation) *
                    Matrix4x4.CreateTranslation(brush.Transform.Position + renderer.Offset);
                
                Add(new LightmapSurfaceId(obj.Guid, 0, false), mesh, model, materials, Enumerable.Range(0, 12).Select(i => i / 2).ToArray());
            }
            else if (obj is Geometry)
            {
                var components = obj.GetRenderComponents().OfType<MeshComponent>().ToArray();
                
                for (var index = 0; index < components.Length; index++)
                {
                    var component = components[index];
                    
                    if (!component.IsEnabled || component.IsDebug || component.Mesh == null)
                    {
                        continue;
                    }
                    
                    var mesh = Template(component.Mesh);
                    var model = Matrix4x4.CreateScale(component.UseParentScale ? obj.Transform.Scale * component.Scale : new Vector3(component.Scale)) *
                        Matrix4x4.CreateFromQuaternion(obj.Transform.Rotation * component.LocalRotation) *
                        Matrix4x4.CreateTranslation(obj.Transform.Position + component.Offset);

                    if (component.Shader != null)
                    {
                        warnings.Add($"{obj.Name}: preparation uses the undeformed source mesh; custom shader deformation is not baked.");
                    }
                    
                    Add(new LightmapSurfaceId(obj.Guid, index, false), mesh, model,
                        [new(component.Texture, component.Color, component.SurfaceMode, component.AlphaCutoff, component.BackfaceCulling,
                            component.Lit && component.SurfaceMode != SurfaceMode.Transparent)],
                        new int[mesh.Indices.Length / 3]);
                }
            }
        }

        foreach (var detail in world.Details.OrderBy(detail => detail.Id))
        {
            detail.Validate();
            
            var asset = Content.LoadDetail(detail.AssetPath) ?? throw new FileNotFoundException(detail.AssetPath);
            var source = Content.LoadMesh(asset.Mesh) ?? throw new FileNotFoundException(asset.Mesh);
            
            var mesh = Template(source);
            
            var model = Matrix4x4.CreateScale(detail.Scale) * Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(detail.Rotation)) *
                Matrix4x4.CreateTranslation(detail.Position);
            
            Add(new LightmapSurfaceId(detail.Id, 0, true), mesh, model,
                [new(TextureFor(asset.Texture, asset.TextureFormat), asset.Color, SurfaceMode.Cutout, .5f, asset.BackfaceCulling, asset.Lit, asset.CastBakedShadow)],
                new int[mesh.Indices.Length / 3]);
        }

        return new LightmapGeometry(surfaces, options.AtlasSize, (surfaces.Count + perPage - 1) / perPage, warnings);
    }
}