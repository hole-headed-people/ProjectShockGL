using System.Numerics;
using ProjectShockGL;
using ProjectShockGL.Rendering;
using ProjectShockGL.UI;
using Silk.NET.Input;

Run("Settings creates one custom canvas, initialized once with its size", () =>
{
    Configure("ConfiguredCanvas, UIChecks");
    var manager = new UIManager();
    manager.Initialize();
    var canvas = (ConfiguredCanvas)manager.Canvas;
    var pointer = manager.Pointer;
    manager.Initialize();
    Check(ReferenceEquals(canvas, manager.Canvas) && canvas.Creates == 1);
    Check(ReferenceEquals(pointer, manager.Pointer));
    Check(canvas.SizeAtCreation == new Vector2(800, 600));
    manager.Destroy();
});

Run("Missing or invalid settings use a default canvas", () =>
{
    foreach (var type in new[] { "", "Missing.Canvas, Missing", "System.String, System.Private.CoreLib" })
    {
        Configure(type);
        var manager = new UIManager();
        manager.Initialize();
        Check(manager.Canvas.GetType() == typeof(Canvas));
        manager.Destroy();
    }
});

Run("Children inherit canvas and support reparenting back to the root", () =>
{
    var canvas = new Canvas();
    var parent = canvas.Add<Widget>();
    var child = parent.Add<Widget>();
    Check(child.Canvas == canvas && child.Parent == parent && parent.Children.Single() == child);
    child.SetParent(null);
    Check(child.Parent == null && parent.Children.Count == 0 && canvas.Children.Count == 2);
    child.SetParent(parent);
    Check(canvas.Children.Count == 1 && child.Parent == parent);
    Throws<InvalidOperationException>(() => parent.SetParent(child));
    Throws<InvalidOperationException>(() => child.SetParent(child));
    Throws<InvalidOperationException>(() => child.SetParent(new Canvas().Add<Widget>()));
});

Run("Anchors and pivots follow nested parent movement and canvas resize", () =>
{
    var canvas = new Canvas();
    canvas.Resize(new Vector2(800, 600));
    var panel = canvas.Add<Widget>();
    panel.Rect.Size = new Vector2(200, 100);
    panel.Rect.Anchor = panel.Rect.Pivot = new Vector2(0.5f);
    Check(panel.Rect.AbsolutePosition == new Vector2(300, 250));
    var child = panel.Add<Widget>();
    child.Rect.Size = new Vector2(20, 10);
    child.Rect.Anchor = child.Rect.Pivot = Vector2.One;
    child.Rect.Position = new Vector2(-5, -5);
    Check(child.Rect.AbsolutePosition == new Vector2(475, 335));
    canvas.Resize(new Vector2(1000, 800));
    Check(child.Rect.AbsolutePosition == new Vector2(575, 435));
    panel.Rect.Position += new Vector2(10, 20);
    Check(child.Rect.AbsolutePosition == new Vector2(585, 455));
    Check(child.Rect.Contains(new Vector2(585, 455)));
    Check(!child.Rect.Contains(new Vector2(605, 465)));
    Throws<ArgumentOutOfRangeException>(() => child.Rect.Size = new Vector2(-1));
});

Run("Traversal draws parents before children and later siblings on top", () =>
{
    var canvas = new Canvas();
    var events = new List<string>();
    var first = canvas.Add<Probe>();
    first.DrawAction = () => events.Add("first");
    first.Add<Probe>().DrawAction = () => events.Add("child");
    canvas.Add<Probe>().DrawAction = () => events.Add("last");
    canvas.Draw(null!); // Probes record traversal without requiring an OpenGL context.
    Check(events.SequenceEqual(new[] { "first", "child", "last" }));
});

Run("Visibility hides a branch; disabled branches stop updating", () =>
{
    var canvas = new Canvas();
    var parent = canvas.Add<Probe>();
    var child = parent.Add<Probe>();
    parent.Visible = false;
    canvas.Draw(null!);
    canvas.Update(0);
    Check(parent.Draws == 0 && child.Draws == 0 && child.Updates == 1);
    parent.IsEnabled = false;
    canvas.Update(0);
    Check(child.Updates == 1);
    parent.Visible = true;
    canvas.Draw(null!);
    Check(child.Draws == 1);
});

Run("Removing a branch during traversal skips it and destroys it once", () =>
{
    var canvas = new Canvas();
    var first = canvas.Add<Probe>();
    var parent = canvas.Add<Probe>();
    var child = parent.Add<Probe>();
    first.UpdateAction = parent.Destroy;
    canvas.Update(0);
    canvas.Clear();
    parent.Destroy();
    Check(parent.Updates == 0 && child.Updates == 0);
    Check(parent.Destroys == 1 && child.Destroys == 1 && canvas.Children.Count == 0);
    Check(parent.Children.Count == 0 && child.Parent == null);
    Throws<ObjectDisposedException>(() => parent.Add<Widget>());
});

