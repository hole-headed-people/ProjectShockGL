using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ProjectShockGL;

public static class UserSettings
{
    public static event Action<IReadOnlyDictionary<string, UserSettingsOption>>? ProfileChanged;
    
    private static UserSettingsProfile? _profile;
    
    private static string? _path;

    public static UserSettingsProfile Profile => _profile ??
        throw new InvalidOperationException("UserSettings has not been initialized.");
    
    public static void Initialize()
    {
        if (_profile != null)
        {
            return;
        }

        if (Settings.IsEditor)
        {
            throw new InvalidOperationException("UserSettings is only available in the game.");
        }
        
        var name = Settings.GetString("Game", "UserSettingsDefaultProfile", string.Empty).Trim();
        var profile = string.IsNullOrEmpty(name) ? new UserSettingsProfile() : Content.LoadUserSettingsProfile(name);
        
        var fileName = Settings.GetString("Game", "UserSettingsProfile", "UserSettings").Trim();

        if (string.IsNullOrEmpty(fileName) || fileName is "." or ".." ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Path.HasExtension(fileName) ||
            fileName.EndsWith('.'))
        {
            throw new InvalidOperationException("UserSettingsProfile must be a file name without a directory or extension.");
        }
        
        var saves = Path.GetDirectoryName(SaveManager.GetDefaultPath())!;
        
        _path = Path.Combine(Directory.GetParent(saves)?.FullName ??
            throw new InvalidOperationException("SaveDirectory requires a parent directory."), fileName + ".json");
        
        Load(profile);
        
        _profile = profile;
        profile.Changed += changes => ProfileChanged?.Invoke(changes);
    }

    private static void Load(UserSettingsProfile profile)
    {
        if (!File.Exists(_path))
        {
            return;
        }
        
        try
        {
            using var stream = File.OpenText(_path!);
            using var reader = new JsonTextReader(stream) { DateParseHandling = DateParseHandling.None };
            
            var values = JObject.Load(reader, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            
            foreach (var option in profile.Options.Values)
            {
                if (!values.TryGetValue(option.Name, out var value))
                {
                    continue;
                }

                try
                {
                    option.Set(UserSettingsOption.ReadValue(value, option.Type));
                }
                catch (InvalidDataException error)
                {
                    Log.PrintWarning($"User setting '{option.Name}' uses its default: {error.Message}");
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            profile.Reset();
            
            Log.PrintWarning($"Cannot load user settings; defaults are used: {error.Message}");
        }
    }

    public static void Reset()
    {
        Profile.Reset();
    }

    public static void Save()
    {
        var profile = Profile;
        var values = new JObject();

        foreach (var option in profile.Options.Values)
        {
            values.Add(option.Name, JToken.FromObject(option.Value));
        }
        
        Directory.CreateDirectory(Path.GetDirectoryName(_path!)!);
        
        var temporary = _path + $".{Guid.NewGuid():N}.tmp";
        
        try
        {
            File.WriteAllText(temporary, values.ToString(Formatting.Indented));
            File.Move(temporary, _path!, overwrite: true);
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
