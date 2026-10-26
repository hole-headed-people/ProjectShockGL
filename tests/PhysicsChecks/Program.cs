using System.Numerics;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Tools;
using Body = ProjectShockGL.Core.Object;

Physics.Initialize();

Run("Every query preserves defaults and consistently filters objects, colliders, triggers and layers", scene =>
{
    var owner = scene.Body(Vector3.Zero);
    var first = owner.AddComponent<SphereCollider>();
    var second = owner.AddComponent<SphereCollider>();
    second.Center = new Vector3(0, 0, 2);
    var trigger = scene.Sphere(new Vector3(0, 0, 4));
    trigger.IsTrigger = true;
    var solid = scene.Sphere(new Vector3(0, 0, 6));
    solid.Parent.Layer = 2;
    var combinations = new (PhysicsQueryFilter Filter, Collider[] Expected)[]
    {
        (default, [first, second, trigger, solid]),
        (new() { IgnoreObject = owner }, [trigger, solid]),
        (new() { IgnoreCollider = first }, [second, trigger, solid]),
        (new() { IgnoreTriggers = true }, [first, second, solid]),
        (new() { IgnoreObject = owner, IgnoreTriggers = true }, [solid]),
        (new() { IgnoreObject = owner, IgnoreCollider = solid, IgnoreTriggers = true }, [])
    };
    foreach (var (filter, expected) in combinations)
    {
        Check(Physics.OverlapSphereAll(Vector3.Zero, 20, -1, filter).SequenceEqual(expected));
        var sphereFound = Physics.OverlapSphere(Vector3.Zero, 20, -1, out var sphere, filter);
        var capsuleFound = Physics.OverlapCapsule(-Vector3.UnitY, Vector3.UnitY, 20, -1, out var capsule, filter);
        var rayFound = Physics.Raycast(new Vector3(0, 0, -3), Vector3.UnitZ, 30, -1, out var ray, filter);
        Check(sphereFound == (expected.Length > 0) && capsuleFound == sphereFound && rayFound == sphereFound);
        Check(sphere == expected.FirstOrDefault() && capsule == expected.FirstOrDefault());
        if (rayFound) Check(ray.Collider == expected[0] && ray.Object == expected[0].Parent);
    }
    Check(Physics.OverlapSphereAll(Vector3.Zero, 20, 2).SequenceEqual(new[] { solid }));
    Check(!Physics.OverlapSphere(Vector3.Zero, 20, 0, out _));
});

Run("Inactive, disabled and destroyed colliders are excluded from every query", scene =>
{
    var sphere = scene.Sphere(Vector3.Zero);
    void Missing()
    {
        Check(!Physics.OverlapSphere(Vector3.Zero, 2, -1, out _));
        Check(Physics.OverlapSphereAll(Vector3.Zero, 2, -1).Length == 0);
        Check(!Physics.OverlapCapsule(-Vector3.UnitY, Vector3.UnitY, 2, -1, out _));
        Check(!Physics.Raycast(new Vector3(-3, 0, 0), Vector3.UnitX, 10, -1, out _));
    }
    sphere.Parent.SetActive(false);
    Missing();
    sphere.Parent.SetActive(true);
    sphere.IsEnabled = false;
    Missing();
    sphere.IsEnabled = true;
    Check(Physics.OverlapSphere(Vector3.Zero, 2, -1, out _));
    sphere.Destroy();
    Missing();
});

Run("Duplicate registration and colliders on the same owner do not duplicate contacts", scene =>
{
    var owner = scene.Body(Vector3.Zero);
    var a = owner.AddComponent<SphereCollider>();
    owner.AddComponent<BoxCollider>();
    Physics.Register(a);
    Check(Physics.OverlapSphereAll(Vector3.Zero, 2, -1).Length == 2);
    Physics.Update();
    Check(owner.Events.Count == 0);
});

Run("Collider offsets and object bounds follow rotation, negative scale and translation", scene =>
{
    var owner = scene.Body(new Vector3(10, 20, 30));
    owner.Transform.Scale = new Vector3(-2, 3, 4);
    owner.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
    var sphere = owner.AddComponent<SphereCollider>();
    sphere.Center = new Vector3(1, 2, 3);
    Near(sphere.GetWorldCenter(), new Vector3(22, 26, 32));
    Check(sphere.WorldRadius == 2);
    var bounds = sphere.GetWorldBounds();
    Near(bounds.min, sphere.GetWorldCenter() - new Vector3(2));
    Near(bounds.max, sphere.GetWorldCenter() + new Vector3(2));
    Check(owner.GetWorldBounds() == bounds);
    Check(Physics.OverlapSphere(sphere.GetWorldCenter(), 0, -1, out var found) && found == sphere);
});

