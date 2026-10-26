namespace ProjectShockGL.Core;

public class Level
{
    public EnvironmentData? Environment { get; init; } = new();
    public Placeable[] Placeables { get; init; } = [];
}
