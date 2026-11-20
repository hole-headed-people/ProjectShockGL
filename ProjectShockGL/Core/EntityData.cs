namespace ProjectShockGL.Core;

public class EntityData
{
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public Guid? AssetId { get; init; }
    public string Class { get; init; } = "";
    public Dictionary<string, object> Parameters { get; init; } = [];
}