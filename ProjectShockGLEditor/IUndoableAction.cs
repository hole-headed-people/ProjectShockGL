namespace ProjectShockGLEditor;

public interface IUndoableAction
{
    void Undo();
    void Redo();
}