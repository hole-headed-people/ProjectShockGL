using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL;

namespace ProjectShockGLEditor;

public sealed class UserSettingsDefaultOption
{
    public string Name { get; set; } = "";
    public UserSettingsOptionType Type { get; set; }
    public object DefaultValue { get; set; } = 0;
}

public sealed class UserSettingsDocument
{
    private string _saved;
    public string DirectoryPath { get; }
    public string? FilePath { get; private set; }
    public List<UserSettingsDefaultOption> Options { get; private set; } = [];
    public bool IsDirty => Snapshot() != _saved;

    public UserSettingsDocument(string directoryPath)
    {
        DirectoryPath = Path.GetFullPath(directoryPath);
        _saved = Snapshot();
    }

    private string Snapshot() => new JObject
    {
        ["Options"] = new JArray(Options.Select(option => new JObject
        {
            ["Name"] = option.Name,
            ["Type"] = option.Type.ToString(),
            ["DefaultValue"] = JToken.FromObject(option.DefaultValue)
        }))
    }.ToString(Formatting.Indented);

    public void New()
    {
        Options = [];
        FilePath = null;
        _saved = Snapshot();
    }

    private string CheckedPath(string path)
    {
        path = Path.GetFullPath(path);
        if (!path.StartsWith(DirectoryPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) ||
            !Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a JSON file inside Content.");
        return path;
    }

    public void Open(string path)
    {
        path = CheckedPath(path);
        var profile = UserSettingsProfile.Read(path);
        var options = profile.Options.Values.Select(option => new UserSettingsDefaultOption
        {
            Name = option.Name,
            Type = option.Type,
            DefaultValue = option.DefaultValue
        }).ToList();
        Options = options;
        FilePath = path;
        _saved = Snapshot();
    }

    public void Save(string path)
    {
        path = CheckedPath(path);
        var text = Snapshot();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, text);
            UserSettingsProfile.Read(temporary); // Validate with the runtime loader before replacing the asset.
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        FilePath = path;
        _saved = text;
    }
}
