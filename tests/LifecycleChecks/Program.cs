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

Run("Editor maintenance publishes objects without running gameplay or activation callbacks", () =>
{
    var world = new World();
    var obj = world.Spawn<ActivationProbe>();
    world.Update(0, simulate: false);
    obj.SetActive(false);
    obj.SetActive(true);
    world.Update(0, simulate: false);
    Check(world.GetAllObjects().Contains(obj));
    Check(obj.Starts == 0 && obj.Updates == 0 && obj.Events.Count == 0 && obj.Part.Starts == 0);
    world.Update(0);
    Check(obj.Starts == 1 && obj.Updates == 1 && obj.Events.SequenceEqual(new[] { true }));
    world.Clear();
});

Run("A failed component creation unregisters its collider and detaches it", () =>
{
    Physics.Initialize();
    var obj = new GameObject();
    try { obj.AddComponent<FailingCollider>(); Check(false); }
    catch (InvalidOperationException) { }
    Check(obj.GetComponent<FailingCollider>() == null && obj.Collider == null);
    Check(FailingCollider.Last.IsDestroyed);
    Check(!Physics.OverlapSphere(default, 1, Layers.Everything, out _));
    obj.Destroy();
});

Run("Destroying an owner during component creation cannot attach a live component", () =>
{
    var obj = new GameObject();
    try { obj.AddComponent<OwnerDestroyingComponent>(); Check(false); }
    catch (ObjectDisposedException) { }
    Check(obj.IsDestroyed && OwnerDestroyingComponent.Last.IsDestroyed);
    Check(obj.GetComponent<OwnerDestroyingComponent>() == null);
});

Run("Cleanup continues after activity and component callbacks throw", () =>
{
    var world = new World();
    var first = world.Spawn<ActivationProbe>();
    var badPart = first.AddComponent<TrackedComponent>();
    badPart.OnDestroy = () => throw new InvalidOperationException("component cleanup");
    first.AddComponent<SphereCollider>();
    var second = world.Spawn<TrackedObject>();
    world.Update(0);
    first.OnActivity = active => { if (!active) throw new InvalidOperationException("activity cleanup"); };
    try { world.Clear(); Check(false); }
    catch (AggregateException error) { Check(error.Flatten().InnerExceptions.Count == 2); }
    Check(first.IsDestroyed && first.Part.IsDestroyed && badPart.IsDestroyed && first.Collider == null);
    Check(second.IsDestroyed && second.Part.IsDestroyed && world.GetAllObjects(true).Length == 0);
    Check(!Physics.OverlapSphere(default, 1, Layers.Everything, out _));
});

Run("Failed object creation releases its partially created components", () =>
{
    var world = new World();
    try { world.Spawn<FailingObject>(); Check(false); }
    catch (InvalidOperationException) { }
    Check(FailingObject.Last.IsDestroyed && FailingObject.Last.Part.IsDestroyed);
    world.Update(0);
    Check(world.GetAllObjects(true).Length == 0);
});

Run("A throwing Destroy override cannot prevent base cleanup or leave a failed spawn alive", () =>
{
    var world = new World();
    var obj = world.Spawn<ThrowingDestroyObject>();
    try { world.Destroy(obj); Check(false); }
    catch (AggregateException) { }
    Check(obj.IsDestroyed && obj.Part.IsDestroyed);
    world.Clear();
    Check(obj.Destroys == 1);
});

Run("Objects spawned during Start still start and update in the same runtime frame", () =>
{
    var world = new World();
    var first = world.Spawn<TrackedObject>();
    TrackedObject? child = null;
    first.OnStart = () => child = world.Spawn<TrackedObject>();
    world.Update(0);
    Check(child is { Starts: 1, Updates: 1 });
    world.Clear();
});

Run("World rendering separates debug components from the postprocessed world", () =>
{
    var debugProperty = typeof(Application).GetProperty(nameof(Application.IsDebug))!;
    var previous = Application.IsDebug;
    var world = new World();
    try
    {
        var obj = world.Spawn<GameObject>();
        var normal = obj.AddComponent<CountingRenderComponent>();
        var debug = obj.AddComponent<CountingRenderComponent>();
        debug.IsDebug = true;
        world.Update(0, simulate: false);
        var queue = new ProjectShockGL.Rendering.RenderQueue();
        debugProperty.SetValue(null, true);
        world.Render(queue, debugOnly: false);
        Check(normal.Renders == 1 && debug.Renders == 0);
        world.Render(queue, debugOnly: true);
        Check(normal.Renders == 1 && debug.Renders == 1);
        debugProperty.SetValue(null, false);
        world.Render(queue, debugOnly: true);
        Check(debug.Renders == 1);
    }
    finally
    {
        world.Clear();
        debugProperty.SetValue(null, previous);
    }
});

