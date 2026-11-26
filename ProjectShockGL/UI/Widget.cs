using System.Numerics;
using ProjectShockGL.Rendering;
using Silk.NET.Input;

namespace ProjectShockGL.UI;

public class Widget
{
    private bool _isEnabled = true;
    
    private bool _isHitTestVisible = true;
    private bool _isVisible = true;
    
    private Canvas? _canvas;
    private readonly List<Widget> _children = [];
    
    public Canvas Canvas => _canvas ?? throw new InvalidOperationException("Add the widget to a canvas first.");
    
    private bool _fillCanvas;
    private bool _isScreenRoot;

    public bool FillCanvas
    {
        get => _fillCanvas || _isScreenRoot;
        set
        {
            _fillCanvas = value;
            Rect.ScreenSize = FillCanvas ? () => Canvas.Size : null;
        }
    }

    internal void SetAsScreenRoot()
    {
        _isScreenRoot = true;
        FillCanvas = true;
    }

    public Guid Id { get; internal set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public T? Find<T>(string name) where T : Widget => WidgetSearch.Find<T>(Children, name);

    public Widget? Parent { get; private set; }
    public IReadOnlyList<Widget> Children { get; }
    public RectTransform Rect { get; }

    public bool Visible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
            {
                return;
            }
            
            _isVisible = value;
            
            OnVisibleChanged(_isVisible);
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }
            
            _isEnabled = value;
            
