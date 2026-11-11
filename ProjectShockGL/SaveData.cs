using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL.Core;
using ProjectShockGL.Tools;

namespace ProjectShockGL;

public sealed class SaveData
{
    [JsonProperty(Required = Required.Always)]
    public Dictionary<string, Dictionary<Guid, SavedObjectState>> Levels { get; init; } = [];

    [JsonProperty("GameState", NullValueHandling = NullValueHandling.Ignore)]
    private JObject? _gameState;

    public void SetGameState(GameState? state)
    {
        _gameState = state == null ? null : JObject.FromObject(state, JsonSerializer.Create(JsonHelper.GetSettings()));
    }

    public GameState? GetGameState(Type type)
    {
        if (!typeof(GameState).IsAssignableFrom(type))
        {
            throw new ArgumentException("The type must derive from GameState.", nameof(type));
        }

        return (GameState?)_gameState?.ToObject(type, JsonSerializer.Create(JsonHelper.GetSettings()));
    }

    internal void Validate()
    {
        foreach (var (level, objects) in Levels)
        {
            if (string.IsNullOrWhiteSpace(level) || objects == null)
            {
                throw new InvalidDataException("The save contains a missing level or object states.");
            }

            foreach (var (guid, state) in objects)
            {
                if (guid == Guid.Empty || state == null || !state.IsValid())
                {
                    throw new InvalidDataException($"The save contains an invalid state for object '{guid}' in level '{level}'.");
                }
            }
        }
    }
}

public sealed record ObjectSpawnSource(string? Path = null, int? PrefabMemberIndex = null, string? ClassName = null)
{
    internal bool IsValid()
    {
        if (ClassName != null)
        {
            return !string.IsNullOrWhiteSpace(ClassName) && Path == null && PrefabMemberIndex == null;
        }

        return !string.IsNullOrWhiteSpace(Path) && PrefabMemberIndex is null or >= 0;
    }
}

public sealed class SavedObjectState : IObjectStateWriter, IObjectStateReader
{
    public Vector3 Position { get; init; }
    public Quaternion Rotation { get; init; } = Quaternion.Identity;
    public Vector3 Scale { get; init; } = Vector3.One;
    public bool IsActive { get; init; } = true;
    public bool IsDestroyed { get; init; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ObjectSpawnSource? SpawnSource { get; init; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; init; }

    [JsonProperty]
    private JObject Custom { get; set; } = new();

    public void Set(string key, object? value)
    {
        Custom[key] = value == null ? JValue.CreateNull() :
            JToken.FromObject(value, JsonSerializer.Create(JsonHelper.GetSettings()));
    }

    public T Get<T>(string key, T defaultValue = default!)
    {
        return Custom.TryGetValue(key, out var value) ?
            value.ToObject<T>(JsonSerializer.Create(JsonHelper.GetSettings()))! : defaultValue;
    }

    internal bool IsValid()
    {
        var rotationLength = Rotation.LengthSquared();
        
        return IsDestroyed || (Finite(Position) && Finite(Scale) &&
            float.IsFinite(rotationLength) && rotationLength >= 1e-8f && Custom != null &&
            (SpawnSource == null || SpawnSource.IsValid()));
    }

    private static bool Finite(Vector3 vector)
    {
        return float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);
    }
}
