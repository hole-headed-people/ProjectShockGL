using System.Numerics;
using ProjectDungeon.Core;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools;

namespace ProjectDungeon.Game;

public class ShockPlayerCharacter : Character
{
    private float _speed = 1.2f;
    
    private Vector2 _lookVector;
    
    private Vector3 _inputDirection;
    private Vector3 _velocity;
    
    private float _yaw;
    private float _pitch;

    private Camera _camera = null!;
    private CharacterController _characterController = null!;
    
    private Light? _light = null;

    public override void Create()
    {
        base.Create();
        
        Layer = Layers.Get("Player");
        CollidesWith = Layers.Everything;
    }

    public override void Start()
    {
        base.Start();

        _camera = Camera.Active;
        _camera.ClearColor = Color.White;
        _camera.Fov = 50f * MathHelper.Deg2Rad;

        _characterController = AddComponent<CharacterController>();
        
        _characterController.Height = 2f;
        _characterController.Radius = 0.3f;

        var angles = QuaternionHelper.ToEulerDegrees(Transform.Rotation);
        
        _yaw = angles.Y;
        _pitch = Math.Clamp(angles.X, -89f, 89f);
        
        _camera.Transform.Position = Transform.Position;
        _camera.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(
            MathHelper.ToRadians(_yaw), MathHelper.ToRadians(_pitch), 0f);

        _light = ProjectShockGL.Core.Game.Instance.World.Spawn<Light>();
        
        _light.Intensity = 0f;
        _light.Radius = 10f;
        _light.OuterAngle = 90f;
        _light.Type = LightType.Spot;
        _light.Color = Color.White;
        
        _light.Transform.Position = Transform.Position;
        _light.Transform.Rotation = _camera.Transform.Rotation;
    }

    public override void Update(float deltaSeconds)
    {
        base.Update(deltaSeconds);
        
        UpdateMovement(deltaSeconds);
        UpdateRotation();

        if (_light != null)
        {
            _light.Transform.Position = Transform.Position;
            _light.Transform.Rotation = _camera.Transform.Rotation;
        }

        _camera.Transform.Position = Transform.Position;
    }

    public override void OnActiveChanged(bool isActive)
    {
        base.OnActiveChanged(isActive);
        
        _light?.SetActive(isActive);
    }

    protected override void OnControlling(Controller controller)
    {
        base.OnControlling(controller);
        
        Console.WriteLine(controller is Player);
    }

    public override void Destroy()
    {
        try
        {
            base.Destroy();
        }
        finally
        {
            _light?.Destroy();
        }
    }
    
    private void UpdateMovement(float deltaTime)
    {
        var worldDir = Transform.TransformDirection(_inputDirection);
        
        _velocity.Y -= 9.8f * deltaTime;
        
        var desiredMove = new Vector3(worldDir.X * _speed * deltaTime, _velocity.Y * deltaTime,
            worldDir.Z * _speed * deltaTime);
        
        var collisions = _characterController.Move(desiredMove);
        
        if ((_velocity.Y < 0f && (collisions & CollisionFlags.Below) != 0f) ||
            (_velocity.Y > 0f && (collisions & CollisionFlags.Above) != 0f))
        {
            _velocity.Y = 0f;
        }
    }

    private void UpdateRotation()
    {
        if (_lookVector != Vector2.Zero)
        {
            _yaw += _lookVector.X;
            _pitch -= _lookVector.Y;
            
            _pitch = Math.Clamp(_pitch, -89f, 89f);

            _camera.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(
                MathHelper.ToRadians(_yaw),
                MathHelper.ToRadians(_pitch),
                0f
            );

            Transform.Rotation = Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(_yaw), 0f, 0f);
        }
    }
    
    public void Move(Vector3 direction)
    {
        _inputDirection = direction;
    }

    public void Turn(float angle)
    {
        _lookVector.X = angle;
    }

    public void LookAt(float angle)
    {
        _lookVector.Y = angle;
    }

    public void Interact()
    {
        if (!Physics.Raycast(_camera.Transform.Position, _camera.Transform.Forward, 1f, Layers.Get("Default"), out var hit))
        {
             return;   
        }

        if (hit.Object is Interactive interactive && interactive.IsInteractable)
        {
            interactive.Interact(this);
        }
    }
}
