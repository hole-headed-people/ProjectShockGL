using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL.Tools;

namespace ProjectShockGL.Core;

/// <summary>Persistent entity asset identities, separate from the GUIDs of placed objects.</summary>
public static class EntityAssetReferences
{
    private static Dictionary<Guid, string>? _paths;
    private static string? _root;

    public static void Refresh()
    {
        _paths = null;
    }

    private static JObject Read(string path)
    {
        using var text = File.OpenText(path);
        using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None };
        return JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
    }

    private static Guid? ReadId(JObject json, string path)
    {
        var token = json["AssetId"];
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        if (token.Type == JTokenType.String && Guid.TryParse(token.Value<string>(), out var id) && id != Guid.Empty)
        {
            return id;
        }

        throw new InvalidDataException($"Invalid entity AssetId in '{path}'.");
    }

    private static void BuildIndex()
    {
        var root = Path.GetFullPath(Path.Combine(Content.Path, "Entities"));
        var paths = new Dictionary<Guid, string>();

        if (Directory.Exists(root))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
            {
                var id = ReadId(Read(file), file);
                
                if (id == null)
                {
                    continue;
                }

                var name = Path.ChangeExtension(Path.GetRelativePath(root, file), null)!.Replace('\\', '/');
                
                if (!paths.TryAdd(id.Value, name))
                {
                    throw new InvalidDataException($"Duplicate entity AssetId '{id}': '{paths[id.Value]}' and '{name}'.");
                }
            }
        }

        _root = root;
        _paths = paths;
    }

    public static string ResolvePath(string path, Guid? assetId = null)
    {
        if (assetId == null)
        {
            return path;
        }

        if (assetId == Guid.Empty)
        {
            throw new InvalidDataException("An entity asset GUID cannot be empty.");
        }

        var root = Path.GetFullPath(Path.Combine(Content.Path, "Entities"));
        if (_paths == null || _root != root)
        {
            BuildIndex();
        }

        // A move made outside the editor invalidates the cached location.
        if (!_paths!.TryGetValue(assetId.Value, out var name) ||
            !File.Exists(Path.Combine(root, name + ".json")) ||
            ReadId(Read(Path.Combine(root, name + ".json")), name) != assetId)
        {
            BuildIndex();
            
            if (!_paths!.TryGetValue(assetId.Value, out name))
            {
                throw new FileNotFoundException($"Entity asset '{assetId}' was not found (previous path: '{path}').");
            }
        }

        return name;
    }

    private static Guid EnsureId(string name)
    {
        var root = Path.GetFullPath(Path.Combine(Content.Path, "Entities"));
        var path = Path.GetFullPath(Path.Combine(root, name + ".json"));

        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Entity assets must remain inside Content/Entities.");
        }

        var json = Read(path);
        
        if (ReadId(json, path) is { } existing)
        {
            // Also validate that this identifier is unique.
            ResolvePath(name, existing);
            
            return existing;
        }

        var id = Guid.NewGuid();
        
        json["AssetId"] = id;
        
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            File.WriteAllText(temporary, json.ToString(Formatting.Indented));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }

        Content.InvalidateEntity(name);
        return id;
    }

    // Called only on explicit map/prefab saves, never while inspecting or tracking dirty state.
    public static Placeable Upgrade(Placeable source)
    {
        if (source.Type != PlaceableType.Entity)
        {
            return source;
        }

        var path = ResolvePath(source.Path, source.AssetId);
        
        var id = source.AssetId ?? EnsureId(path);
        
        var serializer = JsonSerializer.Create(JsonHelper.GetSettings());
        var copy = JObject.FromObject(source, serializer);
        
        copy[nameof(Placeable.AssetId)] = id;
        copy[nameof(Placeable.Path)] = path;
        
        return copy.ToObject<Placeable>(serializer)!;
    }
}