Run("Static collider bounds update when transforms, centers or dimensions change", scene =>
{
    var owner = scene.Body(Vector3.Zero);
    owner.IsStatic = true;
    var box = owner.AddComponent<BoxCollider>();
    Check(box.GetWorldBounds().min == -Vector3.One);
    owner.Transform.Position = new Vector3(5, 0, 0);
    box.Center = Vector3.UnitX;
    box.HalfExtents = new Vector3(2, 1, 1);
    Check(box.GetWorldBounds().min == new Vector3(4, -1, -1));
    Check(owner.GetWorldBounds().max == new Vector3(8, 1, 1));
    Check(!Physics.OverlapSphere(Vector3.Zero, 0.5f, -1, out _));
    Check(Physics.OverlapSphere(new Vector3(6, 0, 0), 0, -1, out _));
});

Run("Capsules support arbitrary normalized local axes and their perpendicular scale", scene =>
{
    var owner = scene.Body(Vector3.Zero);
    owner.Transform.Scale = new Vector3(2, 3, 4);
    var capsule = owner.AddComponent<CapsuleCollider>();
    capsule.LocalAxis = new Vector3(10, 0, 0);
    capsule.Height = 3;
    Check(capsule.LocalAxis == Vector3.UnitX && capsule.WorldRadius == 2 && capsule.WorldHeight == 6);
    var (a, b) = capsule.GetWorldSegment();
    Near(a, -Vector3.UnitX);
    Near(b, Vector3.UnitX);
    Check(capsule.GetWorldBounds().min == new Vector3(-3, -2, -2));
    var local = capsule.GetLocalBounds();
    Check(local.min == new Vector3(-1.5f, -0.5f, -0.5f));
});

Run("A zero-length capsule behaves as a sphere in overlaps and raycasts", scene =>
{
    var capsule = scene.Body(Vector3.Zero).AddComponent<CapsuleCollider>();
    capsule.Height = 0;
    capsule.Radius = 1;
    Check(capsule.GetWorldSegment() == (Vector3.Zero, Vector3.Zero));
    Check(Physics.OverlapSphere(new Vector3(1.5f, 0, 0), 0.5f, -1, out _));
    Check(Physics.OverlapCapsule(new Vector3(1.5f, 0, 0), new Vector3(1.5f, 0, 0), 0.5f, -1, out _));
    Check(Physics.Raycast(new Vector3(-3, 0, 0), Vector3.UnitX, 2, -1, out var hit));
    NearFloat(hit.Distance, 2);
    Near(hit.Normal, -Vector3.UnitX);
    Check(capsule.GetWorldBounds().min == -Vector3.One);
});

Run("Zero-size shapes and tiny or zero-length segments produce finite results", scene =>
{
    var closest = PhysicsHelper.ClosestPointOnSegment(Vector3.Zero, Vector3.Zero, Vector3.One);
    Check(closest == Vector3.Zero);
    closest = PhysicsHelper.ClosestPointOnSegment(Vector3.Zero, new Vector3(1e-6f, 0, 0), new Vector3(0.75e-6f, 1e-6f, 0));
    NearFloat(closest.X, 0.75e-6f, 1e-12f);
    var points = PhysicsHelper.ClosestPointsBetweenSegments(Vector3.Zero, Vector3.Zero, Vector3.One, Vector3.One);
    Check(points == (Vector3.Zero, Vector3.One));
    var sphere = scene.Sphere(Vector3.Zero, 0);
    Check(Physics.Raycast(-Vector3.UnitX, Vector3.UnitX, 1, -1, out var hit));
    Check(hit.Collider == sphere && PhysicsHelper.IsFinite(hit.Normal));
});

