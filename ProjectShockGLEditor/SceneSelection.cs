using ProjectShockGL;
using ProjectShockGL.Core;

namespace ProjectShockGLEditor;

internal static class SceneSelection
{
    internal static int Duplicate()
    {
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();
        var previous = Selection.Targets;
        if (previous.Length == 0) return 0;
        var world = ProjectShockGL.Core.Game.Instance.World;
        var existing = world.GetAllObjects(true).ToHashSet();
        var copies = new List<EditorSelectionTarget>();
        try
        {
            foreach (var target in previous)
            {
                if (target.Detail is { } source)
                {
                    var copy = world.Details.Add(new DetailPlacement
                    {
                        AssetPath = source.AssetPath, Position = source.Position, Rotation = source.Rotation,
                        Scale = source.Scale, EditorGroups = source.EditorGroups
                    });
                    copies.Add(EditorSelectionTarget.ForDetail(world, copy.Id));
                }
                else
                {
                    copies.Add(EditorSelectionTarget.ForObject(EditorLevelManager.DuplicateObject(target.Object!)));
                }
            }
            var action = new SelectionSetAction(copies.ToArray(), previous, true);
            Selection.SetTargets(copies);
            EditorLevelManager.RefreshDirty();
            UndoRedo.Register(action);
            return copies.Count;
        }
        catch (Exception error)
        {
            List<Exception> errors = [error];
            foreach (var copy in copies.Where(target => target.IsDetail))
            {
                try { world.Details.Remove(copy.Id); }
                catch (Exception cleanup) { errors.Add(cleanup); }
            }
            try { EditorLevelManager.RollbackCreated(existing, error); }
            catch (Exception cleanup) { errors.Add(cleanup); }
            Selection.SetTargets(previous);
            if (errors.Count > 1) throw new AggregateException("Selection duplication and cleanup failed.", errors);
            throw;
        }
    }

    internal static void Delete()
    {
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();
        var selected = Selection.Targets;
        if (selected.Length == 0) return;
        var action = new SelectionSetAction(selected, selected, false);
        try { action.Redo(); }
        finally
        {
            if (selected.Any(target => target.IsDestroyed)) UndoRedo.Register(action);
            EditorLevelManager.RefreshDirty();
        }
    }
}