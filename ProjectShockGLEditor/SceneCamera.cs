using System.Numerics;
using ProjectShockGL;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools;
using Silk.NET.Input;
using Plane = ProjectShockGL.Rendering.Plane;

namespace ProjectShockGLEditor;

public enum SceneViewMode
{
    Perspective,
    Top,
    Front,
    Side
}

public static class SceneCamera
{
    private static Vector2 _lookVector;
    private static Vector3 _velocity;
    
    private static float _yaw;
    private static float _pitch;

    private static bool _isLook;

    private static Vector3 _perspectivePosition;
    private static Quaternion _perspectiveRotation;
    private static Vector3 _orthoCenter;
    public static SceneViewMode ViewMode { get; private set; }
    public static Plane GridPlane => ViewMode switch
    {
        SceneViewMode.Front => Plane.Xy,
        SceneViewMode.Side => Plane.Yz,
        _ => Plane.Xz
    };

    public static void SetView(SceneViewMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        if (mode == ViewMode)
        {
            return;
        }
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();
        var camera = Camera.Active;
        if (_isLook)
        {
            _isLook = false;
            Input.SetCursorMode(CursorMode.Normal);
        }
        if (ViewMode == SceneViewMode.Perspective)
        {
            _perspectivePosition = camera.Transform.Position;
            _perspectiveRotation = camera.Transform.Rotation;
            _orthoCenter = camera.Transform.Position + camera.Transform.Forward * 10;
        }
        ViewMode = mode;
        if (mode == SceneViewMode.Perspective)
        {
            camera.Orthographic = false;
            camera.Transform.Position = _perspectivePosition;
            camera.Transform.Rotation = _perspectiveRotation;
            return;
        }
        camera.Orthographic = true;
        camera.Transform.Rotation = mode switch
        {
            SceneViewMode.Top => Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2),
            SceneViewMode.Side => Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2),
            _ => Quaternion.Identity
        };
        var selected = Selection.Targets.Where(target => target.IsEditorVisible).ToArray();
        if (selected.Length > 0)
        {
            var bounds = selected.Select(target => target.GetWorldBounds()).ToArray();
            Focus((bounds.Select(value => value.min).Aggregate(Vector3.Min),
                bounds.Select(value => value.max).Aggregate(Vector3.Max)));
        }
        else
        {
            PositionOrthographic(camera);
        }
    }

    private static void PositionOrthographic(Camera camera)
    {
        camera.Transform.Position = _orthoCenter - camera.Transform.Forward * ((camera.Near + camera.Far) * 0.5f);
    }

    internal static void Pan(Vector2 delta, Vector2 screenSize)
    {
        if (ViewMode == SceneViewMode.Perspective || screenSize.Y <= 0) return;
        var camera = Camera.Active;
        var movement = (-camera.Transform.Right * delta.X + camera.Transform.Up * delta.Y) * camera.OrthoSize / screenSize.Y;
        _orthoCenter += movement;
        camera.Transform.Position += movement;
    }

    internal static void Zoom(float wheel)
    {
        if (ViewMode == SceneViewMode.Perspective || !float.IsFinite(wheel) || wheel == 0) return;
        var camera = Camera.Active;
        camera.OrthoSize = Math.Clamp(camera.OrthoSize * MathF.Exp(-wheel * 0.15f), 0.01f, 10000f);
    }

    public static float Speed { get; set; } = 5f;
    public static float MouseSensitivity { get; set; } = 0.2f;
    
    public static void Initialize()
    {
        ViewMode = SceneViewMode.Perspective;
        Camera.Active.Orthographic = false;
        _yaw = 0;
        _pitch = -20;
        
        Camera.Active.Transform.Position = new Vector3(0, 4, 8);
        Camera.Active.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(0, MathHelper.ToRadians(_pitch), 0);
    }

    public static void Focus(ProjectShockGL.Core.Object obj)
    {
        Focus(obj.GetWorldBounds());
    }

    internal static void Focus((Vector3 min, Vector3 max) bounds)
    {
        var center = (bounds.min + bounds.max) * 0.5f;
        if (ViewMode != SceneViewMode.Perspective)
        {
            var camera = Camera.Active;
            var size = bounds.max - bounds.min;
            var width = Vector3.Dot(Vector3.Abs(camera.Transform.Right), size);
            var height = Vector3.Dot(Vector3.Abs(camera.Transform.Up), size);
            camera.OrthoSize = MathF.Max(0.01f, MathF.Max(height, width / camera.AspectRatio) * 1.2f);
            _orthoCenter = center;
            PositionOrthographic(camera);
            return;
        }
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
        if (ViewMode != SceneViewMode.Perspective)
        {
            if (Input.IsMouseButton(MouseButton.Middle))
                Pan(Input.MouseDeltaPosition, ImGuiNET.ImGui.GetIO().DisplaySize);
            if (Input.IsKeyDown(Key.F) && Selection.Targets.LastOrDefault() is { IsEditorVisible: true } selected)
                Focus(selected.GetWorldBounds());
            return;
        }
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
        
        if (Input.IsKeyDown(Key.F) && Selection.Targets.LastOrDefault() is { IsEditorVisible: true })
        {
            Focus(Selection.Targets.Last().GetWorldBounds());
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
