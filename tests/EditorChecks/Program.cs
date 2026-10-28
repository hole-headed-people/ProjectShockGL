using System.Numerics;
using Newtonsoft.Json;
using ProjectShockGL;
using ProjectShockGL.Core;
using Color = ProjectShockGL.Rendering.Color;
using ProjectShockGL.Tools;
using ProjectShockGLEditor;
using GameObject = ProjectShockGL.Core.Object;
using Game = ProjectShockGL.Core.Game;
using Environment = ProjectShockGL.Environment;

var testRoot = Path.Combine(AppContext.BaseDirectory, "EditorCheckData");
Directory.CreateDirectory(Path.Combine(testRoot, "Entities"));
Directory.CreateDirectory(Path.Combine(testRoot, "Geometry"));
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Settings.ini"),
    $"[Game]\nClass=CheckGame, EditorChecks\n[Content]\nCustomPath={testRoot}\n");
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Layers.ini"), "[Layers]\n0=Default\n");
File.WriteAllText(Path.Combine(testRoot, "Entities", "Check.json"),
    "{\"Class\":\"CheckEntity, EditorChecks\",\"Parameters\":{}}");
File.WriteAllText(Path.Combine(testRoot, "Entities", "Broken.json"),
    "{\"Class\":\"BrokenEntity, EditorChecks\",\"Parameters\":{}}");
File.WriteAllText(Path.Combine(testRoot, "Entities", "Rejecting.json"),
    "{\"Class\":\"RejectingEntity, EditorChecks\",\"Parameters\":{}}");
File.WriteAllText(Path.Combine(testRoot, "Entities", "Light.json"),
    "{\"Class\":\"ProjectShockGL.Core.Light, ProjectShockGL\",\"Parameters\":{}}");
File.WriteAllText(Path.Combine(testRoot, "Geometry", "Box.json"),
    "{\"Model\":\"Missing\",\"Texture\":\"Missing\",\"ColliderType\":\"Box\",\"Size\":[1,1,1]}");
Settings.Initialize();
Content.Initialize(null!); // Fixture assets deliberately contain no GPU resources.
Game.Initialize();
((CheckGame)Game.Instance).LoadForTest();

Run("Projection and ray agree for a perspective camera", () =>
{
    var size = new Vector2(1280, 768);
    var vp = Matrix4x4.CreateLookAt(new Vector3(3, 2, 5), Vector3.Zero, Vector3.UnitY) *
             Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, size.X / size.Y, 0.1f, 100);
    var point = new Vector3(0.3f, -0.2f, 0);
    Check(GizmoMath.Project(point, vp, size, out var pixel));
    Check(GizmoMath.ScreenRay(pixel, size, vp, out var origin, out var direction));
    Check(GizmoMath.RayPlane(origin, direction, Vector3.Zero, Vector3.UnitZ, out var hit));
    Check(Vector3.Distance(point, hit) < 0.001f);
    Check(!GizmoMath.Project(new Vector3(6, 4, 10), vp, size, out _));
});

Run("Orthographic rays, degenerate handles and parallel planes are stable", () =>
{
    var size = new Vector2(800, 600);
    var vp = Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY) *
             Matrix4x4.CreateOrthographic(8, 6, 0.1f, 100);
    Check(GizmoMath.ScreenRay(new Vector2(500, 200), size, vp, out var origin, out var direction));
    Check(GizmoMath.RayPlane(origin, direction, Vector3.Zero, Vector3.UnitZ, out var hit));
    Check(Vector3.Distance(hit, new Vector3(1, 1, 0)) < 0.001f);
    Check(!GizmoMath.RayPlane(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitY, out _));
    Check(GizmoMath.SegmentDistance(Vector2.One, Vector2.Zero, Vector2.Zero) > 1);
    Check(!GizmoMath.ScreenRay(Vector2.Zero, Vector2.Zero, vp, out _, out _));
});

Run("Rotation supports all axes and snapping has predictable steps", () =>
{
    Check(MathF.Abs(GizmoMath.SignedAngle(Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX) - MathF.PI / 2) < 0.001f);
    Check(MathF.Abs(GizmoMath.SignedAngle(Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY) - MathF.PI / 2) < 0.001f);
    Check(MathF.Abs(GizmoMath.SignedAngle(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ) - MathF.PI / 2) < 0.001f);
    Check(GizmoMath.Snap(1.24f, 0.5f) == 1 && GizmoMath.Snap(-1.24f, 0.5f) == -1);
});

Run("A transform edit undoes/redoes as one action and new edits clear redo", () =>
{
    UndoRedo.Clear();
    var obj = new GameObject();
    var before = TransformState.Capture(obj.Transform);
    obj.Transform.Position = Vector3.One;
    obj.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(0.2f, 0.4f, 0.6f);
    obj.Transform.Scale = new Vector3(2, 3, 4);
    var after = TransformState.Capture(obj.Transform);
    UndoRedo.Register(new PropertyChangeAction<TransformState>(value => value.Apply(obj.Transform), before, after));
    UndoRedo.Undo();
    Check(TransformState.Capture(obj.Transform) == before);
    UndoRedo.Redo();
    Check(TransformState.Capture(obj.Transform) == after);
    UndoRedo.Undo();
    UndoRedo.Register(new PropertyChangeAction<int>(_ => { }, 0, 1));
    Check(!UndoRedo.CanRedo);
    UndoRedo.Clear();
});

Run("Dragging each translation arrow follows its world axis and supports undo", () =>
{
    var size = new Vector2(1280, 768);
    var eye = new Vector3(5, 4, 6);
    var forward = Vector3.Normalize(-eye);
    var vp = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY) *
             Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, size.X / size.Y, 0.1f, 100);
    var axes = new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
    ObjectManipulator.Mode = ManipulationMode.Move;
    ObjectManipulator.Local = false;
    for (var i = 0; i < axes.Length; i++)
    {
        UndoRedo.Clear();
        var target = new GameObject();
        target.Transform.Position = new Vector3(0.2f, 0.3f, -0.1f);
        var before = TransformState.Capture(target.Transform);
        Check(GizmoMath.Project(before.Position + axes[i], vp, size, out var start));
        Check(GizmoMath.Project(before.Position + axes[i] * 1.6f, vp, size, out var end));
        ObjectManipulator.BeginDrag(target, i, start, 2, forward, vp, size);
        Check(ObjectManipulator.IsDragging);
        ObjectManipulator.ApplyDrag(end, vp, size, false);
        Check(Vector3.Distance(target.Transform.Position, before.Position + axes[i] * 0.6f) < 0.002f);
        ObjectManipulator.Commit();
        UndoRedo.Undo();
        Check(TransformState.Capture(target.Transform) == before);
        UndoRedo.Redo();
        Check(Vector3.Distance(target.Transform.Position, before.Position + axes[i] * 0.6f) < 0.002f);
    }
    UndoRedo.Clear();
});

Run("Rotation rings rotate around X, Y and Z without frame-time dependence", () =>
{
    var size = new Vector2(1280, 768);
    var eye = new Vector3(5, 4, 6);
    var vp = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY) *
             Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, size.X / size.Y, 0.1f, 100);
    var axes = new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
    var from = new[] { Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX };
    var to = new[] { Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY };
    ObjectManipulator.Mode = ManipulationMode.Rotate;
    ObjectManipulator.Local = false;
    for (var i = 0; i < 3; i++)
    {
        var target = new GameObject();
        Check(GizmoMath.Project(from[i], vp, size, out var start));
        Check(GizmoMath.Project(to[i], vp, size, out var end));
        ObjectManipulator.BeginDrag(target, i, start, 1, Vector3.Normalize(-eye), vp, size);
        Check(ObjectManipulator.IsDragging);
        ObjectManipulator.ApplyDrag(end, vp, size, false);
        var expected = Quaternion.CreateFromAxisAngle(axes[i], MathF.PI / 2);
        Check(MathF.Abs(Quaternion.Dot(target.Transform.Rotation, expected)) > 0.9999f);
        ObjectManipulator.Cancel();
        Check(target.Transform.Rotation == Quaternion.Identity);
    }
});

Run("Scale handles use local axes; uniform scale and cancel preserve the original transform", () =>
{
    var size = new Vector2(1280, 768);
    var eye = new Vector3(5, 4, 6);
    var vp = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY) *
             Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, size.X / size.Y, 0.1f, 100);
    var target = new GameObject();
    target.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.4f);
    target.Transform.Scale = new Vector3(2, 3, 4);
    var before = TransformState.Capture(target.Transform);
    var axis = Vector3.Transform(Vector3.UnitX, before.Rotation);
    ObjectManipulator.Mode = ManipulationMode.Scale;
    Check(GizmoMath.Project(axis, vp, size, out var start));
    Check(GizmoMath.Project(axis * 2, vp, size, out var end));
    ObjectManipulator.BeginDrag(target, 0, start, 2, Vector3.Normalize(-eye), vp, size);
    Check(ObjectManipulator.IsDragging);
    ObjectManipulator.ApplyDrag(end, vp, size, false);
    Check(Vector3.Distance(target.Transform.Scale, new Vector3(3, 3, 4)) < 0.002f);
    ObjectManipulator.Cancel();
    Check(TransformState.Capture(target.Transform) == before);
    ObjectManipulator.BeginDrag(target, 3, Vector2.Zero, 2, Vector3.Normalize(-eye), vp, size);
    ObjectManipulator.ApplyDrag(new Vector2(100, 0), vp, size, false);
    Check(Vector3.Distance(target.Transform.Scale, before.Scale * MathF.E) < 0.002f);
    ObjectManipulator.Cancel();
    ObjectManipulator.Mode = ManipulationMode.Move;
});

Run("Saving includes pending objects, skips runtime objects and copies output values", () =>
{
    Game.Instance.World.Clear();
    var entity = (CheckEntity)Entity.Create("Check")!;
    entity.Name = "switch_1";
    entity.Number = 42;
    entity.Outputs.Add(new OutputData { Event = "Use", TargetName = "door_1", Input = "Open", Parameter = "1" });
    Game.Instance.World.Spawn<GameObject>();
    var level = EditorLevelManager.CaptureCurrentLevel();
    Check(level.Placeables.Length == 1 && level.Placeables[0].Name == "switch_1");
    entity.Outputs[0].Parameter = "2";
    Check(level.Placeables[0].Outputs[0].Parameter == "1");
    Check((int)level.Placeables[0].Properties["Number"] == 42);
});

Run("Map roundtrip restores transforms, editable properties, outputs and geometry names", () =>
{
    var level = new Level { Placeables = [
        new Placeable { Path = "Check", Type = PlaceableType.Entity, Name = "panel_1",
            Position = new Vector3(2, 3, 4), Rotation = Quaternion.CreateFromYawPitchRoll(0.4f, 0.2f, 0.1f),
            Scale = new Vector3(2), Properties = new() { ["Number"] = 27 },
            Outputs = [new OutputData { Event = "Use", TargetName = "door", Input = "Open" }] },
        new Placeable { Path = "Box", Type = PlaceableType.Geometry, Name = "floor" }
    ] };
    var path = Path.Combine(testRoot, "roundtrip.map");
    EditorLevelManager.SaveLevel(level, path);
    EditorLevelManager.OpenLevel(path);
    var objects = Game.Instance.World.GetAllObjects(true);
    Check(objects.Length == 2 && objects[1].Name == "floor");
    var entity = (CheckEntity)objects[0];
    Check(entity.Number == 27 && entity.Name == "panel_1" && entity.Outputs[0].TargetName == "door");
    Check(entity.Transform.Position == new Vector3(2, 3, 4));
    Check(Vector3.Distance(entity.Transform.Scale, new Vector3(2)) < 0.001f);
    Check(MathF.Abs(Quaternion.Dot(entity.Transform.Rotation, level.Placeables[0].Rotation)) > 0.9999f);
    Check(!EditorLevelManager.IsDirty && !UndoRedo.CanUndo);
});

Run("A failed map load keeps the old scene and releases staged colliders", () =>
{
    var previous = Game.Instance.World.GetAllObjects(true);
    var count = Physics.OverlapSphereAll(Vector3.Zero, 100, Layers.Everything).Length;
    Throws(() => LevelManager.Load(new Level { Placeables = [
        new Placeable { Path = "Check", Type = PlaceableType.Entity },
        new Placeable { Path = "Unknown", Type = PlaceableType.Entity }
    ] }));
    Check(Game.Instance.World.GetAllObjects(true).SequenceEqual(previous));
    Check(previous.All(obj => !obj.IsDestroyed));
    Check(Physics.OverlapSphereAll(Vector3.Zero, 100, Layers.Everything).Length == count);
});

