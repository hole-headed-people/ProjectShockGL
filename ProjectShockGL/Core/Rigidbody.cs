using System.Numerics;
using ProjectShockGL.Tools;

namespace ProjectShockGL.Core;

public sealed class Rigidbody : Component
{
    private float _mass = 1f;
    
    private Vector3 _linearVelocity;
    private Vector3 _angularVelocity;

    public float Mass
    {
        get => _mass;
        set
        {
            if (value <= 0f || !float.IsFinite(value) || !float.IsFinite(1f / value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Mass must be finite and positive with a finite inverse.");
            }

            _mass = value;
        }
    }

    public Vector3 LinearVelocity
    {
        get => _linearVelocity;
        set
        {
            if (!PhysicsHelper.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Velocity must be finite.");
            }

            _linearVelocity = value;
        }
    }
    
    public Vector3 AngularVelocity
    {
        get => _angularVelocity;
        set
        {
            if (!PhysicsHelper.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Angular velocity must be finite.");
            }

            _angularVelocity = value;
        }
    }

    public void AddImpulse(Vector3 impulse)
    {
        LinearVelocity += impulse / Mass;
    }

    /// <summary>Applies an impulse at a world-space point.</summary>
    public void AddImpulse(Vector3 impulse, Vector3 worldPoint)
    {
        if (!PhysicsHelper.IsFinite(impulse))
        {
            throw new ArgumentOutOfRangeException(nameof(impulse));
        }

        if (!PhysicsHelper.IsFinite(worldPoint))
        {
            throw new ArgumentOutOfRangeException(nameof(worldPoint));
        }

        var velocity = BepuSimulation.CalculatePointImpulse(this, impulse, worldPoint);
        if (!PhysicsHelper.IsFinite(velocity.Linear) || !PhysicsHelper.IsFinite(velocity.Angular))
        {
            throw new ArgumentOutOfRangeException(nameof(impulse), "The impulse must produce finite velocities.");
        }

        _linearVelocity = velocity.Linear;
        _angularVelocity = velocity.Angular;
    }
}
