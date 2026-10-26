using System.Numerics;
using ProjectShockGL.Rendering;
using Silk.NET.Input;

namespace ProjectShockGL.UI;

public class Widget
{
    private Canvas? _canvas;
    private readonly List<Widget> _children = [];
    
    public Canvas Canvas => _canvas ?? throw new InvalidOperationException("Add the widget to a canvas first.");
    public Widget? Parent { get; private set; }
    public IReadOnlyList<Widget> Children { get; }
    public RectTransform Rect { get; }
    public bool Visible { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public bool HitTestVisible { get; set; } = true;
    public bool IsMouseOver { get; internal set; }
    public bool IsDestroyed { get; private set; }
    
    public Widget()
    {
        Children = _children.AsReadOnly();
        Rect = new RectTransform(GetParentBounds);
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

    public T Add<T>() where T : Widget, new()
    {
        ObjectDisposedException.ThrowIf(IsDestroyed || Canvas.IsDestroyed, this);
        
        var widget = new T();
        widget.Initialize(Canvas, this);
        
        return widget;
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

    internal void Draw(Graphics graphics, HashSet<Widget> visited)
    {
        if (IsDestroyed || !Visible || !visited.Add(this))
        {
            return;
        }
        
        OnDraw(graphics);
        
        foreach (var child in _children.ToArray())
        {
            if (IsDestroyed || !Visible)
            {
                break;
            }

            if (child.Parent == this)
            {
                child.Draw(graphics, visited);
            }
        }
    }

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

