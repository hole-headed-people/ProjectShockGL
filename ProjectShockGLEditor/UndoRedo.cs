namespace ProjectShockGLEditor;

public static class UndoRedo
{
    private static readonly Stack<IUndoableAction> ToUndo = new();
    private static readonly Stack<IUndoableAction> ToRedo = new();

    public static bool CanUndo => ToUndo.Count > 0;
    public static bool CanRedo => ToRedo.Count > 0;

    public static void Undo()
    {
        if (!CanUndo || ObjectManipulator.IsDragging)
        {
            return;
        }
        
        var action = ToUndo.Pop();
        action.Undo();
        
        ToRedo.Push(action);
    }

    public static void Redo()
    {
        if (!CanRedo || ObjectManipulator.IsDragging)
        {
            return;
        }
        
        var action = ToRedo.Pop();
        action.Redo();
        
        ToUndo.Push(action);
    }

    public static void Register(IUndoableAction action)
    {
        ToUndo.Push(action);
        ToRedo.Clear();
    }

    public static void Clear()
    {
        ToUndo.Clear();
        ToRedo.Clear();
    }
}

