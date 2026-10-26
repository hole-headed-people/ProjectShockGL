using System.Numerics;
using Silk.NET.Input;

namespace ProjectShockGL;

public static class Input
{
    private static bool _isInitialized;
    private static IInputContext _context = null!;

    private static Vector2 _mouseLastPosition;
    
    private static HashSet<Key> CurrentKeys { get; } = [];
    private static HashSet<MouseButton> CurrentMouseButtons { get; } = [];
    
    private static HashSet<Key> PreviousKeys { get; } = [];
    private static HashSet<MouseButton> PreviousMouseButtons { get; } = [];
    
    public static Vector2 MouseDeltaPosition { get; private set; }
    public static bool HasMouse => _isInitialized && _context.Mice.Count > 0;

    public static void Initialize(IInputContext context)
    {
        if (_isInitialized)
        {
            return;
        }
        
        _isInitialized = true;
        
        _context = context;
        _context.ConnectionChanged += OnConnectionChanged;

        if (_context.Keyboards.Count > 0)
        {
            OnKeyboardConnected(_context.Keyboards[0]);
        }

        if (_context.Mice.Count > 0)
        {
            OnMouseConnected(_context.Mice[0]);
        }
    }

    private static void OnConnectionChanged(IInputDevice device, bool status)
    {
        if (device is IKeyboard keyboard)
        {
            if (status)
            {
                OnKeyboardConnected(keyboard);
            }
            else
            {
                OnKeyboardDisconnected(keyboard);
            }
        }
        else if (device is IMouse mouse)
        {
            if (status)
            {
                OnMouseConnected(mouse);
            }
            else
            {
                OnMouseDisconnected(mouse);
            }
        }
    }

    private static void OnKeyboardConnected(IKeyboard keyboard)
    {
        keyboard.KeyDown += KeyboardOnKeyDown;
        keyboard.KeyUp += KeyboardOnKeyUp;
    }

    private static void OnKeyboardDisconnected(IKeyboard keyboard)
    {
        keyboard.KeyDown -= KeyboardOnKeyDown;
        keyboard.KeyUp -= KeyboardOnKeyUp;
    }

    private static void OnMouseConnected(IMouse mouse)
    {
        mouse.MouseDown += MouseOnMouseDown;
        mouse.MouseUp += MouseOnMouseUp;
        mouse.MouseMove += MouseOnMouseMove;
    }

    private static void OnMouseDisconnected(IMouse mouse)
    {
        mouse.MouseDown -= MouseOnMouseDown;
        mouse.MouseUp -= MouseOnMouseUp;
        mouse.MouseMove -= MouseOnMouseMove;
    }
    
    private static void KeyboardOnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        CurrentKeys.Add(key);
    }
    
    private static void KeyboardOnKeyUp(IKeyboard keyboard, Key key, int scancode)
    {
        CurrentKeys.Remove(key);
    }
    
    private static void MouseOnMouseDown(IMouse mouse, MouseButton button)
    {
        CurrentMouseButtons.Add(button);
    }
    
    private static void MouseOnMouseUp(IMouse mouse, MouseButton button)
    {
        CurrentMouseButtons.Remove(button);
    }
    
    private static void MouseOnMouseMove(IMouse mouse, Vector2 position)
    {
        MouseDeltaPosition += position - _mouseLastPosition;
        _mouseLastPosition = position;
    }

    public static void Update()
    {
        PreviousKeys.Clear();
        PreviousMouseButtons.Clear();
        
        PreviousKeys.UnionWith(CurrentKeys);
        PreviousMouseButtons.UnionWith(CurrentMouseButtons);
        
        MouseDeltaPosition = Vector2.Zero;
    }
    
    public static bool IsKeyDown(Key key)
    {
        if (!_isInitialized)
        {
            return false;
        }
        
        return CurrentKeys.Contains(key) && !PreviousKeys.Contains(key);
    }

    public static bool IsKeyUp(Key key)
    {
        if (!_isInitialized)
        {
            return false;
        }
        
        return !CurrentKeys.Contains(key) && PreviousKeys.Contains(key);
    }

    public static bool IsKey(Key key)
    {
        return _isInitialized && CurrentKeys.Contains(key);
    }
    
    public static bool IsMouseButtonDown(MouseButton button)
    {
        if (!_isInitialized)
        {
            return false;
        }
        
        return CurrentMouseButtons.Contains(button) && 
               !PreviousMouseButtons.Contains(button);
    }
    
    public static bool IsMouseButtonUp(MouseButton button)
    {
        if (!_isInitialized)
        {
            return false;
        }
        
        return !CurrentMouseButtons.Contains(button) && PreviousMouseButtons.Contains(button);
    }

    public static bool IsMouseButton(MouseButton button)
    {
        return _isInitialized && CurrentMouseButtons.Contains(button);
    }

    public static Vector2 GetMousePosition()
    {
        if (!_isInitialized || _context.Mice.Count == 0)
        {
            return Vector2.Zero;
        }
        
        return _context.Mice[0].Position;
    }

    public static void SetCursorMode(CursorMode mode)
    {
        if (_isInitialized && _context.Mice.Count > 0)
        {
            _context.Mice[0].Cursor.CursorMode = mode;
        }
    }
    
    public static CursorMode GetCursorMode()
    {
        return HasMouse ? _context.Mice[0].Cursor.CursorMode : CursorMode.Normal;
    }
}
