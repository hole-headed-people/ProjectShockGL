using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public sealed class BrushBoxData
{
    public Vector3 Size { get; init; } = Vector3.One;
    public string Texture { get; init; } = "WhitePixel";
    public TextureFormat TextureFormat { get; init; } = TextureFormat.Jpeg;
    public float TextureScale { get; init; } = 1;
    public Color Color { get; init; } = Color.White;
    public bool Lit { get; init; } = true;
}