Run("Failure inside Create releases partially constructed objects", () =>
{
    var previous = Game.Instance.World.GetAllObjects(true);
    var count = Physics.OverlapSphereAll(Vector3.Zero, 100, Layers.Everything).Length;
    Throws(() => LevelManager.Load(new Level { Placeables = [new Placeable { Path = "Broken", Type = PlaceableType.Entity }] }));
    Check(Game.Instance.World.GetAllObjects(true).SequenceEqual(previous));
    Check(Physics.OverlapSphereAll(Vector3.Zero, 100, Layers.Everything).Length == count);
});

Run("Invalid editable data or transform does not clear the current scene", () =>
{
    var previous = Game.Instance.World.GetAllObjects(true);
    Throws(() => LevelManager.Load(new Level { Placeables = [new Placeable { Path = "Check", Type = PlaceableType.Entity,
        Properties = new() { ["Number"] = "invalid" } }] }));
    Throws(() => LevelManager.Load(new Level { Placeables = [new Placeable { Path = "Check", Type = PlaceableType.Entity,
        Rotation = default }] }));
    Check(Game.Instance.World.GetAllObjects(true).SequenceEqual(previous));
});

Run("Serialization failure leaves an existing map untouched", () =>
{
    var path = Path.Combine(testRoot, "protected.map");
    File.WriteAllText(path, "previous contents");
    Throws(() => EditorLevelManager.SaveLevel(new Level { Placeables = [new Placeable { Properties = new() {
        ["Bad"] = new ThrowingValue() } }] }, path));
    Check(File.ReadAllText(path) == "previous contents");
    Check(Directory.GetFiles(testRoot, "*.tmp").Length == 0);
});

Run("Dirty state reflects edits, undo, successful save, and empty maps", () =>
{
    var obj = Game.Instance.World.GetAllObjects(true)[0];
    var before = TransformState.Capture(obj.Transform);
    obj.Transform.Position += Vector3.One;
    EditorLevelManager.RefreshDirty();
    Check(EditorLevelManager.IsDirty);
    before.Apply(obj.Transform);
    EditorLevelManager.RefreshDirty();
    Check(!EditorLevelManager.IsDirty);
    EditorLevelManager.SaveCurrentLevel(Path.Combine(testRoot, "saved.map"));
    Check(!EditorLevelManager.IsDirty);
    LevelManager.Load(new Level());
    Check(Game.Instance.World.GetAllObjects(true).Length == 0);
});

Run("Malformed JSON does not change the document path or scene", () =>
{
    var previousPath = EditorLevelManager.CurrentPath;
    var path = Path.Combine(testRoot, "broken.map");
    File.WriteAllText(path, "{ definitely invalid json");
    Throws(() => EditorLevelManager.OpenLevel(path));
    Check(EditorLevelManager.CurrentPath == previousPath);
});

Run("Environment is captured independently and survives a saved map roundtrip", () =>
{
    Environment.Apply(new EnvironmentData
    {
        AmbientColor = new(0.15f, 0.25f, 0.35f), IsFogEnabled = true,
        FogColor = new(0.45f, 0.55f, 0.65f), FogNear = 4, FogFar = 45
    });
    var expected = Environment.Capture();
    var snapshot = EditorLevelManager.CaptureCurrentLevel();
    Environment.Reset();
    Check(snapshot.Environment!.IsFogEnabled && snapshot.Environment.FogNear == 4);
    var path = Path.Combine(testRoot, "environment.map");
    EditorLevelManager.SaveLevel(snapshot, path);
    Check(File.ReadAllText(path).Contains("\"Environment\""));
    EditorLevelManager.OpenLevel(path);
    Check(SameEnvironment(expected, Environment.Capture()));
    Check(!EditorLevelManager.IsDirty);
});

Run("Environment changes mark the map dirty; reverting or saving clears it", () =>
{
    var before = Environment.Capture();
    Environment.AmbientColor = new(0.8f, 0.2f, 0.1f);
    Environment.IsFogEnabled = !before.IsFogEnabled;
    Environment.FogFar += 10;
    EditorLevelManager.RefreshDirty();
    Check(EditorLevelManager.IsDirty);
    Environment.Apply(before);
    EditorLevelManager.RefreshDirty();
    Check(!EditorLevelManager.IsDirty);
    Environment.FogNear += 1;
    EditorLevelManager.SaveCurrentLevel(Path.Combine(testRoot, "environment-edited.map"));
    Check(!EditorLevelManager.IsDirty);
    var saved = EditorLevelManager.ReadLevel(EditorLevelManager.CurrentPath!);
    Check(saved.Environment!.FogNear == Environment.FogNear);
});

Run("Legacy maps and null environment use defaults instead of previous map values", () =>
{
    var legacy = JsonConvert.DeserializeObject<Level>("{\"Placeables\":[]}", JsonHelper.GetSettings())!;
    Environment.AmbientColor = new(0.1f, 0.2f, 0.3f);
    Environment.IsFogEnabled = true;
    LevelManager.Load(legacy);
    Check(SameEnvironment(new EnvironmentData(), Environment.Capture()));
    Environment.FogFar = 250;
    LevelManager.Load(new Level { Environment = null });
    Check(SameEnvironment(new EnvironmentData(), Environment.Capture()));
});

Run("Invalid environment rejects the map without changing scene or environment", () =>
{
    var keeper = Game.Instance.World.Spawn<GameObject>();
    var before = Environment.Capture();
    foreach (var invalid in new[] {
        new EnvironmentData { FogNear = 10, FogFar = 10 },
        new EnvironmentData { FogNear = -1 },
        new EnvironmentData { FogFar = float.NaN },
        new EnvironmentData { AmbientColor = new(float.PositiveInfinity, 0, 0) }
    })
    {
        Throws(() => LevelManager.Load(new Level { Environment = invalid }));
        Check(!keeper.IsDestroyed && Game.Instance.World.GetAllObjects(true).Contains(keeper));
        Check(SameEnvironment(before, Environment.Capture()));
    }
});

Run("Failed object creation does not apply the incoming map environment", () =>
{
    var before = Environment.Capture();
    Throws(() => LevelManager.Load(new Level
    {
        Environment = new EnvironmentData { FogNear = 5, FogFar = 75, IsFogEnabled = true },
        Placeables = [new Placeable { Type = PlaceableType.Entity, Path = "Unknown" }]
    }));
    Check(SameEnvironment(before, Environment.Capture()));
});

Run("New map resets environment and starts clean", () =>
{
    Environment.IsFogEnabled = true;
    Environment.FogFar = 500;
    EditorLevelManager.NewLevel();
    Check(SameEnvironment(new EnvironmentData(), Environment.Capture()));
    Check(!EditorLevelManager.IsDirty && EditorLevelManager.CurrentPath == null);
});

Run("Duplicate selection copies a pending entity and detaches properties and outputs", () =>
{
    EditorLevelManager.NewLevel();
    var source = (CheckEntity)Entity.Create("Check")!;
    source.Name = "switch_1";
    source.Number = 42;
    source.Values = [1, 2, 3];
    source.Transform.Position = new Vector3(2, 3, 4);
    source.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(0.2f, 0.3f, 0.4f);
    source.Transform.Scale = new Vector3(2, 4, 3);
    source.Outputs.Add(new OutputData { Event = "Use", TargetName = source.Name, Input = "SetActive", Parameter = "false" });
    source.Outputs.Add(new OutputData { Event = "Use", TargetName = "door_1", Input = "Open" });
    Selection.Object = source;
    Editor.DuplicateSelection();
    var copy = (CheckEntity)Selection.Object!;
    Check(copy != source && copy.Name == "switch_2" && copy.Path == source.Path);
    Check(copy.Number == source.Number && copy.Values.SequenceEqual(source.Values));
    Check(copy.Transform.Position == source.Transform.Position && copy.Transform.Scale == source.Transform.Scale);
    Check(MathF.Abs(Quaternion.Dot(copy.Transform.Rotation, source.Transform.Rotation)) > 0.9999f);
    Check(copy.Outputs[0].TargetName == copy.Name && copy.Outputs[1].TargetName == "door_1");
    copy.Values.Add(4);
    copy.Outputs[0].Parameter = "true";
    copy.Transform.Position += Vector3.One;
    Check(source.Values.Count == 3 && source.Outputs[0].Parameter == "false");
    Check(source.Transform.Position == new Vector3(2, 3, 4));
    Check(Game.Instance.World.GetAllObjects(true).Length == 2 && EditorLevelManager.IsDirty);
});

Run("Repeated duplication reserves unique pending names and retains undo history", () =>
{
    UndoRedo.Register(new PropertyChangeAction<int>(_ => { }, 0, 1));
    Editor.DuplicateSelection();
    Check(Selection.Object!.Name == "switch_3" && UndoRedo.CanUndo && !UndoRedo.CanRedo);
    Editor.DuplicateSelection();
    Check(Selection.Object!.Name == "switch_4");
    var objects = Game.Instance.World.GetAllObjects(true);
    Check(objects.Length == 4 && objects.Select(obj => obj.Name).Distinct().Count() == 4);
    var path = Path.Combine(testRoot, "duplicates.map");
    EditorLevelManager.SaveCurrentLevel(path);
    EditorLevelManager.OpenLevel(path);
    objects = Game.Instance.World.GetAllObjects(true);
    Check(objects.Length == 4);
    foreach (var copy in objects.OfType<CheckEntity>()) Check(copy.Outputs[0].TargetName == copy.Name);
});

Run("Unnamed geometry duplicates with a unique asset-based name and its own collider", () =>
{
    EditorLevelManager.NewLevel();
    var source = Geometry.Create("Box")!;
    source.Transform.Position = new Vector3(1, 2, 3);
    source.Transform.Scale = new Vector3(4, 0.2f, 4);
    Game.Instance.World.Spawn<GameObject>().Name = "Box_1";
    var copy = (Geometry)EditorLevelManager.DuplicateObject(source);
    Check(copy.Name == "Box_2" && copy.Path == source.Path);
    Check(copy.Transform.Position == source.Transform.Position && copy.Transform.Scale == source.Transform.Scale);
    Check(copy.Collider != null && copy.Collider != source.Collider && copy.Collider.Parent == copy);
});

Run("Failed duplication removes partial objects and unregisters their colliders", () =>
{
    EditorLevelManager.NewLevel();
    var source = (RejectingEntity)Entity.Create("Rejecting")!;
    source.Name = "reject_1";
    source.Value = 5;
    var before = Game.Instance.World.GetAllObjects(true);
    var colliderCount = Physics.OverlapSphereAll(Vector3.Zero, 100, Layers.Everything).Length;
    RejectingEntity.RejectWrites = true;
    try { Throws(() => EditorLevelManager.DuplicateObject(source)); }
    finally { RejectingEntity.RejectWrites = false; }
    Check(Game.Instance.World.GetAllObjects(true).SequenceEqual(before) && !source.IsDestroyed);
    Check(Physics.OverlapSphereAll(Vector3.Zero, 100, Layers.Everything).Length == colliderCount);
});

Run("Duplication rejects runtime-only, destroyed and foreign objects without changing the scene", () =>
{
    var runtime = Game.Instance.World.Spawn<GameObject>();
    var before = Game.Instance.World.GetAllObjects(true);
    Throws(() => EditorLevelManager.DuplicateObject(runtime));
    Throws(() => EditorLevelManager.DuplicateObject(new CheckEntity()));
    Check(Game.Instance.World.GetAllObjects(true).SequenceEqual(before));
    runtime.Destroy();
    Throws(() => EditorLevelManager.DuplicateObject(runtime));
    Selection.Object = null;
    Editor.DuplicateSelection();
});

Run("Level transition events expose the old world before commit and the new world before Start", () =>
{
    LevelManager.Unload();
    var first = new Level();
    LevelManager.Load(first);
    var previous = Game.Instance.World.Spawn<GameObject>();
    var next = new Level { Placeables = [new Placeable { Path = "Check", Type = PlaceableType.Entity,
        Name = "arrival", Position = new Vector3(1, 2, 3) }],
        Environment = new EnvironmentData { AmbientColor = Color.Red } };
    var events = new List<string>();
    void Loading(Level level)
    {
        Check(level == next && LevelManager.ActiveLevel == first && LevelManager.IsLoading && !previous.IsDestroyed);
        events.Add("loading");
    }
    void Unloading(Level level)
    {
        Check(level == first && !previous.IsDestroyed && LevelManager.ActiveLevel == first);
        events.Add("unloading");
    }
    void Unloaded(Level level)
    {
        Check(level == first && previous.IsDestroyed && LevelManager.ActiveLevel == next);
        events.Add("unloaded");
    }
    void Loaded(Level level)
    {
        Check(level == next && LevelManager.ActiveLevel == next && !LevelManager.IsLoading);
        var arrival = Game.Instance.World.Find<CheckEntity>();
        Check(arrival != null && arrival.Name == "arrival" && arrival.Transform.Position == new Vector3(1, 2, 3));
        Check(Game.Instance.World.GetAllObjects().Length == 0 && Environment.AmbientColor.Equals(Color.Red));
        Game.Instance.World.Spawn<GameObject>().Name = "runtime-player";
        events.Add("loaded");
    }
    LevelManager.Loading += Loading;
    LevelManager.Unloading += Unloading;
    LevelManager.Unloaded += Unloaded;
    LevelManager.Loaded += Loaded;
    try { LevelManager.Load(next); }
    finally
    {
        LevelManager.Loading -= Loading; LevelManager.Unloading -= Unloading;
        LevelManager.Unloaded -= Unloaded; LevelManager.Loaded -= Loaded;
    }
    Check(events.SequenceEqual(new[] { "loading", "unloading", "unloaded", "loaded" }));
    Game.Instance.World.Update(0);
    Check(Game.Instance.World.GetAllObjects().Length == 2);
});

