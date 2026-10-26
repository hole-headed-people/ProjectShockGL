using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public class Object
{
    private readonly List<RenderComponent> _renderComponents = [];
    private readonly List<Component> _components = [];

    private bool _isStatic;
    private bool _isActive = true;
    private bool _activityInitialized;
    private bool _hasStarted;
    private bool _isReleasing;
    
    private (Vector3 min, Vector3 max)? _cachedWorldBounds;

    public string Name { get; set; } = string.Empty;
    public Transform Transform { get; } = new();
    public Collider? Collider { get; private set; }
    public int Layer { get; set; } = 1;
    public int CollidesWith { get; set; } = ~0;

    public bool IsStatic
    {
        get => _isStatic;
        set
        {
            if (_isStatic && !Application.IsEditor)
            {
                return;
            }
            
            _isStatic = value;
        }
    }

    public bool IsActive => _isActive;
    
    public bool IsDestroyed { get; private set; }

    public virtual void Create(){}

    internal void InitializeActivity()
    {
        if (_activityInitialized || IsDestroyed)
        {
            return;
        }
        
        _activityInitialized = true;

        if (_isActive)
        {
            OnActiveChanged(true);
        }
    }

    internal void TryStart()
    {
        if (_hasStarted || IsDestroyed || !IsActive)
        {
            return;
        }
        
        _hasStarted = true;
        
        Start();
    }

    public virtual void Start()
    {
        foreach (var component in _components.ToArray())
        {
            if (IsDestroyed || !IsActive)
            {
                break;
            }
            
            if (!component.IsDestroyed && component.IsEnabled)
            {
                component.TryStart();
            }
        }
    }

    public virtual void Update(float deltaTime)
    {
        foreach (var component in _components.ToArray())
        {
            if (IsDestroyed || !IsActive)
            {
                break;
            }
            
            if (!component.IsDestroyed && component.IsEnabled)
            {
                component.TryStart();

                if (!IsDestroyed && IsActive && !component.IsDestroyed && component.IsEnabled)
                {
                    component.Update(deltaTime);
                }
            }
        }
        
        _components.RemoveAll(c => c.IsDestroyed);
    }

    public void Render(RenderQueue queue)
    {
        if (IsDestroyed || !IsActive || _renderComponents.Count <= 0)
        {
            return;
        }
        
        foreach (var component in _renderComponents)
        {
            if (!component.IsDestroyed && component.IsEnabled && (!component.IsDebug || Application.IsDebug))
            {
                component.Render(queue);
            }
        }
    }

    public virtual void OnActiveChanged(bool isActive){}
    
    public virtual void OnCollisionEnter(Object other){}
    public virtual void OnCollisionExit(Object other){}
    
    public virtual void OnTriggerEnter(Object other){}
    public virtual void OnTriggerExit(Object other){}
    
    public virtual void Destroy()
    {
        DestroyCore();
    }

    internal void Release()
    {
        if (IsDestroyed || _isReleasing)
        {
            return;
        }
        
        _isReleasing = true;

        try
        {
            Cleanup.Run(Destroy, DestroyCore);
        }

        finally
        {
            _isReleasing = false;
        }
    }

    private void DestroyCore()
    {
        if (IsDestroyed)
        {
            return;
        }
        
        IsDestroyed = true;
        List<Exception> errors = [];
        
        if (_isActive)
        {
            _isActive = false;

            if (_activityInitialized)
            {
                try
                {
                    OnActiveChanged(false);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
        }

        foreach (var component in _components.ToArray())
        {
            if (!component.IsDestroyed)
            {
                try
                {
                    component.Release();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
        }
        
        _components.Clear();
        _renderComponents.Clear();
        
        Collider = null;
        
        if (errors.Count > 0)
        {
            throw new AggregateException("Object cleanup failed.", errors);
        }
    }
    
    public virtual (Vector3 min, Vector3 max) GetLocalBounds()
    {
        if (Collider != null)
        {
            return Collider.GetLocalBounds();
        }
        
        var meshComponent = GetComponent<MeshComponent>();
        
        if (meshComponent?.Mesh != null)
        {
            return (meshComponent.Mesh.BoundsMin, meshComponent.Mesh.BoundsMax);
        }
        
        return (new Vector3(-0.5f), new Vector3(0.5f));
    }
    
    public (Vector3 min, Vector3 max) GetWorldBounds()
    {
        if (Collider is { IsDestroyed: false })
        {
            return Collider.GetWorldBounds();
        }

        if (!Application.IsEditor && IsStatic && _cachedWorldBounds.HasValue)
        {
            return _cachedWorldBounds.Value;
        }

        var (localMin, localMax) = GetLocalBounds();
        
        var worldMin = new Vector3(float.MaxValue);
        var worldMax = new Vector3(float.MinValue);
        
        var corners = new Vector3[]
        {
            new(localMin.X, localMin.Y, localMin.Z),
            new(localMax.X, localMin.Y, localMin.Z),
            new(localMax.X, localMax.Y, localMin.Z),
            new(localMin.X, localMax.Y, localMin.Z),
            new(localMin.X, localMin.Y, localMax.Z),
            new(localMax.X, localMin.Y, localMax.Z),
            new(localMax.X, localMax.Y, localMax.Z),
            new(localMin.X, localMax.Y, localMax.Z),
        };

        var position = Transform.Position;
        var rotation = Transform.Rotation;
        var scale = Transform.Scale;

        for (var i = 0; i < 8; i++)
        {
            var scaled = corners[i] * scale;
            var rotated = Vector3.Transform(scaled, rotation);
            var world = rotated + position;
        
            worldMin = Vector3.Min(worldMin, world);
            worldMax = Vector3.Max(worldMax, world);
        }

        var result = (worldMin, worldMax);

        if (!Application.IsEditor && IsStatic)
        {
            _cachedWorldBounds = result;
        }

        return result;
    }
    
    public T AddComponent<T>() where T : Component, new()
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);

        var component = new T();
        
        component.Initialize(this);
        _components.Add(component);
        try
        {
            component.Create();
            ObjectDisposedException.ThrowIf(IsDestroyed || component.IsDestroyed, this);
        }
        catch (Exception creationError)
        {
            _components.Remove(component);
            try
            {
                component.Release();
            }
            catch (Exception cleanupError) { throw new AggregateException(creationError, cleanupError); }
            throw;
        }

        if (component is RenderComponent renderComponent)
        {
            _renderComponents.Add(renderComponent);
        }

        if (Collider == null && component is Collider collider)
        {
            Collider = collider;
        }
    
        return component;
    }
    
    public void RemoveComponent<T>() where T : Component
    {
        var component = GetComponent<T>();

        if (component == null)
        {
            return;
        }
        
        if (component is RenderComponent renderComponent)
        {
            _renderComponents.Remove(renderComponent);
        }

        if (Collider != null && component is Collider)
        {
            Collider = null;
        }

        _components.Remove(component);
        component.IsEnabled = false;
        component.Release();
    }

    public T? GetComponent<T>() where T : Component
    {
        return _components.OfType<T>().FirstOrDefault();
    }

    [Input("SetActive")]
    public void SetActive(bool isActive)
    {
        if (IsDestroyed || _isActive == isActive)
        {
            return;
        }
        
        _isActive = isActive;
        
        if (_activityInitialized)
        {
            OnActiveChanged(isActive);
        }
    }
}
