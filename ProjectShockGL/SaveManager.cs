using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL.Tools;

namespace ProjectShockGL;

public enum SaveResult
{
    Success,
    Failed
}

public enum LoadResult
{
    Success,
    Failed
}

public readonly record struct SaveSlot(string Id, DateTime LastWriteTimeUtc);

public static class SaveManager
{
    public static event Action? Saving;
    public static event Action<SaveResult>? Saved;
    public static event Action<LoadResult>? Loaded;

    private static SaveData _data = new();
    
    private static bool _isSavingOrLoading;
    private static string? _selectedSlotId;

    public static IReadOnlyList<SaveSlot> GetSlots()
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(GetDefaultPath())!);

        if (!directory.Exists)
        {
            return [];
        }

        return directory.EnumerateFiles("*.save")
            .Select(file => new SaveSlot(Path.GetFileNameWithoutExtension(file.Name), file.LastWriteTimeUtc))
            .OrderByDescending(slot => slot.LastWriteTimeUtc)
            .ThenBy(slot => slot.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static string CreateSlot()
    {
        _selectedSlotId = $"slot-{Guid.NewGuid():N}";
        
        return _selectedSlotId;
    }

    public static void SelectSlot(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (GetSlots().All(slot => slot.Id != id))
        {
            throw new FileNotFoundException($"Save slot '{id}' does not exist.");
        }

        _selectedSlotId = id;
    }

    public static void Save()
    {
        if (_isSavingOrLoading)
        {
            return;
        }

        var result = SaveResult.Failed;
        
        _isSavingOrLoading = true;
        
        try
        {
            var path = GetPath();
            Saving?.Invoke();

            _data.Validate();
            
            var json = JsonConvert.SerializeObject(_data, JsonHelper.GetSettings());
            WriteFile(path, json);
            
            result = SaveResult.Success;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Failed to save game: {error.Message}");
        }
        finally
        {
            _isSavingOrLoading = false;
        }

        Saved?.Invoke(result);
    }

    public static void Load()
    {
        if (_isSavingOrLoading)
        {
            return;
        }

        var result = LoadResult.Failed;
        
        _isSavingOrLoading = true;
        
        try
        {
            var json = JObject.Parse(File.ReadAllText(GetPath()));
            var serializer = JsonSerializer.Create(JsonHelper.GetSettings());
            var data = ReadData(json, serializer);

            data.Validate();
            
            _data = data;
            
            result = LoadResult.Success;
        }
        
        catch (Exception error)
        {
            Console.Error.WriteLine($"Failed to load game: {error.Message}");
        }
        finally
        {
            _isSavingOrLoading = false;
        }

        Loaded?.Invoke(result);
    }

    public static SaveData GetData()
    {
        return _data;
    }
    
    private static string GetPath()
    {
        var defaultPath = GetDefaultPath();
        
        return _selectedSlotId == null ? defaultPath :
            Path.Combine(Path.GetDirectoryName(defaultPath)!, _selectedSlotId + ".save");
    }

    private static string GetDefaultPath()
    {
        if (Application.IsEditor)
        {
            throw new InvalidOperationException("Game saves are only available in the game.");
        }

        var directory = Settings.GetString("Game", "SaveDirectory", "");

        if (string.IsNullOrWhiteSpace(directory))
        {
            var legacyPath = Settings.GetString("Game", "SavePath", "");

            if (!string.IsNullOrWhiteSpace(legacyPath))
            {
                return Path.GetFullPath(legacyPath, AppContext.BaseDirectory);
            }

            directory = "Saves";
        }

        if (Path.IsPathFullyQualified(directory))
        {
            return Path.Combine(directory, "Game.save");
        }

        var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        var gameName = Settings.GetString("Game", "Name", "ProjectShockGL").Trim();

        if (string.IsNullOrEmpty(localAppData))
        {
            throw new InvalidOperationException("The user's local application data directory is unavailable.");
        }

        if (string.IsNullOrEmpty(gameName) || gameName.EndsWith('.') ||
            gameName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException("Game Name must be a valid folder name when SaveDirectory is relative.");
        }

        var gameDirectory = Path.Combine(localAppData, gameName);
        var saveDirectory = Path.GetFullPath(directory, gameDirectory);
        var relativePath = Path.GetRelativePath(gameDirectory, saveDirectory);

        if (Path.IsPathRooted(directory) || relativePath == ".." ||
            relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A relative SaveDirectory must stay inside the game's data directory.");
        }

        return Path.Combine(saveDirectory, "Game.save");
    }

    private static SaveData ReadData(JObject json, JsonSerializer serializer)
    {
        if (json.TryGetValue(nameof(SaveData.Levels), out _))
        {
            return json.ToObject<SaveData>(serializer)!;
        }
        
        var level = json.Value<string>("Level");
        var objects = json["Objects"]?.ToObject<Dictionary<Guid, SavedObjectState>>(serializer);
        
        if (string.IsNullOrWhiteSpace(level) || objects == null)
        {
            throw new InvalidDataException("The save must contain level states.");
        }

        return new SaveData { Levels = { [level] = objects } };
    }

    private static void WriteFile(string path, string json)
    {
        var directory = Path.GetDirectoryName(path)!;
        
        Directory.CreateDirectory(directory);
        
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporary, json);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
