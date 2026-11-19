using System.Numerics;

namespace ProjectShockGL.Core;

public enum BrushFace
{
    PositiveX,
    NegativeX,
    PositiveY,
    NegativeY,
    PositiveZ,
    NegativeZ
}

public sealed record BrushFaceData
{
    public string? Texture { get; init; }
    public TextureFormat? TextureFormat { get; init; }
    public Vector2? TextureScale { get; init; }
    public Vector2 TextureOffset { get; init; }

    internal void Validate()
    {
        if (TextureFormat is { } format && !Enum.IsDefined(format))
        {
            throw new InvalidDataException("Invalid brush face texture format.");
        }

        if (TextureScale is { } scale &&
            (!float.IsFinite(scale.X) || !float.IsFinite(scale.Y) || scale.X <= 0 || scale.Y <= 0))
        {
            throw new InvalidDataException("Brush face texture scale must be finite and positive.");
        }

        if (!float.IsFinite(TextureOffset.X) || !float.IsFinite(TextureOffset.Y))
        {
            throw new InvalidDataException("Brush face texture offset must be finite.");
        }
    }

    internal static (string Name, TextureFormat Format) GetTexture(BrushFaceData? face, string texture, TextureFormat format)
    {
        var name = face?.Texture ?? texture;
        var resolvedFormat = face?.TextureFormat ?? format;
        
        return string.IsNullOrWhiteSpace(name) || name == "WhitePixel"
            ? ("WhitePixel", ProjectShockGL.TextureFormat.Jpeg) : (name, resolvedFormat);
    }
}
