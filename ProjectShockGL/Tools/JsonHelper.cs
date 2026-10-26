using Newtonsoft.Json;
using ProjectShockGL.Tools.Converters;

namespace ProjectShockGL.Tools;

public static class JsonHelper
{
    private static JsonSerializerSettings? _settings;
    
    public static JsonSerializerSettings GetSettings()
    {
        if (_settings != null)
        {
            return _settings;
        }
        
        _settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter>
            {
                new Vector3Converter(),
                new ColorConverter()
            }
        };
        
        return _settings;
    }
}