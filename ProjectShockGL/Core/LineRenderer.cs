using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public class LineRenderer : RenderComponent
{
    public Vector3 LineStart { get; set; }
    public Vector3 LineEnd { get; set; }
    public float Thickness { get; set; } = 0.1f;
    public Texture? Texture { get; set; }
    
    public override void Render(RenderQueue queue)
    {
        base.Render(queue);

        var command = new LineCommand()
        {
            Color = Color,
            Thickness = Thickness,
            From = LineStart,
            To = LineEnd,
            Texture = Texture,
            Shader = Shader,
            ShaderParameters = Shader == null ? null : ShaderParameters.Snapshot(),
            Lit = Lit,
            IgnoreDepth = IgnoreDepth,
            RenderOrder = RenderOrder
        };
        
        queue.Add(command);
    }
}
