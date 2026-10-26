using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.UI;

public enum HorizontalAlignment { Left, Center, Right }
public enum VerticalAlignment { Top, Center, Bottom }

public class Text : Widget
{
    private Font? _layoutFont;
    
    private string _layoutContent = string.Empty;
    
    private float _layoutWidth = -1;
    
    private TextLayout? _layout;
    
    public string Content { get; set; } = string.Empty;
    public Font? Font { get; set; }
    public Color Color { get; set; } = Color.White;
    public bool WordWrap { get; set; } = true;
    public bool ClipToRect { get; set; } = true;
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;

    public Text()
    {
        HitTestVisible = false;
    }
    
    public Vector2 MeasureText()
    {
        return GetLayout()?.Size ?? Vector2.Zero;
    }

    private TextLayout? GetLayout()
    {
        if (Font == null || Font.IsDisposed)
        {
            return null;
        }
        
        var width = WordWrap ? Rect.Size.X : float.PositiveInfinity;
        var content = Content;
        
        if (_layout == null || _layoutFont != Font || _layoutContent != content || _layoutWidth != width)
        {
            _layout = Font.Atlas.LayoutText(content, width);
            _layoutFont = Font;
            _layoutContent = content;
            _layoutWidth = width;
        }
        
        return _layout;
    }

    protected override void OnDraw(Graphics graphics)
    {
        base.OnDraw(graphics);
        
        var layout = GetLayout();

        if (layout == null || Font == null)
        {
            return;
        }
        
        var origin = Rect.AbsolutePosition;
        
        var vertical = VerticalAlignment switch
        {
            VerticalAlignment.Center => (Rect.Size.Y - layout.Size.Y) * 0.5f,
            VerticalAlignment.Bottom => Rect.Size.Y - layout.Size.Y,
            _ => 0
        };
        
        foreach (var placed in layout.Glyphs)
        {
            var horizontal = HorizontalAlignment switch
            {
                HorizontalAlignment.Center => (Rect.Size.X - layout.LineWidths[placed.Line]) * 0.5f,
                HorizontalAlignment.Right => Rect.Size.X - layout.LineWidths[placed.Line],
                _ => 0
            };
            
            graphics.DrawUIRegion(origin + placed.Position + new Vector2(horizontal, vertical),
                placed.Glyph.Size, Font.Texture, placed.Glyph.UvRect, Color,
                ClipToRect ? origin : null, ClipToRect ? Rect.Size : null);
        }
    }

    protected override void OnDestroy()
    {
        _layout = null;
        _layoutFont = null;
        
        Font = null;
        
        base.OnDestroy();
    }
}