Run("Invalid query inputs return a clean miss; invalid shape settings are rejected", scene =>
{
    var sphere = scene.Sphere(Vector3.Zero);
    Check(!Physics.Raycast(Vector3.Zero, Vector3.Zero, 10, -1, out _));
    Check(!Physics.Raycast(Vector3.Zero, new Vector3(float.NaN), 10, -1, out _));
    Check(!Physics.Raycast(Vector3.Zero, Vector3.UnitX, -1, -1, out _));
    Check(!Physics.OverlapSphere(Vector3.Zero, -1, -1, out var result) && result == null);
    Check(Physics.OverlapSphereAll(new Vector3(float.PositiveInfinity), 1, -1).Length == 0);
    Check(!Physics.OverlapCapsule(Vector3.Zero, new Vector3(float.NaN), 1, -1, out _));
    Throws(() => sphere.Radius = -1);
    Throws(() => sphere.Center = new Vector3(float.NaN));
    var capsule = scene.Body(Vector3.Zero).AddComponent<CapsuleCollider>();
    Throws(() => capsule.LocalAxis = Vector3.Zero);
    Throws(() => capsule.Height = -1);
    Throws(() => scene.Body(Vector3.Zero).AddComponent<BoxCollider>().HalfExtents = new Vector3(-1));
});

Run("Sphere rays return surface exits from inside and world distances for nonunit directions", scene =>
{
    scene.Sphere(Vector3.Zero, 1);
    Check(Physics.Raycast(Vector3.Zero, new Vector3(5, 0, 0), 1, -1, out var hit));
    NearFloat(hit.Distance, 1);
    Near(hit.Normal, Vector3.UnitX);
    Check(!Physics.Raycast(Vector3.Zero, Vector3.UnitX, 0.99f, -1, out _));
    Check(Physics.Raycast(new Vector3(-2, 0, 0), new Vector3(1e-25f, 0, 0), 1, -1, out hit));
    NearFloat(hit.Distance, 1);
    Near(hit.Normal, -Vector3.UnitX);
});

Run("Raycasts starting inside boxes use the exit face normal", scene =>
{
    var box = scene.Body(Vector3.Zero).AddComponent<BoxCollider>();
    Check(Physics.Raycast(new Vector3(0.9f, 0, 0), new Vector3(1, 2, 0), 10, -1, out var hit));
    Near(hit.Normal, Vector3.UnitX);
    NearFloat(hit.Distance, 0.1f * MathF.Sqrt(5));
    box.Parent.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);
    Check(Physics.Raycast(new Vector3(0, 0.9f, 0), new Vector3(-2, 1, 0), 10, -1, out hit));
    Near(hit.Normal, Vector3.UnitY);
});

Run("Capsule rays hit between old sample points and respect maximum distance", scene =>
{
    var capsule = scene.Body(Vector3.Zero).AddComponent<CapsuleCollider>();
    capsule.Height = 10;
    capsule.Radius = 0.05f;
    Check(Physics.Raycast(new Vector3(-2, 0.3f, 0), Vector3.UnitX, 2, -1, out var hit));
    NearFloat(hit.Distance, 1.95f);
    Near(hit.Normal, -Vector3.UnitX);
    Check(!Physics.Raycast(new Vector3(-2, 0.3f, 0), Vector3.UnitX, 1.94f, -1, out _));
});

Run("Capsule rays handle axial, inside, cap and tangent intersections", scene =>
{
    var capsule = scene.Body(Vector3.Zero).AddComponent<CapsuleCollider>();
    capsule.Height = 4;
    capsule.Radius = 0.5f;
    Check(Physics.Raycast(new Vector3(0, 6, 0), -Vector3.UnitY, 10, -1, out var hit));
    NearFloat(hit.Distance, 4);
    Near(hit.Normal, Vector3.UnitY);
    Check(Physics.Raycast(Vector3.Zero, Vector3.UnitY, 10, -1, out hit));
    NearFloat(hit.Distance, 2);
    Near(hit.Normal, Vector3.UnitY);
    Check(Physics.Raycast(new Vector3(-2, 0, 0.5f), Vector3.UnitX, 10, -1, out hit));
    NearFloat(hit.Distance, 2);
    Near(hit.Normal, Vector3.UnitZ);
    Check(Physics.Raycast(new Vector3(-2, 2, 0), Vector3.UnitX, 10, -1, out hit));
    NearFloat(hit.Distance, 2);
    Near(hit.Normal, Vector3.UnitY);
});

