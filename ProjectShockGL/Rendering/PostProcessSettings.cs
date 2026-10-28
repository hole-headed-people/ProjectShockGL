using ProjectShockGL.Core;

namespace ProjectShockGL.Rendering;

public static class PostProcessSettings
{
    public static bool Enabled { get; set; }
    public static ShaderProgram? Shader { get; set; }
    public static ShaderParameters Parameters { get; } = new();

    public static void Apply(string name)
    {
        var profile = Content.LoadPostProcessProfile(name)
            ?? throw new FileNotFoundException($"Postprocess profile '{name}' was not found.");
        
        Apply(profile);
    }

    public static void Apply(PostProcessProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(profile.VertexShader) || string.IsNullOrWhiteSpace(profile.FragmentShader) ||
            profile.Parameters == null)
        {
            throw new InvalidDataException("A postprocess profile needs shader names and a Parameters dictionary.");
        }

        var shader = Content.LoadShaderProgram(profile.VertexShader, profile.FragmentShader);
        var parameters = shader.ConvertParameters(profile.Parameters);
        
        Parameters.CopyFrom(parameters);
        Shader = shader;
        Enabled = profile.Enabled;
    }

    public static void Reset()
    {
        Enabled = false;
        Shader = null;
        Parameters.Clear();
    }
}