Run("Failed level preparation preserves the active level and never announces unloading or completion", () =>
{
    var active = LevelManager.ActiveLevel;
    var before = Game.Instance.World.GetAllObjects(true);
    var environment = Environment.Capture();
    var events = new List<string>();
    void Loading(Level _) => events.Add("loading");
    void Unloading(Level _) => events.Add("unloading");
    void Loaded(Level _) => events.Add("loaded");
    LevelManager.Loading += Loading; LevelManager.Unloading += Unloading; LevelManager.Loaded += Loaded;
    try { Throws(() => LevelManager.Load(new Level { Placeables = [new Placeable { Path = "Broken", Type = PlaceableType.Entity }] })); }
    finally { LevelManager.Loading -= Loading; LevelManager.Unloading -= Unloading; LevelManager.Loaded -= Loaded; }
    Check(events.SequenceEqual(new[] { "loading" }) && LevelManager.ActiveLevel == active && !LevelManager.IsLoading);
    Check(Game.Instance.World.GetAllObjects(true).SequenceEqual(before) && SameEnvironment(environment, Environment.Capture()));
});

Run("Level transitions reject nested Load and Unload even from Loaded callbacks", () =>
{
    var called = 0;
    void CheckNested(Level _)
    {
        Throws(() => LevelManager.Load(new Level()));
        Throws(() => LevelManager.Unload());
        called++;
    }
    LevelManager.Loading += CheckNested; LevelManager.Loaded += CheckNested;
    try { LevelManager.Load(new Level()); }
    finally { LevelManager.Loading -= CheckNested; LevelManager.Loaded -= CheckNested; }
    Check(called == 2 && !LevelManager.IsLoading);
    LevelManager.Load(new Level());
});

Run("Subscriber failures release transition guards and post-commit failures preserve the new scene", () =>
{
    var previous = LevelManager.ActiveLevel;
    void Fail(Level _) => throw new InvalidOperationException("Subscriber failure");
    LevelManager.Loading += Fail;
    try { Throws(() => LevelManager.Load(new Level())); }
    finally { LevelManager.Loading -= Fail; }
    Check(LevelManager.ActiveLevel == previous && !LevelManager.IsLoading);
    var next = new Level();
    LevelManager.Loaded += Fail;
    try { Throws(() => LevelManager.Load(next)); }
    finally { LevelManager.Loaded -= Fail; }
    Check(LevelManager.ActiveLevel == next && !LevelManager.IsLoading);
    LevelManager.Load(new Level());
});

Run("Unload destroys runtime objects, clears the active level and resets environment exactly once", () =>
{
    var previous = LevelManager.ActiveLevel;
    var runtime = Game.Instance.World.Spawn<GameObject>();
    var events = new List<string>();
    void Unloading(Level level)
    {
        Check(level == previous && !runtime.IsDestroyed);
        events.Add("unloading");
    }
    void Unloaded(Level level)
    {
        Check(level == previous && runtime.IsDestroyed && LevelManager.ActiveLevel == null);
        Check(Game.Instance.World.GetAllObjects(true).Length == 0);
        events.Add("unloaded");
    }
    LevelManager.Unloading += Unloading; LevelManager.Unloaded += Unloaded;
    try { LevelManager.Unload(); LevelManager.Unload(); }
    finally { LevelManager.Unloading -= Unloading; LevelManager.Unloaded -= Unloaded; }
    Check(events.SequenceEqual(new[] { "unloading", "unloaded" }));
    Check(SameEnvironment(Environment.Capture(), new EnvironmentData()));
});

Run("Missing named levels fail explicitly without changing active state", () =>
{
    Throws(() => LevelManager.Load("Missing-Level"));
    Check(LevelManager.ActiveLevel == null && !LevelManager.IsLoading);
});

Run("Inactive placement survives save/load, defers Start and can be enabled through SetActive input", () =>
{
    EditorLevelManager.NewLevel();
    var source = (CheckEntity)Entity.Create("Check")!;
    source.Name = "disabled_target";
    source.SetActive(false);
    var geometry = Geometry.Create("Box")!;
    geometry.SetActive(false);
    var snapshot = EditorLevelManager.CaptureCurrentLevel();
    Check(snapshot.Placeables.Length == 2 && snapshot.Placeables.All(p => !p.IsActive));
    var path = Path.Combine(testRoot, "inactive.map");
    EditorLevelManager.SaveLevel(snapshot, path);
    EditorLevelManager.OpenLevel(path);
    var loaded = Game.Instance.World.Find<CheckEntity>()!;
    Check(!loaded.IsActive && loaded.Starts == 0);
    Game.Instance.World.Update(0);
    Check(loaded.Starts == 0 && !Physics.OverlapSphere(Vector3.Zero, 1, Layers.Everything, out _));
    var target = Game.Instance.World.Find("disabled_target");
    Check(target == loaded);
    InputInvoker.Invoke(target!, "SetActive", "true");
    Check(loaded.IsActive && loaded.Starts == 0);
    Game.Instance.World.Update(0);
    Check(loaded.Starts == 1);
    loaded.SetActive(false);
    loaded.SetActive(true);
    Game.Instance.World.Update(0);
    Check(loaded.Starts == 1);
});

Run("Inactive placement duplicates as inactive and activity participates in dirty tracking", () =>
{
    EditorLevelManager.NewLevel();
    var source = (CheckEntity)Entity.Create("Check")!;
    EditorLevelManager.Initialize();
    source.SetActive(false);
    EditorLevelManager.RefreshDirty();
    Check(EditorLevelManager.IsDirty);
    source.SetActive(true);
    EditorLevelManager.RefreshDirty();
    Check(!EditorLevelManager.IsDirty);
    source.SetActive(false);
    var copy = EditorLevelManager.DuplicateObject(source);
    Check(!copy.IsActive);
});

Run("Legacy maps without IsActive default to enabled placements", () =>
{
    var legacy = JsonConvert.DeserializeObject<Level>(
        "{\"Placeables\":[{\"Path\":\"Check\",\"Type\":1}]}", JsonHelper.GetSettings())!;
    Check(legacy.Placeables[0].IsActive);
    LevelManager.Load(legacy);
    Check(Game.Instance.World.Find<CheckEntity>()!.IsActive);
});

Run("Viewport preview toggles leave the map snapshot, Environment and dirty state unchanged", () =>
{
    EditorLevelManager.Initialize();
    var before = JsonConvert.SerializeObject(EditorLevelManager.CaptureCurrentLevel(), JsonHelper.GetSettings());
    var environment = Environment.Capture();
    var preview = Application.EditorPreview;
    var previousUnlit = preview.Unlit;
    var previousEnvironment = preview.EnvironmentEnabled;
    try
    {
        foreach (var unlit in new[] { false, true })
            foreach (var showEnvironment in new[] { false, true })
            {
                preview.Unlit = unlit;
                preview.EnvironmentEnabled = showEnvironment;
                EditorLevelManager.RefreshDirty();
                Check(!EditorLevelManager.IsDirty && SameEnvironment(environment, Environment.Capture()));
                Check(JsonConvert.SerializeObject(EditorLevelManager.CaptureCurrentLevel(), JsonHelper.GetSettings()) == before);
            }
    }
    finally { preview.Unlit = previousUnlit; preview.EnvironmentEnabled = previousEnvironment; }
});

Run("BrushBox mesh has outward faces and constant world UV density after nonuniform scaling", () =>
{
    var size = new Vector3(2, 3, 4);
    var scale = new Vector3(3, 2, 0.5f);
    var (vertices, indices) = ProjectShockGL.Rendering.BrushBoxMesh.Build(size, scale, 2);
    Check(vertices.Length == 24 * 8 && indices.Length == 36);
    Vector3 Position(int vertex) => new(vertices[vertex * 8], vertices[vertex * 8 + 1], vertices[vertex * 8 + 2]);
    Vector2 Uv(int vertex) => new(vertices[vertex * 8 + 6], vertices[vertex * 8 + 7]);
    for (var face = 0; face < 6; face++)
    {
        var first = face * 4;
        var a = Position(first); var b = Position(first + 1); var c = Position(first + 2);
        var normal = new Vector3(vertices[first * 8 + 3], vertices[first * 8 + 4], vertices[first * 8 + 5]);
        Check(Vector3.Dot(Vector3.Normalize(Vector3.Cross(b - a, c - b)), normal) > 0.999f);
        Check(MathF.Abs(Vector2.Distance(Uv(first), Uv(first + 1)) / ((b - a) * scale).Length() - 2) < 0.0001f);
        Check(MathF.Abs(Vector2.Distance(Uv(first + 1), Uv(first + 2)) / ((c - b) * scale).Length() - 2) < 0.0001f);
    }
});

Run("BrushBox dimensions keep collider and transformed bounds aligned", () =>
{
    EditorLevelManager.NewLevel();
    var brush = Game.Instance.World.Spawn<BrushBox>();
    brush.Size = new Vector3(2, 4, 6);
    brush.Transform.Position = new Vector3(10, 0, 0);
    brush.Transform.Scale = new Vector3(2, 1, 0.5f);
    var collider = (BoxCollider)brush.Collider!;
    Check(collider.HalfExtents == new Vector3(1, 2, 3));
    var (min, max) = brush.GetWorldBounds();
    Check(min == new Vector3(8, -2, -1.5f) && max == new Vector3(12, 2, 1.5f));
    brush.Size = Vector3.One;
    Check(collider.HalfExtents == new Vector3(0.5f));
    Check(Physics.Raycast(new Vector3(10, 0, -5), Vector3.UnitZ, 10, Layers.Everything, out var hit) && hit.Object == brush);
});

Run("BrushBox roundtrip and duplication preserve independent size, material, transform and activity", () =>
{
    EditorLevelManager.NewLevel();
    var brush = Game.Instance.World.Spawn<BrushBox>();
    brush.Name = "wall_1";
    brush.Size = new Vector3(4, 3, 0.25f);
    brush.TextureScale = 2.5f;
    brush.Color = Color.Cyan;
    brush.Lit = false;
    brush.Transform.Position = new Vector3(2, 3, 4);
    brush.Transform.Scale = new Vector3(2, 1, 3);
    brush.SetActive(false);
    var path = Path.Combine(testRoot, "brush.map");
    EditorLevelManager.SaveCurrentLevel(path);
    EditorLevelManager.OpenLevel(path);
    var loaded = Game.Instance.World.Find<BrushBox>()!;
    Check(loaded.Name == "wall_1" && loaded.Size == brush.Size && !loaded.IsActive);
    Check(loaded.TextureScale == 2.5f && loaded.Color.Equals(Color.Cyan) && !loaded.Lit);
    Check(loaded.Transform.Position == brush.Transform.Position && loaded.Transform.Scale == brush.Transform.Scale);
    Selection.Object = loaded;
    Check(Editor.CanDuplicateSelection);
    var copy = (BrushBox)EditorLevelManager.DuplicateObject(loaded);
    Check(copy.Name == "wall_2" && copy.Size == loaded.Size && copy.Collider != loaded.Collider && !copy.IsActive);
    copy.Size = Vector3.One;
    Check(loaded.Size == new Vector3(4, 3, 0.25f));
    EditorLevelManager.RefreshDirty();
    Check(EditorLevelManager.IsDirty);
});

Run("Invalid BrushBox descriptions reject loading without replacing the current world", () =>
{
    var before = Game.Instance.World.GetAllObjects(true);
    foreach (var data in new[]
    {
        new BrushBoxData { Size = new Vector3(0, 1, 1) },
        new BrushBoxData { TextureScale = float.NaN },
        new BrushBoxData { Texture = "missing-texture" }
    })
        Throws(() => LevelManager.Load(new Level { Placeables = [new Placeable { Type = PlaceableType.BrushBox, Brush = data }] }));
    Check(Game.Instance.World.GetAllObjects(true).SequenceEqual(before));
    Throws(() => ProjectShockGL.Rendering.BrushBoxMesh.Build(Vector3.One, Vector3.Zero, 1));
    var legacy = JsonConvert.DeserializeObject<Placeable>("{\"Type\":0,\"Path\":\"Box\"}")!;
    Check(legacy.Type == PlaceableType.Geometry && legacy.Brush == null);
});