Run("Capsule-box tests detect thin obstacles between samples and corner distances", scene =>
{
    var box = scene.Body(new Vector3(0, 0.37f, 0)).AddComponent<BoxCollider>();
    box.HalfExtents = new Vector3(0.05f, 0.02f, 0.05f);
    Check(Physics.OverlapCapsule(new Vector3(0, -10, 0), new Vector3(0, 10, 0), 0.01f, -1, out var hit) && hit == box);
    Check(Physics.OverlapCapsule(new Vector3(0, -10, 0), new Vector3(0, 10, 0), 0, -1, out _));
    box.Parent.Transform.Position = Vector3.Zero;
    box.HalfExtents = Vector3.One;
    Check(!Physics.OverlapCapsule(new Vector3(2, 2, -3), new Vector3(2, 2, 3), 1.4f, -1, out _));
    Check(Physics.OverlapCapsule(new Vector3(2, 2, -3), new Vector3(2, 2, 3), 1.42f, -1, out _));
    Check(Physics.OverlapCapsule(new Vector3(1.5f, -3, 0), new Vector3(1.5f, 3, 0), 0.5f, -1, out _));
    Check(!Physics.OverlapCapsule(new Vector3(1.501f, -3, 0), new Vector3(1.501f, 3, 0), 0.5f, -1, out _));
});

Run("Direct capsule-box intersections and overlap queries use the same rotated geometry", scene =>
{
    var box = scene.Body(Vector3.Zero).AddComponent<BoxCollider>();
    box.HalfExtents = new Vector3(0.1f, 0.01f, 0.1f);
    box.Parent.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.3f);
    var capsule = scene.Body(new Vector3(0, 0.37f, 0)).AddComponent<CapsuleCollider>();
    capsule.Height = 10;
    capsule.Radius = 0.02f;
    Check(capsule.Intersects(box) && box.Intersects(capsule));
    var (a, b) = capsule.GetWorldSegment();
    Check(Physics.OverlapCapsule(a, b, capsule.WorldRadius, -1, out var hit,
        new PhysicsQueryFilter { IgnoreObject = capsule.Parent }) && hit == box);
});

Run("Segment-box minimization agrees with dense independent samples", scene =>
{
    var random = new Random(17);
    for (var sample = 0; sample < 150; sample++)
    {
        Vector3 RandomPoint() => new((float)random.NextDouble() * 6 - 3,
            (float)random.NextDouble() * 6 - 3, (float)random.NextDouble() * 6 - 3);
        var a = RandomPoint();
        var b = RandomPoint();
        var exact = PhysicsHelper.SegmentAabbDistanceSquared(a, b, Vector3.One);
        var sampled = double.PositiveInfinity;
        for (var i = 0; i <= 1000; i++)
        {
            var point = Vector3.Lerp(a, b, i / 1000f);
            sampled = Math.Min(sampled, Vector3.DistanceSquared(point, Vector3.Clamp(point, -Vector3.One, Vector3.One)));
        }
        Check(exact >= 0 && exact <= sampled + 1e-5 && sampled - exact < 0.002);
    }
});

Run("Collision enter/exit are delivered once and disabled objects stop colliding", scene =>
{
    var a = scene.Sphere(Vector3.Zero);
    var b = scene.Sphere(Vector3.Zero);
    var ownerA = (EventBody)a.Parent;
    var ownerB = (EventBody)b.Parent;
    Physics.Update();
    Physics.Update();
    Check(ownerA.Events.SequenceEqual(new[] { "enter" }) && ownerB.Events.SequenceEqual(new[] { "enter" }));
    ownerB.SetActive(false);
    Physics.Update();
    Physics.Update();
    Check(ownerA.Events.SequenceEqual(new[] { "enter", "exit" }) && ownerB.Events.SequenceEqual(new[] { "enter", "exit" }));
    ownerB.SetActive(true);
    Physics.Update();
    Check(ownerA.Events.Count == 3);
});

Run("Changing trigger mode ends the old event type before starting the new type", scene =>
{
    var a = scene.Sphere(Vector3.Zero);
    var b = scene.Sphere(Vector3.Zero);
    Physics.Update();
    b.IsTrigger = true;
    Physics.Update();
    b.IsTrigger = false;
    Physics.Update();
    Check(((EventBody)a.Parent).Events.SequenceEqual(new[] { "enter", "exit", "trigger-enter", "trigger-exit", "enter" }));
});

Run("Destroying the other object inside enter skips dead callbacks and balances the survivor", scene =>
{
    var a = scene.Sphere(Vector3.Zero);
    var b = scene.Sphere(Vector3.Zero);
    var ownerA = (EventBody)a.Parent;
    var ownerB = (EventBody)b.Parent;
    ownerA.Enter = other => scene.World.Destroy(other);
    Physics.Update();
    Physics.Update();
    Check(ownerA.Events.SequenceEqual(new[] { "enter", "exit" }));
    Check(ownerB.IsDestroyed && ownerB.Events.Count == 0);
});

