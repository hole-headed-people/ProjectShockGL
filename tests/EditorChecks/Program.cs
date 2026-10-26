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

Run("Repeated duplication reserves unique pending names and clears transform history", () =>
{
    UndoRedo.Register(new PropertyChangeAction<int>(_ => { }, 0, 1));
    Editor.DuplicateSelection();
    Check(Selection.Object!.Name == "switch_3" && !UndoRedo.CanUndo && !UndoRedo.CanRedo);
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

Game.Instance.World.Clear();
Console.WriteLine("All editor checks passed.");

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

public sealed class CheckGame : Game { public void LoadForTest() => Load(); }
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
