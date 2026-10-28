namespace ProjectShockGL.Core;

[Serializable]
public class PostProcessProfile
{
    public bool Enabled { get; init; } = true;
    public string VertexShader { get; init; } = "PostProcess";
    public string FragmentShader { get; init; } = "PostProcess";
    public Dictionary<string, object> Parameters { get; init; } = new();
}
