using System.Numerics;
using ProjectShockGL.Rendering;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGL;

public class World
{
    private readonly List<Object> _objects;
    private readonly Queue<Object> _spawnedObjects;
    
    private Object[]? _cachedObjectsArray;
    
    private int _cachedObjectsVersion = -1;
    private int _objectsVersion;
    private int _clearVersion;
    
    public static HashSet<string> HiddenEditorGroups { get; } = new(StringComparer.Ordinal);

    public World()
    {
        _objects = [];
        _spawnedObjects = [];
    }

    public void Update(float deltaSeconds, bool simulate = true, float? unscaledDeltaSeconds = null)
    {
        var routines = simulate ? GetAllObjects(true).SelectMany(obj => obj.CaptureCoroutines()).ToArray() : [];
        
        while (_spawnedObjects.Count > 0)
        {
            var obj = _spawnedObjects.Dequeue();

            if (obj.IsDestroyed)
            {
                continue;
            }
            
            _objects.Add(obj);
            _objectsVersion++;
            
            if (simulate)
            {
                obj.InitializeActivity();
                obj.TryStart();
            }
        }
        
        var objectsToUpdate = GetAllObjects();
        var clearVersion = _clearVersion;
        
        foreach (var obj in objectsToUpdate)
        {
            if (!simulate)
            {
                continue;
            }
            
            if (_clearVersion != clearVersion)
            {
                break;
            }

            obj.InitializeActivity();
            obj.TryStart();

            if (_clearVersion != clearVersion)
            {
                break;
            }

            if (obj.IsStatic && !Application.IsEditor)
            {
                continue;
            }
            
            if (!obj.IsDestroyed && obj.IsActive)
            {
                obj.Update(deltaSeconds);
            }
        }

        foreach (var routine in routines)
        {
            routine.Tick(deltaSeconds, unscaledDeltaSeconds ?? deltaSeconds);
        }

        if (_objects.RemoveAll(obj => obj.IsDestroyed) > 0)
        {
            _objectsVersion++;
        }
    }

    public void Render(RenderQueue queue, bool? debugOnly = null)
    {
        foreach (var obj in _objects)
        {
            if (!obj.IsDestroyed && obj.IsActive)
            {
                obj.Render(queue, debugOnly);
            }
        }
    }

    public T? Spawn<T>(Type? type) where T : Object
    {
        if (type == null)
        {
            return null;
        }

        if (Activator.CreateInstance(type) is not T obj)
        {
            return null;
        }
        
        _spawnedObjects.Enqueue(obj);
        
        CreateObject(obj);
        
        return obj;
    }
    
    public T Spawn<T>() where T : Object, new()
    {
        var obj = new T();
        _spawnedObjects.Enqueue(obj);
        
        CreateObject(obj);
        
        return obj;
    }

    public T Spawn<T>(Vector3 position, Quaternion rotation) where T : Object, new()
    {
        var obj = Spawn<T>();
        
        obj.Transform.Position = position;
        obj.Transform.Rotation = rotation;
        
        return obj;
    }

    private static void CreateObject(Object obj)
    {
        try
        {
            obj.Create();
        }
        
        catch (Exception creationError)
        {
            try
            {
                obj.Release();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(creationError, cleanupError);
            }
            
            throw;
        }
    }

    public void Destroy(Object? obj)
    {
        if (obj == null || obj.IsDestroyed)
        {
            return;
        }

        if (obj.IsStatic && !Application.IsEditor)
        {
            return;
        }
        
        obj.Release();
    }
    
    public void MoveToIndex(Object obj, int index)
    {
        var active = _objects.Where(item => !item.IsDestroyed).ToList();
        var pending = _spawnedObjects.Where(item => !item.IsDestroyed).ToList();

        if (!active.Remove(obj) && !pending.Remove(obj))
        {
            throw new InvalidOperationException("The object is not in this world.");
        }
        
        index = Math.Clamp(index, 0, active.Count + pending.Count);

        if (index <= active.Count)
        {
            active.Insert(index, obj);
        }
        else
        {
            pending.Insert(index - active.Count, obj);
        }
        
        _objects.Clear();
        _objects.AddRange(active);
        _spawnedObjects.Clear();

        foreach (var item in pending)
        {
            _spawnedObjects.Enqueue(item);
        }
        
        _objectsVersion++;
        _cachedObjectsArray = null;
    }

    public void Clear()
    {
        var objects = _objects.Concat(_spawnedObjects).Distinct().ToArray();
        
        _objects.Clear();
        _spawnedObjects.Clear();
        
        _objectsVersion++;
        _clearVersion++;
        
        _cachedObjectsArray = null;

        List<Exception> errors = [];
        
        foreach (var obj in objects)
        {
            if (!obj.IsDestroyed)
            {
                try
                {
                    obj.Release();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new AggregateException("World cleanup failed.", errors);
        }
    }

    public T? Find<T>() where T : Object
    {
        foreach (var obj in GetAllObjects(true))
        {
            if (!obj.IsDestroyed && obj is T match)
            {
                return match;
            }
        }
        
        return null;
    }

    public T[] FindAll<T>() where T : Object
    {
        var list = new List<T>();

        foreach (var obj in GetAllObjects(true))
        {
            if (!obj.IsDestroyed && obj is T match)
            {
                list.Add(match);
            }
        }
        
        return list.ToArray();
    }

    public Object? Find(string name)
    {
        foreach (var obj in GetAllObjects(true))
        {
            if (!obj.IsDestroyed && obj.Name == name)
            {
                return obj;
            }
        }
        
        return null;
    }

    public Object[] FindAll(string name)
    {
        var list = new List<Object>();

        foreach (var obj in GetAllObjects(true))
        {
            if (!obj.IsDestroyed && obj.Name == name)
            {
                list.Add(obj);
            }
        }
        
        return list.ToArray();
    }
    
    public Object[] GetAllObjects(bool includePending = false)
    {
        if (includePending)
        {
            return _objects.Concat(_spawnedObjects).Where(obj => !obj.IsDestroyed).ToArray();
        }
        if (_cachedObjectsArray != null && _cachedObjectsVersion == _objectsVersion)
        {
            return _cachedObjectsArray;
        }

        _cachedObjectsArray = _objects.ToArray();
        _cachedObjectsVersion = _objectsVersion;

        return _cachedObjectsArray;
    }

    internal void ReplaceWith(World replacement)
    {
        if (ReferenceEquals(this, replacement))
        {
            return;
        }

        var objects = replacement.GetAllObjects(includePending: true);
        
        Clear();
        
        replacement._objects.Clear();
        replacement._spawnedObjects.Clear();
        replacement._objectsVersion++;

        foreach (var obj in objects)
        {
            _spawnedObjects.Enqueue(obj);
        }
    }
}
