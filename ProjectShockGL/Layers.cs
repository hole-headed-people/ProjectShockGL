using IniParser;

namespace ProjectShockGL;

public static class Layers
{
    private static readonly Dictionary<string, int> ByName = new();
    
    private static int _nextBit;

    public const int None = 0;
    public const int Everything = ~0;

    public static void Load()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Layers.ini");
        
        Reset();

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Layers file not found: {path}");
        }
        
        var parser = new FileIniDataParser();
        var data = parser.ReadFile(path);

        var layersSection = data["Layers"];

        if (layersSection == null)
        {
            throw new InvalidOperationException("Section Layers not found");
        }
        
        var entries = new List<(int index, string name)>();

        foreach (var key in layersSection)
        {
            var name = key.Value;

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }
            
            var indexStr = new string(key.KeyName.Where(char.IsDigit).ToArray());

            if (!int.TryParse(indexStr, out var index))
            {
                index = entries.Count;
            }

            entries.Add((index, name.Trim()));
        }
        
        entries.Sort((a, b) => a.index.CompareTo(b.index));
        
        foreach (var (index, name) in entries)
        {
            if (index >= 32)
            {
                throw new InvalidOperationException($"Layer index out of range: {index}");
            }

            var bit = 1 << index;
            
            ByName[name] = bit;
            _nextBit = Math.Max(_nextBit, index + 1);
        }
    }
    
    public static int Get(string name)
    {
        return ByName.TryGetValue(name, out var bit) ? bit : None;
    }
    
    public static void Reset()
    {
        _nextBit = 0;
        ByName.Clear();
    }
}