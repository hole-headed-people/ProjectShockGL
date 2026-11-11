using System.Numerics;
using System.Reflection;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;
using Silk.NET.Maths;
using Body = ProjectShockGL.Core.Object;

var count = 0;
void Check(bool value)
{
    if (!value) throw new Exception($"Camera check {count + 1} failed.");
    count++;
}
void Sync() => typeof(Camera).GetMethod("SynchronizeComponents", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
var world = new World();
Camera.UseDefault();
try
{
    var owner = world.Spawn<Body>();
    owner.Transform.Position = new Vector3(2, 3, 4);
    owner.Transform.Scale = new Vector3(2, 3, 1);
    owner.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, .7f);
    var component = owner.AddComponent<CameraComponent>();
    Check(Camera.Active == Camera.Default);
    component.LocalPosition = new Vector3(0, .6f, 1);
    component.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, .3f);
    Check(component.MakeActive());
    Check(Vector3.Distance(component.Camera.Transform.Position, owner.Transform.TransformPoint(component.LocalPosition)) < .0001f);
    Check(Math.Abs(Quaternion.Dot(component.Camera.Transform.Rotation, Quaternion.Normalize(owner.Transform.Rotation * component.LocalRotation))) > .99999f);
    owner.Transform.Position += Vector3.One;
    Sync();
    Check(Vector3.Distance(component.Camera.Transform.Position, owner.Transform.TransformPoint(component.LocalPosition)) < .0001f);
    var otherOwner = world.Spawn<Body>();
    var other = otherOwner.AddComponent<CameraComponent>();
    Check(Camera.Active == component.Camera);
    other.MakeActive();
    component.IsEnabled = false;
    Check(Camera.Active == other.Camera);
    other.IsEnabled = false;
    Check(Camera.Active == Camera.Default && !other.MakeActive());
    other.IsEnabled = true;
    Check(Camera.Active == Camera.Default);
    other.MakeActive();
    otherOwner.SetActive(false);
    otherOwner.SetActive(true);
    Check(Camera.Active == Camera.Default);
    other.MakeActive();
    otherOwner.RemoveComponent<CameraComponent>();
    Check(Camera.Active == Camera.Default && other.Camera.IsDisposed);
    component.IsEnabled = true;
    component.MakeActive();
    world.Clear();
    Check(Camera.Active == Camera.Default && component.Camera.IsDisposed);

    var resize = typeof(Camera).GetMethod("OnFramebufferResize", BindingFlags.Static | BindingFlags.NonPublic)!;
    var initialized = typeof(Camera).GetField("_isInitialized", BindingFlags.Static | BindingFlags.NonPublic)!;
    initialized.SetValue(null, true);
    var a = Camera.Create(); var b = Camera.Create();
    Camera.ToggleTo(a);
    resize.Invoke(null, [new Vector2D<int>(1600, 800)]);
    Check(a.AspectRatio == 2 && Camera.Default.AspectRatio == 2);
    Camera.ToggleTo(b); Check(b.AspectRatio == 2);
    resize.Invoke(null, [new Vector2D<int>(0, 0)]);
    Check(b.AspectRatio == 2);
    b.Dispose(); Check(Camera.Active == Camera.Default);
    a.Dispose();
    initialized.SetValue(null, false);

    Physics.Initialize();
    var player = world.Spawn<Body>();
    player.AddComponent<CharacterController>();
    var attached = player.AddComponent<CameraComponent>();
    attached.LocalPosition = Vector3.UnitY * .6f;
    attached.MakeActive();
    var obstacle = world.Spawn<Body>();
    obstacle.Transform.Position = new Vector3(-.7f, 0, 0);
    var box = obstacle.AddComponent<BoxCollider>();
    box.HalfExtents = new Vector3(.5f, 2, 2);
    var game = new CheckGame();
    typeof(Game).GetProperty("World")!.SetValue(game, world);
    typeof(Game).GetField("_timerManager", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, TimerManager.Create());
    var ui = new ProjectShockGL.UI.UIManager();
    typeof(ProjectShockGL.UI.UIManager).GetField("_isDestroyed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(ui, true);
    typeof(Game).GetProperty("UIManager")!.SetValue(game, ui);
    game.Step(.1f);
    Check(player.Transform.Position.X > 0 && Vector3.Distance(attached.Camera.Transform.Position, player.Transform.Position + attached.LocalPosition) > .0001f);
    Sync();
    Check(player.Transform.Position.X > 0 && Vector3.Distance(attached.Camera.Transform.Position, player.Transform.Position + attached.LocalPosition) < .0001f);
    var editor = typeof(Application).GetProperty(nameof(Application.IsEditor))!;
    editor.SetValue(null, true);
    try { Camera.UseDefault(); Check(!attached.MakeActive() && Camera.Active == Camera.Default); }
    finally { editor.SetValue(null, false); }
}
finally
{
    world.Clear();
    Camera.UseDefault();
}
Console.WriteLine($"PASS: {count} camera checks");

public sealed class CheckGame : Game
{
    public void Step(float delta) => base.Update(delta);
}
