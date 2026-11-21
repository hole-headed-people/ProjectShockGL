using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public class MeshComponent : RenderComponent
{
    public bool BackfaceCulling { get; set; } = true;
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
            LightmapSurface = new LightmapSurfaceId(Owner.Guid,
                Owner is BrushBox ? 0 : Owner.GetRenderComponents().OfType<MeshComponent>().TakeWhile(component => component != this).Count(), false),
            StaticBatchEligible = Owner.IsStatic && (Owner is Geometry or BrushBox) && !IsDebug,
            UseDynamicAmbient = !Owner.IsStatic && !IsDebug,
            SurfaceMode = SurfaceMode, AlphaCutoff = AlphaCutoff,
            Color = Color,
            Mesh = Mesh,
            Position = Owner.Transform.Position + Offset,
            Rotation = Owner.Transform.Rotation * LocalRotation,
            Scale = UseParentScale ? Owner.Transform.Scale * Scale : new Vector3(Scale),
            Texture = Texture,
            Shader = Shader,
            ShaderParameters = Shader == null ? null : ShaderParameters.Snapshot(),
            BackfaceCulling = BackfaceCulling,
            Lit = Lit,
            IgnoreDepth = IgnoreDepth,
            RenderOrder = RenderOrder
        };
        
        queue.Add(command);
    }
}
