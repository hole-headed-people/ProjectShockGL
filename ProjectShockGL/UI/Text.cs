using System.Numerics;
using ProjectShockGL.Core;
using ProjectShockGL.Localization;
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

    private bool _localize;
    private string _key = "";
    private string _table = "";
    private bool _created;
    private bool _localizationSubscribed;

    [Editable]
    public bool Localize
    {
        get => _localize;
        set
        {
            if (_localize == value) return;
            _localize = value;
            UpdateLocalization();
        }
    }

    [Editable]
    public string Key
    {
        get => _key;
        set
        {
            value ??= "";
            
            if (_key == value)
            {
                return;
            }
            
            _key = value;
            
            UpdateLocalization();
        }
    }

    [Editable]
    public string Table
    {
        get => _table;
        set
        {
            value ??= "";

            if (_table == value)
            {
                return;
            }
            
            _table = value;
            
            UpdateLocalization();
        }
    }
    public Text()
    {
        HitTestVisible = false;
    }

    protected override void OnCreate()
    {
        base.OnCreate();

        _created = true;
        
        UpdateLocalization();
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

        LocalizationManager.LanguageChanged -= RefreshLocalize;
        
        _localizationSubscribed = false;
        _created = false;

        base.OnDestroy();
    }
    
    private void UpdateLocalization()
    {
        var subscribe = _created && !IsDestroyed && Localize && !Application.IsEditor;
        
        if (subscribe != _localizationSubscribed)
        {
            if (subscribe)
            {
                LocalizationManager.LanguageChanged += RefreshLocalize;
            }
            else
            {
                LocalizationManager.LanguageChanged -= RefreshLocalize;
            }
            
            _localizationSubscribed = subscribe;
        }
        
        RefreshLocalize();
    }

    internal void RefreshLocalize()
    {
        if (_created && !IsDestroyed && !Application.IsEditor && Localize &&
            !string.IsNullOrWhiteSpace(Table) && !string.IsNullOrWhiteSpace(Key))
        {
            Content = LocalizationManager.GetString(Key, Table);
        }
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
    
    public Vector2 MeasureText()
    {
        return GetLayout()?.Size ?? Vector2.Zero;
    }
    
    [UIAction("Text/Set text")]
    public void SetText(string text)
    {
        Content = text;
    }
}
