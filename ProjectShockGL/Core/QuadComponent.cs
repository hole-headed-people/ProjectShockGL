using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public class QuadComponent : RenderComponent
{
    public Texture? Texture { get; set; }
    
    public override void Render(RenderQueue queue)
    {
        base.Render(queue);

        var parentScale = Owner.Transform.Scale;
        
        var command = new QuadCommand()
        {
            Color = Color,
            Position = Owner.Transform.Position + Offset,
            Rotation = Owner.Transform.Rotation,
            Size = UseParentScale ? new Vector2(parentScale.X, parentScale.Y) * Scale : new Vector2(Scale),
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