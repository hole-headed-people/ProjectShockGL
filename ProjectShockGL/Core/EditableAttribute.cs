namespace ProjectShockGL.Core;

[AttributeUsage(AttributeTargets.Property)]
public class EditableAttribute : Attribute
{
    public string? ContentDirectory { get; set; }
    public int MaxEntries { get; set; } = int.MaxValue;
}
