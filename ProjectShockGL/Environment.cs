using ProjectShockGL.Rendering;

namespace ProjectShockGL;

public static class Environment
{
    public static bool SkyColorEnabled { get; set; }
    public static Color SkyColor { get; set; } = Color.Black;

    public static Color AmbientColor { get; set; } = Color.White;
    public static bool DynamicAmbientColorEnabled { get; set; }
    public static Color DynamicAmbientColor { get; set; } = Color.White;

    public static bool IsFogEnabled { get; set; } = false;
    public static Color FogColor { get; set; } = Color.Gray;

    public static float FogNear { get; set; } = 1f;
    public static float FogFar { get; set; } = 10f;
    
    public static Core.EnvironmentData Capture() => new()
    {
        SkyColorEnabled = SkyColorEnabled,
        SkyColor = SkyColor,
        AmbientColor = AmbientColor,
        DynamicAmbientColorEnabled = DynamicAmbientColorEnabled,
        DynamicAmbientColor = DynamicAmbientColor,
        IsFogEnabled = IsFogEnabled,
        FogColor = FogColor,
        FogNear = FogNear,
        FogFar = FogFar
    };

    public static void Apply(Core.EnvironmentData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        
        data.Validate();
        
        SkyColorEnabled = data.SkyColorEnabled;
        SkyColor = data.SkyColor;
        AmbientColor = data.AmbientColor;
        DynamicAmbientColorEnabled = data.DynamicAmbientColorEnabled;
        DynamicAmbientColor = data.DynamicAmbientColor;
        IsFogEnabled = data.IsFogEnabled;
        FogColor = data.FogColor;
        FogNear = data.FogNear;
        FogFar = data.FogFar;
    }

    public static void Reset()
    {
        Apply(new Core.EnvironmentData());
    }
}
