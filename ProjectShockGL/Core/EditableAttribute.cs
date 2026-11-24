namespace ProjectShockGL.Core;

[AttributeUsage(AttributeTargets.Property)]
public class EditableAttribute : Attribute
{
    public string? ContentDirectory { get; set; }
    public string ContentPattern { get; set; } = "*.json";
    public int MaxEntries { get; set; } = int.MaxValue;
}