Run("Reordering active and pending objects preserves order and defers lifecycle until Update", () =>
{
    var world = new World();
    var first = world.Spawn<TrackedObject>();
    var second = world.Spawn<TrackedObject>();
    world.Update(0,simulate:false);
    var pending = world.Spawn<TrackedObject>();
    world.MoveToIndex(pending,1);
    Check(world.GetAllObjects(true).SequenceEqual(new[] { first,pending,second }));
    Check(first.Starts==0 && second.Starts==0 && pending.Starts==0);
    world.Update(0);
    Check(first.Starts==1 && second.Starts==1 && pending.Starts==1);
    world.MoveToIndex(second,0);
    world.Update(0);
    Check(world.GetAllObjects().SequenceEqual(new[] { second,first,pending }));
    Check(second.Starts==1 && second.Updates==2 && pending.Updates==2);
    world.Clear();
});

Run("Debug callbacks respect mode, selection, visibility and component lifetime without starting gameplay", () =>
{
    var editor = typeof(Application).GetProperty(nameof(Application.IsEditor))!;
    var debug = typeof(Application).GetProperty(nameof(Application.IsDebug))!;
    var wasEditor = Application.IsEditor;
    var wasDebug = Application.IsDebug;
    var world = new World();
    var draw = new ProjectShockGL.Rendering.DebugDraw(null!);

    try
    {
        editor.SetValue(null, false);
        debug.SetValue(null, false);
        var obj = world.Spawn<DebugProbe>();
        var part = obj.AddComponent<DebugPart>();
        world.Update(0, simulate: false);
        world.DebugRender(draw);
        Check(obj.Draws == 0 && part.Draws == 0);
        debug.SetValue(null, true);
        world.DebugRender(draw);
        Check(obj.Draws == 1 && part.Draws == 1 && obj.Starts == 0);
        world.DebugRenderSelected(draw, [obj]);
        Check(obj.SelectedDraws == 0 && part.SelectedDraws == 0);
        part.IsEnabled = false;
        world.DebugRender(draw);
        Check(obj.Draws == 2 && part.Draws == 1);
        obj.SetActive(false);
        world.DebugRender(draw);
        Check(obj.Draws == 2);

        editor.SetValue(null, true);
        debug.SetValue(null, false);
        world.DebugRender(draw);
        Check(obj.Draws == 3 && part.Draws == 2 && obj.Starts == 0);
        world.DebugRenderSelected(draw, [obj, obj]);
        Check(obj.SelectedDraws == 1 && part.SelectedDraws == 1);
        var other = world.Spawn<DebugProbe>();
        world.Update(0, simulate: false);
        world.DebugRenderSelected(draw, [obj, other]);
        Check(obj.SelectedDraws == 2 && other.SelectedDraws == 1);
        obj.EditorGroups.Add("debug-hidden-test");
        World.HiddenEditorGroups.Add("debug-hidden-test");
        world.DebugRender(draw);
        world.DebugRenderSelected(draw, [obj]);
        Check(obj.Draws == 3 && obj.SelectedDraws == 2 && part.Draws == 2);
        World.HiddenEditorGroups.Remove("debug-hidden-test");
        obj.RemoveComponent<DebugPart>();
        world.DebugRender(draw);
        Check(obj.Draws == 4 && part.Draws == 2);
        obj.Destroy();
        world.DebugRender(draw);
        world.DebugRenderSelected(draw, [obj]);
        Check(obj.Draws == 4 && obj.SelectedDraws == 2);
    }
    finally
    {
        world.Clear();
        World.HiddenEditorGroups.Remove("debug-hidden-test");
        editor.SetValue(null, wasEditor);
        debug.SetValue(null, wasDebug);
    }
});

