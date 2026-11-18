using System.Numerics;

namespace ProjectShockGL.UI;

public sealed class RectTransform
{
    private readonly Func<(Vector2 Position, Vector2 Size)> _parentBounds;
    
    private Vector2 _size = new(100f, 100f);

    internal (Vector2 Position, Vector2 Size, Vector2 Anchor, Vector2 Pivot) AuthoredRect =>
        (_position, _size, _anchor, _pivot);

    internal Func<Vector2>? ScreenSize { get; set; }
    private Vector2 _position;
    private Vector2 _anchor;
    private Vector2 _pivot;

    public Vector2 Position
    {
        get => ScreenSize == null ? _position : Vector2.Zero;
        set => _position = value;
    }

    public Vector2 Size
    {
        get => ScreenSize?.Invoke() ?? _size;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || value.X < 0 || value.Y < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Size must be finite and nonnegative.");
            _size = value;
        }
    }
    
    public Vector2 Anchor
    {
        get => ScreenSize == null ? _anchor : Vector2.Zero;
        set => _anchor = value;
    }
    public Vector2 Pivot
    {
        get => ScreenSize == null ? _pivot : Vector2.Zero;
        set => _pivot = value;
    }
    public Vector2 AbsolutePosition
    {
        get
        {
            if (ScreenSize != null) return Vector2.Zero;
            var parent = _parentBounds();
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

