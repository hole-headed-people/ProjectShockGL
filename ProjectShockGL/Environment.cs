using ProjectShockGL.Rendering;

namespace ProjectShockGL;

public static class Environment
{
    public static Color AmbientColor { get; set; } = Color.White;

    public static bool IsFogEnabled { get; set; } = false;
    public static Color FogColor { get; set; } = Color.Gray;

    public static float FogNear { get; set; } = 1f;
    public static float FogFar { get; set; } = 10f;
    
    public static Core.EnvironmentData Capture() => new()
    {
        AmbientColor = AmbientColor,
        IsFogEnabled = IsFogEnabled,
        FogColor = FogColor,
        FogNear = FogNear,
        FogFar = FogFar
    };

    public static void Apply(Core.EnvironmentData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        
        data.Validate();
        
        AmbientColor = data.AmbientColor;
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
