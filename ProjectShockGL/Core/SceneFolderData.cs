namespace ProjectShockGL.Core;

public sealed class SceneFolderData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Folder";
    public bool Expanded { get; set; } = true;
    public Guid[] ObjectIds { get; set; } = [];
    public Guid[] DetailIds { get; set; } = [];
}
