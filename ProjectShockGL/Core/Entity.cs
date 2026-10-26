namespace ProjectShockGL.Core;

public class Entity : Object
{
    private BillboardComponent _billboardComponent = null!;
    
    public string Path { get; private set; } = "";
    public List<OutputData> Outputs { get; } = [];

    public override void Create()
    {
        base.Create();

        _billboardComponent = AddComponent<BillboardComponent>();
        _billboardComponent.IsDebug = true;
        _billboardComponent.IgnoreDepth = true;
        _billboardComponent.UseParentScale = false;
        _billboardComponent.RenderOrder = 1000;
    }

    public virtual void Load(Dictionary<string, object> arguments)
    {
        if (arguments.TryGetValue("DebugBillboard", out var argument))
        {
            _billboardComponent.Texture = Content.LoadTexture((string)argument, TextureFormat.Png);
        }
    }

    public static Entity? Create(string name, World? world = null)
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

        var entity = (world ?? Game.Instance.World).Spawn<Entity>(type);

        if (entity == null)
        {
            return null;
        }

        entity.Path = name;
        entity.Load(data.Parameters);
        
        return entity;
    }
}
