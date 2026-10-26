using System.Numerics;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools;

namespace ProjectDungeon.Game;

public class PlayerCharacter : Entity
{
    private const float MouseSensitivity = 0.05f;

    private Vector2 _lookVector;
    
    private Vector3 _inputDirection;
    private Vector3 _velocity;
    
    private float _yaw;
    private float _pitch;

    private Camera _camera = null!;
    private CharacterController _characterController = null!;
    
    private Light _light = null!;

    public override void Create()
    {
        base.Create();
        
        _camera = Camera.Active;
        _camera.ClearColor = Color.Black;
        _camera.Fov = 50f * MathHelper.Deg2Rad;

        _light = ProjectShockGL.Core.Game.Instance.World.Spawn<Light>();
        
        _light.Intensity = 1f;
        _light.Radius = 2f;
        _light.Type = LightType.Point;
        _light.Color = Color.White;
        
        Layer = Layers.Get("Player");
        CollidesWith = Layers.Everything;
    }

public override void Start()
    {
        base.Start();

        _characterController = AddComponent<CharacterController>();
        
        _characterController.Height = 2f;
        _characterController.Radius = 0.3f;
        
        _light.Transform.Position = Transform.Position;
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);
        
        UpdateMovement(deltaTime);
        UpdateRotation();
        
        _light.Transform.Position = Transform.Position;
        _camera.Transform.Position = Transform.Position;
    }
    
    private void UpdateMovement(float deltaTime)
    {
        var worldDir = Transform.TransformDirection(_inputDirection);
        
        _velocity.Y -= 9.8f * deltaTime;
        
        var desiredMove = new Vector3(worldDir.X * 3f * deltaTime, _velocity.Y * deltaTime,
            worldDir.Z * 3f * deltaTime);
        
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
            _yaw += _lookVector.X * MouseSensitivity;
            _pitch -= _lookVector.Y * MouseSensitivity;
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
}