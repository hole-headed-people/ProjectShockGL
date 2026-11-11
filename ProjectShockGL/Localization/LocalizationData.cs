namespace ProjectShockGL.Localization
{
    [Serializable]
    public class LocalizationData
    {
        public Dictionary<string, LanguageData> Languages = new(StringComparer.OrdinalIgnoreCase);
    }
}
