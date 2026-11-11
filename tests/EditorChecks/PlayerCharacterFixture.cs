using System.Numerics;
using ProjectOxygenGL.Core;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools;

namespace EditorChecks;

public class PlayerCharacterFixture : Character
{
    protected override bool IsSaveableByDefault => false;

    private float _speed = 2.2f;
    private float _maxCoyoteTime = 0.1f;

    private float _jumpForce = 3.5f;
    private float _jumpHoldMaxTime = 0.1f;

    private float _jumpCeilCheckHeight = 0.1f;
    private float _jumpCeilCheckRadius = 0.1f;
    
    private Vector2 _lookVector;
    
    private Vector3 _inputDirection;
    private Vector3 _velocity;
 
    private bool _isJumping;
    private bool _jumpingInput;
        
    private float _jumpHoldTime;
    
    private float _coyoteTimer;
    private bool _isCoyoteTime;
        
    private float _groundHoldTimer;
    private bool _isGrounded;
    
    private float _yaw;
    private float _pitch;

    private Camera _camera = null!;
    private CameraComponent _cameraComponent = null!;
    private bool _cameraReady;
    private CharacterController _characterController = null!;
    
    private Light? _light;

    public override void Create()
    {
        base.Create();
        
        Layer = Layers.Get("Player");
        CollidesWith = Layers.Everything;
        
        _cameraComponent = AddComponent<CameraComponent>();
        _cameraComponent.LocalPosition = Vector3.UnitY * 0.6f;

        _camera = _cameraComponent.Camera;
        _camera.ClearColor = Color.White;
        _camera.Fov = 50f * MathHelper.Deg2Rad;

        _characterController = AddComponent<CharacterController>();
        _characterController.Height = 2f;
        _characterController.Radius = 0.3f;
    }

    public override void Start()
    {
        base.Start();

        var angles = QuaternionHelper.ToEulerDegrees(Transform.Rotation);
        
        _yaw = angles.Y;
        _pitch = Math.Clamp(angles.X, -89f, 89f);
        
        Transform.Rotation = Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(_yaw), 0f, 0f);
        _cameraComponent.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(_pitch));
        _cameraReady = true;
        _cameraComponent.MakeActive();

        _light = ProjectShockGL.Core.Game.Instance.World.Spawn<Light>();
        _light.Saveable = false;
        
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
        
        UpdateGrounded();
        UpdateCoyoteTime();
        UpdateMovement(deltaSeconds);
        UpdateRotation();
        _cameraComponent.Synchronize();

        if (_light != null)
        {
            _light.Transform.Position = Transform.Position;
            _light.Transform.Rotation = _camera.Transform.Rotation;
        }


    }

    public override void OnActiveChanged(bool isActive)
    {
        base.OnActiveChanged(isActive);
        
        _light?.SetActive(isActive);

        if (isActive && _cameraReady)
        {
            _cameraComponent.MakeActive();
        }
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
    
    private void UpdateGrounded()
    {
        var rawGrounded = _characterController.IsGrounded;

        if (rawGrounded)
        {
            _groundHoldTimer = 0.06f;
        }
        else
        {
            _groundHoldTimer -= Time.DeltaSeconds;
        }
            
        _isGrounded = _groundHoldTimer > 0f;
    }
    
    private void UpdateCoyoteTime()
    {
        if (_isJumping)
        {
            return;
        }
            
        if (_isGrounded)
        {
            if (_coyoteTimer < 0f)
            {
                return;
            }
                
            _coyoteTimer = 0f;
            _isCoyoteTime = false;
        }
        else
        {
            if (_coyoteTimer < _maxCoyoteTime)
            {
                _coyoteTimer += Time.DeltaSeconds;
                _isCoyoteTime = true;
            }
            else
            {
                _isCoyoteTime = false;
            }
        }
    }
    
    private void UpdateMovement(float deltaTime)
    {
        if (_characterController.IsGrounded)
        {
            if (_isJumping && !_jumpingInput)
            {
                _isJumping = false;
            }
        }
        
        var worldDir = Transform.TransformDirection(_inputDirection);
        
        _velocity.Y -= 9.8f * deltaTime;
        
        var desiredMove = new Vector3(worldDir.X * _speed * deltaTime, _velocity.Y * deltaTime,
            worldDir.Z * _speed * deltaTime);
        
        if (_jumpingInput)
        {
            _velocity.Y = _jumpForce;   

            if (_jumpHoldTime < _jumpHoldMaxTime)
            {
                _jumpHoldTime += deltaTime;
            }
            else
            {
                StopJumping();
            }

            if (Physics.OverlapSphere(_camera.Transform.Position + Vector3.UnitY * _jumpCeilCheckHeight,
                    _jumpCeilCheckRadius, Layers.Get("Default"), out _))
            {
                _jumpHoldTime = 0f;
                _velocity.Y = 0f;
                    
                StopJumping();
            }
        }
        
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

            _cameraComponent.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(_pitch));

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
    
    public void Jump()
    {
        if (_isJumping)
        {
            return;
        }

        if (!_isGrounded)
        {
            if (!_isCoyoteTime)
            {
                return;
            }
        }
            
        _jumpingInput = true;
        _isJumping = true;

        _isCoyoteTime = false;
        _coyoteTimer = 0f;
    }

    public void StopJumping()
    {
        _jumpingInput = false;
        _jumpHoldTime = 0f;
    }
}

