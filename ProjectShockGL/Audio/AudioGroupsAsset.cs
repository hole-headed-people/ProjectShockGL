using Newtonsoft.Json;

namespace ProjectShockGL.Audio;

public sealed class AudioGroupData
{
    public string Name { get; set; } = "";
    public string? Parent { get; set; }
    public float Volume { get; set; } = 1;
}

public sealed class AudioGroupsAsset
{
    [JsonProperty(Required = Required.Always)]
    public List<AudioGroupData> Groups { get; set; } = [];

    public void Validate() => GetValidatedGroups();

    internal Dictionary<string, AudioGroupData> GetValidatedGroups()
    {
        if (Groups == null) throw new InvalidDataException("Audio Groups needs a Groups list.");
        var groups = new Dictionary<string, AudioGroupData>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Groups)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
                throw new InvalidDataException("Audio group names cannot be empty.");
            var name = entry.Name.Trim();
            if (!float.IsFinite(entry.Volume) || entry.Volume < 0 || entry.Volume > 1)
                throw new InvalidDataException($"Audio group '{name}': Volume must be between 0 and 1.");
            var master = name.Equals("Master", StringComparison.OrdinalIgnoreCase);
            if (master && !string.IsNullOrWhiteSpace(entry.Parent))
                throw new InvalidDataException("Master cannot have a parent.");
            if (!groups.TryAdd(name, new AudioGroupData
                {
                    Name = master ? "Master" : name,
                    Parent = master ? null : string.IsNullOrWhiteSpace(entry.Parent) ? "Master" : entry.Parent.Trim(),
                    Volume = entry.Volume
                }))
                throw new InvalidDataException($"Duplicate audio group '{name}'.");
        }
        groups.TryAdd("Master", new AudioGroupData { Name = "Master" });
        foreach (var entry in groups.Values)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var current = entry; current.Parent != null;)
            {
                if (!visited.Add(current.Name))
                    throw new InvalidDataException($"Audio group cycle involving '{current.Name}'.");
                if (!groups.TryGetValue(current.Parent, out var parent))
                    throw new InvalidDataException($"Audio group '{current.Name}': parent '{current.Parent}' does not exist.");
                current = parent;
            }
        }
        return groups;
    }

    public static AudioGroupsAsset Read(string path)
    {
        var asset = JsonConvert.DeserializeObject<AudioGroupsAsset>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Audio Groups file is empty.");
        asset.Validate();
        return asset;
    }
}
