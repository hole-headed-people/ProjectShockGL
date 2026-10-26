using System.Numerics;

namespace ProjectShockGL.Rendering;

public class BillboardCommand : RenderCommand
{
    public Vector3 Position { get; set; }
    public Vector2 Size { get; set; }
    public Texture? Texture { get; set; }
    public Color Color { get; set; }
    public bool Lit { get; set; }
    public bool IgnoreDepth { get; set; }

    public override void Execute(Graphics graphics)
    {
        if (Texture != null)
        {
            graphics.DrawBillboard(Position, Size, Texture, Color, Lit, IgnoreDepth);
        }
    }
}