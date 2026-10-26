using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.UI;

public sealed class UIManager
{
    public Canvas Canvas { get; private set; } = null!;
    public Pointer Pointer { get; private set; } = null!;

    private bool _isInitialized;
    private bool _isDestroyed;

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_isDestroyed, this);

        if (_isInitialized)
        {
            return;
        }
        
        _isInitialized = true;
        
        Canvas = CreateCanvas();
        
        var size = Application.WindowSize;
        
        Resize(size.X > 0 && size.Y > 0 ? size : new Vector2(
            Settings.GetInt("Window", "Width", 1024),
            Settings.GetInt("Window", "Height", 768)));
        
        Pointer = new Pointer(Canvas);
        Canvas.Initialize();
    }

    private static Canvas CreateCanvas()
    {
        var name = Settings.GetString("Game", "UICanvasClass", "");

        if (string.IsNullOrEmpty(name))
        {
            return new Canvas();
        }

        var type = Type.GetType(name);
        
        if (type == null || !typeof(Canvas).IsAssignableFrom(type) || type.IsAbstract ||
            type.GetConstructor(Type.EmptyTypes) == null)
        {
            Console.WriteLine($"UI canvas class '{name}' is invalid. Using the default Canvas.");
            
            return new Canvas();
        }

        return (Canvas)Activator.CreateInstance(type)!;
    }

    public void Update(float deltaSeconds)
    {
        if (_isDestroyed)
        {
            return;
        }
        
        Canvas.Update(deltaSeconds);
        Pointer.Update();
    }

    public void Draw(Graphics graphics)
    {
        if (Canvas.IsDestroyed || !Canvas.Visible || Canvas.Size.X <= 0 || Canvas.Size.Y <= 0)
        {
            return;
        }
        
        graphics.BeginUI(Canvas.Size);
        
        try
        {
            Canvas.Draw(graphics);
        }
        finally
        {
            graphics.EndUI();
        }
    }

    public void Resize(Vector2 size)
    {
        Canvas.Resize(size);
    }

    public void Destroy()
    {
        if (_isDestroyed)
        {
            return;
        }
        
        _isDestroyed = true;

        try
        {
            Pointer.Reset();
        }
        finally
        {
            Canvas.Destroy();
        }
    }
}

