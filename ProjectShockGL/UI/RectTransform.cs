using System.Numerics;

namespace ProjectShockGL.UI;

public sealed class RectTransform
{
    private readonly Func<(Vector2 Position, Vector2 Size)> _parentBounds;
    
    private Vector2 _size = new(100f, 100f);

    public Vector2 Position { get; set; }

    public Vector2 Size
    {
        get => _size;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || value.X < 0 || value.Y < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Size must be finite and nonnegative.");
            _size = value;
        }
    }
    
    public Vector2 Anchor { get; set; }
    public Vector2 Pivot { get; set; }
    public Vector2 AbsolutePosition
    {
        get
        {
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

