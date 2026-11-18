namespace ProjectShockGL.Core;

public class Entity : Object
{
    private BillboardComponent? _debugBillboardComponent;
    private MeshComponent? _debugMeshComponent;
    
    private bool _belowKillY;
    
    protected sealed override bool CanBeStatic => false;
    protected sealed override bool AlwaysStatic => false;
    protected override bool IsSaveableByDefault => true;
    
    public string Path { get; private set; } = "";
    public List<OutputData> Outputs { get; } = [];
    
    public Rigidbody? Rigidbody => GetComponent<Rigidbody>() is { IsDestroyed: false } body ? body : null;
    
    internal void CheckKillY(float killY)
    {
        if (IsDestroyed || !IsActive)
        {
            return;
        }

        if (Transform.Position.Y >= killY)
        {
            _belowKillY = false;
            
            return;
        }

        if (_belowKillY || !float.IsFinite(Transform.Position.Y))
        {
            return;
        }

        _belowKillY = true;
        
        Kill();

        if (!IsDestroyed && Transform.Position.Y >= killY)
        {
            _belowKillY = false;
        }
    }

    public virtual void Load(Dictionary<string, object> arguments)
    {
        if (arguments.TryGetValue("DebugBillboard", out var debugBillboard))
        {
            _debugBillboardComponent ??= AddComponent<BillboardComponent>();
            
            _debugBillboardComponent.IsDebug = true;
            _debugBillboardComponent.IgnoreDepth = true;
            _debugBillboardComponent.UseParentScale = false;
            
            _debugBillboardComponent.RenderOrder = 1000;
            
            _debugBillboardComponent.Texture = Content.LoadTexture((string)debugBillboard, TextureFormat.Png);
        }

        if (arguments.TryGetValue("DebugMesh", out var debugMesh))
        {
            _debugMeshComponent ??= AddComponent<MeshComponent>();
            
            _debugMeshComponent.IsDebug = true;
            _debugMeshComponent.IgnoreDepth = true;
            _debugMeshComponent.UseParentScale = false;
            
            _debugMeshComponent.RenderOrder = 500;
            
            _debugMeshComponent.Mesh = Content.LoadMesh((string)debugMesh);
            _debugMeshComponent.Texture = Content.LoadTexture("Default") ?? Content.GetWhitePixel();
        }
    }
    
    public virtual void Kill()
    {
        Destroy();
    }

    public static Entity? Create(string name, World? world = null)
    {
        var entity = Create(name, world, null);

        if (entity != null)
        {
            entity.SpawnSource = new ObjectSpawnSource(name);
        }

        return entity;
    }

    internal static Entity? Create(string name, World? world, Guid? guid)
    {
        var data = Content.LoadEntity(name);

        if (data == null)
        {
            return null;
        }

        var type = Type.GetType(data.Class);

        if (type == null)
        {
            return null;
        }

        var entity = (world ?? Game.Instance.World).Spawn<Entity>(type, guid);

        if (entity == null)
        {
            return null;
        }

        entity.Path = name;
        try
        {
            entity.Load(data.Parameters);
        }
        catch (Exception loadError)
        {
            try
            {
                entity.Saveable = false;
                entity.Release();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(loadError, cleanupError);
            }

            throw;
        }
        
        return entity;
    }

    public void InvokeEvent(string name)
    {
        InvokeEventCore(name, null, false);
    }

    public void InvokeEvent(string name, object? value)
    {
        InvokeEventCore(name, value, true);
    }

    private void InvokeEventCore(string name, object? value, bool hasValue)
    {
        foreach (var output in Outputs.ToArray())
        {
            if (output.Event != name)
            {
                continue;
            }

            var target = Game.Instance.World.Find(output.TargetName);

            if (target == null)
            {
                Console.WriteLine($"Trigger target not found: {output.TargetName}");
                
                continue;
            }

            if (hasValue)
            {
                InputInvoker.Invoke(target, output.Input, output.Parameter, value);
            }
            else
            {
                InputInvoker.Invoke(target, output.Input, output.Parameter);
            }
        }
    }
    
    [Input(nameof(SetSimulatePhysics))]
    public void SetSimulatePhysics(bool enabled)
    {
        if (Rigidbody is not { } body)
        {
            return;
        }

        body.IsEnabled = enabled;
    }
}
