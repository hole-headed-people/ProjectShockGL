using System.Numerics;

namespace ProjectShockGL.Core;

public struct RaycastHit
{
    public Object Object { get; }
    public Collider? Collider { get; }
    public float Distance { get; }
    public Vector3 Point { get; }
    public Vector3 Normal { get; }

    public RaycastHit(Object obj, float distance, Vector3 point, Vector3 normal)
    {
        Object = obj;
        Distance = distance;
        Point = point;
        Normal = normal;
    }

    public RaycastHit(Collider collider, float distance, Vector3 point, Vector3 normal)
        : this(collider.Parent, distance, point, normal)
    {
        Collider = collider;
    }
}
