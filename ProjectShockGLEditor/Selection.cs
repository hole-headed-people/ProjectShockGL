using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

public static class Selection
{
    private static Object? _object;
    
    public static Object? Object
    {
        get => _object;
        set
        {
            if (ReferenceEquals(_object, value))
            {
                return;
            }

            if (_object != null)
            {
                UndoRedo.CommitPending(_object);
            }
            
            _object = value;
        }
    }
}
