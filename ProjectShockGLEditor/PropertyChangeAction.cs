namespace ProjectShockGLEditor;

public class PropertyChangeAction<T> : IUndoableAction
{
    private readonly Action<T> _setter;
    
    private readonly T _oldValue;
    private readonly T _newValue;

    public PropertyChangeAction(Action<T> setter, T oldValue, T newValue)
    {
        _setter = setter;
        
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public void Undo() => _setter(_oldValue);
    public void Redo() => _setter(_newValue);
}