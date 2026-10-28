using System.Runtime.CompilerServices;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

public sealed class EditorObjectReference
{
    private static readonly ConditionalWeakTable<Object, EditorObjectReference> References = new();
    
    public Object Current { get; private set; }
    
    private EditorObjectReference(Object obj) => Current = obj;

    public static EditorObjectReference Get(Object obj)
    {
        return References.GetValue(obj, value => new EditorObjectReference(value));
    }

    internal void Replace(Object obj)
    {
        References.Add(obj, this);
        Current = obj;
    }

    internal static void Clear()
    {
        References.Clear();
    }
}
