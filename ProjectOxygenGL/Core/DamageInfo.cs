using System.Numerics;
using Object = ProjectShockGL.Core.Object;

namespace ProjectOxygenGL.Core;

public struct DamageInfo
{
    public Vector3 HitPosition;
    public Vector3 HitNormal;
    public Vector3 HitDirection;
    public Object? Instigator;
    public string? Type;
}