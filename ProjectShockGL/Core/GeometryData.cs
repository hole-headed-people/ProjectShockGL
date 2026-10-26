using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools.Converters;

namespace ProjectShockGL.Core;

public enum ColliderType
{
    None,
    Box,
    Sphere,
    Capsule
}

public class GeometryData
{
    public string Model { get; init; } = "";
    public string Texture { get; init; } = "";
    
    [JsonConverter(typeof(ColorConverter))]
    public Color Color { get; init; }
    
    [JsonConverter(typeof(StringEnumConverter))]
    public ColliderType ColliderType { get; init; } = ColliderType.None;
    
    [JsonConverter(typeof(Vector3Converter))]
    public Vector3 Size { get; init; } = Vector3.One;
    public float Radius { get; init; }
    public float Height { get; init; }
    
    [JsonConverter(typeof(Vector3Converter))]
    public Vector3 Offset {get; init;}

    public bool Lit { get; init; }
}