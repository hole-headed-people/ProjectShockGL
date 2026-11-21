using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public sealed class EnvironmentData
{
    public bool SkyColorEnabled { get; init; }
    public Color SkyColor { get; init; } = Color.Black;
    public Color AmbientColor { get; init; } = Color.White;
    public bool DynamicAmbientColorEnabled { get; init; }
    public Color DynamicAmbientColor { get; init; } = Color.White;
    public bool IsFogEnabled { get; init; }
    public Color FogColor { get; init; } = Color.Gray;
    public float FogNear { get; init; } = 1f;
    public float FogFar { get; init; } = 10f;

    public void Validate()
    {
        if (!Finite(SkyColor) || !Finite(AmbientColor) || !Finite(DynamicAmbientColor) || !Finite(FogColor))
        {
            throw new InvalidDataException("Environment colors must contain finite values.");
        }

        if (!float.IsFinite(FogNear) || !float.IsFinite(FogFar) || FogNear < 0 || FogFar <= FogNear)
        {
            throw new InvalidDataException("Environment requires 0 <= FogNear < FogFar.");
        }
    }

    private static bool Finite(Color color)
    {
        return float.IsFinite(color.R) && float.IsFinite(color.G) &&
               float.IsFinite(color.B) && float.IsFinite(color.A);
    }
}
