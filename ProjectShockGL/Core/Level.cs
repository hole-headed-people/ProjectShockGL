namespace ProjectShockGL.Core;

public class Level
{
    public EnvironmentData? Environment { get; init; } = new();
    public Placeable[] Placeables { get; init; } = [];
    public EditorGroupData[] Groups { get; init; } = [];
}

public class EditorGroupData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Group";
    public bool Visible { get; set; } = true;
}
