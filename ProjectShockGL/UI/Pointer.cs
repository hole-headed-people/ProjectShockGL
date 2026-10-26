using System.Numerics;
using Silk.NET.Input;

namespace ProjectShockGL.UI;

public sealed class Pointer
{
    private static readonly MouseButton[] Buttons = Enum.GetValues<MouseButton>()
        .Where(button => (int)button >= 0).ToArray();
    
    private readonly Canvas _canvas;
    private readonly Dictionary<MouseButton, Widget> _captures = [];
    private readonly HashSet<MouseButton> _previousButtons = [];
    private readonly HashSet<MouseButton> _currentButtons = [];
    private readonly List<MouseButton> _polledButtons = [];
    
    private bool _wasAvailable;
    private bool _isUpdating;
    private int _resetVersion;

    public Vector2 Position { get; private set; }
    public Widget? HoveredWidget { get; private set; }
    public bool IsEnabled { get; set; } = true;

    public Pointer(Canvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        
        _canvas = canvas;
    }

    public Widget? GetPressedWidget(MouseButton button)
    {
        return _captures.GetValueOrDefault(button);
    }

    public void Update()
    {
        _polledButtons.Clear();

        foreach (var button in Buttons)
        {
            if (Input.IsMouseButton(button)) _polledButtons.Add(button);
        }

        var size = Application.WindowSize;

        if (size.X <= 0 || size.Y <= 0)
        {
            size = _canvas.Size;
        }
        
        Update(Input.GetMousePosition(), size, _polledButtons,
            Input.HasMouse && Application.IsFocused && Input.GetCursorMode() == CursorMode.Normal);
    }
    
    public void Update(Vector2 mousePosition, Vector2 inputSize,
        IReadOnlyCollection<MouseButton> pressedButtons, bool available = true)
    {
        ArgumentNullException.ThrowIfNull(pressedButtons);

        if (_isUpdating)
        {
            throw new InvalidOperationException("Pointer updates cannot be re-entered.");
        }
        
        _isUpdating = true;
        
        try
        {
            _currentButtons.Clear();

            foreach (var button in pressedButtons)
            {
                if ((int)button >= 0) _currentButtons.Add(button);
            }

            if (!available || !IsEnabled || !CanvasAvailable() || !Finite(mousePosition) ||
                !Finite(inputSize) || inputSize.X <= 0 || inputSize.Y <= 0)
            {
                Reset();
                
                return;
            }

            Position = mousePosition / inputSize * _canvas.Size;
            
            if (!_wasAvailable)
            {
                _previousButtons.Clear();
                _previousButtons.UnionWith(_currentButtons);
            }
            
            _wasAvailable = true;
            
            var version = _resetVersion;
            CancelInvalidCaptures();

            if (version != _resetVersion)
            {
                return;
            }
            
            UpdateHover();

            if (version != _resetVersion)
            {
                return;
            }

            foreach (var button in Buttons)
            {
                if (_currentButtons.Contains(button) == _previousButtons.Contains(button))
                {
                    continue;
                }
                
                if (_currentButtons.Contains(button) && !_previousButtons.Contains(button))
                {
                    var target = HoveredWidget;
                    
                    if (target != null && Interactive(target))
                    {
                        _captures[button] = target;
                        target.MousePressed(button);
                    }
                }
                else if (!_currentButtons.Contains(button) && _previousButtons.Contains(button) &&
                         _captures.Remove(button, out var target))
                {
                    if (Interactive(target)) target.MouseReleased(button);
                    else if (!target.IsDestroyed) target.MouseCancelled(button);
                }

                if (version != _resetVersion)
                {
                    return;
                }
                
                CancelInvalidCaptures();

                if (version != _resetVersion)
                {
                    return;
                }
                
                UpdateHover();

                if (version != _resetVersion)
                {
                    return;
                }
            }
            
            CancelInvalidCaptures();
        }
        finally
        {
            _previousButtons.Clear();
            _previousButtons.UnionWith(_currentButtons);
            
            _isUpdating = false;
        }
    }
    
    public void Reset()
    {
        _resetVersion++;
        _wasAvailable = false;
        
        var captures = _captures.ToArray();
        _captures.Clear();
        
        var hovered = HoveredWidget;
        
        HoveredWidget = null;

        if (hovered != null)
        {
            hovered.IsMouseOver = false;
        }

        foreach (var (button, target) in captures)
        {
            if (!target.IsDestroyed) target.MouseCancelled(button);
        }

        if (hovered is { IsDestroyed: false })
        {
            hovered.MouseExit();
        }
    }

    private bool CanvasAvailable()
    {
        return !_canvas.IsDestroyed && _canvas.Visible && _canvas.IsEnabled &&
               _canvas.Size.X > 0 && _canvas.Size.Y > 0;
    }

    private bool Interactive(Widget widget)
    {
        if (!IsEnabled || !CanvasAvailable() || widget.Canvas != _canvas || !widget.HitTestVisible)
        {
            return false;
        }

        for (var current = widget; current != null; current = current.Parent)
        {
            if (current.IsDestroyed || !current.Visible || !current.IsEnabled) return false;
        }
        
        return true;
    }

    private Widget? HitTest()
    {
        if (!IsEnabled || !CanvasAvailable() || Position.X < 0 || Position.Y < 0 ||
            Position.X >= _canvas.Size.X || Position.Y >= _canvas.Size.Y)
        {
            return null;
        }
        
        return HitTestChildren(_canvas.Children);
    }

    private Widget? HitTestChildren(IReadOnlyList<Widget> children)
    {
        for (var i = children.Count - 1; i >= 0; i--)
        {
            var widget = children[i];

            if (widget.IsDestroyed || !widget.Visible || !widget.IsEnabled)
            {
                continue;
            }
            
            var child = HitTestChildren(widget.Children);

            if (child != null)
            {
                return child;
            }

            if (widget.HitTestVisible && widget.Rect.Contains(Position))
            {
                return widget;
            }
        }
        
        return null;
    }

    private void UpdateHover()
    {
        if (HitTest() == HoveredWidget)
        {
            return;
        }
        
        var version = _resetVersion;
        var old = HoveredWidget;
        
        HoveredWidget = null;
        
        if (old != null)
        {
            old.IsMouseOver = false;
            if (!old.IsDestroyed)
            {
                old.MouseExit();
            }
        }

        if (version != _resetVersion)
        {
            return;
        }
        
        var next = HitTest();
        HoveredWidget = next;

        if (next == null)
        {
            return;
        }
        
        next.IsMouseOver = true;
        next.MouseEnter();

        if (version != _resetVersion)
        {
            return;
        }
        
        if (!Interactive(next))
        {
            HoveredWidget = null;
            next.IsMouseOver = false;
            
            if (!next.IsDestroyed)
            {
                next.MouseExit();
            }
        }
    }

    private void CancelInvalidCaptures()
    {
        if (_captures.Count == 0)
        {
            return;
        }
        
        var version = _resetVersion;
        
        foreach (var (button, target) in _captures.ToArray())
        {
            if (Interactive(target))
            {
                continue;
            }
            
            _captures.Remove(button);

            if (!target.IsDestroyed)
            {
                target.MouseCancelled(button);
            }

            if (version != _resetVersion)
            {
                return;
            }
        }
    }

    private static bool Finite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
