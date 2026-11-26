using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ProjectShockGL.Localization
{
    [Serializable]
    public class LocalizationData
    {
        public Dictionary<string, LanguageData> Languages = new(StringComparer.OrdinalIgnoreCase);

        private static JToken? Member(JObject data, string name)
        {
            var properties = data.Properties().Where(property =>
                property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
            
            if (properties.Length > 1)
            {
                throw new InvalidDataException($"Duplicate localization field '{name}'.");
            }
            
            return properties.Length == 0 ? null : properties[0].Value;
        }
        public static LocalizationData Read(string path)
        {
            using var stream = File.OpenText(path);
            using var reader = new JsonTextReader(stream) { DateParseHandling = DateParseHandling.None };
            
            var json = JObject.Load(reader, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });

            if (Member(json, nameof(Languages)) is not JObject languages || languages.Count == 0)
            {
                throw new InvalidDataException("A localization table requires a nonempty Languages object.");
            }

            var result = new LocalizationData();
            
            foreach (var language in languages.Properties())
            {
                if (string.IsNullOrWhiteSpace(language.Name) || language.Name != language.Name.Trim() ||
                    result.Languages.ContainsKey(language.Name))
                {
                    throw new InvalidDataException($"Empty, untrimmed or duplicate language '{language.Name}'.");
                }

                if (language.Value is not JObject data ||
                    Member(data, nameof(LanguageData.Translations)) is not JObject translations)
                {
                    throw new InvalidDataException($"Language '{language.Name}' requires a Translations object.");
                }

                var entries = new LanguageData();
                
                foreach (var translation in translations.Properties())
                {
                    if (string.IsNullOrWhiteSpace(translation.Name))
                    {
                        throw new InvalidDataException($"Language '{language.Name}' has an empty translation key.");
                    }

                    if (translation.Value is not JArray variants || variants.Count == 0 ||
                        variants.Any(value => value.Type != JTokenType.String))
                    {
                        throw new InvalidDataException($"Translation '{language.Name}/{translation.Name}' requires a nonempty array of strings.");
                    }
                    
                    entries.Translations.Add(translation.Name, variants.Values<string>().Select(value => value!).ToArray());
                }
                
                result.Languages.Add(language.Name, entries);
            }
            
            return result;
        }
    }
}
