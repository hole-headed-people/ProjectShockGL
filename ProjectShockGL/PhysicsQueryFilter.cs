using ProjectShockGL.Core;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGL;

public readonly record struct PhysicsQueryFilter
{
    public Object? IgnoreObject { get; init; }
    public Collider? IgnoreCollider { get; init; }
    public bool IgnoreTriggers { get; init; }

    internal bool Allows(Collider collider)
    {
        return collider.Parent != IgnoreObject &&
               collider != IgnoreCollider && (!IgnoreTriggers || !collider.IsTrigger);
    }
}
