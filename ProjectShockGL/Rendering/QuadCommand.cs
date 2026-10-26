using System.Numerics;

namespace ProjectShockGL.Rendering;

public class QuadCommand : RenderCommand
{
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector2 Size { get; set; }
    public Texture? Texture { get; set; }
    public Color Color { get; set; }
    public bool Lit { get; set; }
    public bool IgnoreDepth { get; set; }

    public override void Execute(Graphics graphics)
    {
        if (Texture != null)
        {
            graphics.DrawQuad(Position, Rotation, Size, Texture, Color, Lit, IgnoreDepth);
        }
    }
}