Run("Spotlight map roundtrip and duplication preserve full cone angles and orientation", () =>
{
    EditorLevelManager.NewLevel();
    var light = (Light)Entity.Create("Light")!;
    light.Name = "spot_1";
    light.Type = LightType.Spot;
    light.InnerAngle = 20;
    light.OuterAngle = 55;
    light.Radius = 8;
    light.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(0.6f, -0.3f, 0);
    var rotation = light.Transform.Rotation;
    var path = Path.Combine(testRoot, "spot.map");
    EditorLevelManager.SaveCurrentLevel(path);
    EditorLevelManager.OpenLevel(path);
    var loaded = Game.Instance.World.Find<Light>()!;
    Check(loaded.Type == LightType.Spot && loaded.InnerAngle == 20 && loaded.OuterAngle == 55 && loaded.Radius == 8);
    Check(MathF.Abs(Quaternion.Dot(loaded.Transform.Rotation, rotation)) > 0.9999f);
    Check(Light.All.Contains(loaded) && Light.Main != loaded);
    var copy = (Light)EditorLevelManager.DuplicateObject(loaded);
    Check(copy.Type == LightType.Spot && copy.InnerAngle == 20 && copy.OuterAngle == 55);
    copy.InnerAngle = 35;
    Check(loaded.InnerAngle == 20);
});

Run("Spotlight angle constraints, type transitions and destruction keep registries consistent", () =>
{
    EditorLevelManager.NewLevel();
    var light = Game.Instance.World.Spawn<Light>();
    light.Type = LightType.Spot;
    light.OuterAngle = 20;
    Check(light.InnerAngle == 20);
    light.InnerAngle = 60;
    Check(light.OuterAngle == 60);
    Throws(() => light.OuterAngle = 0);
    Throws(() => light.InnerAngle = float.NaN);
    Throws(() => light.OuterAngle = 180);
    Check(Light.All.Count(l => l == light) == 1);
    light.Type = LightType.Directional;
    Check(Light.Main == light && !Light.All.Contains(light));
    light.Type = LightType.Spot;
    Check(Light.Main == null && Light.All.Count(l => l == light) == 1);
    light.Destroy();
    light.Type = LightType.Point;
    Check(!Light.All.Contains(light) && Light.Main != light);
});

Run("Spotlight gizmo matches the rotated outer cone and radial cutoff", () =>
{
    var light = new Light { Radius = 7, OuterAngle = 60 };
    light.Transform.Position = new Vector3(2, 3, 4);
    light.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(0.9f, 0.4f, 0);
    var lines = LightGizmo.ConeLines(light, light.OuterAngle).ToArray();
    Check(lines.Length == 52);
    foreach (var line in lines)
    {
        var toRim = line.To - light.Transform.Position;
        Check(MathF.Abs(toRim.Length() - light.Radius) < 0.0001f);
        Check(MathF.Abs(Vector3.Dot(Vector3.Normalize(toRim), light.Transform.Forward) - MathF.Cos(MathF.PI / 6)) < 0.0001f);
    }
    light.Radius = 0;
    Check(!LightGizmo.ConeLines(light, 60).Any());
});

Run("Corrupt BrushBox texture preserves the current world", () =>
{
    Directory.CreateDirectory(Path.Combine(testRoot, "Textures"));
    File.WriteAllText(Path.Combine(testRoot, "Textures", "Corrupt.png"), "not an image");
    var existing = Game.Instance.World.Spawn<CheckEntity>();
    Throws(() => LevelManager.Load(new Level { Placeables = [new Placeable {
        Type = PlaceableType.BrushBox, Brush = new BrushBoxData { Texture = "Corrupt", TextureFormat = TextureFormat.Png }
    }] }));
    Check(!existing.IsDestroyed && Game.Instance.World.GetAllObjects(true).Contains(existing));
    Game.Instance.World.Clear();
});

Run("Editor Game update skips gameplay lifecycle and physics events", () =>
{
    var editorProperty = typeof(Application).GetProperty(nameof(Application.IsEditor))!;
    var previous = Application.IsEditor;
    editorProperty.SetValue(null, true);
    try
    {
        var first = Game.Instance.World.Spawn<RuntimeProbe>();
        Game.Instance.World.Spawn<RuntimeProbe>();
        ((CheckGame)Game.Instance).UpdateForTest();
        Check(first.Starts == 0 && first.Updates == 0 && first.Enters == 0);
        Check(Game.Instance.World.GetAllObjects().Contains(first));
        Game.Instance.World.Clear();
    }
    finally { editorProperty.SetValue(null, previous); }
});

Run("Player camera keeps spawn direction and character owns its flashlight", () =>
{
    var camera = (ProjectShockGL.Rendering.Camera)Activator.CreateInstance(typeof(ProjectShockGL.Rendering.Camera),
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null,
        new object[] { new Silk.NET.Maths.Vector2D<int>(800, 600) }, null)!;
    ProjectShockGL.Rendering.Camera.ToggleTo(camera);
    var world = Game.Instance.World;
    var character = world.Spawn<ProjectDungeon.Game.ShockPlayerCharacter>();
    character.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(1.2f, 0.2f, 0);
    var expected = character.Transform.Forward;
    world.Update(0);
    Check(Vector3.Distance(camera.Transform.Forward, expected) < 0.0001f);
    character.Turn(1);
    world.Update(0);
    var turnedForward = Vector3.Transform(-Vector3.UnitZ,
        Quaternion.CreateFromYawPitchRoll(1.2f + MathF.PI / 180f, 0.2f, 0));
    Check(Vector3.Distance(camera.Transform.Forward, turnedForward) < 0.0001f);
    var light = world.Find<Light>()!;
    Check(light.IsActive);
    character.SetActive(false);
    Check(!light.IsActive);
    character.SetActive(true);
    Check(light.IsActive);
    world.Destroy(character);
    Check(light.IsDestroyed);
    world.Clear();
});

Run("Inspector property edits share chronological history with transforms and coalesce a drag", () =>
{
    EditorLevelManager.NewLevel();
    var obj = Entity.Create("Check")!;
    Selection.Object = obj;
    obj.Transform.Position = new Vector3(2,3,4);
    UndoRedo.Register(new PropertyChangeAction<Vector3>(v => obj.Transform.Position = v, Vector3.Zero, obj.Transform.Position));
    RecordInspector(obj, "Number", () => ((CheckEntity)obj).Number = 10, editing: true);
    RecordInspector(obj, "Number", () => ((CheckEntity)obj).Number = 20, editing: true);
    Check(UndoRedo.CanUndo);
    UndoRedo.Undo(); // Also finalizes an input that still has focus.
    Check(((CheckEntity)obj).Number == 0 && obj.Transform.Position == new Vector3(2,3,4));
    UndoRedo.Undo();
    Check(obj.Transform.Position == Vector3.Zero && !UndoRedo.CanUndo);
    UndoRedo.Redo();
    UndoRedo.Redo();
    Check(((CheckEntity)obj).Number == 20 && obj.Transform.Position == new Vector3(2,3,4));
});

Run("Separate Inspector fields and selection changes end pending transactions", () =>
{
    EditorLevelManager.NewLevel();
    var obj = (CheckEntity)Entity.Create("Check")!;
    Selection.Object = obj;
    RecordInspector(obj, "Number", () => obj.Number = 2, editing: true);
    RecordInspector(obj, "Name", () => obj.Name = "new name", editing: true);
    Selection.Object = Entity.Create("Check");
    UndoRedo.Undo();
    Check(obj.Name == "" && obj.Number == 2);
    UndoRedo.Undo();
    Check(obj.Number == 0);
    RecordInspector(obj, "Number", () => obj.Number = 3);
    Check(!UndoRedo.CanRedo);
    UndoRedo.Clear();
    RecordInspector(obj, "Name", () => obj.Name = "pending", editing: true);
    UndoRedo.Clear();
    Check(!UndoRedo.CanUndo && !UndoRedo.CanRedo);
});

Run("Returning an Inspector edit to its original value produces no action and preserves redo", () =>
{
    EditorLevelManager.NewLevel();
    var obj = (CheckEntity)Entity.Create("Check")!;
    RecordInspector(obj, "Number", () => obj.Number = 1);
    UndoRedo.Undo();
    RecordInspector(obj, "Name", () => obj.Name = "temporary", editing: true);
    RecordInspector(obj, "Name", () => obj.Name = "", editing: false);
    Check(!UndoRedo.CanUndo && UndoRedo.CanRedo);
    UndoRedo.Redo();
    Check(obj.Number == 1);
});

Run("Inspector snapshots restore all BrushBox data, paired texture fields and collider size", () =>
{
    EditorLevelManager.NewLevel();
    var brush = Game.Instance.World.Spawn<BrushBox>();
    var before = InspectorState.Capture(brush);
    RecordInspector(brush, "Brush settings", () => {
        brush.Name = "wall"; brush.Size = new Vector3(4,5,6); brush.TextureScale = 3;
        brush.TextureName = "paint"; brush.TextureFormat = TextureFormat.Png;
        brush.Color = Color.Red; brush.Lit = false; brush.SetActive(false);
    });
    var after = InspectorState.Capture(brush);
    UndoRedo.Undo();
    Check(InspectorState.Capture(brush) == before && brush.Collider is BoxCollider box && box.HalfExtents == Vector3.One * 0.5f);
    UndoRedo.Redo();
    Check(InspectorState.Capture(brush) == after && ((BoxCollider)brush.Collider!).HalfExtents == new Vector3(2,2.5f,3));
});

Run("Spotlight undo restores linked inner and outer angles and light registration", () =>
{
    EditorLevelManager.NewLevel();
    var light = Game.Instance.World.Spawn<Light>();
    light.Type = LightType.Spot;
    light.InnerAngle = 30;
    light.OuterAngle = 45;
    RecordInspector(light, "InnerAngle", () => light.InnerAngle = 70);
    Check(light.InnerAngle == 70 && light.OuterAngle == 70);
    UndoRedo.Undo();
    Check(light.InnerAngle == 30 && light.OuterAngle == 45);
    UndoRedo.Redo();
    Check(light.InnerAngle == 70 && light.OuterAngle == 70);
    RecordInspector(light, "Type", () => light.Type = LightType.Directional);
    Check(Light.Main == light && !Light.All.Contains(light));
    UndoRedo.Undo();
    Check(light.Type == LightType.Spot && Light.Main != light && Light.All.Contains(light));
});

Run("Outputs and mutable Editable values are deep snapshots across repeated undo and redo", () =>
{
    EditorLevelManager.NewLevel();
    var obj = (CheckEntity)Entity.Create("Check")!;
    RecordInspector(obj, "Outputs", () => obj.Outputs.Add(new OutputData {
        Event = "Enter", TargetName = "door", Input = "Open", Parameter = "1"
    }));
    RecordInspector(obj, "Output/0/Input", () => obj.Outputs[0].Input = "Close");
    UndoRedo.Undo();
    Check(obj.Outputs.Single().Input == "Open");
    UndoRedo.Undo();
    Check(obj.Outputs.Count == 0);
    UndoRedo.Redo();
    UndoRedo.Redo();
    Check(obj.Outputs.Single().Input == "Close");
    RecordInspector(obj, "Outputs", () => obj.Outputs.Clear());
    UndoRedo.Undo();
    Check(obj.Outputs.Single().TargetName == "door" && obj.Outputs[0].Parameter == "1");
    RecordInspector(obj, "Values", () => obj.Values.Add(7));
    UndoRedo.Undo();
    Check(obj.Values.Count == 0);
    UndoRedo.Redo();
    obj.Values[0] = 99;
    UndoRedo.Undo();
    UndoRedo.Redo();
    Check(obj.Values.SequenceEqual(new[] { 7 }));
});

Run("Undoing Inspector data restores the saved dirty state and does not resurrect destroyed owners", () =>
{
    EditorLevelManager.NewLevel();
    var obj = (CheckEntity)Entity.Create("Check")!;
    var path = Path.Combine(testRoot, "inspector-undo.map");
    EditorLevelManager.SaveCurrentLevel(path);
    RecordInspector(obj, "IsActive", () => obj.SetActive(false));
    EditorLevelManager.RefreshDirty();
    Check(EditorLevelManager.IsDirty);
    UndoRedo.Undo();
    EditorLevelManager.RefreshDirty();
    Check(obj.IsActive && !EditorLevelManager.IsDirty);
    UndoRedo.Redo();
    EditorLevelManager.RefreshDirty();
    Check(!obj.IsActive && EditorLevelManager.IsDirty);
    obj.Destroy();
    UndoRedo.Undo();
    Check(obj.IsDestroyed);
});

