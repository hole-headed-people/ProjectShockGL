using ProjectShockGL;
using ProjectShockGL.Core;
using GameObject = ProjectShockGL.Core.Object;

Run("Create and Start run once while Update runs every frame", () =>
{
    var world = new World();
    var obj = world.Spawn<TrackedObject>();
    Check(obj.Starts == 0 && obj.Updates == 0);
    world.Update(0);
    world.Update(0);
    Check(obj.Starts == 1 && obj.Updates == 2);
    world.Clear();
});

Run("Destroy is called once, including repeated requests and Clear", () =>
{
    var world = new World();
    var obj = world.Spawn<TrackedObject>();
    world.Update(0);
    world.Destroy(obj);
    world.Destroy(obj);
    world.Clear();
    world.Update(0);
    Check(obj.Destroys == 1 && obj.Part.Destroys == 1);
});

Run("Destroy before Start never starts or updates the object", () =>
{
    var world = new World();
    var obj = world.Spawn<TrackedObject>();
    world.Destroy(obj);
    world.Update(0);
    Check(obj.Starts == 0 && obj.Updates == 0 && world.GetAllObjects().Length == 0);
});

Run("Clear releases pending and active objects", () =>
{
    var world = new World();
    var active = world.Spawn<TrackedObject>();
    world.Update(0);
    var pending = world.Spawn<TrackedObject>();
    world.Clear();
    world.Clear();
    world.Update(0);
    Check(active.Destroys == 1 && pending.Destroys == 1 && pending.Starts == 0);
    Check(active.Part.Destroys == 1 && pending.Part.Destroys == 1);
    Check(world.GetAllObjects().Length == 0);
});

Run("An object destroyed by another update is skipped", () =>
{
    var world = new World();
    var first = world.Spawn<TrackedObject>();
    var second = world.Spawn<TrackedObject>();
    first.OnUpdate = () => world.Destroy(second);
    world.Update(0);
    Check(second.Updates == 0 && second.Destroys == 1);
    Check(world.GetAllObjects().Length == 1 && world.Find(second.Name) == null);
});

Run("Self-destruction in Start is removed", () =>
{
    var world = new World();
    var obj = world.Spawn<TrackedObject>();
    obj.OnStart = () => world.Destroy(obj);
    world.Update(0);
    Check(obj.Starts == 1 && obj.Updates == 0 && obj.Destroys == 1);
    Check(world.GetAllObjects().Length == 0);
});

Run("Direct destruction is removed without a second callback", () =>
{
    var world = new World();
    var obj = world.Spawn<TrackedObject>();
    world.Update(0);
    obj.Destroy();
    Check(world.Find(obj.Name) == null);
    world.Update(0);
    Check(obj.Destroys == 1 && world.GetAllObjects().Length == 0);
});

Run("Pending colliders and lights are unregistered by Clear", () =>
{
    Physics.Initialize();
    var world = new World();
    var obj = world.Spawn<TrackedObject>();
    obj.AddComponent<SphereCollider>();
    var light = world.Spawn<Light>();
    Check(Physics.OverlapSphere(default, 1, Layers.Everything, out _));
    Check(Light.All.Contains(light));
    world.Clear();
    Check(!Physics.OverlapSphere(default, 1, Layers.Everything, out _));
    Check(!Light.All.Contains(light) && obj.Collider == null);
});

Run("Destruction callbacks cannot re-enter through World.Destroy", () =>
{
    var world = new World();
    var obj = world.Spawn<TrackedObject>();
    obj.Part.OnDestroy = () => world.Destroy(obj);
    world.Destroy(obj);
    Check(obj.Destroys == 1 && obj.Part.Destroys == 1);
    try
    {
        obj.AddComponent<Component>();
        throw new Exception("Destroyed object accepted a new component");
    }
    catch (ObjectDisposedException) { }
});

Run("Clear during Start releases the starting object and pending objects", () =>
{
    var world = new World();
    var first = world.Spawn<TrackedObject>();
    var pending = world.Spawn<TrackedObject>();
    first.OnStart = world.Clear;
    world.Update(0);
    Check(first.Destroys == 1 && first.Updates == 0);
    Check(pending.Destroys == 1 && pending.Starts == 0);
    Check(world.GetAllObjects().Length == 0);
});

Run("Clear during Update replaces the scene without updating old objects", () =>
{
    var world = new World();
    var first = world.Spawn<TrackedObject>();
    var second = world.Spawn<TrackedObject>();
    TrackedObject? replacement = null;
    first.OnUpdate = () =>
    {
        world.Clear();
        replacement = world.Spawn<TrackedObject>();
    };
    world.Update(0);
    Check(first.Destroys == 1 && second.Destroys == 1 && second.Updates == 0);
    Check(replacement != null && replacement.Starts == 0);
    world.Update(0);
    Check(replacement!.Starts == 1 && replacement.Updates == 1);
    world.Clear();
});

Run("Static objects retain runtime protection but Clear releases them", () =>
{
    var world = new World();
    var obj = world.Spawn<TrackedObject>();
    obj.IsStatic = true;
    world.Update(0);
    world.Destroy(obj);
    Check(!obj.IsDestroyed && obj.Starts == 1 && obj.Updates == 0);
    world.Clear();
    Check(obj.Destroys == 1);
});

