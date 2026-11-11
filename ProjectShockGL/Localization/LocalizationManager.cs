using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace ProjectShockGL.Localization
{
    public static class LocalizationManager
    {
        public static event Action? LanguageChanged; 
        
        public const string DefaultLanguage = "en";
        
        private static readonly Dictionary<string, LocalizationData> TablesData = [];
        
        private static string _language = DefaultLanguage;
        private static bool _isInitialized;
        
        public static void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }
            
            var tables = Settings.GetString("Localization", "Tables", string.Empty)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal);

            foreach (var table in tables)
            {
                var path = Path.Combine(Content.Path, "Localization", $"{table}.json");
                
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    var data = JsonConvert.DeserializeObject<LocalizationData>(File.ReadAllText(path));
                    
                    if (data?.Languages == null)
                    {
                        Console.Error.WriteLine($"Localization table '{path}' has no language data.");
                        continue;
                    }

                    TablesData[table] = data;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
                {
                    Console.Error.WriteLine($"Cannot load localization table '{path}': {error.Message}");
                }
            }
            
            _isInitialized = true;
        }
        
        private static bool HasFormatPlaceholders(string text)
        {
            return !string.IsNullOrEmpty(text) && Regex.IsMatch(text, @"{\d+}");
        }
        
        public static void SetLanguage(string language)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(language);
            
            language = language.Trim();

            if (string.Equals(_language, language, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _language = language;
            
            LanguageChanged?.Invoke();
        }

        public static string GetStringFormat(string key, string table, params object[] args)
        {
            var text = GetString(key, table);
            
            return !HasFormatPlaceholders(text) ? text : string.Format(text, args);
        }

        public static string GetString(string key, string table)
        {
            if (!TablesData.TryGetValue(table, out var tableData))
            {
                return key;
            }

            return FindString(tableData, _language, key)
                ?? FindString(tableData, DefaultLanguage, key)
                ?? key;
        }

        private static string? FindString(LocalizationData table, string language, string key)
        {
            if (table.Languages.TryGetValue(language, out var data) &&
                data?.Translations != null &&
                data.Translations.TryGetValue(key, out var variants) &&
                variants is { Length: > 0 })
            {
                return variants[Tools.Random.Range(0, variants.Length)];
            }

            return null;
        }
    }
}
