namespace ProjectShockGL.Core;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class OutputEventAttribute : Attribute
{
    public string Name { get; }

    public OutputEventAttribute(string name) => Name = name;
}
