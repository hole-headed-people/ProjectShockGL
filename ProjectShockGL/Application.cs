using ProjectShockGL.Rendering;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Renderer = ProjectShockGL.Rendering.Renderer;
using Window = Silk.NET.Windowing.Window;

namespace ProjectShockGL;

public static class Application
{
    public static event Action<GL, IWindow, IInputContext>? LoadGl;

    public static event Action? Load;
    
    public static event Action<float>? Update;

    public static event Action<float>? BeforeRender;
    public static event Action<Graphics>? Render;
    public static event Action<Graphics>? RenderUI;
    public static event Action<float>? AfterRender;

    public static event Action<Vector2D<int>>? Resized;
    
    public static event Action? Unload;
    public static event Func<bool>? Closing;
    
    private static IWindow _window = null!;
    private static GL _gl = null!;
    
    private static IInputContext _inputContext = null!;
    
    private static Renderer _renderer = null!;
    private static Graphics _graphics = null!;
    
    private static double _titleTimer;
    private static string _title = "";
    
    private static int _frameCount;
    private static double _fpsTimer;
    private static bool _quitRequested;
    private static bool _closeApproved;
    private static bool _hasUnloaded;
    
    public static double Time { get; private set; }
    public static double Fps { get; private set; }
    public static double FrameTimeMs { get; private set; }
    public static bool IsEditor { get; private set; }
    public static bool IsDebug { get; private set; }
    public static RenderPreviewSettings EditorPreview { get; } = new();
    public static bool IsFocused { get; private set; } = true;
    public static System.Numerics.Vector2 WindowSize { get; private set; }
    
    public static void Run()
    {
        var width = Settings.GetInt("Window", "Width", 1024);
        var height = Settings.GetInt("Window", "Height", 768);
        
        var canResize = Settings.GetBool("Window", "CanResize", false);
        
        _title = Settings.GetString("Game", "Name", "ProjectShockGL");
        
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(width, height),
            Title = _title,
            WindowBorder = canResize ? WindowBorder.Resizable : WindowBorder.Fixed
        };
        
        IsEditor = Settings.GetBool("Application", "IsEditor", false);
        IsDebug = Settings.GetBool("Application", "IsDebug", false);
        
        _window = Window.Create(options);
        
        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.Resize += OnResize;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClosing;
        _window.FocusChanged += focused => IsFocused = focused;
        
        _window.Run();
    }

    private static void OnLoad()
    {
        WindowSize = new System.Numerics.Vector2(_window.Size.X, _window.Size.Y);
        
        _gl = _window.CreateOpenGL();
        _inputContext = _window.CreateInput();
        
        Input.Initialize(_inputContext);
        Content.Initialize(_gl);
        
        _renderer = new Renderer(_gl, _window.Size.X, _window.Size.Y, IsEditor ? EditorPreview : null);
        _graphics = new Graphics(_renderer);
        
        Camera.Initialize(_window);
        
        LoadGl?.Invoke(_gl, _window, _inputContext);
        Load?.Invoke();
    }
    
    private static void OnUpdate(double deltaTime)
    {
        if (_quitRequested)
        {
            _quitRequested = false;
            
            if (CanClose())
            {
                _closeApproved = true;
                _window.Close();
                
                return;
            }
        }

        Time += deltaTime;
        Update?.Invoke((float)deltaTime);
        
        Input.Update();
    }

    private static void OnRender(double deltaTime)
    {
        _frameCount++;
        _fpsTimer += deltaTime;

        if (_fpsTimer >= 1.0)
        {
            Fps = _frameCount / _fpsTimer;
            FrameTimeMs = 1000.0 / Fps;

            _frameCount = 0;
            _fpsTimer = 0;
        }
        
        _graphics.BeginDraw(Camera.Active);

        BeforeRender?.Invoke((float)deltaTime);
        
        Render?.Invoke(_graphics);

        RenderUI?.Invoke(_graphics);

        AfterRender?.Invoke((float)deltaTime);
        
        _graphics.EndDraw();
        
        _titleTimer += deltaTime;

        if (_titleTimer >= 0.5)
        {
            _titleTimer = 0;

            UpdateTitle();
        }
    }

    private static void OnResize(Vector2D<int> size)
    {
        WindowSize = new System.Numerics.Vector2(size.X, size.Y);
        Resized?.Invoke(size);
    }
    
    private static void OnFramebufferResize(Vector2D<int> size)
    {
        _gl.Viewport(size);
        _renderer.OnResize(size.X, size.Y);
    }
    
    private static void OnClosing()
    {
        if (_hasUnloaded)
        {
            return;
        }
        
        if (!_closeApproved && !CanClose())
        {
            _window.IsClosing = false;
            
            return;
        }
        
        _hasUnloaded = true;

        Unload?.Invoke();

        _renderer.Dispose();

        Content.UnloadAll();

        _inputContext.Dispose();
        _gl.Dispose();
    }

    private static bool CanClose()
    {
        return Closing == null || Closing.GetInvocationList().Cast<Func<bool>?>().All(handler => handler != null && handler());
    }
    
    private static void UpdateTitle()
    {
        _window.Title = $"{_title} | " +
                        $"FPS: {Fps:F0} | " +
                        $"Frame: {FrameTimeMs:F2}ms";
    }

    public static void Quit()
    {
        _quitRequested = true;
    }
}
