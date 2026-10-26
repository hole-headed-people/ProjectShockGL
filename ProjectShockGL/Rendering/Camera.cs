using System.Numerics;
using ProjectShockGL.Core;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace ProjectShockGL.Rendering;

public class Camera
{
    private static bool _isInitialized;
    
    public Transform Transform { get; } = new();
    public bool Orthographic { get; set; }
    public float OrthoSize { get; set; } = 10f;
    public float Fov { get; set; } = MathF.PI / 3f;
    public float Near { get; set; } = 0.1f;
    public float Far { get; set; } = 1000f;
    public float AspectRatio { get; set; }
    public Color ClearColor { get; set; } = Color.Black;
    
    public static Camera Active { get; private set; } = null!;
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
        
        Active = Create();
    }

    public static Camera Create()
    {
        var camera = new Camera(Window.Size);
        
        return camera;
    }

    public static void ToggleTo(Camera camera)
    {
        Active = camera;
    }

    private static void OnFramebufferResize(Vector2D<int> size)
    {
        Active.AspectRatio = (float)size.X / size.Y;
    }
        
    private Camera(Vector2D<int> size)
    {
        AspectRatio = (float)size.X / size.Y;
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
}