using System.Numerics;
using ProjectShockGL.Core;

namespace ProjectShockGL.UI;

public abstract class LayoutGroup : Widget
{
    private Vector4 _padding;
    
    private float _spacing;
    
    private HorizontalAlignment _horizontalAlignment;
    private VerticalAlignment _verticalAlignment;

    protected abstract bool Horizontal { get; }
    
    [Editable]
    public Vector4 Padding
    {
        get => _padding;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z) ||
                !float.IsFinite(value.W) || value.X < 0 || value.Y < 0 || value.Z < 0 || value.W < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            
            _padding = value;
        }
    }

    [Editable]
    public float Spacing
    {
        get => _spacing;
        set
        {
            if (!float.IsFinite(value) || value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            
            _spacing = value;
        }
    }

    [Editable]
    public HorizontalAlignment HorizontalAlignment
    {
        get => _horizontalAlignment;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            
            _horizontalAlignment = value;
        }
    }

    [Editable]
    public VerticalAlignment VerticalAlignment
    {
        get => _verticalAlignment;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            
            _verticalAlignment = value;
        }
    }

    [Editable]
    public bool StretchChildren { get; set; }

    internal (Vector2 Position, Vector2 Size) Arrange(Widget child)
    {
        var inner = Vector2.Max(Vector2.Zero, Rect.Size - new Vector2(Padding.X + Padding.Z, Padding.Y + Padding.W));
        
        var count = 0;
        
        var length = 0f;
        var preceding = 0f;
        
        foreach (var sibling in Children)
        {
            if (sibling.IsDestroyed || !sibling.Visible || sibling.FillCanvas)
            {
                continue;
            }

            if (sibling == child)
            {
                preceding = length + count * Spacing;
            }
            
            var size = sibling.Rect.AuthoredRect.Size;
            
            length += Horizontal ? size.X : size.Y;
            
            count++;
        }
        
        length += Math.Max(0, count - 1) * Spacing;
        
        var resultSize = child.Rect.AuthoredRect.Size;
        
        if (StretchChildren)
        {
            if (Horizontal)
            {
                resultSize.Y = inner.Y;
            }
            else
            {
                resultSize.X = inner.X;
            }
        }
        
        var horizontal = HorizontalAlignment switch
        {
            HorizontalAlignment.Center => .5f,
            HorizontalAlignment.Right => 1f,
            _ => 0f
        };
        
        var vertical = VerticalAlignment switch
        {
            VerticalAlignment.Center => .5f,
            VerticalAlignment.Bottom => 1f,
            _ => 0f
        };
        
        var position = new Vector2(Padding.X, Padding.Y);
        
        if (Horizontal)
        {
            position.X += Math.Max(0, inner.X - length) * horizontal + preceding;
            position.Y += Math.Max(0, inner.Y - resultSize.Y) * vertical;
        }
        else
        {
            position.X += Math.Max(0, inner.X - resultSize.X) * horizontal;
            position.Y += Math.Max(0, inner.Y - length) * vertical + preceding;
        }
        
        return (position, resultSize);
    }
}

public class HorizontalLayout : LayoutGroup
{
    protected override bool Horizontal => true;
}

public class VerticalLayout : LayoutGroup
{
    protected override bool Horizontal => false;
}
