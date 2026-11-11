using ProjectShockGL.Rendering;

namespace ProjectShockGL.UI;

public class Image : Widget
{
    public Color Color { get; set; } = Color.White;
    public Texture? Texture { get; set; }
    public TextureRegion? Region { get; set; }

    protected override void OnDraw(Graphics graphics)
    {
        base.OnDraw(graphics);
        
        if (Region is { } region)
        {
            graphics.DrawUIRegion(Rect.AbsolutePosition, Rect.Size, region.Texture, region.UvRect, Color);
        }
        else
        {
            graphics.DrawUIRect(Rect.AbsolutePosition, Rect.Size, Color, Texture);
        }
    }
}