Run("A failed history action remains available instead of being lost", () =>
{
    UndoRedo.Clear();
    UndoRedo.Register(new PropertyChangeAction<int>(_ => throw new InvalidOperationException("test"), 1, 2));
    Throws(UndoRedo.Undo);
    Check(UndoRedo.CanUndo && !UndoRedo.CanRedo);
    UndoRedo.Clear();
});

RunScene("Creation of entity, geometry and BrushBox is undoable, repeatable and preserves selection", () =>
{
    var world = Game.Instance.World;
    var previous = Entity.Create("Check")!;
    previous.Name = "previous";
    Selection.Object = previous;
    foreach (var (type,path) in new[] { (PlaceableType.Entity,"Check"), (PlaceableType.Geometry,"Box"), (PlaceableType.BrushBox,"") })
    {
        var created = SceneObjects.Create(type,path);
        var reference = EditorObjectReference.Get(created);
        var originalState = InspectorState.Capture(created);
        var originalName = created.Name;
        world.Update(0,simulate:false);
        UndoRedo.Undo();
        Check(created.IsDestroyed && Selection.Object == previous && world.GetAllObjects(true).Length == 1);
        UndoRedo.Redo();
        var restored = reference.Current;
        Check(!ReferenceEquals(created,restored) && !restored.IsDestroyed && Selection.Object == restored);
        Check(restored.Name == originalName && InspectorState.Capture(restored) == originalState);
        UndoRedo.Undo();
    }
});

RunScene("Delete undo keeps order, dirty state and deferred Start for active and pending placements", () =>
{
    foreach (var activate in new[] { false,true })
    {
        EditorLevelManager.NewLevel();
        var world = Game.Instance.World;
        var first = SceneObjects.Create(PlaceableType.Entity,"Check");
        var middle = SceneObjects.Create(PlaceableType.Entity,"Check");
        var last = SceneObjects.Create(PlaceableType.Entity,"Check");
        var reference = EditorObjectReference.Get(middle);
        middle.Transform.Position = new Vector3(2,3,4);
        middle.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(0.2f,0.3f,0.4f);
        if (activate) world.Update(0,simulate:false);
        var path = Path.Combine(testRoot, "delete-order-" + activate + ".map");
        EditorLevelManager.SaveCurrentLevel(path);
        var before = JsonConvert.SerializeObject(EditorLevelManager.CaptureCurrentLevel(),JsonHelper.GetSettings());
        SceneObjects.Delete(middle);
        Check(middle.IsDestroyed && EditorLevelManager.IsDirty);
        UndoRedo.Undo();
        var restored = (CheckEntity)reference.Current;
        Check(world.GetAllObjects(true).SequenceEqual(new[] { first,restored,last }));
        Check(Selection.Object == restored && restored.Starts == 0);
        Check(JsonConvert.SerializeObject(EditorLevelManager.CaptureCurrentLevel(),JsonHelper.GetSettings()) == before);
        EditorLevelManager.RefreshDirty();
        Check(!EditorLevelManager.IsDirty && Physics.OverlapSphereAll(Vector3.Zero,100,Layers.Everything).Length == 3);
        world.Update(0,simulate:false);
        Check(restored.Starts == 0 && world.GetAllObjects().Contains(restored));
        UndoRedo.Redo();
        EditorLevelManager.RefreshDirty();
        Check(restored.IsDestroyed && EditorLevelManager.IsDirty);
        UndoRedo.Undo();
        Check(world.GetAllObjects(true)[1] == reference.Current && reference.Current != restored);
    }
});

RunScene("Older Inspector and gizmo actions follow the restored instance through full undo/redo chains", () =>
{
    var world = Game.Instance.World;
    var obj = (CheckEntity)SceneObjects.Create(PlaceableType.Entity,"Check");
    var reference = EditorObjectReference.Get(obj);
    var originalName = obj.Name;
    RecordInspector(obj,"Number",()=>obj.Number=5);
    var size = new Vector2(1280,768);
    var eye = new Vector3(5,4,6);
    var forward = Vector3.Normalize(-eye);
    var vp = Matrix4x4.CreateLookAt(eye,Vector3.Zero,Vector3.UnitY) * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI/3,size.X/size.Y,0.1f,100);
    ObjectManipulator.Mode = ManipulationMode.Move;
    ObjectManipulator.Local = false;
    Check(GizmoMath.Project(Vector3.UnitX,vp,size,out var start));
    Check(GizmoMath.Project(Vector3.UnitX*1.6f,vp,size,out var end));
    ObjectManipulator.BeginDrag(obj,0,start,2,forward,vp,size);
    ObjectManipulator.ApplyDrag(end,vp,size,false);
    ObjectManipulator.Commit();
    var moved = obj.Transform.Position;
    Check(moved.Length()>0.1f);
    RecordInspector(obj,"Name",()=>obj.Name="renamed");
    SceneObjects.Delete(obj);
    for (var cycle=0; cycle<3; cycle++)
    {
        UndoRedo.Undo();
        Check(reference.Current is CheckEntity { Number:5, Name:"renamed" });
        UndoRedo.Undo();
        Check(reference.Current.Name==originalName && reference.Current.Transform.Position==moved);
        UndoRedo.Undo();
        Check(reference.Current.Transform.Position==Vector3.Zero);
        UndoRedo.Undo();
        Check(((CheckEntity)reference.Current).Number==0);
        UndoRedo.Undo();
        Check(world.GetAllObjects(true).Length==0 && !UndoRedo.CanUndo);
        UndoRedo.Redo();
        Check(reference.Current.Name==originalName);
        UndoRedo.Redo();
        Check(((CheckEntity)reference.Current).Number==5);
        UndoRedo.Redo();
        Check(reference.Current.Transform.Position==moved);
        UndoRedo.Redo();
        Check(reference.Current.Name=="renamed");
        UndoRedo.Redo();
        Check(world.GetAllObjects(true).Length==0 && !UndoRedo.CanRedo);
    }
});

RunScene("Duplication redo restores the same independent data, name and self-targeted outputs", () =>
{
    var source = (CheckEntity)SceneObjects.Create(PlaceableType.Entity,"Check");
    RecordInspector(source,"setup",()=> {
        source.Name="logic_1"; source.Number=7; source.Values.Add(1); source.SetActive(false);
        source.Outputs.Add(new OutputData { Event="Enter",TargetName=source.Name,Input="SetActive",Parameter="true" });
    });
    var clone = (CheckEntity)SceneObjects.Duplicate(source);
    var reference = EditorObjectReference.Get(clone);
    Check(clone.Name=="logic_2" && clone.Outputs[0].TargetName==clone.Name);
    var state = InspectorState.Capture(clone);
    UndoRedo.Undo();
    Check(clone.IsDestroyed && Selection.Object==source && !source.IsDestroyed);
    UndoRedo.Redo();
    var restored = (CheckEntity)reference.Current;
    Check(InspectorState.Capture(restored)==state && !restored.IsActive);
    Check(!ReferenceEquals(source.Values,restored.Values) && !ReferenceEquals(source.Outputs[0],restored.Outputs[0]));
    RecordInspector(restored,"Number",()=>restored.Number=20);
    SceneObjects.Delete(restored);
    UndoRedo.Undo();
    UndoRedo.Undo();
    Check(((CheckEntity)reference.Current).Number==7 && source.Number==7);
});

RunScene("Pending edits precede creation and a new creation discards the redo branch", () =>
{
    var obj = (CheckEntity)SceneObjects.Create(PlaceableType.Entity,"Check");
    RecordInspector(obj,"Number",()=>obj.Number=3,editing:true);
    var brush = SceneObjects.Create(PlaceableType.BrushBox);
    UndoRedo.Undo();
    Check(brush.IsDestroyed && Selection.Object==obj && obj.Number==3);
    UndoRedo.Undo();
    Check(obj.Number==0);
    var replacement = SceneObjects.Create(PlaceableType.BrushBox);
    Check(!UndoRedo.CanRedo && !replacement.IsDestroyed && Game.Instance.World.GetAllObjects(true).Length==2);
});

RunScene("Failed creation/duplication preserves the scene, selection, collider count and redo", () =>
{
    var obj = (CheckEntity)SceneObjects.Create(PlaceableType.Entity,"Check");
    RecordInspector(obj,"Number",()=>obj.Number=3);
    UndoRedo.Undo();
    var world = Game.Instance.World;
    var before = world.GetAllObjects(true);
    var colliders = Physics.OverlapSphereAll(Vector3.Zero,100,Layers.Everything).Length;
    Throws(()=>SceneObjects.Create(PlaceableType.Entity,"Broken"));
    Check(world.GetAllObjects(true).SequenceEqual(before) && Selection.Object==obj && UndoRedo.CanRedo);
    Check(Physics.OverlapSphereAll(Vector3.Zero,100,Layers.Everything).Length==colliders);
    var rejected = (RejectingEntity)Entity.Create("Rejecting")!;
    rejected.Name="rejected";
    rejected.Value=5;
    var withRejected = world.GetAllObjects(true);
    var withRejectedColliders = Physics.OverlapSphereAll(Vector3.Zero,100,Layers.Everything).Length;
    RejectingEntity.RejectWrites=true;
    try { Throws(()=>SceneObjects.Duplicate(rejected)); }
    finally { RejectingEntity.RejectWrites=false; }
    Check(world.GetAllObjects(true).SequenceEqual(withRejected) && Selection.Object==obj && UndoRedo.CanRedo);
    Check(Physics.OverlapSphereAll(Vector3.Zero,100,Layers.Everything).Length==withRejectedColliders);
});

RunScene("Failed deletion restore can be retried without orphaned colliders or lost older edits", () =>
{
    var obj = (RejectingEntity)SceneObjects.Create(PlaceableType.Entity,"Rejecting");
    var reference = EditorObjectReference.Get(obj);
    RecordInspector(obj,"Value",()=>obj.Value=9);
    SceneObjects.Delete(obj);
    RejectingEntity.RejectWrites=true;
    try { Throws(UndoRedo.Undo); }
    finally { RejectingEntity.RejectWrites=false; }
    Check(UndoRedo.CanUndo && !UndoRedo.CanRedo && Game.Instance.World.GetAllObjects(true).Length==0);
    Check(Physics.OverlapSphereAll(Vector3.Zero,100,Layers.Everything).Length==0);
    UndoRedo.Undo();
    Check(((RejectingEntity)reference.Current).Value==9);
    UndoRedo.Undo();
    Check(((RejectingEntity)reference.Current).Value==0);
});

RunScene("Light deletion and restoration keep registries correct over repeated cycles", () =>
{
    var light = (Light)SceneObjects.Create(PlaceableType.Entity,"Light");
    var reference = EditorObjectReference.Get(light);
    RecordInspector(light,"Spot",()=> {light.Type=LightType.Spot;light.InnerAngle=20;light.OuterAngle=40;light.SetActive(false);});
    SceneObjects.Delete(light);
    for (var i=0;i<4;i++)
    {
        Check(Light.All.Count==0);
        UndoRedo.Undo();
        var restored = (Light)reference.Current;
        Check(!restored.IsActive && restored.InnerAngle==20 && restored.OuterAngle==40 && Light.All.Single()==restored);
        UndoRedo.Redo();
    }
    UndoRedo.Undo();
    UndoRedo.Undo();
    Check(((Light)reference.Current).Type==LightType.Point && reference.Current.IsActive);
});

RunScene("A failing Destroy callback still records deletion and permits full restoration", () =>
{
    File.WriteAllText(Path.Combine(testRoot,"Entities","ThrowDestroy.json"),
        "{\"Class\":\"ThrowingDeleteEntity, EditorChecks\",\"Parameters\":{}}");
    var obj = SceneObjects.Create(PlaceableType.Entity,"ThrowDestroy");
    var reference = EditorObjectReference.Get(obj);
    try { Throws(()=>SceneObjects.Delete(obj)); }
    finally { ThrowingDeleteEntity.RejectDestroy=false; }
    Check(obj.IsDestroyed && Game.Instance.World.GetAllObjects(true).Length==0 && UndoRedo.CanUndo);
    Check(Physics.OverlapSphereAll(Vector3.Zero,100,Layers.Everything).Length==0);
    UndoRedo.Undo();
    Check(!reference.Current.IsDestroyed && Selection.Object==reference.Current);
    Check(Physics.OverlapSphereAll(Vector3.Zero,100,Layers.Everything).Length==1);
});

