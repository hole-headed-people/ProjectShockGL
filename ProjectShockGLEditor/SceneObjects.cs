using ProjectShockGL.Core;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

public static class SceneObjects
{
    public static Object Create(PlaceableType type, string path = "")
    {
        var stem = type == PlaceableType.BrushBox ? "BrushBox" : path;
        return CreateRecorded(() => EditorLevelManager.CreatePlacement(new Placeable
        {
            Type = type, Path = path, Name = UniqueName(stem)
        }));
    }

    public static Object Duplicate(Object source)
    {
        return CreateRecorded(() => EditorLevelManager.DuplicateObject(source));
    }

    private static Object CreateRecorded(Func<Object> factory)
    {
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();
        
        var previous = Selection.Object;
        var world = ProjectShockGL.Core.Game.Instance.World;
        var existing = world.GetAllObjects(true).ToHashSet();
        
        Object obj;
        SceneObjectAction action;
        
        try
        {
            obj = factory();
            action = new SceneObjectAction(obj, previous, creation: true);
        }
        catch (Exception error)
        {
            EditorLevelManager.RollbackCreated(existing, error);
            throw;
        }
        
        UndoRedo.Register(action);
        Selection.Object = obj;
        
        EditorLevelManager.RefreshDirty();
        
        return obj;
    }

    public static void Delete(Object obj)
    {
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();
        
        var action = new SceneObjectAction(obj, Selection.Object, creation: false);
        try
        {
            action.Remove(false);
        }
        finally
        {
            if (obj.IsDestroyed)
            {
                UndoRedo.Register(action);
            }
        }
        
        EditorLevelManager.RefreshDirty();
    }

    private static string UniqueName(string stem)
    {
        var names = ProjectShockGL.Core.Game.Instance.World.GetAllObjects(true).Select(obj => obj.Name).ToHashSet(StringComparer.Ordinal);
        var suffix = 1;

        while (names.Contains($"{stem}_{suffix}"))
        {
            suffix++;
        }
        
        return $"{stem}_{suffix}";
    }
}
