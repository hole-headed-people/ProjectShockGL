using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Tools;

namespace ProjectShockGLEditor;

public sealed class ParticleAssetDocument
{
    private string _saved = "";
    
    public ParticleAsset Asset { get; private set; } = new();
    
    public string? FilePath { get; private set; }
    public string DirectoryPath => Path.GetFullPath(Path.Combine(Content.Path, "Particles"));
    
    public bool IsDirty => Serialize() != _saved;
    
    public int Revision { get; private set; }

    public ParticleAssetDocument()
    {
        New();
    }

    private string Serialize()
    {
        return JsonConvert.SerializeObject(Asset, JsonHelper.GetSettings());
    }

    public void New()
    {
        Asset = new ParticleAsset();
        FilePath = null;
        
        _saved = Serialize();
        
        Revision++;
    }

    public void Set(string property, object? value)
    {
        if (property == nameof(ParticleAsset.Version) || typeof(ParticleAsset).GetProperty(property) == null)
        {
            throw new ArgumentException("Unknown or read-only particle setting.", nameof(property));
        }
        
        var serializer = JsonSerializer.Create(JsonHelper.GetSettings());
        var json = JObject.FromObject(Asset, serializer);
        
        json[property] = value == null ? JValue.CreateNull() : JToken.FromObject(value, serializer);
        
        var next = json.ToObject<ParticleAsset>(serializer)!;
        next.Validate();

        if (JToken.DeepEquals(json, JObject.FromObject(Asset, serializer)))
        {
            return;
        }
        
        Asset = next;
        Revision++;
    }

    public void Open(string path)
    {
        path = CheckedPath(path);
        
        var asset = JsonConvert.DeserializeObject<ParticleAsset>(File.ReadAllText(path), JsonHelper.GetSettings())
            ?? throw new InvalidDataException("Particle asset is empty.");
        
        asset.Validate();
        
        Asset = asset;
        FilePath = path;
        
        _saved = Serialize();
        
        Revision++;
    }

    public void Save(string? path = null)
    {
        path = CheckedPath(path ?? FilePath ?? throw new InvalidOperationException("Choose a file for this particle asset."));
        
        var name = Path.ChangeExtension(Path.GetRelativePath(DirectoryPath, path), null)!.Replace('\\', '/');
        
        Content.SaveParticle(name, Asset);
        FilePath = path;
        
        _saved = Serialize();
    }

    private string CheckedPath(string path)
    {
        path = Path.GetFullPath(path);

        if (!path.StartsWith(DirectoryPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Choose a JSON file inside Content/Particles.");
        }
        
        return path;
    }
}