RunScene("Changing levels clears object history and cannot restore objects from the old scene", () =>
{
    var obj = SceneObjects.Create(PlaceableType.Entity,"Check");
    SceneObjects.Delete(obj);
    EditorLevelManager.NewLevel();
    Check(!UndoRedo.CanUndo && !UndoRedo.CanRedo);
    UndoRedo.Undo();
    Check(Game.Instance.World.GetAllObjects(true).Length==0);
});

RunScene("Groups preserve activity, membership, visibility and scene undo", () =>
{
    var obj = SceneObjects.Create(PlaceableType.BrushBox);
    var group = new EditorGroupData { Id = "room", Name = "Room", Visible = false };
    SceneGroups.Load([group]);
    UndoRedo.Clear();
    SceneGroups.Change(() => obj.EditorGroups.Add("room"));
    Check(!obj.IsEditorVisible && obj.IsActive);
    UndoRedo.Undo(); Check(obj.IsEditorVisible && obj.IsActive);
    UndoRedo.Redo(); Check(!obj.IsEditorVisible && obj.IsActive);
    var editorProperty = typeof(Application).GetProperty(nameof(Application.IsEditor))!;
    try
    {
        editorProperty.SetValue(null, false);
        Check(obj.IsEditorVisible && obj.IsActive);
    }
    finally { editorProperty.SetValue(null, true); }
    var copy = SceneObjects.Duplicate(obj);
    Check(copy.EditorGroups.SequenceEqual(obj.EditorGroups) && !copy.IsEditorVisible);
    SceneObjects.Delete(obj);
    UndoRedo.Undo();
    var restored = Selection.Object!;
    Check(restored.EditorGroups.Contains("room") && !restored.IsEditorVisible && restored.IsActive);
    var level = EditorLevelManager.CaptureCurrentLevel();
    Check(level.Groups.Single().Id == "room" && !level.Groups.Single().Visible);
    Check(level.Placeables.All(p => p.EditorGroups.Contains("room")));
    var path = Path.Combine(AppContext.BaseDirectory, $"groups-{Guid.NewGuid():N}.map");
    try
    {
        EditorLevelManager.SaveCurrentLevel(path);
        EditorLevelManager.NewLevel();
        Check(SceneGroups.Capture().Length == 0 && World.HiddenEditorGroups.Count == 0);
        EditorLevelManager.OpenLevel(path);
        Check(SceneGroups.Capture().Single().Name == "Room");
        Check(ProjectShockGL.Core.Game.Instance.World.GetAllObjects(true).All(o => !o.IsEditorVisible && o.IsActive));
        Check(!EditorLevelManager.IsDirty);
    }
    finally { File.Delete(path); }
});

RunScene("Brush Movable survives Inspector undo, duplication and level data", () =>
{
    var brush = (BrushBox)SceneObjects.Create(PlaceableType.BrushBox);
    Check(!brush.Movable && brush.IsStatic);
    var before = InspectorState.Capture(brush);
    brush.Movable = true;
    var after = InspectorState.Capture(brush);
    InspectorState.Apply(brush, before); Check(!brush.Movable && brush.IsStatic);
    InspectorState.Apply(brush, after); Check(brush.Movable && !brush.IsStatic);
    var duplicate = (BrushBox)SceneObjects.Duplicate(brush);
    Check(duplicate.Movable && !duplicate.IsStatic);
    var placement = EditorLevelManager.CaptureLevel([brush]).Placeables.Single();
    Check(placement.Brush!.Movable);
    var restored = (BrushBox)EditorLevelManager.CreatePlacement(placement);
    Check(restored.Movable && !restored.IsStatic);
});

RunScene("Palette creation uses short unique names while retaining nested asset paths", () =>
{
    var folder = Path.Combine(testRoot, "Entities", "Logic");
    Directory.CreateDirectory(folder);
    File.Copy(Path.Combine(testRoot, "Entities", "Check.json"), Path.Combine(folder, "Check.json"), true);
    var first = (Entity)SceneObjects.Create(PlaceableType.Entity, "Logic/Check");
    var second = (Entity)SceneObjects.Create(PlaceableType.Entity, "Check");
    Check(first.Name == "Check_1" && first.Path == "Logic/Check");
    Check(second.Name == "Check_2");
    UndoRedo.Undo(); UndoRedo.Redo();
    Check(Selection.Object!.Name == "Check_2");
});

RunScene("Single-object prefabs save independent values and Outputs, and support editor history", () =>
{
    var source = (CheckEntity)SceneObjects.Create(PlaceableType.Entity, "Check");
    source.Number = 17;
    source.Transform.Position = new Vector3(2, 3, 4);
    source.SetActive(false);
    source.EditorGroups.Add("level-only");
    source.Outputs.Add(new OutputData { Event = "OnUsed", TargetName = "gate", Input = "SetActive", Parameter = "true" });
    var name = "Logic/Lever-" + Guid.NewGuid().ToString("N");
    source.Values = [1, 2, 3];
    var path = Content.PrefabPath(name);
    try
    {
        PrefabTools.Save(source, name);
        Check(Content.GetAllPrefabNames().Contains(name));
        var template = Content.LoadPrefab(name)!;
        Check(template.EditorGroups.Length == 0 && template.Outputs.Single().TargetName == "gate");
        var a = (CheckEntity)SceneObjects.CreatePrefab(name);
        var b = (CheckEntity)SceneObjects.CreatePrefab(name);
        Check(a.Number == 17 && b.Number == 17 && !a.IsActive && a.Transform.Position == source.Transform.Position);
        Check(a.Name != b.Name && a.EditorGroups.Count == 0 && a.Path == source.Path);
        a.Outputs[0].Parameter = "false"; a.Number = 99;
        a.Values.Add(4);
        Check(b.Values.SequenceEqual(new[] { 1, 2, 3 }) && source.Values.Count == 3);
        Check(b.Outputs[0].Parameter == "true" && template.Outputs[0].Parameter == "true");
        var reference = EditorObjectReference.Get(b);
        UndoRedo.Undo(); Check(b.IsDestroyed);
        UndoRedo.Redo(); Check(((CheckEntity)reference.Current).Number == 17);
        var editorProperty = typeof(Application).GetProperty(nameof(Application.IsEditor))!;
        try
        {
            editorProperty.SetValue(null, false);
            var runtime = (CheckEntity)Prefab.Instantiate(name, Game.Instance.World, Vector3.One, Quaternion.Identity);
            Check(runtime.Transform.Position == Vector3.One && runtime.Number == 17 && runtime.Outputs[0].Parameter == "true");
        }
        finally { editorProperty.SetValue(null, true); }
        var previous = File.ReadAllText(path);
        Throws(() => PrefabTools.Save(source, name));
        Check(File.ReadAllText(path) == previous);
        Throws(() => Content.PrefabPath("../outside"));
        Throws(() => Content.PrefabPath(Path.GetFullPath("outside")));
    }
    finally { File.Delete(path); }
});

RunScene("Brush prefab retains procedural settings and failed prefab spawn cleans up", () =>
{
    var brush = (BrushBox)SceneObjects.Create(PlaceableType.BrushBox);
    brush.Size = new Vector3(4, 2, 3); brush.Movable = true;
    brush.TextureScale = 2;
    var template = EditorLevelManager.CaptureLevel([brush]).Placeables.Single();
    var copy = (BrushBox)Prefab.Instantiate(template, Game.Instance.World);
    Check(copy.Size == brush.Size && copy.Movable && copy.TextureScale == 2);
    var first = (CheckEntity)SceneObjects.Create(PlaceableType.Entity, "Check");
    first.Values = [5];
    var shared = EditorLevelManager.CaptureLevel([first]).Placeables.Single();
    var left = (CheckEntity)Prefab.Instantiate(shared, Game.Instance.World);
    var right = (CheckEntity)Prefab.Instantiate(shared, Game.Instance.World);
    left.Values.Add(6);
    Check(right.Values.Count == 1 && first.Values.Count == 1);
    var world = Game.Instance.World;
    var rejecting = (RejectingEntity)SceneObjects.Create(PlaceableType.Entity, "Rejecting");
    var bad = EditorLevelManager.CaptureLevel([rejecting]).Placeables.Single();
    var count = world.GetAllObjects(true).Count(o => !o.IsDestroyed);
    RejectingEntity.RejectWrites = true;
    try { Throws(() => Prefab.Instantiate(bad, world)); }
    finally { RejectingEntity.RejectWrites = false; }
    Check(world.GetAllObjects(true).Count(o => !o.IsDestroyed) == count);
});

RunScene("Multiple selection transforms around a common pivot with atomic undo and cancellation", () =>
{
    var a = SceneObjects.Create(PlaceableType.BrushBox);
    var b = SceneObjects.Create(PlaceableType.BrushBox);
    a.Transform.Position = new Vector3(-2, 0, 0);
    b.Transform.Position = new Vector3(2, 0, 0);
    Selection.Object = a; Selection.Toggle(b);
    Check(Selection.Objects.Length == 2 && Selection.Contains(a) && Selection.Object == b);
    Selection.Toggle(a); Check(Selection.Objects.Length == 1);
    Selection.Toggle(a); Check(Selection.Objects.Length == 2);
    UndoRedo.Clear();
    var size = new Vector2(1280, 768);
    var eye = new Vector3(5, 4, 6);
    var vp = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY) * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, size.X / size.Y, .1f, 100);
    var forward = Vector3.Normalize(-eye);
    ObjectManipulator.Local = false;
    ObjectManipulator.Mode = ManipulationMode.Move;
    GizmoMath.Project(Vector3.UnitX, vp, size, out var start);
    GizmoMath.Project(Vector3.UnitX * 2, vp, size, out var end);
    ObjectManipulator.BeginDrag(a, 0, start, 2, forward, vp, size);
    ObjectManipulator.ApplyDrag(end, vp, size, false);
    ObjectManipulator.Commit();
    Check(Vector3.Distance(a.Transform.Position, new Vector3(-1, 0, 0)) < .001f);
    Check(Vector3.Distance(b.Transform.Position, new Vector3(3, 0, 0)) < .001f);
    UndoRedo.Undo(); Check(a.Transform.Position.X == -2 && b.Transform.Position.X == 2 && !UndoRedo.CanUndo);
    UndoRedo.Redo(); UndoRedo.Undo();
    ObjectManipulator.Mode = ManipulationMode.Rotate;
    GizmoMath.Project(Vector3.UnitX, vp, size, out start);
    GizmoMath.Project(Vector3.UnitY, vp, size, out end);
    ObjectManipulator.BeginDrag(a, 2, start, 1, forward, vp, size);
    ObjectManipulator.ApplyDrag(end, vp, size, false);
    Check(Vector3.Distance(a.Transform.Position, new Vector3(0, -2, 0)) < .001f);
    Check(Vector3.Distance(b.Transform.Position, new Vector3(0, 2, 0)) < .001f);
    ObjectManipulator.Commit(); UndoRedo.Undo();
    Check(a.Transform.Rotation == Quaternion.Identity && a.Transform.Position.X == -2);
    ObjectManipulator.Mode = ManipulationMode.Scale;
    ObjectManipulator.BeginDrag(a, 3, Vector2.Zero, 2, forward, vp, size);
    ObjectManipulator.ApplyDrag(new Vector2(100, 0), vp, size, false);
    Check(Math.Abs(a.Transform.Position.X + 2 * MathF.E) < .001f && Math.Abs(b.Transform.Scale.X - MathF.E) < .001f);
    ObjectManipulator.Cancel();
    Check(a.Transform.Position.X == -2 && b.Transform.Position.X == 2 && a.Transform.Scale == Vector3.One);
    ObjectManipulator.BeginDrag(a, 3, Vector2.Zero, 2, forward, vp, size);
    ObjectManipulator.ApplyDrag(new Vector2(100, 0), vp, size, false);
    ObjectManipulator.Commit();
    var reference = EditorObjectReference.Get(a);
    SceneObjects.Delete(a); UndoRedo.Undo(); UndoRedo.Undo();
    Check(reference.Current.Transform.Position.X == -2 && b.Transform.Position.X == 2);
    Selection.Object = b; Check(Selection.Objects.Length == 1);
    Selection.Object = null; Check(Selection.Objects.Length == 0);
});

