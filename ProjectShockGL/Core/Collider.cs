using System.Numerics;

namespace ProjectShockGL.Core;

public abstract class Collider : Component
{
    private Vector3 _center;
    
    public Vector3 Center
    {
        get => _center;
        set
        {
            if (!Tools.PhysicsHelper.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
            _center = value;
        }
    }
    
    public bool IsTrigger { get; set; }

    protected static float NonNegative(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name);
        return value;
    }

    public override void Create()
    {
        base.Create();
        
        Physics.Register(this);
    }

    public override void Destroy()
    {
        Physics.Unregister(this);
        
        base.Destroy();
    }
    
    public abstract (Vector3 min, Vector3 max) GetLocalBounds();
    public abstract (Vector3 min, Vector3 max) CalculateWorldBounds(); 
    public abstract bool Intersects(Collider other);
    
    public (Vector3 min, Vector3 max) GetWorldBounds()
    {
        return CalculateWorldBounds();
    }

    public void NotifyEnter(Object other, bool trigger)
    {
        if (trigger)
        {
            Parent.OnTriggerEnter(other);
        }
        else
        {
            Parent.OnCollisionEnter(other);
        }
    }

    public void NotifyExit(Object other, bool trigger)
    {
        if (trigger)
        {
            Parent.OnTriggerExit(other);
        }
        else
        {
            Parent.OnCollisionExit(other);
        }
    }

    public Vector3 GetWorldCenter()
    {
        return Parent.Transform.TransformPoint(Center);
    }
}
