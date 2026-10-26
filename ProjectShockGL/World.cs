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

    public World()
    {
        _objects = [];
        _spawnedObjects = [];
    }

    public void Update(float deltaSeconds)
    {
        while (_spawnedObjects.Count > 0)
        {
            var obj = _spawnedObjects.Dequeue();

            if (obj.IsDestroyed)
            {
                continue;
            }
            
            _objects.Add(obj);
            _objectsVersion++;
            obj.InitializeActivity();
            obj.TryStart();
        }
        
        var objectsToUpdate = GetAllObjects();
        var clearVersion = _clearVersion;
        
        foreach (var obj in objectsToUpdate)
        {
            if (_clearVersion != clearVersion)
            {
                break;
            }

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
        
        if (_objects.RemoveAll(obj => obj.IsDestroyed) > 0)
        {
            _objectsVersion++;
        }
    }

    public void Render(RenderQueue queue)
    {
        foreach (var obj in _objects)
        {
            if (!obj.IsDestroyed && obj.IsActive)
            {
                obj.Render(queue);
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
        
        obj.Create();
        
        return obj;
    }
    
    public T Spawn<T>() where T : Object, new()
    {
        var obj = new T();
        _spawnedObjects.Enqueue(obj);
        
        obj.Create();
        
        return obj;
    }

    public T Spawn<T>(Vector3 position, Quaternion rotation) where T : Object, new()
    {
        var obj = Spawn<T>();
        
        obj.Transform.Position = position;
        obj.Transform.Rotation = rotation;
        
        return obj;
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
        
        obj.Destroy();
    }

    public void Clear()
    {
        var objects = _objects.Concat(_spawnedObjects).Distinct().ToArray();
        
        _objects.Clear();
        _spawnedObjects.Clear();
        
        _objectsVersion++;
        _clearVersion++;
        
        _cachedObjectsArray = null;

        foreach (var obj in objects)
        {
            if (!obj.IsDestroyed)
            {
                obj.Destroy();
            }
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
