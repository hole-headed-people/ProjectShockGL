using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;

namespace ProjectShockGL;

public enum UserSettingsOptionType
{
    Int, 
    Float, 
    String, 
    Bool
}

public sealed class UserSettingsOption
{
    internal event Action<UserSettingsOption>? Changed;
    
    public string Name { get; }
    public UserSettingsOptionType Type { get; }
    public object DefaultValue { get; }
    public object Value { get; private set; }

    internal UserSettingsOption(string name, UserSettingsOptionType type, object value)
    {
        Name = name;
        Type = type;
        DefaultValue = value;
        Value = value;
    }

    public T Get<T>()
    {
        return Value is T value
            ? value
            : throw new InvalidOperationException($"Setting '{Name}' is {Type}, not {typeof(T).Name}.");
    }

    public void Set<T>(T value)
    {
        if (value == null || value.GetType() != DefaultValue.GetType() ||
            value is float number && !float.IsFinite(number))
        {
            throw new ArgumentException($"Setting '{Name}' requires {Type}.", nameof(value));
        }

        if (Equals(Value, value))
        {
            return;
        }
        
        Value = value;
        Changed?.Invoke(this);
    }

    internal bool RestoreDefault()
    {
        if (Equals(Value, DefaultValue))
        {
            return false;
        }
        
        Value = DefaultValue;
        
        return true;
    }

    internal static object ReadValue(JToken token, UserSettingsOptionType type)
    {
        return type switch
        {
            UserSettingsOptionType.Int when token.Type == JTokenType.Integer &&
                long.TryParse(token.ToString(), out var integer) && integer >= int.MinValue && integer <= int.MaxValue => (int)integer,
            
            UserSettingsOptionType.Float when token.Type is JTokenType.Integer or JTokenType.Float &&
                float.TryParse(token.ToString(Newtonsoft.Json.Formatting.None), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var number) && float.IsFinite(number) => number,
            
            UserSettingsOptionType.String when token.Type == JTokenType.String => token.Value<string>()!,
            
            UserSettingsOptionType.Bool when token.Type == JTokenType.Boolean => token.Value<bool>(),
            
            _ => throw new InvalidDataException($"Invalid value for setting type {type}.")
        };
    }
}

public sealed class UserSettingsProfile
{
    public event Action<IReadOnlyDictionary<string, UserSettingsOption>>? Changed;
    
    private readonly Dictionary<string, UserSettingsOption> _options = new(StringComparer.Ordinal);
    
    public IReadOnlyDictionary<string, UserSettingsOption> Options { get; }
    
    public UserSettingsProfile()
    {
        Options = new ReadOnlyDictionary<string, UserSettingsOption>(_options);
    }

    public UserSettingsOption Get(string name)
    {
        return _options.TryGetValue(name, out var option)
            ? option
            : throw new KeyNotFoundException($"User setting '{name}' does not exist.");
    }

    public static UserSettingsProfile Read(string path)
    {
        using var stream = File.OpenText(path);
        using var reader = new Newtonsoft.Json.JsonTextReader(stream) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None };
        
        var json = JObject.Load(reader, new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
        });
        
        var entries = json[nameof(Options)] as JArray ?? throw new InvalidDataException("Profile requires an Options array.");
        var profile = new UserSettingsProfile();
        
        foreach (var entry in entries)
        {
            var name = entry.Value<string>("Name");

            if (string.IsNullOrWhiteSpace(name) || profile._options.ContainsKey(name))
            {
                throw new InvalidDataException($"Empty or duplicate setting name '{name}'.");
            }

            if (!Enum.TryParse<UserSettingsOptionType>(entry.Value<string>("Type"), true, out var type) ||
                !Enum.IsDefined(type))
            {
                throw new InvalidDataException($"Invalid type for setting '{name}'.");
            }
            
            var value = UserSettingsOption.ReadValue(entry["DefaultValue"] ??
                throw new InvalidDataException($"Setting '{name}' requires DefaultValue."), type);
            
            var option = new UserSettingsOption(name, type, value);
            option.Changed += changed => profile.Changed?.Invoke(new ReadOnlyDictionary<string, UserSettingsOption>(
                new Dictionary<string, UserSettingsOption>(StringComparer.Ordinal) { [changed.Name] = changed }));
            
            profile._options.Add(name, option);
        }
        
        return profile;
    }

    internal void Reset()
    {
        var changed = _options.Values.Where(option => option.RestoreDefault()).ToArray();

        if (changed.Length > 0)
        {
            Changed?.Invoke(new ReadOnlyDictionary<string, UserSettingsOption>(
                changed.ToDictionary(option => option.Name, StringComparer.Ordinal)));
        }
    }
}
