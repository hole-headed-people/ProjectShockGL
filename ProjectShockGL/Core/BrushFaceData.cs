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
    public Rendering.SurfaceMode? SurfaceMode { get; init; }
    public float? AlphaCutoff { get; init; }
    public string? Texture { get; init; }
    public TextureFormat? TextureFormat { get; init; }
    public Vector2? TextureScale { get; init; }
    public Vector2 TextureOffset { get; init; }

    internal void Validate()
    {
        if (SurfaceMode is { } mode && !Enum.IsDefined(mode) ||
            AlphaCutoff is { } cutoff && (!float.IsFinite(cutoff) || cutoff < 0 || cutoff > 1))
            throw new InvalidDataException("Invalid brush face surface settings.");
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
