using System.Diagnostics;
using System.Numerics;
using ProjectShockGL.Core;

namespace ProjectShockGL.Rendering;

public sealed class DirectLightBakeOptions
{
    public float RayBias { get; init; } = .001f;
    public int SamplesPerTexel { get; init; } = 1;
    public int IndirectSamples { get; init; }
    public float IndirectStrength { get; init; } = 1f;
    public int IndirectFilterRadius { get; init; } = 1;

    internal void Validate()
    {
        if (!float.IsFinite(RayBias) || RayBias <= 0 || SamplesPerTexel is not (1 or 4) ||
            IndirectSamples < 0 || IndirectSamples > 4096 || !float.IsFinite(IndirectStrength) || IndirectStrength < 0 || IndirectFilterRadius < 0 || IndirectFilterRadius > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(RayBias), "Use a positive ray bias, 1 or 4 texel samples, 0..4096 indirect samples, nonnegative indirect strength and filter radius 0..2.");
        }
    }
}

public readonly record struct BakedLightSource(Guid Id, LightType Type, Vector3 Position, Vector3 Direction,
    Vector3 Color, float Intensity, float Radius, float InnerCosine, float OuterCosine);

public sealed class DirectLightBakeInput
{
    public LightmapGeometry Geometry { get; }
    public Vector3 Ambient { get; }
    public IReadOnlyList<BakedLightSource> Lights { get; }
    
    internal LightmapTriangle[] Triangles { get; }
    internal LightmapAlpha?[] Alpha { get; }
    internal LightmapAlbedo?[] Albedo { get; }

    internal DirectLightBakeInput(LightmapGeometry geometry, Vector3 ambient, BakedLightSource[] lights,
        LightmapTriangle[] triangles, LightmapAlpha?[] alpha, LightmapAlbedo?[] albedo)
    {
        Geometry = geometry;
        Ambient = ambient;
        
        Lights = Array.AsReadOnly(lights);
        
        Triangles = triangles;
        
        Alpha = alpha;
        Albedo = albedo;
    }
}

public sealed class BakedLightmapPage
{
    private readonly Vector3[] _pixels;
    private readonly byte[] _coverage;
    public int Size { get; }
    
    public ReadOnlyMemory<Vector3> Pixels => _pixels;
    public ReadOnlyMemory<byte> Coverage => _coverage;
    internal Vector3[] Values => _pixels;
    internal byte[] Covered => _coverage;

    internal BakedLightmapPage(int size)
    {
        Size = size;
        
        _pixels = new Vector3[checked(size * size)];
        _coverage = new byte[_pixels.Length];
    }
}

public readonly record struct LightmapBakeProgress(int CompletedTriangles, int TotalTriangles);
public sealed record DirectLightBakeResult(LightmapGeometry Geometry, IReadOnlyList<BakedLightmapPage> Pages,
    long ShadowRays, long TriangleTests, int MissedTriangles, TimeSpan Elapsed)
{
    public long IndirectRays { get; init; }
}

public static partial class DirectLightBaker
{
    /// <summary>Call on the main thread before editing/unloading the level. Bake uses only the captured CPU data.</summary>
    public static DirectLightBakeInput Capture(World world, LightmapGeometry? geometry = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        
        geometry ??= world.PrepareLightmapGeometry();
        
        var color = Environment.AmbientColor;
        var ambient = new Vector3(color.R, color.G, color.B);

        if (!Finite(ambient) || Vector3.Min(ambient, Vector3.Zero) != Vector3.Zero)
        {
            throw new InvalidDataException("Baked Ambient must be finite and nonnegative.");
        }

        var lights = world.GetAllObjects(true).OfType<Light>()
            .Where(light => light.Mode == LightMode.Baked && light.IsActive && !light.IsDestroyed)
            .OrderBy(light => light.Guid).Select(light =>
            {
                var position = light.Transform.Position;
                var direction = light.Transform.Forward;
                var rgb = new Vector3(light.Color.R, light.Color.G, light.Color.B);

                if (!Enum.IsDefined(light.Type) || !Finite(position) || !Finite(direction) ||
                    direction.LengthSquared() < 1e-16f ||
                    !Finite(rgb) || Vector3.Min(rgb, Vector3.Zero) != Vector3.Zero ||
                    !float.IsFinite(light.Intensity) || light.Intensity < 0 || !float.IsFinite(light.Radius))
                {
                    throw new InvalidDataException($"Invalid Baked light '{light.Name}'.");
                }
                
                return new BakedLightSource(light.Guid, light.Type, position, Vector3.Normalize(direction), rgb,
                    light.Intensity, light.Radius, MathF.Cos(light.InnerAngle * MathF.PI / 360f),
                    MathF.Cos(light.OuterAngle * MathF.PI / 360f));
            }).ToArray();

        var triangles = geometry.WorldTriangles();
        var alpha = new LightmapAlpha?[triangles.Length];
        var textures = new Dictionary<Texture, LightmapAlpha?>();
        var albedo = new LightmapAlbedo?[triangles.Length];
        var colorTextures = new Dictionary<Texture, LightmapAlbedo>();
        
        for (var i = 0; i < triangles.Length; i++)
        {
            var triangle = triangles[i];
            
            foreach (var corner in new[] { triangle.A, triangle.B, triangle.C })
            {
                if (!Finite(corner.Position) || !Finite(corner.Normal) ||
                    !float.IsFinite(corner.TextureUV.X) || !float.IsFinite(corner.TextureUV.Y) ||
                    !float.IsFinite(corner.LightmapUV.X) || !float.IsFinite(corner.LightmapUV.Y))
                {
                    throw new InvalidDataException($"Invalid baked triangle '{triangle.Surface}'.");
                }
            }

            var tint = triangle.Material.Color;
            if (!float.IsFinite(tint.R) || !float.IsFinite(tint.G) || !float.IsFinite(tint.B))
                throw new InvalidDataException("Surface reflectance must be finite.");
            if (triangle.Material.ReceivesLight && triangle.Material.Texture is { } colorTexture)
            {
                if (!colorTextures.TryGetValue(colorTexture, out var capturedColor))
                {
                    capturedColor = colorTexture.CaptureBakeAlbedo();
                    colorTextures.Add(colorTexture,capturedColor);
                }
                albedo[i] = capturedColor;
            }

            if (triangle.Page < 0 || triangle.Page >= geometry.PageCount)
            {
                throw new InvalidDataException("Lightmap triangle has an invalid atlas page.");
            }

            if (triangle.Material.SurfaceMode != SurfaceMode.Cutout || triangle.Material.Texture is not { } texture)
            {
                continue;
            }
            
            if (!textures.TryGetValue(texture, out var captured))
            {
                captured = texture.CaptureBakeAlpha();
                textures.Add(texture, captured);
            }
            
            alpha[i] = captured;
        }

        return new DirectLightBakeInput(geometry, ambient, lights, triangles, alpha, albedo);
    }

