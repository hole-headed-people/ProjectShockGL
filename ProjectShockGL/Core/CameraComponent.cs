using System.Numerics;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools;

namespace ProjectShockGL.Core;

public class CameraComponent : Component
{
    private Vector3 _localPosition;
    private Quaternion _localRotation = Quaternion.Identity;
    
    private Camera? _camera;

    public Camera Camera => _camera ?? throw new InvalidOperationException("Camera component has not been created.");
    internal bool CanActivate => _camera is { IsDisposed: false } && !IsDestroyed && IsEnabled && !Owner.IsDestroyed && Owner.IsActive;

    public Vector3 LocalPosition
    {
        get => _localPosition;
        set
        {
            if (!PhysicsHelper.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            
            _localPosition = value;
        }
    }

    public Quaternion LocalRotation
    {
        get => _localRotation;
        set
        {
            var length = value.LengthSquared();
            
            if (!float.IsFinite(length) || length < 1e-8f)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            
            _localRotation = Quaternion.Normalize(value);
        }
    }

    public override bool IsEnabled
    {
        get => base.IsEnabled;
        set
        {
            base.IsEnabled = value;
            
            if (!value && _camera != null)
            {
                Camera.ReleaseActive(_camera);
            }
        }
    }

    public override void Create()
    {
        base.Create();
        
        _camera = Camera.Create();
        _camera.Component = this;
        
        Camera.Register(this);
        
        Synchronize();
    }

    public bool MakeActive()
    {
        if (!CanActivate || Application.IsEditor)
        {
            return false;
        }
        
        Synchronize();
       
        Camera.ToggleTo(Camera);
        
        return true;
    }

    public void Synchronize()
    {
        if (_camera == null || _camera.IsDisposed || IsDestroyed || Owner.IsDestroyed)
        {
            return;
        }
        
        _camera.Transform.Position = Owner.Transform.TransformPoint(LocalPosition);
        _camera.Transform.Rotation = Quaternion.Normalize(Owner.Transform.Rotation * LocalRotation);
    }

    internal override void OnOwnerActiveChanged(bool isActive)
    {
        if (!isActive && _camera != null)
        {
            Camera.ReleaseActive(_camera);
        }
    }

    public override void Destroy()
    {
        ReleaseCamera();
        
        base.Destroy();
    }

    internal void ReleaseCamera()
    {
        Camera.Unregister(this);
        
        _camera?.Dispose();
    }
}