Run("Reparenting during traversal updates and draws a widget at most once", () =>
{
    var canvas = new Canvas();
    var moving = canvas.Add<Probe>();
    var parent = canvas.Add<Probe>();
    moving.UpdateAction = () => moving.SetParent(parent);
    canvas.Update(0);
    Check(moving.Updates == 1);
    moving.SetParent(null);
    moving.DrawAction = () => moving.SetParent(parent);
    parent.DrawAction = () => moving.SetParent(null);
    canvas.Draw(null!);
    Check(moving.Draws == 1);
});

Run("Destroyed canvas prevents additions and destroys descendants once", () =>
{
    var canvas = new Canvas();
    var widget = canvas.Add<Probe>();
    var child = widget.Add<Probe>();
    canvas.Destroy();
    canvas.Destroy();
    canvas.Update(0);
    canvas.Draw(null!);
    Check(widget.Destroys == 1 && child.Destroys == 1 && child.Updates == 0);
    Throws<ObjectDisposedException>(() => canvas.Add<Widget>());
});

Run("Game loads, updates and destroys its configured UI", () =>
{
    Configure("ConfiguredCanvas, UIChecks");
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Layers.ini"), "[Layers]\n0=Default\n");
    var game = new TestGame();
    game.LoadForTest();
    var canvas = (ConfiguredCanvas)game.UIManager.Canvas;
    var probe = canvas.Add<Probe>();
    game.UpdateForTest();
    Check(probe.Updates == 1);
    game.UnloadForTest();
    Check(canvas.IsDestroyed && probe.Destroys == 1);
});

Run("Mouse enter/exit target only the topmost widget, without repeated enter", () =>
{
    var (canvas, pointer) = PointerScene();
    var bottom = canvas.Add<MouseProbe>();
    var top = canvas.Add<MouseProbe>();
    Mouse(pointer, new Vector2(10));
    Mouse(pointer, new Vector2(10));
    Check(pointer.HoveredWidget == top && top.IsMouseOver && !bottom.IsMouseOver);
    Check(top.Events.SequenceEqual(new[] { "enter" }) && bottom.Events.Count == 0);
    Mouse(pointer, new Vector2(150));
    Check(pointer.HoveredWidget == null && !top.IsMouseOver);
    Check(top.Events.SequenceEqual(new[] { "enter", "exit" }));
});

Run("Children take priority and decorations pass through without blocking children", () =>
{
    var (canvas, pointer) = PointerScene();
    var bottom = canvas.Add<MouseProbe>();
    var parent = canvas.Add<MouseProbe>();
    parent.HitTestVisible = false;
    parent.Rect.Size = new Vector2(20);
    var child = parent.Add<MouseProbe>();
    child.Rect.Position = new Vector2(30);
    child.Rect.Size = new Vector2(20);
    Mouse(pointer, new Vector2(35));
    Check(pointer.HoveredWidget == child); // No clipping: the child extends outside its parent.
    child.HitTestVisible = false;
    Mouse(pointer, new Vector2(35));
    Check(pointer.HoveredWidget == bottom && !child.IsMouseOver);
});

Run("Hidden or disabled ancestor branches are skipped", () =>
{
    var (canvas, pointer) = PointerScene();
    var bottom = canvas.Add<MouseProbe>();
    var parent = canvas.Add<MouseProbe>();
    var child = parent.Add<MouseProbe>();
    parent.Visible = false;
    Mouse(pointer, new Vector2(10));
    Check(pointer.HoveredWidget == bottom && child.Events.Count == 0);
    parent.Visible = true;
    parent.IsEnabled = false;
    Mouse(pointer, new Vector2(10));
    Check(pointer.HoveredWidget == bottom);
    parent.IsEnabled = true;
    Mouse(pointer, new Vector2(10));
    Check(pointer.HoveredWidget == child);
});

Run("Release goes to the pressed widget even after leaving the canvas", () =>
{
    var (canvas, pointer) = PointerScene();
    var widget = canvas.Add<MouseProbe>();
    Mouse(pointer, new Vector2(10));
    Mouse(pointer, new Vector2(10), MouseButton.Left);
    Mouse(pointer, new Vector2(300), MouseButton.Left);
    Check(pointer.HoveredWidget == null && pointer.GetPressedWidget(MouseButton.Left) == widget);
    Mouse(pointer, new Vector2(300));
    Check(widget.Events.SequenceEqual(new[] { "enter", "press:Left", "exit", "release:Left" }));
    Check(pointer.GetPressedWidget(MouseButton.Left) == null);
});