    public static DirectLightBakeResult Bake(DirectLightBakeInput input, DirectLightBakeOptions? options = null,
        CancellationToken cancellationToken = default, IProgress<LightmapBakeProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        
        options ??= new DirectLightBakeOptions();
        options.Validate();
        
        cancellationToken.ThrowIfCancellationRequested();
        
        var timer = Stopwatch.StartNew();
        var scene = new LightmapOcclusion(input.Triangles, input.Alpha, cancellationToken);
        
        var size = input.Geometry.AtlasSize;
        
        var pages = Enumerable.Range(0, input.Geometry.PageCount).Select(_ => new BakedLightmapPage(size)).ToArray();
        var samples = Enumerable.Range(0, pages.Length).Select(_ => new int[checked(size * size)]).ToArray();
        
        Vector3[][]? indirect = null;
        int[][]? indirectTriangles = null;
        if (options.IndirectSamples > 0 && options.IndirectStrength > 0 && options.IndirectFilterRadius > 0)
        {
            indirect = Enumerable.Range(0,pages.Length).Select(_=>new Vector3[checked(size*size)]).ToArray();
            indirectTriangles = Enumerable.Range(0,pages.Length).Select(_=>new int[checked(size*size)]).ToArray();
        }

        var missed = 0;
        
        for (var id = 0; id < input.Triangles.Length; id++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var t = input.Triangles[id];
            
            if (!t.Material.ReceivesLight)
            {
                progress?.Report(new LightmapBakeProgress(id + 1, input.Triangles.Length));
               
                continue;
            }
            
            var a = t.A.LightmapUV * size;
            var b = t.B.LightmapUV * size;
            var c = t.C.LightmapUV * size;
            
            var area = Cross(b - a, c - a);

            if (MathF.Abs(area) < 1e-12f)
            {
                missed++; 
                
                continue;
            }
            
            var min = Vector2.Min(a, Vector2.Min(b, c));
            var max = Vector2.Max(a, Vector2.Max(b, c));
            
            var x0 = Math.Clamp((int)MathF.Floor(min.X), 0, size - 1);
            var x1 = Math.Clamp((int)MathF.Ceiling(max.X), 0, size - 1);
            var y0 = Math.Clamp((int)MathF.Floor(min.Y), 0, size - 1);
            var y1 = Math.Clamp((int)MathF.Ceiling(max.Y), 0, size - 1);
            
            var wrote = false;
            
            for (var y = y0; y <= y1; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                for (var x = x0; x <= x1; x++)
                {
                    for (var sample = 0; sample < options.SamplesPerTexel; sample++)
                    {
                        var offset = options.SamplesPerTexel == 1 ? new Vector2(.5f) :
                            new Vector2((sample % 2 + .5f) / 2, (sample / 2 + .5f) / 2);
                        
                        var p = new Vector2(x, y) + offset;
                        
                        var wb = Cross(p - a, c - a) / area;
                        var wc = Cross(b - a, p - a) / area;
                        var wa = 1 - wb - wc;

                        if (wa < -1e-6f || wb < -1e-6f || wc < -1e-6f)
                        {
                            continue;
                        }
                        
                        var uv = t.A.TextureUV * wa + t.B.TextureUV * wb + t.C.TextureUV * wc;

                        if (t.Material.SurfaceMode == SurfaceMode.Cutout &&
                            (input.Alpha[id]?.Sample(uv) ?? 1) < t.Material.AlphaCutoff)
                        {
                            continue;
                        }
                        
                        var point = t.A.Position * wa + t.B.Position * wb + t.C.Position * wc;
                        var normal = t.A.Normal * wa + t.B.Normal * wb + t.C.Normal * wc;

                        if (normal.LengthSquared() < 1e-16f)
                        {
                            normal = Vector3.Cross(t.B.Position - t.A.Position, t.C.Position - t.A.Position);
                        }

                        if (normal.LengthSquared() < 1e-16f)
                        {
                            continue;
                        }
                        
                        normal = Vector3.Normalize(normal);
                        
                        var value = Evaluate(input, scene, point, normal, id, options.RayBias);
                        var index = y * size + x;
                        if (options.IndirectSamples > 0 && options.IndirectStrength > 0)
                        {
                            var seed = unchecked((uint)id * 73856093u ^ (uint)x * 19349663u ^ (uint)y * 83492791u ^ (uint)sample * 2654435761u);
                            var bounced = EvaluateIndirect(input,scene,point,normal,id,options,seed,cancellationToken);
                            if (indirect != null)
                            {
                                indirect[t.Page][index] += bounced;
                                indirectTriangles![t.Page][index] = id+1;
                            }
                            else value += bounced;
                            if (!Finite(value)) throw new InvalidDataException("Indirect lighting overflowed.");
                        }
                        
                        pages[t.Page].Values[index] += value;
                        samples[t.Page][index]++;
                        pages[t.Page].Covered[index] = 1;
                        
                        wrote = true;
                    }
                }
            }

            if (!wrote)
            {
                missed++;
            }
            
            progress?.Report(new LightmapBakeProgress(id + 1, input.Triangles.Length));
        }
        
        for (var page = 0; page < pages.Length; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            for (var i = 0; i < samples[page].Length; i++)
            {
                if (samples[page][i] > 0)
                {
                    pages[page].Values[i] /= samples[page][i];
                    if (indirect != null) indirect[page][i] /= samples[page][i];
                }
            }
        }
        
        if (indirect != null)
            AddFilteredIndirect(input,pages,indirect,indirectTriangles!,options.IndirectFilterRadius,cancellationToken);

        return new DirectLightBakeResult(input.Geometry, Array.AsReadOnly(pages), scene.Rays, scene.TriangleTests,
            missed, timer.Elapsed) { IndirectRays = scene.IndirectRays };
    }