Run("New colliders created by callbacks participate on the next step", scene =>
{
    var a = (EventBody)scene.Sphere(Vector3.Zero).Parent;
    var b = (EventBody)scene.Sphere(Vector3.Zero).Parent;
    EventBody? added = null;
    a.Enter = _ => { if (added == null) added = (EventBody)scene.Sphere(Vector3.Zero).Parent; };
    Physics.Update();
    Check(added != null && added.Events.Count == 0 && a.Events.Count == 1 && b.Events.Count == 1);
    Physics.Update();
    Check(added!.Events.Count == 2 && a.Events.Count == 2 && b.Events.Count == 2);
});

Run("Unregistering in callbacks and recursive Update do not corrupt contact iteration", scene =>
{
    var a = scene.Sphere(Vector3.Zero);
    var b = scene.Sphere(Vector3.Zero);
    var ownerA = (EventBody)a.Parent;
    ownerA.Enter = _ => { Physics.Update(); Physics.Unregister(a); };
    Physics.Update();
    Check(ownerA.Events.SequenceEqual(new[] { "enter", "exit" }));
    Check(((EventBody)b.Parent).Events.Count == 0);
    ownerA.Enter = null;
    Physics.Register(a);
    Physics.Update();
    Check(ownerA.Events.Count == 3 && ((EventBody)b.Parent).Events.Count == 1);
});

Run("Clearing the world from enter/exit handlers is safe", scene =>
{
    var a = (EventBody)scene.Sphere(Vector3.Zero).Parent;
    scene.Sphere(Vector3.Zero);
    a.Enter = _ => scene.World.Clear();
    Physics.Update();
    Check(scene.World.GetAllObjects(true).Length == 0);
    var c = (EventBody)scene.Sphere(Vector3.Zero).Parent;
    var d = scene.Sphere(Vector3.Zero);
    Physics.Update();
    c.Exit = _ => scene.World.Clear();
    d.Parent.Transform.Position = new Vector3(10);
    Physics.Update();
    Check(scene.World.GetAllObjects(true).Length == 0);
});

Run("Layer masks and static-static suppression are preserved for contact events", scene =>
{
    var a = (EventBody)scene.Sphere(Vector3.Zero).Parent;
    var b = (EventBody)scene.Sphere(Vector3.Zero).Parent;
    a.CollidesWith = 0;
    Physics.Update();
    Check(a.Events.Count == 0 && b.Events.Count == 0);
    a.CollidesWith = -1;
    a.IsStatic = b.IsStatic = true;
    Physics.Update();
    Check(a.Events.Count == 0 && b.Events.Count == 0);
});

Run("Controller moves by displacement, ignores itself and triggers, and has no automatic gravity", scene =>
{
    var owner = scene.Body(Vector3.Zero);
    var controller = owner.AddComponent<CharacterController>();
    scene.Sphere(Vector3.UnitX).IsTrigger = true;
    var flags = controller.Move(new Vector3(2, 3, 4));
    Near(owner.Transform.Position, new Vector3(2, 3, 4));
    Near(controller.LastDisplacement, new Vector3(2, 3, 4));
    Check(flags == CollisionFlags.None && !controller.IsGrounded && owner.Collider == controller);
    controller.Move(Vector3.Zero);
    Near(owner.Transform.Position, new Vector3(2, 3, 4));
});

Run("Controller lands, stays grounded while sliding, and leaves the floor upwards", scene =>
{
    var floor = scene.Body(new Vector3(0, -0.5f, 0)).AddComponent<BoxCollider>();
    floor.HalfExtents = new Vector3(20, 0.5f, 20);
    var owner = scene.Body(new Vector3(0, 3, 0));
    var controller = owner.AddComponent<CharacterController>();
    var flags = controller.Move(new Vector3(0, -5, 0));
    Check((flags & CollisionFlags.Below) != 0 && controller.IsGrounded);
    NearFloat(owner.Transform.Position.Y, 0.99f, 0.001f);
    controller.Move(new Vector3(2, 0, 0));
    Check(controller.IsGrounded);
    NearFloat(owner.Transform.Position.X, 2, 0.001f);
    controller.Move(Vector3.Zero);
    Check(controller.IsGrounded && controller.CollisionFlags == CollisionFlags.None);
    controller.Move(Vector3.UnitY);
    Check(!controller.IsGrounded);
});

