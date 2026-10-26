using ProjectShockGL.Rendering;

namespace ProjectShockGL.UI;

public class Image : Widget
{
    public Color Color { get; set; } = Color.White;
    public Texture? Texture { get; set; }

    protected override void OnDraw(Graphics graphics)
    {
        base.OnDraw(graphics);
        
        graphics.DrawUIRect(Rect.AbsolutePosition, Rect.Size, Color, Texture);
    }
}