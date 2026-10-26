using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.UI;

public class Canvas
{
    private readonly List<Widget> _children = [];

    private bool _isInitialized;
    
    public IReadOnlyList<Widget> Children { get; }
    public Vector2 Size { get; private set; }
    public bool Visible { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public bool IsDestroyed { get; private set; }
    
    public Canvas()
    {
        Children = _children.AsReadOnly();
    }

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);

        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        
        OnCreate();
    }

    public T Add<T>() where T : Widget, new()
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);
        
        var widget = new T();
        widget.Initialize(this, null);
        
        return widget;
    }

    public void Resize(Vector2 size)
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);

        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0 || size.Y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }
        
        Size = size;
    }

    public void Update(float deltaSeconds)
    {
        if (IsDestroyed || !IsEnabled)
        {
            return;
        }
        
        OnUpdate(deltaSeconds);
        
        var visited = new HashSet<Widget>();
        
        foreach (var child in _children.ToArray())
        {
            if (IsDestroyed || !IsEnabled)
            {
                break;
            }

            if (child.Parent == null)
            {
                child.Update(deltaSeconds, visited);
            }
        }
    }

    public void Draw(Graphics graphics)
    {
        if (IsDestroyed || !Visible)
        {
            return;
        }
        
        OnDraw(graphics);
        
        var visited = new HashSet<Widget>();
        
        foreach (var child in _children.ToArray())
        {
            if (IsDestroyed || !Visible)
            {
                break;
            }

            if (child.Parent == null)
            {
                child.Draw(graphics, visited);
            }
        }
    }

    public void Clear()
    {
        foreach (var child in _children.ToArray())
        {
            child.Destroy();
        }
    }

    public void Destroy()
    {
        if (IsDestroyed)
        {
            return;
        }
        
        IsDestroyed = true;
        
        Clear();
        OnDestroy();
    }

    internal void Attach(Widget widget) => _children.Add(widget);
    internal void Detach(Widget widget) => _children.Remove(widget);

    protected virtual void OnCreate() { }
    protected virtual void OnUpdate(float deltaSeconds) { }
    protected virtual void OnDraw(Graphics graphics) { }
    protected virtual void OnDestroy() { }
}