    internal static Vector3 Sample(DirectLightBakeInput input, Vector3 position, Vector3 normal, float bias = .001f)
    {
        return Evaluate(input, new LightmapOcclusion(input.Triangles, input.Alpha), position, Vector3.Normalize(normal), -1, bias);
    }

    private static Vector3 Evaluate(DirectLightBakeInput input, LightmapOcclusion scene, Vector3 position, Vector3 normal,
        int triangle, float bias, bool includeAmbient = true)
    {
        var result = includeAmbient ? input.Ambient : Vector3.Zero;
        
        foreach (var light in input.Lights)
        {
            Vector3 direction;
            
            var attenuation = 1f;
            
            if (light.Type == LightType.Directional)
            {
                direction = -light.Direction;
            }
            else
            {
                var delta = light.Position - position;
                var distance = delta.Length();

                if (distance <= .00001f || distance >= light.Radius)
                {
                    continue;
                }
                
                direction = delta / distance;
                attenuation = Math.Clamp(1 - distance / light.Radius, 0, 1);
                
                if (light.Type == LightType.Spot)
                {
                    var cosine = Vector3.Dot(-direction, light.Direction);
                    var width = light.InnerCosine - light.OuterCosine;
                    
                    var factor = width < .000001f ? (cosine >= light.OuterCosine ? 1f : 0f)
                        : Math.Clamp((cosine - light.OuterCosine) / width, 0, 1);

                    if (width >= .000001f)
                    {
                        factor = factor * factor * (3 - 2 * factor);
                    }
                    
                    attenuation *= factor;
                }
            }
            
            var diffuse = MathF.Max(Vector3.Dot(normal, direction), 0);
            
            if (diffuse <= 0 || attenuation <= 0 || light.Intensity <= 0)
            {
                continue;
            }
            
            var origin = position + normal * bias;
            var rayDirection = direction;
            var rayDistance = float.PositiveInfinity;
            
            if (light.Type != LightType.Directional)
            {
                var toLight = light.Position - origin;
                rayDistance = toLight.Length();

                if (rayDistance <= bias)
                {
                    continue;
                }
                
                rayDirection = toLight / rayDistance;
            }

            if (!scene.Blocked(origin, rayDirection, rayDistance, triangle, bias * .1f))
            {
                result += light.Color * (light.Intensity * diffuse * attenuation);
            }
        }
        
        if (!Finite(result)) throw new InvalidDataException("Baked lighting overflowed. Reduce light color or intensity.");
        return result;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    private static bool Finite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }
}