using ProjectShockGL.Core;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

public static class SceneObjects
{
    public static Object CreatePrefab(string name)
    {
        return CreatePrefabSet(name).Last();
    }

    public static Object[] CreatePrefabSet(string name, System.Numerics.Vector3? position = null,
        System.Numerics.Quaternion? rotation = null)
    {
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();
        
        var previous = Selection.Objects;
        var world = ProjectShockGL.Core.Game.Instance.World;
        var existing = world.GetAllObjects(true).ToHashSet();
        
        try
        {
            var objects = Prefab.InstantiateAll(name, world, position, rotation);
            var action = new PrefabSetAction(objects, previous);
            
            UndoRedo.Register(action);
            Selection.Set(objects);
            EditorLevelManager.RefreshDirty();
            
            return objects;
        }
        catch (Exception error)
        {
            EditorLevelManager.RollbackCreated(existing, error);
            Selection.Set(previous);
            
            throw;
        }
    }

    public static Object Create(PlaceableType type, string path = "")
    {
        var stem = type == PlaceableType.BrushBox ? "BrushBox" : Path.GetFileName(path.Replace('\\', '/'));
        
        return CreateRecorded(() => EditorLevelManager.CreatePlacement(new Placeable
        {
            Type = type, Path = path, Name = UniqueName(stem)
        }));
    }

    public static Object Duplicate(Object source)
    {
        return CreateRecorded(() => EditorLevelManager.DuplicateObject(source));
    }

    public static Object[] Duplicate(IEnumerable<Object> sources)
    {
        var selected = sources.Distinct().ToArray();

        if (selected.Length == 0)
        {
            return [];
        }

        if (selected.Length == 1)
        {
            return [Duplicate(selected[0])];
        }
        
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();
        
        var previous = Selection.Objects;
        var existing = ProjectShockGL.Core.Game.Instance.World.GetAllObjects(true).ToHashSet();
        
        try
        {
            var copies = selected.Select(EditorLevelManager.DuplicateObject).ToArray();
            var action = new SceneSetAction(copies, previous, true);
            
            UndoRedo.Register(action);
            Selection.Set(copies);
            EditorLevelManager.RefreshDirty();
            
            return copies;
        }
        catch (Exception error)
        {
            EditorLevelManager.RollbackCreated(existing, error);
            Selection.Set(previous);
            
            throw;
        }
    }

    public static void Delete(IEnumerable<Object> objects)
    {
        var selected = objects.Distinct().ToArray();

        if (selected.Length == 0)
        {
            return;
        }

        if (selected.Length == 1)
        {
            Delete(selected[0]); 
            return;
        }
        
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();
        
        var action = new SceneSetAction(selected, Selection.Objects, false);

        try
        {
            action.Redo();
        }
        finally
        {
            if (selected.Any(o => o.IsDestroyed))
            {
                UndoRedo.Register(action);
            }
            
            EditorLevelManager.RefreshDirty();
        }
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
