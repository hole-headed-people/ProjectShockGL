using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public class MeshComponent : RenderComponent
{
    public Mesh? Mesh {get; set;}
    public Texture? Texture {get; set;}

    public override void Render(RenderQueue queue)
    {
        base.Render(queue);

        if (Mesh == null)
        {
            return;
        }
        
        var command = new MeshCommand
        {
            Color = Color,
            Mesh = Mesh,
            Position = Parent.Transform.Position + Offset,
            Rotation = Parent.Transform.Rotation,
            Scale = UseParentScale ? Parent.Transform.Scale * Scale : new Vector3(Scale),
            Texture = Texture,
            Lit = Lit,
            IgnoreDepth = IgnoreDepth,
            RenderOrder = RenderOrder
        };
        
        queue.Add(command);
    }
}