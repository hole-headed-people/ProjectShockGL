using Object = ProjectShockGL.Core.Object;

namespace ProjectOxygenGL.Core;

public interface IKillable
{
    void Kill(Object? caller, Object? instigator = null);
}
