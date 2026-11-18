using System.Numerics;

namespace ProjectShockGL.Core;

public class Transform
{
    private readonly Func<bool>? _canChange;
    private readonly Action? _changed;
    private Vector3 _position;
    private Quaternion _rotation = Quaternion.Identity;
    private Vector3 _scale = Vector3.One;

    public Transform() {}

    internal Transform(Func<bool> canChange, Action changed)
    {
        _canChange = canChange;
        _changed = changed;
    }

    public Vector3 Position
    {
        get => _position;
        set
        {
            if (_canChange?.Invoke() == false) return;
            _position = value;
            _changed?.Invoke();
        }
    }

    public Quaternion Rotation
    {
        get => _rotation;
        set
        {
            if (_canChange?.Invoke() == false) return;
            _rotation = value;
            _changed?.Invoke();
        }
    }

    public Vector3 Scale
    {
        get => _scale;
        set
        {
            if (_canChange?.Invoke() == false) return;
            _scale = value;
            _changed?.Invoke();
        }
    }
    
    public Vector3 Forward => Vector3.Transform(-Vector3.UnitZ, Rotation);
    public Vector3 Right => Vector3.Transform(Vector3.UnitX, Rotation);
    public Vector3 Up => Vector3.Transform(Vector3.UnitY, Rotation);
    
    public Matrix4x4 GetModelMatrix()
    {
        return Matrix4x4.CreateScale(Scale)
               * Matrix4x4.CreateFromQuaternion(Rotation)
               * Matrix4x4.CreateTranslation(Position);
    }
    
    public Vector3 TransformDirection(Vector3 localDirection)
    {
        return Vector3.Transform(localDirection, Rotation);
    }
    
    public Vector3 TransformPoint(Vector3 localPoint)
    {
        return Vector3.Transform(localPoint, GetModelMatrix());
    }
    
    public Vector3 InverseTransformPoint(Vector3 worldPoint)
    {
        if (!Matrix4x4.Invert(GetModelMatrix(), out var invMatrix))
        {
            return worldPoint;
        }

        return Vector3.Transform(worldPoint, invMatrix);
    }
}