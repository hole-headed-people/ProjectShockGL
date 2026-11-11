namespace ProjectShockGL.Core;

public class Entity : Object
{
    private BillboardComponent _billboardComponent = null!;
    
    private bool _belowKillY;
    
    protected override bool IsSaveableByDefault => true;
    
    public string Path { get; private set; } = "";
    public List<OutputData> Outputs { get; } = [];
    
    public Rigidbody? Rigidbody => GetComponent<Rigidbody>() is { IsDestroyed: false } body ? body : null;
    
    public override void Create()
    {
        base.Create();

        _billboardComponent = AddComponent<BillboardComponent>();
        
        _billboardComponent.IsDebug = true;
        _billboardComponent.IgnoreDepth = true;
        _billboardComponent.UseParentScale = false;
        _billboardComponent.RenderOrder = 1000;
    }

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
        if (arguments.TryGetValue("DebugBillboard", out var argument))
        {
            _billboardComponent.Texture = Content.LoadTexture((string)argument, TextureFormat.Png);
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
