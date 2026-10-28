using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

public static class Selection
{
    private static readonly List<Object> Selected = [];
    public static Object[] Objects => Selected.Where(o => !o.IsDestroyed).ToArray();
    public static bool Contains(Object obj) => !obj.IsDestroyed && Selected.Contains(obj);
    public static Object? Object
    {
        get => Objects.LastOrDefault();
        set
        {
            if (Objects.Length == 1 && ReferenceEquals(Object, value)) return;
            Change();
            Selected.Clear();
            if (value is { IsDestroyed: false }) Selected.Add(value);
        }
    }
    public static void Toggle(Object obj)
    {
        if (obj.IsDestroyed) return;
        Change();
        if (!Selected.Remove(obj)) Selected.Add(obj);
    }

    public static void Set(IEnumerable<Object> objects)
    {
        var selected = objects.Where(o => !o.IsDestroyed).Distinct().ToArray();
        Change();
        Selected.Clear();
        Selected.AddRange(selected);
    }
    private static void Change()
    {
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();
        Selected.RemoveAll(o => o.IsDestroyed);
    }
}