Run("Left and right buttons capture independently and do not transfer to hovered widgets", () =>
{
    var (canvas, pointer) = PointerScene();
    var left = canvas.Add<MouseProbe>();
    var right = canvas.Add<MouseProbe>();
    right.Rect.Position = new Vector2(100, 0);
    Mouse(pointer, new Vector2(10));
    Mouse(pointer, new Vector2(10), MouseButton.Left);
    Mouse(pointer, new Vector2(110, 10), MouseButton.Left, MouseButton.Right);
    Check(pointer.GetPressedWidget(MouseButton.Left) == left && pointer.GetPressedWidget(MouseButton.Right) == right);
    Mouse(pointer, new Vector2(110, 10), MouseButton.Right);
    Check(left.Events.Contains("release:Left") && !right.Events.Contains("release:Left"));
    Mouse(pointer, new Vector2(110, 10));
    Check(right.Events.Contains("release:Right"));
});

Run("A press starting outside widgets cannot become a release/click on entry", () =>
{
    var (canvas, pointer) = PointerScene();
    var widget = canvas.Add<MouseProbe>();
    Mouse(pointer, new Vector2(150), MouseButton.Left);
    Mouse(pointer, new Vector2(10), MouseButton.Left);
    Mouse(pointer, new Vector2(10));
    Check(widget.Events.SequenceEqual(new[] { "enter" }));
});

Run("Input loss cancels captures; held buttons on return are not new presses", () =>
{
    var (canvas, pointer) = PointerScene();
    var widget = canvas.Add<MouseProbe>();
    Mouse(pointer, new Vector2(10));
    Mouse(pointer, new Vector2(10), MouseButton.Left);
    pointer.Update(new Vector2(10), new Vector2(200), new[] { MouseButton.Left }, available: false);
    Check(widget.Events.Contains("cancel:Left") && !widget.IsMouseOver);
    Check(pointer.GetPressedWidget(MouseButton.Left) == null);
    Mouse(pointer, new Vector2(10), MouseButton.Left);
    Mouse(pointer, new Vector2(10));
    Check(widget.Events.Count(e => e == "press:Left") == 1 && !widget.Events.Contains("release:Left"));
    Mouse(pointer, new Vector2(10), MouseButton.Left);
    Mouse(pointer, new Vector2(10));
    Check(widget.Events.Count(e => e == "press:Left") == 2 && widget.Events.Contains("release:Left"));
});

Run("Hiding a pressed ancestor cancels once and does not synthesize release", () =>
{
    var (canvas, pointer) = PointerScene();
    var parent = canvas.Add<MouseProbe>();
    var child = parent.Add<MouseProbe>();
    Mouse(pointer, new Vector2(10));
    Mouse(pointer, new Vector2(10), MouseButton.Left);
    parent.Visible = false;
    Mouse(pointer, new Vector2(10), MouseButton.Left);
    Mouse(pointer, new Vector2(10));
    Check(child.Events.Count(e => e == "cancel:Left") == 1 && child.Events.Count(e => e == "exit") == 1);
    Check(!child.Events.Contains("release:Left") && pointer.HoveredWidget == null);
});

Run("Destroyed targets receive no further callbacks and no longer block widgets underneath", () =>
{
    var (canvas, pointer) = PointerScene();
    var bottom = canvas.Add<MouseProbe>();
    var top = canvas.Add<MouseProbe>();
    Mouse(pointer, new Vector2(10));
    Mouse(pointer, new Vector2(10), MouseButton.Left);
    top.Destroy();
    Mouse(pointer, new Vector2(10));
    Check(top.Events.SequenceEqual(new[] { "enter", "press:Left" }));
    Check(!top.IsMouseOver && pointer.HoveredWidget == bottom && pointer.GetPressedWidget(MouseButton.Left) == null);
});

Run("Pointer converts window coordinates into canvas coordinates", () =>
{
    var (canvas, pointer) = PointerScene();
    var widget = canvas.Add<MouseProbe>();
    widget.Rect.Position = new Vector2(40);
    widget.Rect.Size = new Vector2(20);
    pointer.Update(new Vector2(25), new Vector2(100), Array.Empty<MouseButton>());
    Check(pointer.Position == new Vector2(50) && pointer.HoveredWidget == widget);
    pointer.Update(new Vector2(30), new Vector2(100), Array.Empty<MouseButton>());
    Check(pointer.HoveredWidget == null); // Half-open rect bounds.
});

