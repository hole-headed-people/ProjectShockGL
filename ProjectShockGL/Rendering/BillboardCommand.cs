using System.Numerics;

namespace ProjectShockGL.Rendering;

public class BillboardCommand : RenderCommand
{
    public BillboardMode Mode { get; set; } = BillboardMode.ViewAligned;
    public Vector3 Position { get; set; }
    public Vector2 Size { get; set; }
    public Texture? Texture { get; set; }
    public Color Color { get; set; }
    public ShaderProgram? Shader { get; set; }
    public ShaderParameters? ShaderParameters { get; set; }
    public bool Lit { get; set; }
    public bool IgnoreDepth { get; set; }

    internal override Vector3 SortPosition => Position;
    internal override bool IsOverlay => IgnoreDepth;

    public override void Execute(Graphics graphics)
    {
        if (Texture != null)
        {
            graphics.DrawBillboard(Position, Size, Texture, Color, Lit, IgnoreDepth, Shader, ShaderParameters, Mode, SurfaceMode, AlphaCutoff);
        }
    }
}