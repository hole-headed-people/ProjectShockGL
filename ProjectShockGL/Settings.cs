using System.Globalization;
using IniParser;
using IniParser.Model;

namespace ProjectShockGL;

public static class Settings
{
    private static IniData _defaults = new();
    private static IniData _overrides = new();
    
    private static string _defaultPath = "";
    private static string _editorPath = "";

    internal static bool IsEditor { get; private set; }

    public static void Initialize(bool isEditor = false)
    {
        var parser = new FileIniDataParser();
        
        var editorPath = Path.Combine(AppContext.BaseDirectory, "EditorSettings.ini");
        var defaultPath = Path.Combine(AppContext.BaseDirectory, "DefaultSettings.ini");
        
        try
        {
            var overrides = isEditor && File.Exists(editorPath) ? parser.ReadFile(editorPath) : new IniData();
            var reference = overrides["Settings"]?["DefaultPath"];
            
            if (!string.IsNullOrWhiteSpace(reference))
            {
                defaultPath = Path.GetFullPath(reference.Trim(), Path.GetDirectoryName(editorPath)!);

                if (!File.Exists(defaultPath))
                {
                    throw new FileNotFoundException("DefaultPath points to a missing settings file.", defaultPath);
                }
            }
            
            var defaults = File.Exists(defaultPath) ? parser.ReadFile(defaultPath) : new IniData();
            
            _defaults = defaults;
            _overrides = overrides;
            
            _defaultPath = defaultPath;
            _editorPath = editorPath;
            
            IsEditor = isEditor;
        }
        catch (Exception error)
        {
            Log.Initialize("ProjectShockGL", isEditor);
            Log.PrintError($"Cannot read settings '{defaultPath}' and '{editorPath}'.", error);
            
            throw;
        }
        
        Log.Initialize(GetString("Game", "Name", "ProjectShockGL"), isEditor);
    }

    private static string Read(string section, string key, string defaultValue)
    {
        return _overrides[section]?[key] ?? _defaults[section]?[key] ?? defaultValue;
    }

    public static float GetFloat(string section, string key, float defaultValue)
    {
        var value = Read(section, key, string.Empty);
        
        return string.IsNullOrEmpty(value) ? defaultValue : float.Parse(value, CultureInfo.InvariantCulture);
    }

    public static int GetInt(string section, string key, int defaultValue)
    {
        var value = Read(section, key, string.Empty);
        
        return string.IsNullOrEmpty(value) ? defaultValue : int.Parse(value, CultureInfo.InvariantCulture);
    }

    public static bool GetBool(string section, string key, bool defaultValue)
    {
        var value = Read(section, key, string.Empty);
        
        return string.IsNullOrEmpty(value) ? defaultValue : bool.Parse(value);
    }

    public static string GetString(string section, string key, string defaultValue)
    {
        return Read(section, key, defaultValue);
    }

    public static string GetPath(string section, string key, string defaultValue)
    {
        var overridden = _overrides[section]?[key];
        var value = overridden ?? _defaults[section]?[key] ?? defaultValue;

        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }
        
        var source = overridden != null ? _editorPath : _defaultPath;
        
        return Path.GetFullPath(value.Trim(), Path.GetDirectoryName(source)!);
    }

    public static IReadOnlyDictionary<string, string> GetSection(string section)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        
        foreach (var data in new[] { _defaults, _overrides })
        {
            var keys = data[section];

            if (keys == null)
            {
                continue;
            }

            foreach (var key in keys)
            {
                result[key.KeyName] = key.Value;
            }
        }
        
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(result);
    }
}
