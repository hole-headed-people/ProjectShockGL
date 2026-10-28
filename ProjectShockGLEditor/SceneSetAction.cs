using ProjectShockGL;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

internal sealed class SceneSetAction : IUndoableAction
{
    private readonly (SceneObjectAction Action, EditorObjectReference Reference)[] _actions;
    private readonly EditorObjectReference[] _members;
    private readonly EditorObjectReference[] _previous;
    private readonly bool _creation;

    public SceneSetAction(Object[] objects, Object[] previous, bool creation)
    {
        var world = ProjectShockGL.Core.Game.Instance.World.GetAllObjects(true);
        
        _actions = objects.OrderBy(o => Array.IndexOf(world, o))
            .Select(o => (new SceneObjectAction(o, null, true), EditorObjectReference.Get(o))).ToArray();
        
        _members = objects.Select(EditorObjectReference.Get).ToArray();
        _previous = previous.Select(EditorObjectReference.Get).ToArray();
        
        _creation = creation;
    }

    public void Undo()
    {
        if (_creation)
        {
            Remove();
        }
        else
        {
            Restore();
        }
    }

    public void Redo()
    {
        if (_creation)
        {
            Restore();
        }
        else
        {
            Remove();
        }
    }

    private void Remove()
    {
        List<Exception> errors = [];
        
        foreach (var item in _actions.Reverse())
        {
            try
            {
                item.Action.Undo();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }
        
        Selection.Set(_previous.Select(r => r.Current));

        if (errors.Count > 0)
        {
            throw new AggregateException("Object set removal failed.", errors);
        }
    }

    private void Restore()
    {
        var selection = Selection.Objects.Select(EditorObjectReference.Get).ToArray();
        var restored = new List<SceneObjectAction>();
        
        try
        {
            foreach (var item in _actions)
            {
                var wasDestroyed = item.Reference.Current.IsDestroyed;
                item.Action.Redo();
                
                if (wasDestroyed)
                {
                    restored.Add(item.Action);
                }
            }
            
            Selection.Set((_creation ? _members : _previous).Select(r => r.Current));
        }
        catch (Exception error)
        {
            List<Exception> errors = [error];

            foreach (var action in restored.AsEnumerable().Reverse())
            {
                try
                {
                    action.Undo();
                }
                catch (Exception cleanup)
                {
                    errors.Add(cleanup);
                }
            }
            
            Selection.Set(selection.Select(r => r.Current));

            if (errors.Count > 1)
            {
                throw new AggregateException("Object set restoration failed.", errors);
            }
            
            throw;
        }
    }
}
