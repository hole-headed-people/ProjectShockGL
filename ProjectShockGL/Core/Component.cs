using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGL.Core;

public class Component
{
    private bool _isInitialized;
    private bool _hasStarted;

    public bool IsEnabled { get; set; } = true;
    public bool IsDestroyed { get; private set; }
    public Object Parent { get; private set; } = null!;

    public void Initialize(Object parent)
    {
        if (_isInitialized)
        {
            return;
        }
        
        _isInitialized = true;
        
        Parent = parent;
    }
    
    public virtual void Create(){}
    internal void TryStart()
    {
        if (_hasStarted || IsDestroyed || !IsEnabled || Parent.IsDestroyed || !Parent.IsActive)
        {
            return;
        }
        
        _hasStarted = true;
        
        Start();
    }
    public virtual void Start(){}
    public virtual void Update(float deltaSeconds){}
    public virtual void Destroy()
    {
        IsDestroyed = true;
    }
}