RunScene("Prefab sets insert relative to a pivot and undo as one independent set", () =>
{
    var a = (CheckEntity)SceneObjects.Create(PlaceableType.Entity, "Check");
    var b = (BrushBox)SceneObjects.Create(PlaceableType.BrushBox);
    a.Transform.Position = new Vector3(8, 0, 0); b.Transform.Position = new Vector3(12, 0, 0);
    a.Values = [1, 2];
    a.Outputs.Add(new OutputData { Event = "OnUsed", TargetName = b.Name, Input = "SetActive", Parameter = "true" });
    var name = "Puzzles/Set-" + Guid.NewGuid().ToString("N");
    var path = Content.PrefabPath(name);
    try
    {
        PrefabTools.Save(new GameObject[] { a, b }, name);
        var data = Content.LoadPrefabData(name)!;
        Check(data.Pivot == new Vector3(10, 0, 0) && data.Placeables.Length == 2);
        Check(data.Placeables[0].Position.X == -2 && data.Placeables[1].Position.X == 2);
        Throws(() => Prefab.Instantiate(name));
        Selection.Set([a, b]); UndoRedo.Clear();
        var inserted = SceneObjects.CreatePrefabSet(name, new Vector3(0, 5, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2));
        Check(Selection.Objects.SequenceEqual(inserted));
        Check(Vector3.Distance(inserted[0].Transform.Position, new Vector3(0, 3, 0)) < .001f);
        Check(Vector3.Distance(inserted[1].Transform.Position, new Vector3(0, 7, 0)) < .001f);
        Check(((CheckEntity)inserted[0]).Outputs[0].TargetName == b.Name);
        var references = inserted.Select(EditorObjectReference.Get).ToArray();
        UndoRedo.Undo(); Check(inserted.All(o => o.IsDestroyed) && Selection.Objects.SequenceEqual(new GameObject[] { a, b }) && !UndoRedo.CanUndo);
        UndoRedo.Redo(); Check(Selection.Objects.SequenceEqual(references.Select(r => r.Current)));
        var second = Prefab.InstantiateAll(data, Game.Instance.World, Vector3.Zero);
        ((CheckEntity)second[0]).Values.Add(3);
        Check(((CheckEntity)references[0].Current).Values.Count == 2 && a.Values.Count == 2);
        Check(second.Select(o => o.Name).Intersect(references.Select(r => r.Current.Name)).Count() == 0);
        var oldName = "Old-" + Guid.NewGuid().ToString("N");
        var oldPath = Content.PrefabPath(oldName);
        try
        {
            File.WriteAllText(oldPath, JsonConvert.SerializeObject(EditorLevelManager.CaptureLevel([a]).Placeables[0], JsonHelper.GetSettings()));
            var legacy = Prefab.InstantiateAll(oldName, Game.Instance.World, Vector3.Zero);
            Check(legacy.Length == 1 && legacy[0].Transform.Position == Vector3.Zero);
        }
        finally { File.Delete(oldPath); }
    }
    finally { File.Delete(path); }
});

RunScene("Failure halfway through prefab set creation leaves no partial set", () =>
{
    var good = SceneObjects.Create(PlaceableType.BrushBox);
    var bad = SceneObjects.Create(PlaceableType.Entity, "Rejecting");
    var placements = EditorLevelManager.CaptureLevel([good, bad]).Placeables;
    var world = Game.Instance.World;
    var count = world.GetAllObjects(true).Count(o => !o.IsDestroyed);
    RejectingEntity.RejectWrites = true;
    try { Throws(() => Prefab.InstantiateAll(new PrefabData { Placeables = placements }, world)); }
    finally { RejectingEntity.RejectWrites = false; }
    Check(world.GetAllObjects(true).Count(o => !o.IsDestroyed) == count);
});

RunScene("Multiple deletion restores scene order, selection and earlier group transforms", () =>
{
    var a = (CheckEntity)SceneObjects.Create(PlaceableType.Entity, "Check");
    var middle = SceneObjects.Create(PlaceableType.BrushBox);
    var b = (CheckEntity)SceneObjects.Create(PlaceableType.Entity, "Check");
    a.Values = [1, 2]; b.Number = 7; b.SetActive(false);
    a.Outputs.Add(new OutputData { Event = "Test", TargetName = b.Name, Input = "SetActive", Parameter = "true" });
    a.EditorGroups.Add("group");
    var aName = a.Name; var bName = b.Name;
    Selection.Set([b, a]); UndoRedo.Clear();
    var ar = EditorObjectReference.Get(a); var br = EditorObjectReference.Get(b);
    var size = new Vector2(1280, 768); var eye = new Vector3(5, 4, 6);
    var vp = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY) * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, size.X / size.Y, .1f, 100);
    ObjectManipulator.Mode = ManipulationMode.Scale; ObjectManipulator.Local = false;
    ObjectManipulator.BeginDrag(a, 3, Vector2.Zero, 2, Vector3.Normalize(-eye), vp, size);
    ObjectManipulator.ApplyDrag(new Vector2(50, 0), vp, size, false); ObjectManipulator.Commit();
    var scale = a.Transform.Scale;
    SceneObjects.Delete(Selection.Objects);
    Check(a.IsDestroyed && b.IsDestroyed && Selection.Objects.Length == 0);
    for (var cycle = 0; cycle < 3; cycle++)
    {
        UndoRedo.Undo();
        Check(Game.Instance.World.GetAllObjects(true).Where(o => !o.IsDestroyed).SequenceEqual(new[] { ar.Current, middle, br.Current }));
        Check(Selection.Objects.SequenceEqual(new[] { br.Current, ar.Current }));
        Check(ar.Current.Name == aName && br.Current.Name == bName && !br.Current.IsActive);
        Check(((CheckEntity)ar.Current).Values.Count == 2 && ar.Current.EditorGroups.Contains("group"));
        Check(((CheckEntity)ar.Current).Outputs[0].TargetName == bName);
        UndoRedo.Undo(); Check(ar.Current.Transform.Scale == Vector3.One && br.Current.Transform.Scale == Vector3.One && !UndoRedo.CanUndo);
        UndoRedo.Redo(); Check(ar.Current.Transform.Scale == scale);
        UndoRedo.Redo(); Check(ar.Current.IsDestroyed && br.Current.IsDestroyed);
    }
});

RunScene("Multiple duplication is one action with independent data and stable redo names", () =>
{
    var a = (CheckEntity)SceneObjects.Create(PlaceableType.Entity, "Check");
    var b = (BrushBox)SceneObjects.Create(PlaceableType.BrushBox);
    a.Values = [1]; b.Size = new Vector3(2, 3, 4); b.EditorGroups.Add("group");
    a.Outputs.Add(new OutputData { Event = "Test", TargetName = a.Name, Input = "SetActive", Parameter = "true" });
    Selection.Set([a, b]); UndoRedo.Clear();
    Check(Editor.CanDuplicateSelection);
    var copies = SceneObjects.Duplicate(Selection.Objects);
    Check(Selection.Objects.SequenceEqual(copies));
    Check(((CheckEntity)copies[0]).Outputs[0].TargetName == copies[0].Name);
    ((CheckEntity)copies[0]).Values.Add(2); Check(a.Values.Count == 1);
    Check(((BrushBox)copies[1]).Size == b.Size && copies[1].EditorGroups.Contains("group"));
    var refs = copies.Select(EditorObjectReference.Get).ToArray(); var names = copies.Select(o => o.Name).ToArray();
    for (var cycle = 0; cycle < 3; cycle++)
    {
        UndoRedo.Undo(); Check(refs.All(r => r.Current.IsDestroyed) && Selection.Objects.SequenceEqual(new GameObject[] { a, b }) && !UndoRedo.CanUndo);
        UndoRedo.Redo(); Check(Selection.Objects.SequenceEqual(refs.Select(r => r.Current)));
        Check(refs.Select(r => r.Current.Name).SequenceEqual(names) && ((CheckEntity)refs[0].Current).Values.Count == 1);
    }
});

RunScene("Failed batch duplication leaves no partial copies or history changes", () =>
{
    var a = SceneObjects.Create(PlaceableType.BrushBox);
    var b = SceneObjects.Create(PlaceableType.Entity, "Rejecting");
    Selection.Set([a, b]); UndoRedo.Clear();
    var before = Game.Instance.World.GetAllObjects(true).Where(o => !o.IsDestroyed).ToArray();
    RejectingEntity.RejectWrites = true;
    try { Throws(() => SceneObjects.Duplicate(Selection.Objects)); }
    finally { RejectingEntity.RejectWrites = false; }
    Check(Game.Instance.World.GetAllObjects(true).Where(o => !o.IsDestroyed).SequenceEqual(before));
    Check(Selection.Objects.SequenceEqual(new[] { a, b }) && !UndoRedo.CanUndo);
});

RunScene("Batch deletion remains undoable after callback errors and failed restoration can be retried", () =>
{
    var good = SceneObjects.Create(PlaceableType.BrushBox);
    var rejecting = SceneObjects.Create(PlaceableType.Entity, "Rejecting");
    Selection.Set([good, rejecting]); UndoRedo.Clear();
    var refs = Selection.Objects.Select(EditorObjectReference.Get).ToArray();
    SceneObjects.Delete(Selection.Objects);
    RejectingEntity.RejectWrites = true;
    try { Throws(UndoRedo.Undo); }
    finally { RejectingEntity.RejectWrites = false; }
    Check(Game.Instance.World.GetAllObjects(true).All(o => o.IsDestroyed) && UndoRedo.CanUndo);
    UndoRedo.Undo();
    Check(Selection.Objects.SequenceEqual(refs.Select(r => r.Current)) && refs.All(r => !r.Current.IsDestroyed));
    var throwing = SceneObjects.Create(PlaceableType.Entity, "ThrowDestroy");
    var plain = SceneObjects.Create(PlaceableType.BrushBox);
    Selection.Set([throwing, plain]); UndoRedo.Clear();
    var throwingRef = EditorObjectReference.Get(throwing);
    ThrowingDeleteEntity.RejectDestroy = true;
    try { Throws(() => SceneObjects.Delete(Selection.Objects)); }
    finally { ThrowingDeleteEntity.RejectDestroy = false; }
    Check(throwing.IsDestroyed && plain.IsDestroyed && UndoRedo.CanUndo);
    UndoRedo.Undo(); Check(!throwingRef.Current.IsDestroyed && Selection.Objects.Length == 2);
});

RunScene("Brush resize keeps opposite faces fixed under rotation and signed nonuniform scale", () =>
{
    var brush = (BrushBox)SceneObjects.Create(PlaceableType.BrushBox);
    Selection.Object = brush;
    var screen = new Vector2(1280, 768);
    var center = new Vector3(1, 2, 0);
    var eye = center + new Vector3(8, 7, 10);
    var forward = Vector3.Normalize(center - eye);
    var vp = Matrix4x4.CreateLookAt(eye, center, Vector3.UnitY) * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, screen.X / screen.Y, .1f, 100);
    var axes = new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
    foreach (var scale in new[] { Vector3.One, new Vector3(2, .5f, 3), new Vector3(-2, 1, .75f) })
    foreach (var rotation in new[] { Quaternion.Identity, Quaternion.CreateFromYawPitchRoll(.6f, .3f, .2f) })
    for (var face = 0; face < 6; face++)
    {
        brush.Size = new Vector3(4, 3, 2); brush.Transform.Position = center;
        brush.Transform.Scale = scale; brush.Transform.Rotation = rotation;
        var initial = TransformState.Capture(brush.Transform); var initialSize = brush.Size;
        var index = face / 2; var sign = face % 2 == 0 ? -1 : 1;
        var local = axes[index] * initialSize[index] * .5f * sign;
        var moving = brush.Transform.TransformPoint(local);
        var fixedPoint = brush.Transform.TransformPoint(-local);
        var outward = Vector3.Transform(axes[index] * sign * MathF.Sign(scale[index]), rotation);
        Check(GizmoMath.Project(moving, vp, screen, out var start));
        Check(GizmoMath.Project(moving + outward * 1.3f, vp, screen, out var end));
        Check(BrushResizer.BeginDrag(brush, face, start, forward, vp, screen));
        BrushResizer.ApplyDrag(end, vp, screen, false);
        Check(Math.Abs(brush.Size[index] - (initialSize[index] + 1.3f / Math.Abs(scale[index]))) < .002f);
        Check(Vector3.Distance(brush.Transform.TransformPoint(-axes[index] * brush.Size[index] * .5f * sign), fixedPoint) < .002f);
        Check(brush.Transform.Scale == scale && brush.Transform.Rotation == rotation);
        ObjectManipulator.Cancel();
        Check(brush.Size == initialSize && TransformState.Capture(brush.Transform) == initial);
    }
});

