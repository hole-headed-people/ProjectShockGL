using System.Numerics;

namespace ProjectShockGL.Rendering;

public class MeshCommand : RenderCommand
{
    public Mesh? Mesh { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector3 Scale { get; set; }
    public Texture? Texture { get; set; }
    public Color Color { get; set; }
    public bool Lit { get; set; }
    public bool IgnoreDepth { get; set; }

    public override void Execute(Graphics graphics)
    {
        if (Mesh != null && Texture != null)
        {
            graphics.DrawMesh(Mesh, Position, Rotation, Scale, Texture, Color, Lit, IgnoreDepth);
        }
    }
}