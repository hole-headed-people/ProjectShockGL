using ProjectShockGL;
using ProjectShockGL.Core;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

public static class Selection
{
    private static readonly List<EditorSelectionTarget> Selected = [];
    internal static EditorSelectionTarget[] Targets => Selected.Where(target => !target.IsDestroyed).ToArray();
    public static Object[] Objects => Targets.Where(target => !target.IsDetail).Select(target => target.Object!).ToArray();
    public static DetailPlacement[] Details => Targets.Where(target => target.IsDetail).Select(target => target.Detail!).ToArray();
    public static DetailPlacement? Detail => Targets.LastOrDefault()?.Detail;
    public static bool Contains(Object obj) => !obj.IsDestroyed && Targets.Any(target => target.Object == obj);
    internal static bool Contains(EditorSelectionTarget target) => Targets.Any(item => item.Matches(target));

    public static Object? Object
    {
        get => Objects.LastOrDefault();
        set
        {
            if (value == null) SetTargets([]);
            else SetTarget(EditorSelectionTarget.ForObject(value));
        }
    }

    public static void SelectDetail(Guid id) =>
        SetTarget(EditorSelectionTarget.ForDetail(ProjectShockGL.Core.Game.Instance.World, id));

    public static void ToggleDetail(Guid id) =>
        ToggleTarget(EditorSelectionTarget.ForDetail(ProjectShockGL.Core.Game.Instance.World, id));

    internal static void SetTarget(EditorSelectionTarget target)
    {
        if (Targets.Length == 1 && Targets[0].Matches(target)) return;
        SetTargets([target]);
    }

    internal static void ToggleTarget(EditorSelectionTarget target)
    {
        if (target.IsDestroyed) return;
        Change();
        var index = Selected.FindIndex(item => item.Matches(target));
        if (index < 0) Selected.Add(target);
        else Selected.RemoveAt(index);
    }

    public static void Toggle(Object obj) => ToggleTarget(EditorSelectionTarget.ForObject(obj));
    public static void Set(IEnumerable<Object> objects) => SetTargets(objects.Distinct().Select(EditorSelectionTarget.ForObject));

    internal static void SetTargets(IEnumerable<EditorSelectionTarget> targets)
    {
        var selected = targets.Where(target => !target.IsDestroyed).ToArray();
        Change();
        Selected.Clear();
        foreach (var target in selected)
        {
            if (!Selected.Any(item => item.Matches(target))) Selected.Add(target);
        }
    }

    private static void Change()
    {
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();
        Selected.RemoveAll(target => target.IsDestroyed);
    }
}