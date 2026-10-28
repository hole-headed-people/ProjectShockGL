namespace ProjectShockGLEditor;

public static class UndoRedo
{
    private static readonly Stack<IUndoableAction> ToUndo = new();
    private static readonly Stack<IUndoableAction> ToRedo = new();
    
    private static PendingEdit? _pending;

    private sealed class PendingEdit(object owner, string field, string before, string after, Action<string> restore)
    {
        public object Owner = owner;
        public string Field = field;
        public string Before = before;
        public string After = after;
        public Action<string> Restore = restore;
    }

    public static bool CanUndo => ToUndo.Count > 0 || _pending is { } edit && edit.Before != edit.After;
    public static bool CanRedo => ToRedo.Count > 0 && (_pending == null || _pending.Before == _pending.After);

    public static void RecordEdit(object owner, string field, string before, string after, Action<string> restore, bool editing)
    {
        if (before != after)
        {
            if (_pending != null && (!ReferenceEquals(_pending.Owner, owner) || _pending.Field != field))
            {
                CommitPending();
            }
            
            _pending ??= new PendingEdit(owner, field, before, after, restore);
            _pending.After = after;
        }

        if (!editing && _pending is { } pending && ReferenceEquals(pending.Owner, owner) && pending.Field == field)
        {
            CommitPending();
        }
    }

    public static void CommitPending(object? owner = null)
    {
        if (_pending == null || owner != null && !ReferenceEquals(_pending.Owner, owner))
        {
            return;
        }
        
        var edit = _pending;
        _pending = null;

        if (edit.Before == edit.After)
        {
            return;
        }
        
        ToUndo.Push(new PropertyChangeAction<string>(edit.Restore, edit.Before, edit.After));
        ToRedo.Clear();
    }

    public static void Undo()
    {
        if (ObjectManipulator.IsDragging)
        {
            return;
        }
        
        CommitPending();

        if (!CanUndo)
        {
            return;
        }
        
        var action = ToUndo.Peek();
        action.Undo();
        
        ToUndo.Pop();
        ToRedo.Push(action);
    }

    public static void Redo()
    {
        if (ObjectManipulator.IsDragging)
        {
            return;
        }
        
        CommitPending();

        if (!CanRedo)
        {
            return;
        }
        
        var action = ToRedo.Peek();
        action.Redo();
        
        ToRedo.Pop();
        ToUndo.Push(action);
    }

    public static void Register(IUndoableAction action)
    {
        CommitPending();
        
        ToUndo.Push(action);
        ToRedo.Clear();
    }

    public static void Clear()
    {
        EditorObjectReference.Clear();
        _pending = null;
        
        ToUndo.Clear();
        ToRedo.Clear();
    }
}