Run("Controller stops at thin walls, slides along them and reports the ceiling", scene =>
{
    var wall = scene.Body(new Vector3(2, 0, 0)).AddComponent<BoxCollider>();
    wall.HalfExtents = new Vector3(0.005f, 20, 20);
    var owner = scene.Body(Vector3.Zero);
    var controller = owner.AddComponent<CharacterController>();
    var flags = controller.Move(new Vector3(10, 0, 3));
    Check((flags & CollisionFlags.Sides) != 0);
    NearFloat(owner.Transform.Position.X, 1.505f, 0.001f);
    NearFloat(owner.Transform.Position.Z, 3, 0.001f);
    var ceiling = scene.Body(new Vector3(0, 3, 0)).AddComponent<BoxCollider>();
    ceiling.HalfExtents = new Vector3(20, 0.1f, 20);
    flags = controller.Move(new Vector3(0, 10, 0));
    Check((flags & CollisionFlags.Above) != 0 && !controller.IsGrounded);
    NearFloat(owner.Transform.Position.Y, 1.91f, 0.001f);
});

Run("Controller respects masks, inactive obstacles, scaled offset capsules and disabled lifecycle", scene =>
{
    var owner = scene.Body(Vector3.Zero);
    var controller = owner.AddComponent<CharacterController>();
    controller.Center = new Vector3(1, 0, 0);
    owner.Transform.Scale = new Vector3(2);
    var wall = scene.Body(new Vector3(5, 0, 0)).AddComponent<BoxCollider>();
    wall.Parent.Layer = 2;
    controller.CollisionMask = 1;
    controller.Move(Vector3.UnitX);
    NearFloat(owner.Transform.Position.X, 1);
    controller.CollisionMask = -1;
    var flags = controller.Move(Vector3.UnitX * 3);
    Check((flags & CollisionFlags.Sides) != 0);
    NearFloat(owner.Transform.Position.X, 1.01f, 0.001f);
    wall.Parent.SetActive(false);
    controller.Move(Vector3.UnitX);
    NearFloat(owner.Transform.Position.X, 2.01f, 0.001f);
    controller.IsEnabled = false;
    controller.Move(Vector3.UnitX);
    NearFloat(owner.Transform.Position.X, 2.01f, 0.001f);
    Check(!controller.IsGrounded && controller.LastDisplacement == Vector3.Zero);
});

Run("Controller rejects invalid movement and excessive work without changing position", scene =>
{
    var owner = scene.Body(Vector3.Zero);
    var controller = owner.AddComponent<CharacterController>();
    Throws(() => controller.Move(new Vector3(float.NaN, 0, 0)));
    Throws(() => controller.Move(new Vector3(1000000, 0, 0)));
    Throws(() => controller.SkinWidth = -1);
    Near(owner.Transform.Position, Vector3.Zero);
});

Run("Controller climbs small stairs but rejects tall stairs and stairs without headroom", scene =>
{
    var floor = scene.Body(new Vector3(0, -0.5f, 0)).AddComponent<BoxCollider>();
    floor.HalfExtents = new Vector3(20, 0.5f, 20);
    var stair = scene.Body(new Vector3(2, 0.1f, 0)).AddComponent<BoxCollider>();
    stair.HalfExtents = new Vector3(1, 0.1f, 2);
    var owner = scene.Body(new Vector3(0, 1, 0));
    var controller = owner.AddComponent<CharacterController>();
    for (var i = 0; i < 40; i++) controller.Move(new Vector3(0.05f, -0.01f, 0));
    Check(owner.Transform.Position.X > 1.9f && controller.IsGrounded);
    NearFloat(owner.Transform.Position.Y, 1.19f, 0.002f);
    owner.Transform.Position = new Vector3(0, 1, 0);
    stair.HalfExtents = new Vector3(1, 0.3f, 2);
    stair.Parent.Transform.Position = new Vector3(2, 0.3f, 0);
    controller.Move(new Vector3(2, -0.01f, 0));
    Check(owner.Transform.Position.X < 1 && owner.Transform.Position.Y < 1.01f);
    stair.HalfExtents = new Vector3(1, 0.1f, 2);
    stair.Parent.Transform.Position = new Vector3(2, 0.1f, 0);
    owner.Transform.Position = new Vector3(0, 1, 0);
    var ceiling = scene.Body(new Vector3(0, 2.1f, 0)).AddComponent<BoxCollider>();
    ceiling.HalfExtents = new Vector3(10, 0.05f, 10);
    controller.Move(new Vector3(2, -0.01f, 0));
    Check(owner.Transform.Position.X < 1);
});

