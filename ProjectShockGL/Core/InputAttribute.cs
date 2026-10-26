namespace ProjectShockGL.Core;

[AttributeUsage(AttributeTargets.Method)]
public class InputAttribute : Attribute
{
    public string Name { get; }
    
    public InputAttribute(string name) => Name = name;
}