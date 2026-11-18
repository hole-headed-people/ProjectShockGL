using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools;

namespace ProjectShockGL.UI;

public sealed class UIAsset
{
    public int Version { get; set; } = 1;
    public WidgetData Root { get; set; } = new();

    public Widget Instantiate(Canvas canvas, Widget? parent = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        if (parent != null && parent.Canvas != canvas)
        {
            throw new ArgumentException("Parent must belong to the supplied canvas.", nameof(parent));
        }

        if (Version != 1)
        {
            throw new InvalidDataException($"Unsupported UI version {Version}.");
        }
        
        var root = parent == null ? canvas.Add<Widget>(Root.GetWidgetType()) : parent.Add<Widget>(Root.GetWidgetType());
        
        try
        {
            Root.Apply(root);
            root.SetAsScreenRoot();
            
            return root;
        }
        catch
        {
            root.Destroy();
            
            throw;
        }
    }

    public static UIAsset Capture(Widget root)
    {
        ArgumentNullException.ThrowIfNull(root);
        ObjectDisposedException.ThrowIf(root.IsDestroyed, root);
        
        return new UIAsset { Root = WidgetData.Capture(root) };
    }
}

public sealed class WidgetData
{
    public string Type { get; set; } = "Widget";
    public string Name { get; set; } = string.Empty;
    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; } = new(100, 100);
    public Vector2 Anchor { get; set; }
    public Vector2 Pivot { get; set; }
    public bool Visible { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public bool HitTestVisible { get; set; } = true;
    public bool FillCanvas { get; set; }
    public JObject Properties { get; set; } = new();
    public string? Texture { get; set; }
    public TextureFormat TextureFormat { get; set; }
    public string? Atlas { get; set; }
    public string? Region { get; set; }
    public string? Font { get; set; }
    public int FontSize { get; set; } = 24;
    public WidgetData? ButtonImage { get; set; }
    public WidgetData? ButtonText { get; set; }
    public WidgetData? Body { get; set; }
    public List<WidgetData> Children { get; set; } = [];

    internal Type GetWidgetType() => Type switch
    {
        "Widget" => typeof(Widget),
        "Image" => typeof(Image),
        "Text" => typeof(Text),
        "Button" => typeof(Button),
        "TextScrollView" => typeof(TextScrollView),
        _ => throw new InvalidDataException($"Unsupported UI widget type '{Type}'.")
    };

    private static string[] PropertyNames(Widget widget) => widget switch
    {
        Image => ["Color", "DrawMode", "Border", "BorderScale", "FillCenter", "EdgeMode", "CenterMode"],
        Text => ["Content", "Color", "WordWrap", "ClipToRect", "HorizontalAlignment", "VerticalAlignment"],
        Button => ["NormalColor", "HoverColor", "PressedColor", "DisabledColor"],
        TextScrollView => ["WheelStep"],
        _ => []
    };

    internal static WidgetData Capture(Widget widget)
    {
        var rect = widget.Rect.AuthoredRect;
        var data = new WidgetData
        {
            Type = widget.GetType().Name, Name = widget.Name,
            Position = rect.Position, Size = rect.Size,
            Anchor = rect.Anchor, Pivot = rect.Pivot,
            Visible = widget.Visible, IsEnabled = widget.IsEnabled, HitTestVisible = widget.HitTestVisible,
            FillCanvas = widget.FillCanvas
        };

        if (data.GetWidgetType() != widget.GetType())
        {
            throw new InvalidDataException($"UI capture supports standard widget types. Found {widget.GetType()}.");
        }

        var serializer = JsonSerializer.Create(JsonHelper.GetSettings());

        foreach (var name in PropertyNames(widget))
        {
            data.Properties[name] = JToken.FromObject(widget.GetType().GetProperty(name)!.GetValue(widget)!, serializer);
        }

        if (widget is Image image)
        {
            if (image.Region is { } region)
            {
                (data.Atlas, data.Region) = Content.GetUIRegionReference(region);
            }
            else if (image.Texture != null)
            {
                (data.Texture, data.TextureFormat) = Content.GetUITextureReference(image.Texture);
            }
        }

        if (widget is Text text && text.Font != null)
        {
            (data.Font, data.FontSize) = Content.GetUIFontReference(text.Font);
        }
        
        if (widget is Button button)
        {
            data.ButtonImage = Capture(button.Background);
            data.ButtonText = Capture(button.Text);
        }

        if (widget is TextScrollView scroll)
        {
            data.Body = Capture(scroll.Body);
        }

        foreach (var child in widget.Children)
        {
            if (child.IsDestroyed)
            {
                continue;
            }

            if (widget is Button b && (child == b.Background || child == b.Text))
            {
                continue;
            }

            if (widget is TextScrollView view && child == view.Body)
            {
                continue;
            }
            
            data.Children.Add(Capture(child));
        }
        
        return data;
    }

    internal void Apply(Widget widget)
    {
        if (widget.GetType() != GetWidgetType())
        {
            throw new InvalidDataException("UI child role has an incompatible type.");
        }

        if (!Finite(Position) || !Finite(Anchor) || !Finite(Pivot))
        {
            throw new InvalidDataException("UI coordinates must be finite.");
        }

        widget.Name = Name;
        widget.Rect.Position = Position;
        widget.Rect.Size = Size;
        widget.Rect.Anchor = Anchor;
        widget.Rect.Pivot = Pivot;
        
        var serializer = JsonSerializer.Create(JsonHelper.GetSettings());
        
        foreach (var property in Properties.Properties())
        {
            if (!PropertyNames(widget).Contains(property.Name))
            {
                throw new InvalidDataException($"Unsupported {Type} property '{property.Name}'.");
            }
            
            var target = widget.GetType().GetProperty(property.Name)!;
            target.SetValue(widget, property.Value.ToObject(target.PropertyType, serializer));
        }
        
        if (widget is Image image)
        {
            image.Texture = Texture == null ? null : Content.LoadTexture(Texture, TextureFormat)
                ?? throw new FileNotFoundException($"UI texture '{Texture}' was not found.");
            image.Region = Atlas == null ? null : (Content.LoadTextureAtlas(Atlas)
                ?? throw new FileNotFoundException($"UI atlas '{Atlas}' was not found.")).GetRegion(Region
                    ?? throw new InvalidDataException("UI atlas requires a region name."));
        }
        
        if (widget is Text text)
        {
            text.Font = Font == null ? null : Content.LoadFont(Font, FontSize)
                                           ?? throw new FileNotFoundException($"UI font '{Font}' was not found.");
        }
        
        if (widget is Button button)
        {
            ButtonImage?.Apply(button.Background);
            ButtonText?.Apply(button.Text);
        }
        
        if (widget is TextScrollView scroll) Body?.Apply(scroll.Body);

        foreach (var child in Children)
        {
            var instance = widget.Add<Widget>(child.GetWidgetType());
            child.Apply(instance);
        }
        
        widget.FillCanvas = FillCanvas;
        widget.HitTestVisible = HitTestVisible;
        widget.IsEnabled = IsEnabled;
        widget.Visible = Visible;
    }

    private static bool Finite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