Run("Controller follows walkable slopes and rejects steep slopes regardless of stair setting", scene =>
{
    var ramp = scene.Body(Vector3.Zero).AddComponent<BoxCollider>();
    ramp.HalfExtents = new Vector3(10, 0.5f, 4);
    ramp.Parent.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 6);
    var owner = scene.Body(new Vector3(-2, 5, 0));
    var controller = owner.AddComponent<CharacterController>();
    controller.StepOffset = 0;
    controller.Move(new Vector3(0, -10, 0));
    Check(controller.IsGrounded);
    var before = owner.Transform.Position;
    for (var i = 0; i < 40; i++) controller.Move(new Vector3(0.05f, -0.01f, 0));
    Check(owner.Transform.Position.X > before.X + 1.9f && controller.IsGrounded);
    NearFloat(owner.Transform.Position.Y - before.Y, 2 * MathF.Tan(MathF.PI / 6), 0.01f);
    controller.SlopeLimit = 20;
    controller.StepOffset = 0.3f;
    before = owner.Transform.Position;
    for (var i = 0; i < 20; i++) controller.Move(new Vector3(0.05f, -0.01f, 0));
    Check(owner.Transform.Position.X < before.X + 0.05f && !controller.IsGrounded);
});

Run("Player-sized capsule negotiates stairs up to StepOffset with small frame movements", scene =>
{
    var floor = scene.Body(new Vector3(0, -0.5f, 0)).AddComponent<BoxCollider>();
    floor.HalfExtents = new Vector3(20, 0.5f, 20);
    var stair = scene.Body(Vector3.Zero).AddComponent<BoxCollider>();
    var owner = scene.Body(new Vector3(0, 1, 0));
    var controller = owner.AddComponent<CharacterController>();
    controller.Radius = 0.3f;
    foreach (var height in new[] { 0.1f, 0.2f, 0.29f, 0.3f })
    {
        stair.Parent.Transform.Position = new Vector3(2, height / 2, 0);
        stair.HalfExtents = new Vector3(1, height / 2, 2);
        owner.Transform.Position = new Vector3(0, 1, 0);
        for (var i = 0; i < 50; i++) controller.Move(new Vector3(0.04f, -0.003f, 0));
        if (owner.Transform.Position.X < 1.9f)
            throw new Exception($"Stair {height}: {owner.Transform.Position}, grounded={controller.IsGrounded}");
        Check(controller.IsGrounded);
    }
});

Run("Stair climbing can be disabled and never lifts an airborne or jumping character", scene =>
{
    var floor = scene.Body(new Vector3(0, -0.5f, 0)).AddComponent<BoxCollider>();
    floor.HalfExtents = new Vector3(20, 0.5f, 20);
    var stair = scene.Body(new Vector3(2, 0.1f, 0)).AddComponent<BoxCollider>();
    stair.HalfExtents = new Vector3(1, 0.1f, 2);
    var owner = scene.Body(new Vector3(0, 1, 0));
    var controller = owner.AddComponent<CharacterController>();
    controller.Radius = 0.3f;
    controller.StepOffset = 0;
    controller.Move(new Vector3(2, -0.01f, 0));
    Check(owner.Transform.Position.X < 1 && owner.Transform.Position.Y < 1.01f);
    controller.StepOffset = 0.3f;
    owner.Transform.Position = new Vector3(0, 1.05f, 0);
    controller.Move(new Vector3(2, 0, 0));
    NearFloat(owner.Transform.Position.Y, 1.05f);
    Check(owner.Transform.Position.X < 1 && !controller.IsGrounded);
    owner.Transform.Position = new Vector3(0, 1, 0);
    controller.Move(new Vector3(2, 0.05f, 0));
    NearFloat(owner.Transform.Position.Y, 1.05f);
    Check(owner.Transform.Position.X < 1 && !controller.IsGrounded);
});