Run("KillY destroys entities only in simulation and allows overriding Kill and rearming", () =>
{
    var world = new World { KillY = -10f };
    try
    {
        var ordinary = world.Spawn<GameObject>();
        ordinary.Transform.Position = new System.Numerics.Vector3(0, -11, 0);
        var doomed = world.Spawn<Entity>();
        doomed.Transform.Position = ordinary.Transform.Position;
        doomed.IsStatic = true;
        var disabled = world.Spawn<Entity>();
        disabled.Transform.Position = ordinary.Transform.Position;
        disabled.SetActive(false);
        var boundary = world.Spawn<Entity>();
        boundary.Transform.Position = new System.Numerics.Vector3(0, -10, 0);
        world.Update(.1f, simulate: false);
        Check(!doomed.IsDestroyed);
        world.Update(0);
        Check(!doomed.IsDestroyed);
        world.Update(.1f);
        Check(doomed.IsDestroyed && !ordinary.IsDestroyed && !disabled.IsDestroyed && !boundary.IsDestroyed);
        disabled.SetActive(true);
        world.Update(.1f);
        Check(disabled.IsDestroyed);

        var probe = world.Spawn<KillProbe>();
        probe.Transform.Position = ordinary.Transform.Position;
        world.Update(.1f);
        world.Update(.1f);
        Check(probe.Kills == 1 && !probe.IsDestroyed);
        probe.Transform.Position = System.Numerics.Vector3.Zero;
        world.Update(.1f);
        probe.Transform.Position = ordinary.Transform.Position;
        world.Update(.1f);
        Check(probe.Kills == 2);
        probe.Respawn = true;
        probe.Transform.Position = System.Numerics.Vector3.Zero;
        world.Update(.1f);
        probe.Transform.Position = ordinary.Transform.Position;
        world.Update(.1f);
        Check(probe.Kills == 3 && probe.Transform.Position.Y == 0f);
        probe.Transform.Position = ordinary.Transform.Position;
        world.Update(.1f);
        Check(probe.Kills == 4);

        var editor = typeof(Application).GetProperty(nameof(Application.IsEditor))!;
        var wasEditor = Application.IsEditor;
        editor.SetValue(null, true);
        try
        {
            probe.Transform.Position = ordinary.Transform.Position;
            world.Update(.1f);
            Check(probe.Kills == 4);
        }
        finally
        {
            editor.SetValue(null, wasEditor);
        }
    }
    finally
    {
        world.Clear();
    }
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

public sealed class FailingCollider : SphereCollider
{
    public static FailingCollider Last = null!;
    public override void Create()
    {
        Last = this;
        base.Create();
        throw new InvalidOperationException("creation failed after registration");
    }
}

public sealed class OwnerDestroyingComponent : Component
{
    public static OwnerDestroyingComponent Last = null!;
    public override void Create() { Last = this; Owner.Destroy(); }
}

public sealed class FailingObject : GameObject
{
    public static FailingObject Last = null!;
    public Component Part = null!;
    public override void Create()
    {
        Last = this;
        Part = AddComponent<Component>();
        throw new InvalidOperationException("object creation failed");
    }
}

public sealed class ThrowingDestroyObject : GameObject
{
    public int Destroys;
    public Component Part = null!;
    public override void Create() => Part = AddComponent<Component>();
    public override void Destroy() { Destroys++; throw new InvalidOperationException("before base.Destroy"); }
}
public sealed class CountingRenderComponent : RenderComponent
{
    public int Renders;
    public override void Render(ProjectShockGL.Rendering.RenderQueue queue) => Renders++;
}

public sealed class DebugProbe : GameObject
{
    public int Draws;
    public int SelectedDraws;
    public int Starts;

    public override void Start() => Starts++;
    public override void DebugRender(ProjectShockGL.Rendering.DebugDraw draw) => Draws++;
    public override void DebugRenderSelected(ProjectShockGL.Rendering.DebugDraw draw) => SelectedDraws++;
}

public sealed class DebugPart : Component
{
    public int Draws;
    public int SelectedDraws;

    public override void DebugRender(ProjectShockGL.Rendering.DebugDraw draw) => Draws++;
    public override void DebugRenderSelected(ProjectShockGL.Rendering.DebugDraw draw) => SelectedDraws++;
}

public sealed class KillProbe : Entity
{
    public int Kills;
    public bool Respawn;

    public override void Kill()
    {
        Kills++;
        if (Respawn)
        {
            Transform.Position = System.Numerics.Vector3.Zero;
        }
    }
}
