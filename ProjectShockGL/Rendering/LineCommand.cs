using System.Numerics;

namespace ProjectShockGL.Rendering;

public class LineCommand : RenderCommand
{
    public Vector3 From { get; set; }
    public Vector3 To { get; set; }
    public Texture? Texture {get; set;}
    public Color Color { get; set; }
    public float Thickness { get; set; }
    public bool Lit { get; set; }
    public ShaderProgram? Shader { get; set; }
    public ShaderParameters? ShaderParameters { get; set; }
    public bool IgnoreDepth { get; set; }

    public override void Execute(Graphics graphics)
    {
        if (Texture != null)
        {
            graphics.DrawLine(From, To, Texture, Color, Thickness, Lit, IgnoreDepth, Shader, ShaderParameters);
        }
    }
}
