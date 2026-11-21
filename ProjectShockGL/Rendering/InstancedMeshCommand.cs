namespace ProjectShockGL.Rendering;

internal sealed class InstancedMeshCommand : RenderCommand
{
    internal required InstancedMesh Instances { get; init; }
    internal required Texture Texture { get; init; }
    internal Color Color { get; init; } = Color.White;
    internal Texture? Lightmap { get; init; }
    internal bool Lit { get; init; }
    internal bool BackfaceCulling { get; init; } = true;

    public override void Execute(Graphics graphics)
    {
        graphics.DrawInstances(Instances, Texture, Color, Lit, BackfaceCulling, Lightmap);
    }
}