Run("Controller follows stairs down from support but does not snap across a large drop", scene =>
{
    var floor = scene.Body(new Vector3(0, -0.5f, 0)).AddComponent<BoxCollider>();
    floor.HalfExtents = new Vector3(20, 0.5f, 20);
    var stair = scene.Body(new Vector3(2, 0.1f, 0)).AddComponent<BoxCollider>();
    stair.HalfExtents = new Vector3(1, 0.1f, 2);
    var owner = scene.Body(new Vector3(2, 1.2f, 0));
    var controller = owner.AddComponent<CharacterController>();
    controller.Radius = 0.3f;
    controller.Move(new Vector3(0, -0.02f, 0));
    for (var i = 0; i < 50; i++) controller.Move(new Vector3(-0.04f, 0, 0));
    Check(controller.IsGrounded);
    NearFloat(owner.Transform.Position.Y, 0.99f, 0.002f);
    stair.HalfExtents = new Vector3(1, 1, 2);
    stair.Parent.Transform.Position = new Vector3(2, 1, 0);
    owner.Transform.Position = new Vector3(2, 3, 0);
    controller.Move(new Vector3(0, -0.02f, 0));
    controller.Move(new Vector3(-2, 0, 0));
    Check(!controller.IsGrounded && owner.Transform.Position.Y > 2.5f);
});

Run("GroundNormal describes the slope and descent keeps contact without applying gravity", scene =>
{
    var ramp = scene.Body(Vector3.Zero).AddComponent<BoxCollider>();
    ramp.HalfExtents = new Vector3(5, 0.5f, 5);
    ramp.Parent.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 6);
    var owner = scene.Body(new Vector3(0, 5, 0));
    var controller = owner.AddComponent<CharacterController>();
    controller.Move(new Vector3(0, -10, 0));
    Near(controller.GroundNormal, Vector3.Transform(Vector3.UnitY, ramp.Parent.Transform.Rotation));
    var before = owner.Transform.Position;
    for (var i = 0; i < 40; i++) controller.Move(new Vector3(0, 0, 0.05f));
    Check(controller.IsGrounded);
    NearFloat(owner.Transform.Position.Y - before.Y, -2 * MathF.Tan(MathF.PI / 6), 0.01f);
});

Run("Controller validates stair height and slope angles", scene =>
{
    var controller = scene.Body(Vector3.Zero).AddComponent<CharacterController>();
    Throws(() => controller.StepOffset = -0.1f);
    Throws(() => controller.StepOffset = float.PositiveInfinity);
    Throws(() => controller.SlopeLimit = -1);
    Throws(() => controller.SlopeLimit = 90);
    Throws(() => controller.SlopeLimit = float.NaN);
    controller.SlopeLimit = 0;
    controller.SlopeLimit = 89;
});

Console.WriteLine("All physics checks passed.");

static void Run(string name, Action<Scene> test)
{
    using var scene = new Scene();
    test(scene);
    Console.WriteLine($"PASS: {name}");
}
static void Check(bool condition) { if (!condition) throw new Exception("Physics check failed"); }
static void NearFloat(float actual, float expected, float tolerance = 0.0002f) =>
    Check(float.IsFinite(actual) && MathF.Abs(actual - expected) <= tolerance);
static void Near(Vector3 actual, Vector3 expected) => Check(PhysicsHelper.IsFinite(actual) && Vector3.Distance(actual, expected) < 0.0002f);
static void Throws(Action action)
{
    try { action(); }
    catch (ArgumentOutOfRangeException) { return; }
    throw new Exception("Expected invalid shape settings to be rejected");
}

public sealed class Scene : IDisposable
{
    public World World { get; } = new();
    public EventBody Body(Vector3 position)
    {
        var body = World.Spawn<EventBody>();
        body.Transform.Position = position;
        return body;
    }
    public SphereCollider Sphere(Vector3 position, float radius = 0.5f)
    {
        var collider = Body(position).AddComponent<SphereCollider>();
        collider.Radius = radius;
        return collider;
    }
    public void Dispose() { World.Clear(); Physics.Update(); }
}

public sealed class EventBody : Body
{
    public List<string> Events { get; } = [];
    public Action<Body>? Enter, Exit;
    public override void OnCollisionEnter(Body other) { Events.Add("enter"); Enter?.Invoke(other); }
    public override void OnCollisionExit(Body other) { Events.Add("exit"); Exit?.Invoke(other); }
    public override void OnTriggerEnter(Body other) { Events.Add("trigger-enter"); Enter?.Invoke(other); }
    public override void OnTriggerExit(Body other) { Events.Add("trigger-exit"); Exit?.Invoke(other); }
}

