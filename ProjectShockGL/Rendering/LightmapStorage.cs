using System.Numerics;
using ProjectShockGL.Core;

namespace ProjectShockGL.Rendering;

internal sealed record StoredLightmapSurface(LightmapSurfaceId Id, int Template, Matrix4x4 Model, int Page, Vector4 Region);
internal sealed record StoredLightmap(int Size, Vector3[][] Pages, LightmapUvMesh[] Templates, StoredLightmapSurface[] Surfaces);

internal static class LightmapStorage
{
    private const string Header = "ShockGL-Lightmap-1";

    internal static string GetPath(string name)
    {
        DetailAsset.ValidateReference(name, nameof(name));
        
        var root = Path.GetFullPath(Path.Combine(Content.Path, "Lightmaps"));
        var path = Path.GetFullPath(Path.Combine(root, name + ".lightmap"));

        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Lightmaps must remain inside Content/Lightmaps.");
        }
        
        return path;
    }

    internal static string Save(string name, DirectLightBakeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        DetailAsset.ValidateReference(name, nameof(name));
        
        var asset = name.Replace('\\', '/') + "-" + Guid.NewGuid().ToString("N");
        
        var path = GetPath(asset);
        
        var temporary = path + ".tmp";
        var templates = result.Geometry.Surfaces.Select(surface => surface.Mesh).Distinct().ToArray();

        if (result.Pages.Count != result.Geometry.PageCount ||
            result.Pages.Any(page => page.Size != result.Geometry.AtlasSize))
        {
            throw new InvalidDataException("Lightmap result does not match its geometry.");
        }
        
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        try
        {
            using (var stream = File.Create(temporary))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Header);
                writer.Write(result.Geometry.AtlasSize);
                writer.Write(result.Pages.Count);
                
                for (var page = 0; page < result.Pages.Count; page++)
                {
                    var values = Dilate(result, page);
                    
                    foreach (var value in values)
                    {
                        Write(writer, value);
                    }
                }
                
                writer.Write(templates.Length);
                
                foreach (var template in templates)
                {
                    writer.Write(template.Vertices.Length);
                    
                    foreach (var value in template.Vertices.Span)
                    {
                        writer.Write(value);
                    }
                    
                    writer.Write(template.Indices.Length);

                    foreach (var value in template.Indices.Span)
                    {
                        writer.Write(value);
                    }

                    foreach (var uv in template.LightmapUv.Span)
                    {
                        writer.Write(uv.X); 
                        writer.Write(uv.Y);
                    }
                }
                
                writer.Write(result.Geometry.Surfaces.Count);
                
                foreach (var surface in result.Geometry.Surfaces)
                {
                    writer.Write(surface.Id.ObjectId.ToByteArray());
                    writer.Write(surface.Id.SurfaceIndex); writer.Write(surface.Id.IsDetail);
                    writer.Write(Array.IndexOf(templates, surface.Mesh));
                    
                    var m = surface.Model;
                    
                    foreach (var value in new[]
                             {
                                 m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
                                 m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44
                             })
                    {
                        writer.Write(value);
                    }
                    
                    writer.Write(surface.Page);
                    writer.Write(surface.ScaleOffset.X); writer.Write(surface.ScaleOffset.Y);
                    writer.Write(surface.ScaleOffset.Z); writer.Write(surface.ScaleOffset.W);
                }
                
                writer.Flush();
                stream.Flush(true);
            }
            
            File.Move(temporary, path);
            
            return asset;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static Vector3[] Dilate(DirectLightBakeResult result, int page)
    {
        var size = result.Geometry.AtlasSize;
        
        var output = result.Pages[page].Pixels.ToArray();
        var covered = result.Pages[page].Coverage.ToArray();
        
        foreach (var surface in result.Geometry.Surfaces.Where(surface => surface.Page == page))
        {
            var r = surface.ScaleOffset;
            
            var x0 = (int)MathF.Round(r.Z * size); var y0 = (int)MathF.Round(r.W * size);
            
            var x1 = Math.Min(size, x0 + (int)MathF.Round(r.X * size));
            var y1 = Math.Min(size, y0 + (int)MathF.Round(r.Y * size));
            
            for (var pass = 0; pass < 2; pass++)
            {
                var fill = new List<(int Index, Vector3 Value)>();
                
                for (var y = y0; y < y1; y++) for (var x = x0; x < x1; x++)
                {
                    var index = y * size + x;
                    
                    if (covered[index] != 0)
                    {
                        continue;
                    }
                    
                    var sum = Vector3.Zero; var count = 0;
                    
                    foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                    {
                        var nx = x + dx; var ny = y + dy;
                        
                        if (nx < x0 || nx >= x1 || ny < y0 || ny >= y1 || covered[ny * size + nx] == 0)
                        {
                            continue;
                        }
                        
                        sum += output[ny * size + nx]; count++;
                    }

                    if (count != 0)
                    {
                        fill.Add((index, sum / count));
                    }
                }

                foreach (var value in fill)
                {
                    covered[value.Index] = 1; 
                    output[value.Index] = value.Value;
                }
            }
        }
        
        return output;
    }

    internal static StoredLightmap Read(string name)
    {
        using var stream = File.OpenRead(GetPath(name));
        using var reader = new BinaryReader(stream);

        if (reader.ReadString() != Header)
        {
            throw new InvalidDataException("Unknown lightmap format.");
        }
        
        var size = reader.ReadInt32();
        var pageCount = Count(reader, 4096);

        if (size < 8 || size > 8192 || (long)size * size * pageCount > 64L * 1024 * 1024)
        {
            throw new InvalidDataException("Invalid lightmap atlas size.");
        }
        
        var pages = new Vector3[pageCount][];
        
        for (var page = 0; page < pages.Length; page++)
        {
            if ((long)size * size * 12 > stream.Length - stream.Position)
            {
                throw new InvalidDataException("Truncated lightmap page.");
            }
            
            pages[page] = new Vector3[size * size];

            for (var i = 0; i < pages[page].Length; i++)
            {
                pages[page][i] = Read3(reader);
            }
        }
        
        var templates = new LightmapUvMesh[Count(reader, 1000000)];
        
        for (var t = 0; t < templates.Length; t++)
        {
            var vertexCount = Count(reader, 80_000_000);

            if (vertexCount == 0 || vertexCount % 8 != 0 || (long)vertexCount * 4 > stream.Length - stream.Position)
            {
                throw new InvalidDataException("Invalid lightmap mesh.");
            }
            
            var vertices = new float[vertexCount];

            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i] = ReadFloat(reader);
            }
            
            var indices = new uint[Count(reader, 30_000_000)];

            if (indices.Length == 0 || indices.Length % 3 != 0 ||
                (long)indices.Length * 4 > stream.Length - stream.Position)
            {
                throw new InvalidDataException("Invalid lightmap indices.");
            }
            
            for (var i = 0; i < indices.Length; i++)
            {
                indices[i] = reader.ReadUInt32();
                
                if (indices[i] >= vertexCount / 8)
                {
                    throw new InvalidDataException("Lightmap vertex index is out of range.");
                }
            }
            
            var uv = new Vector2[vertexCount / 8];
            
            for (var i = 0; i < uv.Length; i++)
            {
                uv[i] = new Vector2(ReadFloat(reader), ReadFloat(reader));
                
                if (uv[i].X < 0 || uv[i].X > 1 || uv[i].Y < 0 || uv[i].Y > 1)
                {
                    throw new InvalidDataException("Invalid lightmap UV.");
                }
            }
            
            templates[t] = LightmapUvMesh.Restore(vertices, indices, uv);
        }
        
        var surfaces = new StoredLightmapSurface[Count(reader, 1000000)];
        var ids = new HashSet<LightmapSurfaceId>();
        
        for (var i = 0; i < surfaces.Length; i++)
        {
            var guid = new Guid(reader.ReadBytes(16));
            var id = new LightmapSurfaceId(guid, reader.ReadInt32(), reader.ReadBoolean());
           
            var template = reader.ReadInt32();
            var f = Enumerable.Range(0, 16).Select(_ => ReadFloat(reader)).ToArray();
            
            var model = new Matrix4x4(f[0],f[1],f[2],f[3],f[4],f[5],f[6],f[7],f[8],f[9],f[10],f[11],f[12],f[13],f[14],f[15]);
            var page = reader.ReadInt32();
            
            var r = new Vector4(ReadFloat(reader), ReadFloat(reader), ReadFloat(reader), ReadFloat(reader));

            if (guid == Guid.Empty || id.SurfaceIndex < 0 || !ids.Add(id) || template < 0 ||
                template >= templates.Length ||
                page < 0 || page >= pages.Length || r.X <= 0 || r.Y <= 0 || r.Z < 0 || r.W < 0 ||
                r.X + r.Z > 1.000001f || r.Y + r.W > 1.000001f || !Matrix4x4.Invert(model, out _))
            {
                throw new InvalidDataException("Invalid lightmap surface binding.");
            }
            
            surfaces[i] = new StoredLightmapSurface(id, template, model, page, r);
        }

        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException("Unexpected lightmap trailing data.");
        }
        
        return new StoredLightmap(size, pages, templates, surfaces);
    }

    private static int Count(BinaryReader reader, int maximum)
    {
        var value = reader.ReadInt32();

        if (value < 0 || value > maximum)
        {
            throw new InvalidDataException("Invalid lightmap count.");
        }
        
        return value;
    }
    private static float ReadFloat(BinaryReader reader)
    {
        var value = reader.ReadSingle();
        
        if (!float.IsFinite(value))
        {
            throw new InvalidDataException("Nonfinite lightmap value.");
        }
        
        return value;
    }
    private static Vector3 Read3(BinaryReader reader)
    {
        return new Vector3(ReadFloat(reader), ReadFloat(reader), ReadFloat(reader));
    }

    private static void Write(BinaryWriter writer, Vector3 value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
        {
            throw new InvalidDataException("Cannot save nonfinite lightmap pixels.");
        }
        
        writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z);
    }
}