RunScene("Resize snap, minimum size and combined size-position history survive restored objects", () =>
{
    var brush = (BrushBox)SceneObjects.Create(PlaceableType.BrushBox);
    brush.Size = new Vector3(2); brush.Transform.Scale = new Vector3(2, 1, 1);
    Selection.Object = brush; UndoRedo.Clear();
    var reference = EditorObjectReference.Get(brush);
    var screen = new Vector2(1280, 768); var eye = new Vector3(5, 4, 6); var forward = Vector3.Normalize(-eye);
    var vp = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY) * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, screen.X / screen.Y, .1f, 100);
    var previousStep = SceneGrid.Step;
    try
    {
        SceneGrid.Step = 1;
        GizmoMath.Project(new Vector3(2, 0, 0), vp, screen, out var start);
        GizmoMath.Project(new Vector3(2.73f, 0, 0), vp, screen, out var end);
        Check(BrushResizer.BeginDrag(brush, 1, start, forward, vp, screen));
        BrushResizer.ApplyDrag(end, vp, screen, true); ObjectManipulator.Commit();
        Check(Math.Abs(brush.Size.X - 2.5f) < .001f && Math.Abs(brush.Transform.Position.X - .5f) < .001f);
        UndoRedo.Undo(); Check(brush.Size.X == 2 && brush.Transform.Position == Vector3.Zero && !UndoRedo.CanUndo);
        UndoRedo.Redo();
        var size = brush.Size; var position = brush.Transform.Position;
        SceneObjects.Delete(brush); UndoRedo.Undo(); UndoRedo.Undo();
        var restored = (BrushBox)reference.Current;
        Check(restored.Size.X == 2 && restored.Transform.Position == Vector3.Zero);
        UndoRedo.Redo(); Check(restored.Size == size && restored.Transform.Position == position);
        GizmoMath.Project(restored.Transform.TransformPoint(Vector3.UnitX * restored.Size.X * .5f), vp, screen, out start);
        GizmoMath.Project(new Vector3(-10, 0, 0), vp, screen, out end);
        Check(BrushResizer.BeginDrag(restored, 1, start, forward, vp, screen));
        BrushResizer.ApplyDrag(end, vp, screen, false);
        Check(restored.Size.X == .01f && Math.Abs(restored.Transform.Position.X - restored.Size.X + 2) < .002f);
        ObjectManipulator.Cancel();
    }
    finally { SceneGrid.Step = previousStep; ObjectManipulator.Cancel(); }
});

if (args.Contains("--imgui"))
{
    var context = ImGuiNET.ImGui.CreateContext();
    try
    {
        var io = ImGuiNET.ImGui.GetIO();
        unsafe { io.NativePtr->IniFilename = null; io.NativePtr->LogFilename = null; }
        io.DisplaySize = new Vector2(600,1000);
        io.DeltaTime = 1f/60;
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int bytesPerPixel);
        io.Fonts.SetTexID((IntPtr)1);
        void Frame(Action draw, float x = -100, float y = -100, bool down = false)
        {
            io.AddMousePosEvent(x,y);
            io.AddMouseButtonEvent(0,down);
            ImGuiNET.ImGui.NewFrame();
            ImGuiNET.ImGui.SetNextWindowPos(Vector2.Zero);
            ImGuiNET.ImGui.SetNextWindowSize(new Vector2(400,900));
            draw();
            ImGuiNET.ImGui.Render();
        }
        RunScene("Real Scene Ctrl-click toggles membership and multiple Inspector does not edit objects", () =>
        {
            var a = SceneObjects.Create(PlaceableType.BrushBox);
            var b = SceneObjects.Create(PlaceableType.BrushBox);
            Selection.Object = null;
            var sizeField = typeof(Editor).GetField("_windowSize", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            var previousSize = sizeField.GetValue(null);
            sizeField.SetValue(null, new Vector2(1280, 768));
            var drawScene = typeof(Editor).GetMethod("DrawHierarchy", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            void Scene() => drawScene.Invoke(null, null);
            try
            {
                Frame(Scene);
                Frame(Scene, 30, 110);
                Frame(Scene, 30, 110, true); Frame(Scene, 30, 110, false);
                Check(Selection.Object == a && Selection.Objects.Length == 1);
                io.AddKeyEvent(ImGuiNET.ImGuiKey.ModCtrl, true);
                Frame(Scene, 30, 127);
                Frame(Scene, 30, 127, true); Frame(Scene, 30, 127, false);
                Check(Selection.Contains(a) && Selection.Contains(b) && Selection.Objects.Length == 2);
                Frame(Inspector.Draw, 30, 110, true); Frame(Inspector.Draw, 80, 110, true); Frame(Inspector.Draw, 80, 110, false);
                Check(a.Transform.Position == Vector3.Zero && b.Transform.Position == Vector3.Zero);
                Frame(Scene); Frame(Scene, 30, 110);
                Frame(Scene, 30, 110, true); Frame(Scene, 30, 110, false);
                Check(Selection.Object == b && Selection.Objects.Length == 1);
            }
            finally
            {
                io.AddKeyEvent(ImGuiNET.ImGuiKey.ModCtrl, false);
                Frame(Scene);
                sizeField.SetValue(null, previousSize);
            }
        });
        Run("Real ImGui Inspector checkbox and a multi-frame numeric drag undo before the earlier transform", () =>
        {
            EditorLevelManager.NewLevel();
            var obj = (CheckEntity)Entity.Create("Check")!;
            Selection.Object = obj;
            Frame(Inspector.Draw);
            var clicked = false;
            for (var y = 105; y <= 135 && !clicked; y += 3)
            {
                Frame(Inspector.Draw,15,y,true);
                Frame(Inspector.Draw,15,y,false);
                clicked = !obj.IsActive;
            }
            Check(clicked && UndoRedo.CanUndo);
            UndoRedo.Undo();
            Check(obj.IsActive && !UndoRedo.CanUndo);
            UndoRedo.Redo();
            Check(!obj.IsActive);
            obj.SetActive(true);
            UndoRedo.Clear();
            obj.Transform.Position = Vector3.One;
            UndoRedo.Register(new PropertyChangeAction<Vector3>(v => obj.Transform.Position=v,Vector3.Zero,Vector3.One));
            for (var y = 170; y <= 205 && obj.Number == 0; y += 3)
            {
                Frame(Inspector.Draw,30,y,true);
                Frame(Inspector.Draw,70,y,true);
                Frame(Inspector.Draw,110,y,true);
                Frame(Inspector.Draw,110,y,false);
            }
            Check(obj.Number != 0);
            var number = obj.Number;
            UndoRedo.Undo();
            Check(obj.Number == 0 && obj.Transform.Position == Vector3.One);
            UndoRedo.Undo();
            Check(obj.Transform.Position == Vector3.Zero && !UndoRedo.CanUndo);
            UndoRedo.Redo();
            UndoRedo.Redo();
            Check(obj.Number == number);
        });
        Run("Real ImGui text editing is one history action after leaving the field", () =>
        {
            EditorLevelManager.NewLevel();
            var obj = (CheckEntity)Entity.Create("Check")!;
            Selection.Object = obj;
            Frame(Inspector.Draw);
            for (var y = 145; y <= 175 && !io.WantTextInput; y += 3)
            {
                Frame(Inspector.Draw,30,y,true);
                Frame(Inspector.Draw,30,y,false);
            }
            Check(io.WantTextInput);
            foreach (var ch in "Door-01")
            {
                io.AddInputCharacter(ch);
                Frame(Inspector.Draw);
            }
            Check(obj.Name == "Door-01");
            Frame(Inspector.Draw,350,500,true);
            Frame(Inspector.Draw,350,500,false);
            UndoRedo.Undo();
            Check(obj.Name == "" && !UndoRedo.CanUndo);
            UndoRedo.Redo();
            Check(obj.Name == "Door-01");
        });
        RunScene("Real Palette creation and Inspector deletion participate in the same history", () =>
        {
            var world = Game.Instance.World;
            Frame(Palette.Draw);
            for (var y=48;y<=78 && world.GetAllObjects(true).Length==0;y+=3)
            {
                Frame(Palette.Draw,20,y,true);
                Frame(Palette.Draw,20,y,false);
            }
            var brush = Selection.Object as BrushBox;
            Check(brush != null && UndoRedo.CanUndo);
            var reference = EditorObjectReference.Get(brush!);
            UndoRedo.Undo();
            Check(world.GetAllObjects(true).Length==0 && brush!.IsDestroyed);
            UndoRedo.Redo();
            Check(Selection.Object==reference.Current && !reference.Current.IsDestroyed);
            Frame(Inspector.Draw);
            for(var y=310;y<=560 && world.GetAllObjects(true).Length>0;y+=3)
            {
                Frame(Inspector.Draw,185,y,true);
                Frame(Inspector.Draw,185,y,false);
            }
            Check(world.GetAllObjects(true).Length==0);
            UndoRedo.Undo();
            Check(Selection.Object==reference.Current && world.GetAllObjects(true).Length==1);
            UndoRedo.Undo();
            Check(world.GetAllObjects(true).Length==0);
        });
        Run("Real ImGui Environment checkbox joins the same undo history", () =>
        {
            UndoRedo.Clear();
            var open = true;
            void DrawEnvironment() => EnvironmentWindow.Draw(ref open);
            Frame(DrawEnvironment);
            var fog = Environment.IsFogEnabled;
            for (var y = 65; y <= 100 && Environment.IsFogEnabled == fog; y += 3)
            {
                Frame(DrawEnvironment,15,y,true);
                Frame(DrawEnvironment,15,y,false);
            }
            Check(Environment.IsFogEnabled != fog && UndoRedo.CanUndo);
            UndoRedo.Undo();
            Check(Environment.IsFogEnabled == fog && !UndoRedo.CanUndo);
            UndoRedo.Redo();
            Check(Environment.IsFogEnabled != fog);
        });
    }
    finally { ImGuiNET.ImGui.DestroyContext(context); }
}

Game.Instance.World.Clear();
Console.WriteLine("All editor checks passed.");

static void RunScene(string name, Action test)
{
    Run(name,()=> {
        var property = typeof(Application).GetProperty(nameof(Application.IsEditor))!;
        var previous = Application.IsEditor;
        property.SetValue(null,true);
        try { EditorLevelManager.NewLevel(); test(); }
        finally { EditorLevelManager.NewLevel(); property.SetValue(null,previous); }
    });
}

static void RecordInspector(GameObject obj, string field, Action change, bool editing = false)
{
    var before = InspectorState.Capture(obj);
    change();
    UndoRedo.RecordEdit(obj, field, before, InspectorState.Capture(obj), state => InspectorState.Apply(obj,state), editing);
}

static bool SameEnvironment(EnvironmentData a, EnvironmentData b) =>
    a.AmbientColor.Equals(b.AmbientColor) && a.IsFogEnabled == b.IsFogEnabled &&
    a.FogColor.Equals(b.FogColor) && a.FogNear == b.FogNear && a.FogFar == b.FogFar;

static void Run(string name, Action action) { action(); Console.WriteLine($"PASS: {name}"); }
static void Check(bool condition) { if (!condition) throw new Exception("Editor check failed"); }
static void Throws(Action action)
{
    try { action(); }
    catch { return; }
    throw new Exception("Expected operation to fail");
}

public sealed class CheckGame : Game
{
    // Tool fixtures have no GPU context; the normal Game.Load now also loads a postprocess program.
    public void LoadForTest()
    {
        typeof(Game).GetProperty(nameof(World))!.SetValue(this, new World());
        Layers.Load();
        Physics.Initialize();
        var ui = new ProjectShockGL.UI.UIManager();
        ui.Initialize();
        typeof(Game).GetProperty(nameof(UIManager))!.SetValue(this, ui);
    }
    public void UpdateForTest() => Update(0);
}
public class CheckEntity : Entity
{
    public int Starts;
    public override void Start() { base.Start(); Starts++; }
    [Editable] public int Number { get; set; }
    [Editable] public List<int> Values { get; set; } = [];
    public override void Create() { base.Create(); AddComponent<SphereCollider>(); }
}
public sealed class BrokenEntity : CheckEntity
{
    public override void Create() { base.Create(); throw new InvalidOperationException("Expected creation failure"); }
}
public sealed class ThrowingValue { public string Value => throw new InvalidOperationException("Expected serialization failure"); }
public sealed class RejectingEntity : Entity
{
    public static bool RejectWrites;
    private int _value;
    [Editable] public int Value
    {
        get => _value;
        set
        {
            if (RejectWrites) throw new InvalidOperationException("Expected duplicate failure");
            _value = value;
        }
    }
    public override void Create() { base.Create(); AddComponent<SphereCollider>(); }
}

public sealed class RuntimeProbe : Entity
{
    public int Starts, Updates, Enters;
    public override void Create() { base.Create(); AddComponent<SphereCollider>().IsTrigger = true; }
    public override void Start() { base.Start(); Starts++; }
    public override void Update(float dt) { base.Update(dt); Updates++; }
    public override void OnTriggerEnter(GameObject other) => Enters++;
}

public sealed class ThrowingDeleteEntity : CheckEntity
{
    public static bool RejectDestroy = true;
    public override void Destroy() { if (RejectDestroy) throw new InvalidOperationException("Destroy before base.Destroy"); base.Destroy(); }
}