            OnEnabledChanged(_isEnabled);
        }
    }

    public bool HitTestVisible
    {
        get => _isHitTestVisible;
        set
        {
            if (_isHitTestVisible == value)
            {
                return;
            }
            
            _isHitTestVisible = value;
            
            OnHitTestVisibleChanged(_isHitTestVisible);
        }
    }
    public bool IsMouseOver { get; internal set; }
    public bool IsDestroyed { get; private set; }
    
    public Widget()
    {
        Children = _children.AsReadOnly();
        Rect = new RectTransform(GetParentBounds);
        Rect.LayoutBounds = () => Parent is LayoutGroup layout && !FillCanvas && Visible && !IsDestroyed
            ? layout.Arrange(this) : null;
    }

    internal void Initialize(Canvas canvas, Widget? parent)
    {
        _canvas = canvas;
        
        Parent = parent;

        if (parent == null)
        {
            canvas.Attach(this);
        }
        else
        {
            parent._children.Add(this);
        }
        
        OnCreate();
    }
    
    
    public T Add<T>(Type type) where T : Widget
    {
        ObjectDisposedException.ThrowIf(IsDestroyed || Canvas.IsDestroyed, this);

        if (!typeof(T).IsAssignableFrom(type))
        {
            throw new ArgumentException($"Type {type} is not assignable to {typeof(T)}", nameof(type));
        }
    
        var widget = (T)Activator.CreateInstance(type)!;
        widget.Initialize(Canvas, this);
    
        return widget;
    }

    public T Add<T>() where T : Widget, new()
    {
        ObjectDisposedException.ThrowIf(IsDestroyed || Canvas.IsDestroyed, this);
        
        var widget = new T();
        widget.Initialize(Canvas, this);
        
        return widget;
    }
    
    internal static Widget[] DrawOrder(IEnumerable<Widget> children)
    {
        return children.OrderBy(widget => widget.SortOrder).ToArray();
    }

    public void SetAsFirstSibling()
    {
        SetSiblingIndex(0);
    }

    public void SetAsLastSibling()
    {
        SetSiblingIndex(int.MaxValue);
    }

    public void SetSiblingIndex(int index)
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);
        ObjectDisposedException.ThrowIf(Canvas.IsDestroyed, Canvas);
        if (Parent == null)
        {
            Canvas.SetSiblingIndex(this, index);
            return;
        }

        var siblings = Parent._children;
        siblings.Remove(this);
        siblings.Insert(Math.Clamp(index, 0, siblings.Count), this);
    }
    public void SetParent(Widget? parent)
    {
        ObjectDisposedException.ThrowIf(IsDestroyed || Canvas.IsDestroyed, this);
        
        if (parent != null)
        {
            ObjectDisposedException.ThrowIf(parent.IsDestroyed, parent);

            if (parent.Canvas != Canvas)
            {
                throw new InvalidOperationException("Widgets must belong to the same canvas.");
            }
            
            for (var ancestor = parent; ancestor != null; ancestor = ancestor.Parent)
            {
                if (ancestor == this)
                {
                    throw new InvalidOperationException("Widget hierarchy cannot contain cycles.");
                }
            }
        }

        if (Parent == parent)
        {
            return;
        }
        
        Detach();
        Parent = parent;

        if (parent == null)
        {
            Canvas.Attach(this);
        }
        else
        {
            parent._children.Add(this);
        }
    }

    internal void Update(float deltaSeconds, HashSet<Widget> visited)
    {
        if (IsDestroyed || !IsEnabled || !visited.Add(this))
        {
            return;
        }
        
        OnUpdate(deltaSeconds);
        
        foreach (var child in _children.ToArray())
        {
            if (IsDestroyed || !IsEnabled)
            {
                break;
            }

            if (child.Parent == this)
            {
                child.Update(deltaSeconds, visited);
            }
        }
    }

    internal virtual bool ClipsChildren => false;
    
    internal virtual bool Scroll(float delta)
    {
        return false;
    }

    internal void Draw(Graphics graphics, HashSet<Widget> visited)
    {
        if (IsDestroyed || !Visible || !visited.Add(this))
        {
            return;
        }
        
        OnDraw(graphics);

        var previousClip = ClipsChildren ? graphics?.UIClip : null;
        if (ClipsChildren)
        {
            graphics?.ClipUI(Rect.AbsolutePosition, Rect.Size);
        }

        try
        {
            foreach (var child in DrawOrder(_children))
            {
                if (IsDestroyed || !Visible)
                {
                    break;
                }

                if (child.Parent == this)
                {
                    child.Draw(graphics!, visited);
                }
            }
        }
        finally
        {
            if (ClipsChildren && graphics != null) graphics.UIClip = previousClip;
        }
    }

    [UIAction("Widget/Set visible")]
    public void SetVisible(bool visible)
    {
        Visible = visible;
    }

    [UIAction("Widget/Set enabled")]
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
    }

    [UIAction("Widget/Set hit testing")]
    public void SetHitTestVisible(bool enabled)
    {
        HitTestVisible = enabled;
    }

    [UIAction("Widget/Destroy")]
    public void Destroy()
    {
        if (IsDestroyed)
        {
            return;
        }
        
        IsDestroyed = true;
        IsMouseOver = false;
        
        Detach();
        
        Parent = null;

        foreach (var child in _children.ToArray())
        {
            child.Destroy();
        }
        
        OnDestroy();
    }

    private void Detach()
    {
        if (Parent == null)
        {
            Canvas.Detach(this);
        }
        else
        {
            Parent._children.Remove(this);
        }
    }

    private (Vector2 Position, Vector2 Size) GetParentBounds()
    {
        return Parent == null
            ? (Vector2.Zero, Canvas.Size)
            : (Parent.Rect.AbsolutePosition, Parent.Rect.Size);
    }

    protected virtual void OnCreate() { }
    protected virtual void OnUpdate(float deltaSeconds) { }
    protected virtual void OnDraw(Graphics graphics) { }
    protected virtual void OnEnabledChanged(bool isEnabled){}
    protected virtual void OnVisibleChanged(bool isVisible) { }
    protected virtual void OnHitTestVisibleChanged(bool isVisible) { }
    protected virtual void OnDestroy() { }

    internal void MouseEnter() => OnMouseEnter();
    internal void MouseExit() => OnMouseExit();
    internal void MousePressed(MouseButton button) => OnMousePressed(button);
    internal void MouseReleased(MouseButton button) => OnMouseReleased(button);
    internal void MouseCancelled(MouseButton button) => OnMouseCancelled(button);

    protected virtual void OnMouseEnter() { }
    protected virtual void OnMouseExit() { }
    protected virtual void OnMousePressed(MouseButton button) { }
    protected virtual void OnMouseReleased(MouseButton button) { }
    protected virtual void OnMouseCancelled(MouseButton button) { }
}

