using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

public readonly record struct TextureRegion(Texture Texture, Vector4 UvRect);

public sealed class TextureAtlas : IDisposable
{
    private readonly Dictionary<string, TextureRegion> _regions;

    public Texture Texture { get; }

    private TextureAtlas(Texture texture, Dictionary<string, TextureRegion> regions)
    {
        Texture = texture;
        _regions = regions;
    }

    public TextureRegion GetRegion(string name) => _regions[name];

    internal static TextureAtlas Load(GL gl, string descriptorPath, string texturePath)
    {
        var json = JObject.Parse(File.ReadAllText(descriptorPath),
            new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        
        var data = json.ToObject<AtlasData>()!;
        var texture = Texture.Load(gl, texturePath);

        try
        {
            var regions = new Dictionary<string, TextureRegion>(StringComparer.Ordinal);
            var dimensions = new Vector4(texture.Width, texture.Height, texture.Width, texture.Height);

            foreach (var (name, bounds) in data.Regions)
            {
                if (string.IsNullOrWhiteSpace(name) || bounds == null || bounds.X < 0 || bounds.Y < 0 ||
                    bounds.Width <= 0 || bounds.Height <= 0 ||
                    bounds.X > texture.Width - bounds.Width || bounds.Y > texture.Height - bounds.Height)
                {
                    throw new InvalidDataException($"Invalid region '{name}' in texture atlas '{descriptorPath}'.");
                }

                var uv = new Vector4(bounds.X, bounds.Y, bounds.Width, bounds.Height) / dimensions;
                regions.Add(name, new TextureRegion(texture, uv));
            }

            return new TextureAtlas(texture, regions);
        }
        catch
        {
            texture.Dispose();
            
            throw;
        }
    }

    public void Dispose()
    {
        Texture.Dispose();
    }

    private sealed class AtlasData
    {
        [JsonProperty(Required = Required.Always)]
        public Dictionary<string, RegionBounds> Regions { get; init; } = [];
    }

    private sealed class RegionBounds
    {
        [JsonProperty(Required = Required.Always)]
        public int X { get; init; }
        [JsonProperty(Required = Required.Always)]
        public int Y { get; init; }
        [JsonProperty(Required = Required.Always)]
        public int Width { get; init; }
        [JsonProperty(Required = Required.Always)]
        public int Height { get; init; }
    }
}
