using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public class MeshComponent : RenderComponent
{
    public Mesh? Mesh {get; set;}
    public Texture? Texture {get; set;}
    public Quaternion LocalRotation { get; set; } = Quaternion.Identity;

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
            Position = Owner.Transform.Position + Offset,
            Rotation = Owner.Transform.Rotation * LocalRotation,
            Scale = UseParentScale ? Owner.Transform.Scale * Scale : new Vector3(Scale),
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
