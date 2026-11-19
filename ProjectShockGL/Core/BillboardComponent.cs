using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public class BillboardComponent : RenderComponent
{
    public Texture? Texture { get; set; }
    public BillboardMode Mode { get; set; } = BillboardMode.ViewAligned;
    
    public override void Render(RenderQueue queue)
    {
        base.Render(queue);
        
        var parentScale = Owner.Transform.Scale;
        
        var command = new BillboardCommand()
        {
            Mode = Mode,
            SurfaceMode = SurfaceMode, AlphaCutoff = AlphaCutoff,
            Color = Color,
            Texture = Texture,
            Size = UseParentScale ? new Vector2(parentScale.X, parentScale.Y) * Scale : new Vector2(Scale),
            Position = Owner.Transform.Position + Offset,
            Shader = Shader,
            ShaderParameters = Shader == null ? null : ShaderParameters.Snapshot(),
            Lit = Lit,
            IgnoreDepth = IgnoreDepth,
            RenderOrder = RenderOrder
        };
        
        queue.Add(command);
    }
}