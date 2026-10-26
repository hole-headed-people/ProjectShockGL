using System.Numerics;
using ProjectShockGL;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools;
using Silk.NET.Input;

namespace ProjectShockGLEditor;

public static class SceneCamera
{
    private static Vector2 _lookVector;
    private static Vector3 _velocity;
    
    private static float _yaw;
    private static float _pitch;

    private static bool _isLook;

    public static float Speed { get; set; } = 5f;
    public static float MouseSensitivity { get; set; } = 0.2f;
    
    public static void Initialize()
    {
        _yaw = 0;
        _pitch = -20;
        
        Camera.Active.Transform.Position = new Vector3(0, 4, 8);
        Camera.Active.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(0, MathHelper.ToRadians(_pitch), 0);
    }

    public static void Focus(ProjectShockGL.Core.Object obj)
    {
        var bounds = obj.GetWorldBounds();
        var center = (bounds.min + bounds.max) * 0.5f;
        var distance = MathF.Max(2, (bounds.max - bounds.min).Length() * 0.5f /
            MathF.Sin(Camera.Active.Fov * 0.5f));
        
        Camera.Active.Transform.Position = center - Camera.Active.Transform.Forward * distance;
    }
    
    public static void Update(float deltaTime, bool allowInput)
    {
        if (_isLook && (!Input.IsMouseButton(MouseButton.Right) || !allowInput))
        {
            _isLook = false;
            Input.SetCursorMode(CursorMode.Normal);
        }
        if (!allowInput) return;
        var rightAxis = 0f;
        var forwardAxis = 0f;

        var upAxis = 0f;

        var camera = Camera.Active;
        
        if (Input.IsKey(Key.A))
        {
            rightAxis = -1f;
        }

        if (Input.IsKey(Key.D))
        {
            rightAxis = 1f;
        }

        if (Input.IsKey(Key.W))
        {
            forwardAxis = -1f;
        }

        if (Input.IsKey(Key.S))
        {
            forwardAxis = 1f;
        }

        if (Input.IsKey(Key.E))
        {
            upAxis = 1f;
        }

        if (Input.IsKey(Key.Q))
        {
            upAxis = -1f;
        }

        if (Input.IsMouseButtonDown(MouseButton.Right))
        {
            _isLook = true;
            
            Input.SetCursorMode(CursorMode.Raw);
        }

        if (Input.IsMouseButtonUp(MouseButton.Right))
        {
            _isLook = false;
            
            Input.SetCursorMode(CursorMode.Normal);
        }
        
        if (Input.IsMouseButton(MouseButton.Middle))
        {
            var mouseDelta = Input.MouseDeltaPosition;
            var pan = camera.Transform.TransformDirection(
                new Vector3(-mouseDelta.X, mouseDelta.Y, 0)) * MouseSensitivity * 0.01f;
            
            camera.Transform.Position += pan * Speed;
        }
        
        if (Input.IsKeyDown(Key.F) && Selection.Object != null)
        {
            Focus(Selection.Object);
        }
        
        var inputDirection = _isLook ? new Vector3(rightAxis, 0f, forwardAxis) : Vector3.Zero;

        _velocity = camera.Transform.TransformDirection(inputDirection) + new Vector3(0f, _isLook ? upAxis : 0f, 0f);
        
        camera.Transform.Position += _velocity * Speed * deltaTime;
        
        _lookVector = Input.MouseDeltaPosition;
        _lookVector.X *= -1f;

        if (!_isLook || _lookVector == Vector2.Zero)
        {
            return;
        }
        
        _yaw += _lookVector.X * MouseSensitivity;
            
        _pitch -= _lookVector.Y * MouseSensitivity;
        _pitch = Math.Clamp(_pitch, -89f, 89f);

        camera.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(_yaw),
            MathHelper.ToRadians(_pitch), 0f);
    }
}
