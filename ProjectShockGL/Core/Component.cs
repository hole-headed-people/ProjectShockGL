namespace ProjectShockGL.Core;

public class Component
{
    private bool _isInitialized;
    private bool _hasStarted;

    public virtual bool IsEnabled { get; set; } = true;
    internal virtual void OnOwnerActiveChanged(bool isActive) { }
    public bool IsDestroyed { get; private set; }
    public Object Owner { get; private set; } = null!;

    public void Initialize(Object parent)
    {
        if (_isInitialized)
        {
            return;
        }
        
        _isInitialized = true;
        
        Owner = parent;
    }
    
    public virtual void Create(){}
    internal void TryStart()
    {
        if (_hasStarted || IsDestroyed || !IsEnabled || Owner.IsDestroyed || !Owner.IsActive)
        {
            return;
        }
        
        _hasStarted = true;
        
        Start();
    }
    public virtual void Start(){}
    public virtual void Update(float deltaSeconds){}
    public virtual void DebugRender(Rendering.DebugDraw draw){}
    public virtual void DebugRenderSelected(Rendering.DebugDraw draw){}
    public virtual void Destroy()
    {
        IsDestroyed = true;
    }

    internal void Release()
    {
        try
        {
            if (!IsDestroyed)
            {
                Destroy();
            }
        }
        finally
        {
            IsEnabled = false;
            IsDestroyed = true;
            
            if (this is Collider collider)
            {
                Physics.Unregister(collider);
            }
            if (this is CameraComponent camera)
            {
                camera.ReleaseCamera();
            }
        }
    }
}
