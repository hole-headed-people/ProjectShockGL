using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public sealed class DetailAsset
{
    public string Mesh { get; init; } = "";
    public string Texture { get; init; } = "WhitePixel";
    
    [JsonConverter(typeof(StringEnumConverter))]
    public TextureFormat TextureFormat { get; init; } = TextureFormat.Jpeg;
    
    public Color Color { get; init; } = Color.White;
    
    public string? Shader { get; init; }
    public string? FragmentShader { get; init; }
    public Dictionary<string, object> ShaderParameters { get; init; } = [];

    public bool Lit { get; init; } = true;
    public bool BackfaceCulling { get; init; } = true;
    public bool CastBakedShadow { get; init; } = true;

    public void Validate()
    {
        ValidateReference(Mesh, nameof(Mesh));
        ValidateReference(Texture, nameof(Texture));
        if (Shader != null) ValidateReference(Shader, nameof(Shader));
        if (FragmentShader != null) ValidateReference(FragmentShader, nameof(FragmentShader));
        if (ShaderParameters == null || ShaderParameters.Keys.Any(key => string.IsNullOrWhiteSpace(key) || ShaderProgram.IsEngineUniform(key)))
            throw new InvalidDataException("Detail ShaderParameters contains a reserved or invalid name.");
        
        if (!Enum.IsDefined(TextureFormat) || !float.IsFinite(Color.R) || !float.IsFinite(Color.G) ||
            !float.IsFinite(Color.B) || !float.IsFinite(Color.A))
        {
            throw new InvalidDataException("Detail has invalid texture format or color.");
        }
    }

    internal static void ValidateReference(string? name, string field)
    {
        if (string.IsNullOrWhiteSpace(name) || System.IO.Path.IsPathRooted(name) ||
            name.Contains(':') || name.Replace('\\', '/').Split('/').Any(part =>
                string.IsNullOrWhiteSpace(part) || part is "." or "..") ||
            name.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0)
        {
            throw new InvalidDataException($"Detail {field} must be a relative resource name without empty, '.' or '..' segments.");
        }
    }
}