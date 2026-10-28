using System.Numerics;
using ProjectShockGL.Core;
using ProjectShockGL.Tools;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGL.Placeables;

public class LinearMovement : Entity
{
    [Editable] public float Speed { get; set; }
    [Editable] public float SnapDistance { get; set; }
    
    [Editable] public bool IsLoop { get; set; }
    [Editable] public bool StartOnAwake { get; set; }
    
    [Editable] public bool IsRelative { get; set; }
    
    [Editable] public Vector3 Position { get; set; }

    [Editable] public string TargetName { get; set; } = "";

    private Object? _target;
    
    private bool _isReversed;
        
    private bool _isPaused;
    private bool _isMoving;

    private Vector3 _currentPosition;
    private Vector3 _targetPosition;
    private Vector3 _startPosition;

    public override void Start()
    {
        base.Start();
        
        _target = Game.Instance.World.Find(TargetName);

        if (_target == null)
        {
            Console.WriteLine("Target not found");
            
            return;
        }
        
        ResetPosition();

        if (StartOnAwake)
        {
            Move();
        }
    }

    public override void Update(float deltaSeconds)
    {
        base.Update(deltaSeconds);
        
        if (_target == null || _target.IsDestroyed || _target.IsStatic || !_isMoving || _isPaused || deltaSeconds <= 0f)
        {
            return;
        }

        _currentPosition = MathHelper.MoveTowards(_currentPosition, _targetPosition, Math.Max(0f, Speed) * deltaSeconds);
            
        if (_currentPosition == _targetPosition || Vector3.Distance(_currentPosition, _targetPosition) <= Math.Max(0f, SnapDistance))
        {
            _currentPosition = _targetPosition;
            _target.Transform.Position = _currentPosition;
            _isReversed = !_isReversed;
            _isMoving = false;
                
            if (!_isReversed)
            {
                InvokeEvent("OnReversed");
            }
            else
            {
                InvokeEvent("OnMoved");
            }
                
            if (IsLoop && !_isMoving && !_isPaused && !IsDestroyed && !_target.IsDestroyed)
            {
                Move(_isReversed);
            }
        }

        else _target.Transform.Position = _currentPosition;
    }

    [Input("Move")]
    public void Move(bool isReversed = false)
    {
        if (_target == null)
        {
            return;
        }
        if (_target.IsStatic)
        {
            Console.WriteLine($"Movement target '{_target.Name}' is static. Enable Movable on BrushBox.");
            return;
        }

        if (_isPaused)
        {
            SetPaused(false);
        }
            
        _isMoving = true;
        _currentPosition = _target.Transform.Position;
        _isReversed = isReversed;
            
        _targetPosition = _isReversed ? _startPosition : IsRelative ? _startPosition + Position : Position;
    }

    [Input("Stop")]
    public void Stop()
    {
        if (!_isMoving)
        {
            return;
        }
            
        _isMoving = false;
        _isPaused = true;
    }
    
    [Input("ResetPosition")]
    public void ResetPosition()
    {
        if (_target == null) 
        {
            return;
        }
        
        _startPosition = _target.Transform.Position;
        _currentPosition = _startPosition;
    }
    
    public void SetPaused(bool isPaused)
    {
        _isPaused = isPaused;
    }
}
