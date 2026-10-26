using System.Numerics;

namespace ProjectShockGL.Core;

public class Transform
{
    public Vector3 Position { get; set; } = Vector3.Zero;
    public Quaternion Rotation { get; set; } = Quaternion.Identity;
    public Vector3 Scale { get; set; } = Vector3.One;
    
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