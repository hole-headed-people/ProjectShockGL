using System.Numerics;

namespace ProjectShockGL.Core;

public enum PlaceableType
{
    Geometry,
    Entity
}

[Serializable]
public class Placeable
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = string.Empty;
    public PlaceableType Type { get; init; }
    public bool IsActive { get; init; } = true;
    public Vector3 Position { get; init; }
    public Quaternion Rotation { get; init; } = Quaternion.Identity;
    public Vector3 Scale { get; init; } = Vector3.One;
    public List<OutputData> Outputs { get; init; } = [];
    public Dictionary<string, object> Properties { get; init; } = new();
}
