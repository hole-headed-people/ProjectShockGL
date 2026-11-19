using System.Numerics;
using ProjectShockGL.Rendering;
using Silk.NET.Input;

namespace ProjectShockGL.UI;

public class Button : Widget
{
    private Color _normalColor = Color.White;
    private Color _hoverColor = Color.LightGray;
    private Color _pressedColor = Color.Gray;
    private Color _disabledColor = Color.DarkGray;
    
    private bool _isPressed;
    
    private Image? _image;

    private Widget? _actionRoot;
    private IReadOnlyDictionary<Guid, Widget>? _actionWidgets;
    
    private bool _actionsEnabled;

    internal List<UIActionCall> ClickActions { get; } = [];
    public UIEvent Click { get; }
    
    public Button()
    {
        Click = new UIEvent(() => _image != null && CanInteract());
        Click.InvokeActions = InvokeClickActions;
    }
    
    public Color NormalColor
    {
        get => _normalColor;
        set { _normalColor = value; RefreshColor(); }
    }

    public Color HoverColor
    {
        get => _hoverColor;
        set { _hoverColor = value; RefreshColor(); }
    }

    public Color PressedColor
    {
        get => _pressedColor;
        set { _pressedColor = value; RefreshColor(); }
    }

    public Color DisabledColor
    {
        get => _disabledColor;
        set { _disabledColor = value; RefreshColor(); }
    }

    internal Image Background => _image!;

    public Text Text { get; private set; } = null!;

    protected override void OnCreate()
    {
        base.OnCreate();

        _image = Add<Image>();
        
        _image.HitTestVisible = false;
        
        _image.Rect.Pivot = Vector2.Zero;
        _image.Rect.Anchor = Vector2.Zero;
        
        _image.Rect.Size = Rect.Size;

        Text = Add<Text>();
        
        Text.Rect.Anchor = Vector2.Zero;
        Text.Rect.Pivot = Vector2.Zero;
        
        Text.Rect.Size = Rect.Size;
        
        Text.Color = Color.Black;
        
        Text.HorizontalAlignment = HorizontalAlignment.Center;
        Text.VerticalAlignment = VerticalAlignment.Center;

        RefreshColor();
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        base.OnUpdate(deltaSeconds);
        
        _image!.Rect.Size = Rect.Size;
        
        Text.Rect.Size = Rect.Size;
        
        RefreshColor();
    }

    private bool CanInteract()
    {
        if (!HitTestVisible || IsDestroyed)
        {
            return false;
        }
        
        for (Widget? widget = this; widget != null; widget = widget.Parent)
        {
            if (widget.IsDestroyed || !widget.Visible || !widget.IsEnabled)
            {
                return false;
            }
        }
        
        return Canvas.Visible && Canvas.IsEnabled && !Canvas.IsDestroyed;
    }

    private void RefreshColor()
    {
        if (_image == null)
        {
            return;
        }

        if (!CanInteract())
        {
            _isPressed = false;
            _image.Color = DisabledColor;
        }
        else if (_isPressed && IsMouseOver)
        {
            _image.Color = PressedColor;
        }
        else
        {
            _image.Color = IsMouseOver ? HoverColor : NormalColor;
        }
    }

    protected override void OnMouseEnter()
    {
        base.OnMouseEnter();
        
        RefreshColor();
    }

    protected override void OnMouseExit()
    {
        base.OnMouseExit();
        
        RefreshColor();
    }

    protected override void OnMousePressed(MouseButton button)
    {
        base.OnMousePressed(button);

        if (button != MouseButton.Left)
        {
            return;
        }
        
        _isPressed = CanInteract();
        
        RefreshColor();
    }

    protected override void OnMouseReleased(MouseButton button)
    {
        base.OnMouseReleased(button);

        if (button != MouseButton.Left)
        {
            return;
        }

        var clicked = _isPressed && IsMouseOver && CanInteract();
        
        _isPressed = false;
        RefreshColor();

        if (!clicked)
        {
            return;
        }

        Click.Invoke();
    }

    protected override void OnMouseCancelled(MouseButton button)
    {
        base.OnMouseCancelled(button);

        if (button != MouseButton.Left)
        {
            return;
        }
        
        _isPressed = false;
        
        RefreshColor();
    }

    protected override void OnEnabledChanged(bool isEnabled)
    {
        base.OnEnabledChanged(isEnabled);
        
        _isPressed = false;
        
        RefreshColor();
    }

    protected override void OnVisibleChanged(bool isVisible)
    {
        base.OnVisibleChanged(isVisible);
        
        _isPressed = false;
        
        RefreshColor();
    }

    protected override void OnHitTestVisibleChanged(bool isVisible)
    {
        base.OnHitTestVisibleChanged(isVisible);
        
        _isPressed = false;
        
        RefreshColor();
    }

    internal void BindActions(Widget root, IReadOnlyDictionary<Guid, Widget> widgets, bool enabled)
    {
        _actionRoot = root;
        _actionWidgets = widgets;
        _actionsEnabled = enabled;
    }

    private void InvokeClickActions()
    {
        if (!_actionsEnabled || Application.IsEditor || _actionRoot == null || _actionWidgets == null)
        {
            return;
        }
        
        foreach (var action in ClickActions.Select(action => action.Copy()).ToArray())
        {
            if (!CanInteract() || _actionRoot.IsDestroyed || !_actionRoot.Visible || !_actionRoot.IsEnabled)
            {
                break;
            }

            if (!UIActionCatalog.Execute(action, _actionWidgets, Name))
            {
                break;
            }
        }
    }
    protected override void OnDestroy()
    {
        _isPressed = false;
        
        Click.RemoveAllListeners();
        ClickActions.Clear();
        
        _actionRoot = null;
        _actionWidgets = null;
        
        base.OnDestroy();
    }
}
