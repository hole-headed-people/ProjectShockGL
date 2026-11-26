using System.Globalization;
using Newtonsoft.Json;

namespace ProjectShockGL.Localization
{
    public static class LocalizationManager
    {
        public static event Action? LanguageChanged; 
        
        public const string DefaultLanguage = "en";
        
        private static readonly Dictionary<string, LocalizationData> TablesData = [];
        private static readonly HashSet<(string Category, string Table, string Language, string Key)> Reported = [];

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
                    Log.PrintWarning($"Localization table was not found: {path}");
                    continue;
                }

                try
                {
                    TablesData[table] = LocalizationData.Read(path);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
                {
                    Log.PrintError($"Cannot load localization table '{path}'.", error);
                }
            }
            
            _isInitialized = true;
            _language = Settings.GetString("Localization", "Language", DefaultLanguage);
        }
        
        private static string GetString(string key, string table, bool random)
        {
            if (!TablesData.TryGetValue(table, out var tableData))
            {
                if (Reported.Add(("Table", table, "", "")))
                {
                    Log.PrintWarning($"Localization table '{table}' is not loaded.");
                }
                
                return key;
            }

            var text = FindString(tableData, _language, key, random);
            
            if (text != null)
            {
                return text;
            }
            
            var fallback = FindString(tableData, DefaultLanguage, key, random);

            if (Reported.Add(("Translation", table, _language, key)))
            {
                Log.PrintWarning($"Localization '{table}/{key}' has no translation for '{_language}'; using {(fallback != null ? DefaultLanguage : "the key")}.");
            }
            
            return fallback ?? key;
        }

        private static string? FindString(LocalizationData table, string language, string key, bool random)
        {
            if (table.Languages.TryGetValue(language, out var data) &&
                data?.Translations != null &&
                data.Translations.TryGetValue(key, out var variants) &&
                variants is { Length: > 0 })
            {
                return variants[random ? Tools.Random.Range(0, variants.Length) : 0];
            }

            return null;
        }
        
        private static string FormatString(string text, string key, string table, object[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            
            try
            {
                return string.Format(CultureInfo.CurrentCulture, text, args);
            }
            catch (FormatException error)
            {
                if (Reported.Add(("Format", table, _language, key)))
                {
                    Log.PrintError($"Cannot format localization '{table}/{key}' for language '{_language}': {error.Message}");
                }
                
                return text;
            }
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
            return FormatString(GetString(key, table), key, table, args);
        }

        public static string GetRandomStringFormat(string key, string table, params object[] args)
        {
            return FormatString(GetRandomString(key, table), key, table, args);
        }

        public static string GetString(string key, string table)
        {
            return GetString(key, table, false);
        }

        public static string GetRandomString(string key, string table)
        {
            return GetString(key, table, true);
        }
    }
}
