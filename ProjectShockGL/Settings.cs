using System.Globalization;
using IniParser;
using IniParser.Model;

namespace ProjectShockGL;

public static class Settings
{
    private static FileIniDataParser _parser = null!;
    private static IniData _data = null!;

    private static string _path = null!;
    
    public static void Initialize()
    {
        _parser = new FileIniDataParser();
        _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.ini");

        _data = File.Exists(_path) ? _parser.ReadFile(_path) : new IniData();
        _data.Configuration.AllowCreateSectionsOnFly = true;
    }

    private static string Read(string section, string key, string defaultValue)
    {
        return _data[section]?[key] ?? defaultValue;
    }

    public static float GetFloat(string section, string key, float defaultValue)
    {
        var value = Read(section, key, string.Empty);

        return string.IsNullOrEmpty(value) ? defaultValue : float.Parse(value, CultureInfo.InvariantCulture);
    }

    public static int GetInt(string section, string key, int defaultValue)
    {
        var value = Read(section, key, string.Empty);
        
        return string.IsNullOrEmpty(value) ? defaultValue : int.Parse(value);
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
}