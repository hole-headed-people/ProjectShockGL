using System.Numerics;

namespace ProjectShockGL.UI;

public sealed class RectTransform
{
    private readonly Func<(Vector2 Position, Vector2 Size)> _parentBounds;
    
    private Vector2 _size = new(100f, 100f);
    
    private Vector2 _position;
    private Vector2 _anchor;
    private Vector2 _pivot;
    
    internal (Vector2 Position, Vector2 Size, Vector2 Anchor, Vector2 Pivot) AuthoredRect =>
        (_position, _size, _anchor, _pivot);

    internal Func<Vector2>? ScreenSize { get; set; }
    internal Func<(Vector2 Position, Vector2 Size)?>? LayoutBounds { get; set; }

    public Vector2 Position
    {
        get => ScreenSize != null ? Vector2.Zero : LayoutBounds?.Invoke()?.Position ?? _position;
        set => _position = value;
    }

    public Vector2 Size
    {
        get => ScreenSize?.Invoke() ?? LayoutBounds?.Invoke()?.Size ?? _size;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || value.X < 0 || value.Y < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Size must be finite and nonnegative.");
            _size = value;
        }
    }
    
    public Vector2 Anchor
    {
        get => ScreenSize != null || LayoutBounds?.Invoke() != null ? Vector2.Zero : _anchor;
        set => _anchor = value;
    }
    public Vector2 Pivot
    {
        get => ScreenSize != null || LayoutBounds?.Invoke() != null ? Vector2.Zero : _pivot;
        set => _pivot = value;
    }
    public Vector2 AbsolutePosition
    {
        get
        {
            if (ScreenSize != null) return Vector2.Zero;
            var parent = _parentBounds();
            if (LayoutBounds?.Invoke() is { } layout) return parent.Position + layout.Position;
            return parent.Position + parent.Size * Anchor + Position - Size * Pivot;
        }
    }
    
    internal RectTransform(Func<(Vector2 Position, Vector2 Size)> parentBounds)
    {
        _parentBounds = parentBounds;
    }

    public bool Contains(Vector2 canvasPosition)
    {
        var local = canvasPosition - AbsolutePosition;
        return local.X >= 0 && local.Y >= 0 && local.X < Size.X && local.Y < Size.Y;
    }
}

