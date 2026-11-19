using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public class Object
{
    private readonly List<Coroutine> _coroutines = [];
    
    private readonly List<RenderComponent> _renderComponents = [];
    private readonly List<Component> _components = [];

    private bool _isStatic;
    private bool _placementComplete;
    private bool _isActive = true;
    private bool _activityInitialized;
    private bool _hasStarted;
    private bool _isReleasing;
    private bool? _saveable;

    private readonly HashSet<Object> _attachedObjects = [];
    
    private Vector3 _attachmentPosition;
    private Quaternion _attachmentRotation = Quaternion.Identity;
    
    private bool _applyingAttachment;

    public Object? AttachmentTarget { get; private set; }
    
    private (Vector3 min, Vector3 max)? _cachedWorldBounds;

    public string Name { get; set; } = string.Empty;
    public Transform Transform { get; }
    public Collider? Collider { get; private set; }
    public int Layer { get; set; } = 1;
    public int CollidesWith { get; set; } = ~0;
    
    public Guid Guid { get; internal set; }
    internal World? OwningWorld { get; set; }
    public World World => OwningWorld ?? throw new InvalidOperationException("The object does not belong to a World.");
    internal ObjectSpawnSource? SpawnSource { get; set; }
    
    public virtual bool Saveable
    {
        get => _saveable ?? IsSaveableByDefault;
        set => _saveable = value;
    }

    protected virtual bool IsSaveableByDefault => false;

    public List<string> EditorGroups { get; } = [];
    public bool IsEditorVisible => !Application.IsEditor || !EditorGroups.Any(ProjectShockGL.World.HiddenEditorGroups.Contains);
    
    protected virtual bool CanBeStatic => true;
    protected virtual bool AlwaysStatic => false;
    protected bool IsRuntimeStaticLocked => IsStatic && _placementComplete && !Application.IsEditor;

    public Object()
    {
        Transform = new Transform(() => !IsRuntimeStaticLocked, OnTransformChanged);
    }

    public bool IsStatic
    {
        get => _isStatic || AlwaysStatic;
        set => ConfigureStatic(value);
    }

    protected bool ConfigureStatic(bool value)
    {
        if (value && AttachmentTarget != null)
        {
            throw new InvalidOperationException("An attached object cannot be static.");
        }

        if (value && !CanBeStatic)
        {
            throw new InvalidOperationException($"{GetType().Name} cannot be static.");
        }

        if (!value && AlwaysStatic)
        {
            throw new InvalidOperationException($"{GetType().Name} must remain static.");
        }

        if (_placementComplete && !Application.IsEditor && IsStatic != value)
        {
            return false;
        }

        _isStatic = value;
        _cachedWorldBounds = null;
        if (IsStatic && !_isActive)
        {
            SetActive(true);
        }

        return true;
    }

    internal void CompletePlacement()
    {
        _placementComplete = true;
        if (IsStatic && !_isActive) SetActive(true);
    }
    public bool IsActive => _isActive;
    
    public bool IsDestroyed { get; private set; }

    public virtual void Create(){}
    
    public virtual void OnCaptureState(IObjectStateWriter state){}
    public virtual void OnRestoreState(IObjectStateReader state){}

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

    public virtual void Update(float deltaSeconds)
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
                    component.Update(deltaSeconds);
                }
            }
        }
        
        _components.RemoveAll(c => c.IsDestroyed);
    }

    public void Render(RenderQueue queue, bool? debugOnly = null)
    {
        if (IsDestroyed || !IsActive || !IsEditorVisible || _renderComponents.Count <= 0)
        {
            return;
        }
        
        foreach (var component in _renderComponents)
        {
            if (debugOnly.HasValue && component.IsDebug != debugOnly.Value)
            {
                continue;
            }
            
            if (!component.IsDestroyed && component.IsEnabled && (!component.IsDebug || Application.IsDebug))
            {
                component.Render(queue);
            }
        }
    }

    public virtual void DebugRender(DebugDraw draw){}
    public virtual void DebugRenderSelected(DebugDraw draw){}

    internal void DrawDebug(DebugDraw draw, bool selected)
    {
        if (IsDestroyed || !IsEditorVisible || (!Application.IsEditor && !IsActive))
        {
            return;
        }

        if (selected)
        {
            DebugRenderSelected(draw);
        }
        else
        {
            DebugRender(draw);
        }

        foreach (var component in _components.ToArray())
        {
            if (IsDestroyed || !IsEditorVisible || (!Application.IsEditor && !IsActive))
            {
                break;
            }

            if (component.IsDestroyed || (!Application.IsEditor && !component.IsEnabled))
            {
                continue;
            }

            if (selected)
            {
                component.DebugRenderSelected(draw);
            }
            else
            {
                component.DebugRender(draw);
            }
        }
    }

    public virtual void OnActiveChanged(bool isActive){}
    
    public virtual void OnCollisionEnter(Object other){}
    public virtual void OnCollisionExit(Object other){}
    
    public virtual void OnTriggerEnter(Object other){}
    public virtual void OnTriggerStay(Object other){}
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
        if (IsDestroyed || (IsStatic && !Application.IsEditor && !_isReleasing))
        {
            return;
        }
        
        IsDestroyed = true;
        Detach();
        foreach (var attached in _attachedObjects.ToArray()) attached.Detach();
        
        OwningWorld?.RecordRemoval(this);
        OwningWorld = null;
        
        StopAllCoroutines();
        
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
    
    internal Coroutine[] CaptureCoroutines()
    {
        return _coroutines.ToArray();
    }

    internal void RemoveCoroutine(Coroutine coroutine)
    {
        _coroutines.Remove(coroutine);
    }

    public Coroutine StartCoroutine(System.Collections.IEnumerator routine)
    {
        ArgumentNullException.ThrowIfNull(routine);
        ObjectDisposedException.ThrowIf(IsDestroyed, this);

        if (Application.IsEditor)
        {
            throw new InvalidOperationException("Coroutines run only in game mode.");
        }
        
        var coroutine = new Coroutine(this, routine);
        
        _coroutines.Add(coroutine);
        coroutine.Tick(0, 0, true);
        
        return coroutine;
    }

    public void StopCoroutine(Coroutine? coroutine)
    {
        if (coroutine != null && coroutine.Owner != this)
        {
            throw new ArgumentException("Coroutine belongs to another object.", nameof(coroutine));
        }
        
        coroutine?.Stop();
    }

    public void StopAllCoroutines()
    {
        foreach (var coroutine in _coroutines.ToArray())
        {
            coroutine.Stop();
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
            catch (Exception cleanupError)
            {
                throw new AggregateException(creationError, cleanupError);
            }
            
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

    internal void RenderCameraCaptures(World world)
    {
        if (IsDestroyed || !IsActive || !IsEditorVisible)
        {
            return;
        }
        
        foreach (var capture in _components.OfType<CameraCaptureComponent>().ToArray())
        {
            if (IsDestroyed || !IsActive)
            {
                break;
            }
            
            capture.Capture(world);
        }
    }
    public IReadOnlyList<RenderComponent> GetRenderComponents() =>
        _renderComponents.Where(component => !component.IsDestroyed).ToArray();

    public T? GetComponent<T>() where T : Component
    {
        return _components.OfType<T>().FirstOrDefault();
    }

    public void AttachTo(Object target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(IsDestroyed, this);
        ObjectDisposedException.ThrowIf(target.IsDestroyed, target);

        if (OwningWorld == null || OwningWorld != target.OwningWorld)
        {
            throw new InvalidOperationException("Attached objects must belong to the same World.");
        }
        
        if (IsStatic)
        {
            throw new InvalidOperationException("A static object cannot be attached.");
        }
        
        if (GetComponent<Rigidbody>() is { IsEnabled: true, IsDestroyed: false })
        {
            throw new InvalidOperationException("Disable Rigidbody before attaching the object.");
        }
        
        for (var current = target; current != null; current = current.AttachmentTarget)
        {
            if (ReferenceEquals(current, this))
            {
                throw new InvalidOperationException("Attachment cannot create a cycle.");
            }
        }

        var rotation = Quaternion.Normalize(target.Transform.Rotation);
        var inverse = Quaternion.Inverse(rotation);
        var position = Vector3.Transform(Transform.Position - target.Transform.Position, inverse);
        var relativeRotation = Quaternion.Normalize(inverse * Transform.Rotation);
        
        Detach();
        
        AttachmentTarget = target;
        
        _attachmentPosition = position;
        _attachmentRotation = relativeRotation;
        
        target._attachedObjects.Add(this);
    }

    [Input("AttachTo")]
    public void AttachTo(string targetName)
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);
        
        var target = string.IsNullOrWhiteSpace(targetName) ? null : OwningWorld?.Find(targetName);
        
        if (target == null)
        {
            Log.PrintWarning($"Object '{Name}': attachment target '{targetName}' was not found.");
            return;
        }
        
        AttachTo(target);
    }

    [Input("Detach")]
    public void Detach()
    {
        AttachmentTarget?._attachedObjects.Remove(this);
        AttachmentTarget = null;
        
        _attachmentPosition = Vector3.Zero;
        _attachmentRotation = Quaternion.Identity;
    }

    private void OnTransformChanged()
    {
        _cachedWorldBounds = null;

        if (_applyingAttachment)
        {
            return;
        }
        
        if (AttachmentTarget is { } target)
        {
            var inverse = Quaternion.Inverse(Quaternion.Normalize(target.Transform.Rotation));
            
            _attachmentPosition = Vector3.Transform(Transform.Position - target.Transform.Position, inverse);
            _attachmentRotation = Quaternion.Normalize(inverse * Transform.Rotation);
        }
        
        UpdateAttachedObjects();
    }

    private void UpdateAttachedObjects()
    {
        foreach (var attached in _attachedObjects)
        {
            attached.UpdateAttachment();
        }
    }

    private void UpdateAttachment()
    {
        if (AttachmentTarget is not { } target || IsDestroyed)
        {
            return;
        }
        
        _applyingAttachment = true;
        
        try
        {
            var rotation = Quaternion.Normalize(target.Transform.Rotation);
            
            Transform.Position = target.Transform.Position + Vector3.Transform(_attachmentPosition, rotation);
            Transform.Rotation = Quaternion.Normalize(rotation * _attachmentRotation);
        }
        finally
        {
            _applyingAttachment = false;
        }
        
        UpdateAttachedObjects();
    }
    
    [Input("SetActive")]
    public void SetActive(bool isActive)
    {
        if (IsDestroyed || _isActive == isActive || (IsStatic && !isActive))
        {
            return;
        }
        
        _isActive = isActive;
        
        foreach (var component in _components.ToArray())
        {
            component.OnOwnerActiveChanged(isActive);
        }
        
        if (_activityInitialized)
        {
            OnActiveChanged(isActive);
        }
    }
}
