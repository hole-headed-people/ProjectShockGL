using System.Reflection;
using ProjectShockGL.Core;

namespace ProjectShockGL;

public static class EditableProperties
{
    public static IEnumerable<PropertyInfo> Get(Type type)
    {
        return type.GetProperties()
            .Where(p => p.GetCustomAttribute<EditableAttribute>() != null
                        && p.CanRead && p.CanWrite);
    }
}