Run("Find returns typed pending and started objects, includes derived types and skips destroyed objects", () =>
{
    var world = new World();
    Check(world.Find<TrackedObject>() == null);
    var first = world.Spawn<TrackedObject>();
    TrackedObject? found = world.Find<TrackedObject>();
    Check(found == first && first.Starts == 0);
    Check(world.Find<GameObject>() == first);
    world.Update(0);
    Check(world.Find<TrackedObject>() == first && first.Starts == 1);
    var second = world.Spawn<TrackedObject>();
    world.Destroy(first);
    Check(world.Find<TrackedObject>() == second);
    world.Destroy(second);
    Check(world.Find<TrackedObject>() == null);
    world.Clear();
});

Run("Initially inactive objects defer Start and components until activation, without restarting", () =>
{
    var world = new World();
    var obj = world.Spawn<ActivationProbe>();
    obj.SetActive(false);
    world.Update(0);
    Check(obj.Starts == 0 && obj.Updates == 0 && obj.Events.Count == 0 && obj.Part.Starts == 0);
    obj.SetActive(true);
    obj.SetActive(true);
    Check(obj.Events.SequenceEqual(new[] { true }) && obj.Starts == 0);
    world.Update(0);
    Check(obj.Starts == 1 && obj.Updates == 1 && obj.Part.Starts == 1);
    obj.SetActive(false);
    obj.SetActive(false);
    world.Update(0);
    Check(obj.Updates == 1 && obj.Events.SequenceEqual(new[] { true, false }));
    obj.SetActive(true);
    world.Update(0);
    Check(obj.Starts == 1 && obj.Part.Starts == 1 && obj.Updates == 2);
    world.Clear();
    obj.SetActive(true);
    Check(!obj.IsActive && obj.Events.SequenceEqual(new[] { true, false, true, false }));
});

Run("Initial activation sees complete Create and assigned transforms", () =>
{
    var world = new World();
    var obj = world.Spawn<ActivationProbe>(new System.Numerics.Vector3(1, 2, 3), System.Numerics.Quaternion.Identity);
    Check(obj.Events.Count == 0);
    obj.OnActivity = active =>
    {
        if (active) Check(obj.Part != null && obj.Transform.Position == new System.Numerics.Vector3(1, 2, 3));
    };
    world.Update(0);
    Check(obj.Events.SequenceEqual(new[] { true }) && obj.Starts == 1);
    obj.SetActive(false);
    world.Clear();
    Check(obj.Events.SequenceEqual(new[] { true, false }));
});

Run("Disabling from initial activation prevents Start until a later enable", () =>
{
    var world = new World();
    var obj = world.Spawn<ActivationProbe>();
    obj.OnActivity = active => { if (active) obj.SetActive(false); };
    world.Update(0);
    Check(obj.Starts == 0 && obj.Updates == 0);
    obj.OnActivity = null;
    obj.SetActive(true);
    world.Update(0);
    Check(obj.Starts == 1 && obj.Updates == 1);
    world.Clear();
});

Run("Clear from initial activation destroys active and pending objects without starting them", () =>
{
    var world = new World();
    var first = world.Spawn<ActivationProbe>();
    var second = world.Spawn<ActivationProbe>();
    first.OnActivity = active => { if (active) world.Clear(); };
    world.Update(0);
    Check(first.IsDestroyed && second.IsDestroyed && first.Starts == 0 && second.Starts == 0);
    Check(world.GetAllObjects(true).Length == 0);
});

Run("Disabled components start once when enabled and late components start before Update", () =>
{
    var world = new World();
    var obj = world.Spawn<ActivationProbe>();
    obj.Part.IsEnabled = false;
    world.Update(0);
    Check(obj.Part.Starts == 0);
    obj.Part.IsEnabled = true;
    world.Update(0);
    Check(obj.Part.Starts == 1 && obj.Part.Updates == 1);
    obj.Part.IsEnabled = false;
    world.Update(0);
    obj.Part.IsEnabled = true;
    var late = obj.AddComponent<ActivationComponent>();
    world.Update(0);
    Check(obj.Part.Starts == 1 && late.Starts == 1 && late.Updates == 1);
    world.Clear();
});

static void Run(string name, Action test)
{
    test();
    Console.WriteLine($"PASS: {name}");
}

static void Check(bool condition)
{
    if (!condition) throw new Exception("Lifecycle check failed");
}

public sealed class TrackedObject : GameObject
{
    public int Starts, Updates, Destroys;
    public Action? OnStart, OnUpdate;
    public TrackedComponent Part = null!;

    public override void Create()
    {
        Name = Guid.NewGuid().ToString();
        Part = AddComponent<TrackedComponent>();
    }

    public override void Start() { base.Start(); Starts++; OnStart?.Invoke(); }
    public override void Update(float dt) { base.Update(dt); Updates++; OnUpdate?.Invoke(); }
    public override void Destroy() { Destroys++; base.Destroy(); }
}

public sealed class TrackedComponent : Component
{
    public int Destroys;
    public Action? OnDestroy;
    public override void Destroy() { Destroys++; OnDestroy?.Invoke(); base.Destroy(); }
}

public sealed class ActivationProbe : GameObject
{
    public List<bool> Events = [];
    public Action<bool>? OnActivity;
    public int Starts, Updates;
    public ActivationComponent Part = null!;
    public override void Create() { base.Create(); Part = AddComponent<ActivationComponent>(); }
    public override void OnActiveChanged(bool active) { Events.Add(active); OnActivity?.Invoke(active); }
    public override void Start() { base.Start(); Starts++; }
    public override void Update(float dt) { base.Update(dt); Updates++; }
}

public sealed class ActivationComponent : Component
{
    public int Starts, Updates;
    public override void Start() => Starts++;
    public override void Update(float dt) { if (Starts != 1) throw new Exception("Update before Start"); Updates++; }
}
