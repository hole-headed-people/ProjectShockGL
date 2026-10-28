using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

internal sealed class PrefabSetAction : IUndoableAction
{
    private readonly SceneObjectAction[] _actions;
    private readonly EditorObjectReference[] _members;
    private readonly EditorObjectReference[] _previous;

    public PrefabSetAction(Object[] objects, Object[] previous)
    {
        _actions = objects.Select(o => new SceneObjectAction(o, null, true)).ToArray();
        _members = objects.Select(EditorObjectReference.Get).ToArray();
        _previous = previous.Select(EditorObjectReference.Get).ToArray();
    }

    public void Undo()
    {
        foreach (var action in _actions.Reverse())
        {
            action.Undo();
        }
        
        Selection.Set(_previous.Select(r => r.Current));
    }

    public void Redo()
    {
        var restored = new List<SceneObjectAction>();
        
        try
        {
            foreach (var action in _actions)
            {
                action.Redo();
                restored.Add(action);
            }
            
            Selection.Set(_members.Select(r => r.Current));
        }
        catch
        {
            foreach (var action in restored.AsEnumerable().Reverse())
            {
                action.Undo();
            }
            
            Selection.Set(_previous.Select(r => r.Current));
            
            throw;
        }
    }
}
