using System.Numerics;
using ProjectShockGL.Core;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace ProjectShockGL.Rendering;

public class Camera : IDisposable
{
    private static bool _isInitialized;
    
    private static Vector2D<int> _framebufferSize = new(1, 1);
    
    private static Camera _active = new(new Vector2D<int>(1, 1));
    
    private static readonly List<CameraComponent> Components = [];
    
    public static Camera Default { get; private set; } = _active;
    public bool IsDisposed { get; private set; }
    
    internal CameraComponent? Component { get; set; }
    
    public Transform Transform { get; } = new();
    public bool Orthographic { get; set; }
    public float OrthoSize { get; set; } = 10f;
    public float Fov { get; set; } = MathF.PI / 3f;
    public float Near { get; set; } = 0.1f;
    public float Far { get; set; } = 1000f;
    public float AspectRatio { get; set; }
    public Color ClearColor { get; set; } = Color.Black;
    
    public static Camera Active
    {
        get
        {
            if (_active.IsDisposed || (_active.Component != null && !_active.Component.CanActivate))
            {
                _active = Default;
            }
            
            return _active;
        }
    }
    
    private static IWindow Window { get; set; } = null!;
    
    public static void Initialize(IWindow window)
    {
        if (_isInitialized)
        {
            return;
        }
        
        _isInitialized = true;
        
        Window = window;
        Window.FramebufferResize += OnFramebufferResize;
        
        var size = window.FramebufferSize;
        
        if (size.X > 0 && size.Y > 0)
        {
            _framebufferSize = size;
        }
        
        Default = new Camera(_framebufferSize);
        
        _active = Default;
    }

    public static Camera Create()
    {
        var camera = new Camera(_framebufferSize);
        
        return camera;
    }

    public static void ToggleTo(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ObjectDisposedException.ThrowIf(camera.IsDisposed, camera);
        
        if (camera.Component != null)
        {
            if (!camera.Component.CanActivate || Application.IsEditor)
            {
                throw new InvalidOperationException("Cannot activate an unavailable component camera.");
            }
            camera.Component.Synchronize();
        }
        
        if (_isInitialized)
        {
            camera.AspectRatio = (float)_framebufferSize.X / _framebufferSize.Y;
        }
        
        _active = camera;
    }

    public static void UseDefault()
    {
        ToggleTo(Default);
    }
    
    private Camera(Vector2D<int> size)
    {
        AspectRatio = (float)size.X / size.Y;
    }

    public void Dispose()
    {
        if (ReferenceEquals(this, Default) || IsDisposed)
        {
            return;
        }
        
        IsDisposed = true;
        
        ReleaseActive(this);
    }
    
    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.CreateLookAt(
            Transform.Position,
            Transform.Position + Transform.Forward,
            Transform.Up
        );
    }

    public Matrix4x4 GetProjectionMatrix()
    {
        if (!Orthographic)
        {
            return Matrix4x4.CreatePerspectiveFieldOfView(Fov, AspectRatio, Near, Far);
        }
        
        var halfHeight = OrthoSize * 0.5f;
        var halfWidth = halfHeight * AspectRatio;
            
        return Matrix4x4.CreateOrthographic(halfWidth * 2f, halfHeight * 2f, Near, Far);
    }
    
    public Vector3 ScreenPointToWorld(Vector2 position, float depth = 0.5f)
    {
        var ndcX = position.X / Window.Size.X * 2f - 1f;
        var ndcY = 1f - position.Y / Window.Size.Y * 2f;

        var ndc = new Vector4(ndcX, ndcY, depth * 2f - 1f, 1f);

        var projection = GetProjectionMatrix();
        var view = GetViewMatrix();

        Matrix4x4.Invert(projection, out var invProjection);
        Matrix4x4.Invert(view, out var invView);

        var world = Vector4.Transform(ndc, invProjection);
        
        world = Vector4.Transform(world, invView);
        world /= world.W;

        return new Vector3(world.X, world.Y, world.Z);
    }
    
    private static void OnFramebufferResize(Vector2D<int> size)
    {
        if (size.X <= 0 || size.Y <= 0)
        {
            return;
        }
        
        _framebufferSize = size;
        
        Default.AspectRatio = (float)size.X / size.Y;
        Active.AspectRatio = Default.AspectRatio;
    }
    
    internal static void ReleaseActive(Camera camera)
    {
        if (ReferenceEquals(_active, camera))
        {
            _active = Default;
        }
    }

    internal static void Register(CameraComponent component) => Components.Add(component);
    internal static void Unregister(CameraComponent component) => Components.Remove(component);

    internal static void SynchronizeComponents()
    {
        foreach (var component in Components.ToArray())
        {
            if (component.CanActivate)
            {
                component.Synchronize();
            }
        }
    }
}
