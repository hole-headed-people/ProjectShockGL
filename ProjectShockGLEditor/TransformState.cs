using System.Numerics;
using ProjectShockGL.Core;

namespace ProjectShockGLEditor;

public readonly record struct TransformState(Vector3 Position, Quaternion Rotation, Vector3 Scale)
{
    public static TransformState Capture(Transform transform)
    {
        return new TransformState(transform.Position, transform.Rotation, transform.Scale);
    }

    public void Apply(Transform transform)
    {
        transform.Position = Position;
        transform.Rotation = Rotation;
        transform.Scale = Scale;
    }
}
