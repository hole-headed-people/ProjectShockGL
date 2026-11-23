using System.Numerics;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools;

namespace ProjectShockGL.UI;

public sealed class UIAsset
{
    public int Version { get; set; } = 1;
    public WidgetData Root { get; set; } = new();

    public Widget Instantiate(Canvas canvas, Widget? parent = null, bool enableActions = true)
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
            
            var widgets = new Dictionary<Guid, Widget>();
            
            void Collect(Widget widget)
            {
                if (widget.Id == Guid.Empty || !widgets.TryAdd(widget.Id, widget))
                {
                    throw new InvalidDataException("UI widget IDs must be nonempty and unique within the screen.");
                }

                foreach (var child in widget.Children)
                {
                    Collect(child);
                }
            }
            
            Collect(root);
            
            foreach (var button in widgets.Values.OfType<Button>())
            {
                button.BindActions(root, widgets, enableActions);
            }
            
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
    public Guid Id { get; set; } = Guid.NewGuid();
    public List<UIActionCall> ClickActions { get; set; } = [];
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
    public int SortOrder { get; set; }
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

    public Type GetWidgetType() => Type switch
    {
        "Widget" => typeof(Widget),
        "Image" => typeof(Image),
        "Text" => typeof(Text),
        "Button" => typeof(Button),
        "TextScrollView" => typeof(TextScrollView),
        _ => throw new InvalidDataException($"Unsupported UI widget type '{Type}'.")
    };

    public IEnumerable<WidgetData> Traverse()
    {
        yield return this;
        foreach (var child in new[] { ButtonImage, ButtonText, Body }.OfType<WidgetData>().Concat(Children))
        {
            foreach (var node in child.Traverse()) yield return node;
        }
    }

    public void RegenerateIds()
    {
        var nodes = Traverse().ToArray();
        var ids = nodes.ToDictionary(node => node.Id, _ => Guid.NewGuid());
        foreach (var node in nodes)
        {
            node.Id = ids[node.Id];
            foreach (var action in node.ClickActions)
            {
                if (action.Target == UIActionTarget.Widget && ids.TryGetValue(action.TargetId, out var replacement))
                    action.TargetId = replacement;
            }
        }
    }
    private static string[] PropertyNames(Type type) => type switch
    {
        var t when typeof(Image).IsAssignableFrom(t) => ["Color", "DrawMode", "Border", "BorderScale", "FillCenter", "EdgeMode", "CenterMode"],
        var t when typeof(Text).IsAssignableFrom(t) => ["Content", "Color", "WordWrap", "ClipToRect", "HorizontalAlignment", "VerticalAlignment"],
        var t when typeof(Button).IsAssignableFrom(t) => ["NormalColor", "HoverColor", "PressedColor", "DisabledColor"],
        var t when typeof(TextScrollView).IsAssignableFrom(t) => ["WheelStep"],
        _ => []
    };

    public static IEnumerable<PropertyInfo> GetAdditionalEditableProperties(Type type)
    {
        var reserved = new HashSet<string>(StringComparer.Ordinal)
        {
            "Id", "Name", "Rect", "Visible", "IsEnabled", "HitTestVisible", "FillCanvas",
            "Texture", "Region", "Font", "Text", "Body", "Canvas", "Parent", "Children",
            "IsMouseOver", "IsDestroyed", "SortOrder"
        };
        reserved.UnionWith(PropertyNames(type));
        return EditableProperties.Get(type).Where(property =>
            property.GetMethod is { IsPublic: true, IsStatic: false } &&
            property.SetMethod is { IsPublic: true, IsStatic: false } &&
            property.GetIndexParameters().Length == 0 && !reserved.Contains(property.Name));
    }

    private static IEnumerable<string> SavedPropertyNames(Widget widget)
    {
        return PropertyNames(widget.GetType()).Concat(GetAdditionalEditableProperties(widget.GetType())
            .Select(property => property.Name)).Distinct(StringComparer.Ordinal);
    }
    internal static JObject CaptureProperties(Widget widget)
    {
        var properties = new JObject();
        var serializer = JsonSerializer.Create(JsonHelper.GetSettings());

        foreach (var name in SavedPropertyNames(widget))
        {
            properties[name] = widget.GetType().GetProperty(name)!.GetValue(widget) is { } value
                ? JToken.FromObject(value, serializer) : JValue.CreateNull();
        }

        return properties;
    }

    internal static WidgetData Capture(Widget widget)
    {
        var rect = widget.Rect.AuthoredRect;
        var data = new WidgetData
        {
            Id = widget.Id, Type = widget.GetType().Name, Name = widget.Name,
            Position = rect.Position, Size = rect.Size,
            Anchor = rect.Anchor, Pivot = rect.Pivot,
            Visible = widget.Visible, IsEnabled = widget.IsEnabled, HitTestVisible = widget.HitTestVisible,
            FillCanvas = widget.FillCanvas, SortOrder = widget.SortOrder
        };

        if (data.GetWidgetType() != widget.GetType())
        {
            throw new InvalidDataException($"UI capture supports standard widget types. Found {widget.GetType()}.");
        }

        data.Properties = CaptureProperties(widget);

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
            data.ClickActions = button.ClickActions.Select(action => action.Copy()).ToList();
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

    internal static void ApplyProperties(Widget widget, JObject properties)
    {
        var serializer = JsonSerializer.Create(JsonHelper.GetSettings());
        
        foreach (var property in properties.Properties())
        {
            if (!SavedPropertyNames(widget).Contains(property.Name))
            {
                throw new InvalidDataException($"Unsupported {widget.GetType().Name} property '{property.Name}'.");
            }
            
            var target = widget.GetType().GetProperty(property.Name)!;
            target.SetValue(widget, property.Value.ToObject(target.PropertyType, serializer));
        }
        
        // JSON property order must not let Content overwrite the resolved translation.
        if (widget is Text text) text.RefreshLocalize();
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

        widget.Id = Id;
        widget.Name = Name;
        widget.SortOrder = SortOrder;
        widget.Rect.Position = Position;
        widget.Rect.Size = Size;
        widget.Rect.Anchor = Anchor;
        widget.Rect.Pivot = Pivot;
        
        ApplyProperties(widget, Properties);

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
            button.ClickActions.Clear();
            button.ClickActions.AddRange(ClickActions.Select(action => action.Copy()));
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
