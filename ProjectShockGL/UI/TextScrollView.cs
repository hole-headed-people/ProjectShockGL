using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.UI;

public class TextScrollView : Widget
{
    private float _scrollY;

    public Text Body { get; private set; } = null!;
    public float WheelStep { get; set; } = 40f;

    public float ScrollY
    {
        get => _scrollY;
        set
        {
            if (!float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            
            _scrollY = Math.Max(0f, value);
            
            LayoutBody();
        }
    }

    internal override bool ClipsChildren => true;

    protected override void OnCreate()
    {
        Body = Add<Text>();
        Body.ClipToRect = false;
    }

    protected override void OnUpdate(float deltaSeconds) => LayoutBody();
    protected override void OnDraw(Graphics graphics) => LayoutBody();

    internal override bool Scroll(float delta)
    {
        var offset = _scrollY;
        ScrollY += -delta * WheelStep;
        
        return _scrollY != offset;
    }

    private void LayoutBody()
    {
        if (Body == null || Body.IsDestroyed || Body.Parent != this)
        {
            return;
        }

        var viewport = Rect.Size;
        Body.Rect.Size = new Vector2(viewport.X, Body.Rect.Size.Y);
        
        var height = viewport.X > 0 ? Body.MeasureText().Y : 0f;
        _scrollY = Math.Min(_scrollY, Math.Max(0f, height - viewport.Y));
        
        Body.Rect.Size = new Vector2(viewport.X, height);
        Body.Rect.Position = new Vector2(0f, -_scrollY);
    }
}
