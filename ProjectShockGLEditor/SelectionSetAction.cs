using ProjectShockGL;
using ProjectShockGL.Core;

namespace ProjectShockGLEditor;

internal sealed class SelectionSetAction : IUndoableAction
{
    private readonly World _world;
    private readonly EditorSelectionTarget[] _members;
    private readonly EditorSelectionTarget[] _previous;
    private readonly (SceneObjectAction Action, EditorSelectionTarget Target)[] _objects;
    private readonly (DetailPlacement Placement, int Index)[] _details;
    private readonly bool _creation;

    internal SelectionSetAction(EditorSelectionTarget[] members, EditorSelectionTarget[] previous, bool creation)
    {
        _world = ProjectShockGL.Core.Game.Instance.World;
        _members = members;
        _previous = previous;
        _creation = creation;
        if (members.Any(target => target.IsDestroyed)) throw new InvalidOperationException("Selection is no longer in the scene.");
        var objects = _world.GetAllObjects(true);
        _objects = members.Where(target => !target.IsDetail)
            .OrderBy(target => Array.IndexOf(objects, target.Object))
            .Select(target => (new SceneObjectAction(target.Object!, null, true), target)).ToArray();
        var details = _world.Details.ToArray();
        _details = members.Where(target => target.IsDetail)
            .Select(target => (target.Detail!, Array.FindIndex(details, detail => detail.Id == target.Id)))
            .OrderBy(item => item.Item2).ToArray();
    }

    public void Undo() { if (_creation) Remove(); else Restore(); }
    public void Redo() { if (_creation) Restore(); else Remove(); }

    private void Remove()
    {
        List<Exception> errors = [];
        foreach (var action in _objects.Reverse())
        {
            try { action.Action.Remove(false); }
            catch (Exception error) { errors.Add(error); }
        }
        foreach (var detail in _details)
        {
            try { _world.Details.Remove(detail.Placement.Id); }
            catch (Exception error) { errors.Add(error); }
        }
        Selection.SetTargets(_previous);
        if (errors.Count > 0) throw new AggregateException("Selection removal failed.", errors);
    }

    private void Restore()
    {
        var selection = Selection.Targets;
        var restoredObjects = new List<SceneObjectAction>();
        var restoredDetails = new List<Guid>();
        try
        {
            // Already-restored members are retained when a failed history action is retried.
            foreach (var action in _objects)
            {
                var wasDestroyed = action.Target.IsDestroyed;
                action.Action.Redo();
                if (wasDestroyed && !action.Target.IsDestroyed) restoredObjects.Add(action.Action);
            }
            foreach (var detail in _details)
            {
                if (_world.Details.Find(detail.Placement.Id) != null) continue;
                _world.Details.Add(detail.Placement);
                restoredDetails.Add(detail.Placement.Id);
                _world.Details.MoveToIndex(detail.Placement.Id, detail.Index);
            }
            Selection.SetTargets(_creation ? _members : _previous);
        }
        catch (Exception error)
        {
            List<Exception> errors = [error];
            foreach (var id in restoredDetails)
            {
                try { _world.Details.Remove(id); }
                catch (Exception cleanup) { errors.Add(cleanup); }
            }
            foreach (var action in restoredObjects.AsEnumerable().Reverse())
            {
                try { action.Remove(false); }
                catch (Exception cleanup) { errors.Add(cleanup); }
            }
            Selection.SetTargets(selection);
            if (errors.Count > 1) throw new AggregateException("Selection restoration and cleanup failed.", errors);
            throw;
        }
    }
}