Run("Widgets may destroy themselves on enter or press without stale delivery", () =>
{
    var (canvas, pointer) = PointerScene();
    var widget = canvas.Add<MouseProbe>();
    widget.EnterAction = widget.Destroy;
    Mouse(pointer, new Vector2(10), MouseButton.Left);
    Check(widget.Events.SequenceEqual(new[] { "enter" }) && pointer.HoveredWidget == null);
    Mouse(pointer, new Vector2(10));
    var second = canvas.Add<MouseProbe>();
    second.PressAction = second.Destroy;
    Mouse(pointer, new Vector2(10), MouseButton.Left);
    Mouse(pointer, new Vector2(10));
    Check(second.Events.SequenceEqual(new[] { "enter", "press:Left" }));
    Check(pointer.GetPressedWidget(MouseButton.Left) == null);
});

Run("Reset inside an event clears state and aborts the rest of that input snapshot", () =>
{
    var (canvas, pointer) = PointerScene();
    var widget = canvas.Add<MouseProbe>();
    widget.PressAction = pointer.Reset;
    Mouse(pointer, new Vector2(10));
    Mouse(pointer, new Vector2(10), MouseButton.Left, MouseButton.Right);
    Check(widget.Events.Contains("cancel:Left") && !widget.Events.Contains("press:Right"));
    Check(pointer.HoveredWidget == null && !widget.IsMouseOver);
});

Run("Pointer disable and manager destruction clear hover and captures", () =>
{
    Configure("");
    var manager = new UIManager();
    manager.Initialize();
    var widget = manager.Canvas.Add<MouseProbe>();
    Mouse(manager.Pointer, new Vector2(-1));
    Mouse(manager.Pointer, new Vector2(1));
    Mouse(manager.Pointer, new Vector2(1), MouseButton.Left);
    manager.Pointer.IsEnabled = false;
    Mouse(manager.Pointer, new Vector2(1), MouseButton.Left);
    Check(widget.Events.Contains("cancel:Left") && !widget.IsMouseOver);
    manager.Pointer.IsEnabled = true;
    Mouse(manager.Pointer, new Vector2(1));
    Mouse(manager.Pointer, new Vector2(1), MouseButton.Right);
    manager.Destroy();
    manager.Destroy();
    Check(widget.IsDestroyed && widget.Events.Contains("cancel:Right"));
    Check(manager.Pointer.HoveredWidget == null && manager.Pointer.GetPressedWidget(MouseButton.Right) == null);
});

static (Canvas, ProjectShockGL.UI.Pointer) PointerScene()
{
    var canvas = new Canvas();
    canvas.Resize(new Vector2(200));
    var pointer = new ProjectShockGL.UI.Pointer(canvas);
    Mouse(pointer, new Vector2(-1));
    return (canvas, pointer);
}

static void Mouse(ProjectShockGL.UI.Pointer pointer, Vector2 position, params MouseButton[] buttons) =>
    pointer.Update(position, new Vector2(200), buttons);

static void Configure(string canvasClass)
{
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Settings.ini"),
        $"[Window]\nWidth=800\nHeight=600\n[Game]\nUICanvasClass={canvasClass}\n");
    Settings.Initialize();
}

static void Run(string name, Action test) { test(); Console.WriteLine($"PASS: {name}"); }
static void Check(bool condition) { if (!condition) throw new Exception("UI check failed"); }
static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}");
}

public sealed class ConfiguredCanvas : Canvas
{
    public int Creates;
    public Vector2 SizeAtCreation;
    protected override void OnCreate() { Creates++; SizeAtCreation = Size; }
}

public sealed class Probe : Widget
{
    public int Updates, Draws, Destroys;
    public Action? UpdateAction, DrawAction;
    protected override void OnUpdate(float dt) { Updates++; UpdateAction?.Invoke(); }
    protected override void OnDraw(Graphics graphics) { Draws++; DrawAction?.Invoke(); }
    protected override void OnDestroy() { Destroys++; }
}

public sealed class TestGame : ProjectShockGL.Core.Game
{
    public void LoadForTest() => Load();
    public void UpdateForTest() => Update(0);
    public void UnloadForTest() => Unload();
}

public sealed class MouseProbe : Widget
{
    public List<string> Events { get; } = [];
    public Action? EnterAction, PressAction;
    protected override void OnMouseEnter() { Events.Add("enter"); EnterAction?.Invoke(); }
    protected override void OnMouseExit() => Events.Add("exit");
    protected override void OnMousePressed(MouseButton button) { Events.Add($"press:{button}"); PressAction?.Invoke(); }
    protected override void OnMouseReleased(MouseButton button) => Events.Add($"release:{button}");
    protected override void OnMouseCancelled(MouseButton button) => Events.Add($"cancel:{button}");
}
