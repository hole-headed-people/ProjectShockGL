namespace ProjectShockGL.Core;

public class Level
{
    public float KillY { get; init; } = -1000f;
    public bool SaveLevelState { get; init; } = true;
    public EnvironmentData? Environment { get; init; } = new();
    public Placeable[] Placeables { get; init; } = [];
    public DetailPlacement[] Details { get; init; } = [];
    public SceneFolderData[] Folders { get; init; } = [];
    public EditorGroupData[] Groups { get; init; } = [];

    public void Validate()
    {
        (Environment ?? new EnvironmentData()).Validate();

        if (!float.IsFinite(KillY))
        {
            throw new InvalidDataException("KillY must be finite.");
        }

        if (Details == null) throw new InvalidDataException("Map has no Details array.");
        var detailIds = new HashSet<Guid>();
        foreach (var detail in Details)
        {
            if (detail == null) throw new InvalidDataException("Map contains a null Detail.");
            detail.Validate();
            if (!detailIds.Add(detail.Id))
            {
                throw new InvalidDataException($"Map has more than one Detail with ID '{detail.Id}'.");
            }
        }

        var guids = new HashSet<Guid>();

        foreach (var placeable in Placeables ?? [])
        {
            if (placeable?.Guid is { } guid && guid != Guid.Empty && !guids.Add(guid))
            {
                throw new InvalidDataException($"Map has more than one object with GUID '{guid}'.");
            }
        }
        if (Folders == null) throw new InvalidDataException("Map has no Folders array.");
        var folderIds = new HashSet<string>(StringComparer.Ordinal);
        var folderObjects = new HashSet<Guid>();
        var folderDetails = new HashSet<Guid>();
        foreach (var folder in Folders)
        {
            if (folder == null || string.IsNullOrWhiteSpace(folder.Id) || string.IsNullOrWhiteSpace(folder.Name) ||
                folder.ObjectIds == null || folder.DetailIds == null || !folderIds.Add(folder.Id))
                throw new InvalidDataException("Map contains an invalid or duplicate Scene folder.");
            foreach (var id in folder.ObjectIds)
                if (id == Guid.Empty || !guids.Contains(id) || !folderObjects.Add(id))
                    throw new InvalidDataException($"Invalid or repeated Scene folder object '{id}'.");
            foreach (var id in folder.DetailIds)
                if (id == Guid.Empty || !detailIds.Contains(id) || !folderDetails.Add(id))
                    throw new InvalidDataException($"Invalid or repeated Scene folder Detail '{id}'.");
        }

    }
}

public class EditorGroupData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Group";
    public bool Visible { get; set; } = true;
}
