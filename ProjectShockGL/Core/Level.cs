namespace ProjectShockGL.Core;

public class Level
{
    public float KillY { get; init; } = -1000f;
    public bool SaveLevelState { get; init; } = true;
    public EnvironmentData? Environment { get; init; } = new();
    public Placeable[] Placeables { get; init; } = [];
    public EditorGroupData[] Groups { get; init; } = [];

    public void Validate()
    {
        (Environment ?? new EnvironmentData()).Validate();

        if (!float.IsFinite(KillY))
        {
            throw new InvalidDataException("KillY must be finite.");
        }

        var guids = new HashSet<Guid>();

        foreach (var placeable in Placeables ?? [])
        {
            if (placeable?.Guid is { } guid && guid != Guid.Empty && !guids.Add(guid))
            {
                throw new InvalidDataException($"Map has more than one object with GUID '{guid}'.");
            }
        }
    }
}

public class EditorGroupData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Group";
    public bool Visible { get; set; } = true;
}
