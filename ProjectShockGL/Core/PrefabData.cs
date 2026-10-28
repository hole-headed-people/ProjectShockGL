using System.Numerics;

namespace ProjectShockGL.Core;

public sealed class PrefabData
{
    public Vector3 Pivot { get; init; }
    public Placeable[] Placeables { get; init; } = [];
}
