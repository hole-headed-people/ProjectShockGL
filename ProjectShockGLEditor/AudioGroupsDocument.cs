using Newtonsoft.Json;
using ProjectShockGL.Audio;

namespace ProjectShockGLEditor;

public sealed class AudioGroupsDocument
{
    private string _saved = "";
    
    public string DirectoryPath { get; }
    public string? FilePath { get; private set; }
    public AudioGroupsAsset Asset { get; private set; } = NewAsset();
    public bool IsDirty => Snapshot() != _saved;

    public AudioGroupsDocument(string directoryPath)
    {
        DirectoryPath = Path.GetFullPath(directoryPath);
        _saved = Snapshot();
    }

    private static AudioGroupsAsset NewAsset() => new()
    {
        Groups = [new AudioGroupData { Name = "Master" }]
    };

    private string Snapshot()
    {
        return JsonConvert.SerializeObject(Asset, Formatting.Indented);
    }

    public void New()
    {
        Asset = NewAsset();
        FilePath = null;
        
        _saved = Snapshot();
    }

    private string CheckedPath(string path)
    {
        path = Path.GetFullPath(path);

        if (!path.StartsWith(DirectoryPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) ||
            !Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Save Audio Groups as a JSON file inside Content/Audio.");
        }
        
        return path;
    }

    public void Open(string path)
    {
        path = CheckedPath(path);
        
        var asset = AudioGroupsAsset.Read(path);

        if (!asset.Groups.Any(group => group.Name.Trim().Equals("Master", StringComparison.OrdinalIgnoreCase)))
        {
            asset.Groups.Insert(0, new AudioGroupData { Name = "Master" });
        }
        
        Asset = asset;
        FilePath = path;
        
        _saved = Snapshot();
    }

    public void Save(string path)
    {
        path = CheckedPath(path);
        Asset.Validate();
        
        var text = Snapshot();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        
        try
        {
            File.WriteAllText(temporary, text);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
        
        FilePath = path;
        
        _saved = text;
    }
}
