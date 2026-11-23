using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public class Entity : Object
{
    private Guid? _assetId;
    
    private string? _debugBillboardName;
    
    private BillboardComponent? _debugBillboardComponent;
    private MeshComponent? _debugMeshComponent;
    
    private bool _belowKillY;
    
    protected sealed override bool CanBeStatic => false;
    protected sealed override bool AlwaysStatic => false;
    
    protected override bool IsSaveableByDefault => true;
    
    public string Path { get; private set; } = "";
    
    public Guid? AssetId
    {
        get
        {
            // A legacy asset can acquire its identity when the editor saves a map or prefab.
            if (_assetId == null && !string.IsNullOrWhiteSpace(Path))
            {
                _assetId = Content.LoadEntity(Path)?.AssetId;
            }

            return _assetId;
        }
        
        private set => _assetId = value;
    }
    
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

            _debugBillboardComponent.SurfaceMode = SurfaceMode.Cutout;
            
            _debugBillboardComponent.IsDebug = true;
            _debugBillboardComponent.IgnoreDepth = true;
            
            _debugBillboardComponent.UseParentScale = false;
            _debugBillboardComponent.Scale = Settings.GetFloat("Debug", "BillboardScale", 1f);
            
            _debugBillboardComponent.RenderOrder = 1000;
            
            _debugBillboardName = (string)debugBillboard;
            
            RefreshDebugBillboard();
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
    
    protected virtual string GetDebugBillboardName(string defaultName)
    {
        return defaultName;
    }

    protected void RefreshDebugBillboard()
    {
        if (_debugBillboardComponent is not { IsDestroyed: false } || _debugBillboardName == null)
        {
            return;
        }

        _debugBillboardComponent.Texture = Content.LoadTexture(
            GetDebugBillboardName(_debugBillboardName), TextureFormat.Png);
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

    internal static Entity? Create(string name, World? world, Guid? guid, Guid? assetId = null)
    {
        name = EntityAssetReferences.ResolvePath(name, assetId);
        var data = Content.LoadEntity(name, assetId);

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
        entity.AssetId = data.AssetId;
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
