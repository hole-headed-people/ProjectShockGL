using System.Numerics;
using Button = ProjectShockGL.UI.Button;
using ProjectShockGL;
using ProjectShockGL.Rendering;
using ProjectShockGL.UI;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

Run("Widget SortOrder and sibling changes agree for drawing and pointer selection at root and nested levels", () =>
{
    var canvas = new Canvas();
    canvas.Resize(new Vector2(200));
    var draws = new List<string>();
    var first = canvas.Add<Probe>();
    first.DrawAction = () => draws.Add("first");
    first.SortOrder = 100;
    var second = canvas.Add<Probe>();
    second.DrawAction = () => draws.Add("second");
    var pointer = new Pointer(canvas);
    void Verify(string expectedOrder, Widget expectedTop)
    {
        draws.Clear();
        canvas.Draw(null!);
        Check(string.Join(",", draws) == expectedOrder);
        pointer.Update(new Vector2(10), canvas.Size, Array.Empty<MouseButton>());
        Check(pointer.HoveredWidget == expectedTop);
    }
    try
    {
        Verify("second,first", first);
        first.SetAsFirstSibling();
        Verify("second,first", first);
        first.SortOrder = 0;
        Verify("first,second", second);
        first.SetAsLastSibling();
        Verify("second,first", first);
        first.SetSiblingIndex(-5);
        Verify("first,second", second);
        first.SetSiblingIndex(500);
        Verify("second,first", first);
        first.SetSiblingIndex(0);
        var nestedA = second.Add<Probe>();
        nestedA.DrawAction = () => draws.Add("A");
        var nestedB = second.Add<Probe>();
        nestedB.DrawAction = () => draws.Add("B");
        nestedA.SortOrder = 2;
        Verify("first,second,B,A", nestedA);
        nestedA.SortOrder = 0;
        nestedB.SetAsFirstSibling();
        Verify("first,second,B,A", nestedA);
        nestedB.SetAsLastSibling();
        Verify("first,second,A,B", nestedB);
        nestedB.SetSiblingIndex(0);
        Verify("first,second,B,A", nestedA);
        nestedB.Destroy();
        Throws<ObjectDisposedException>(() => nestedB.SetAsLastSibling());
    }
    finally { pointer.Reset(); canvas.Destroy(); }
});

Run("UI JSON preserves SortOrder and authored sibling order with zero defaults for old screens", () =>
{
    var canvas = new Canvas();
    canvas.Resize(new Vector2(200));
    try
    {
        var root = canvas.Add<Widget>();
        root.SortOrder = -7;
        var a = root.Add<Widget>(); a.Name = "A"; a.SortOrder = 100;
        var b = root.Add<Widget>(); b.Name = "B"; b.SortOrder = -1;
        b.SetAsFirstSibling();
        var asset = UIAsset.Capture(root);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(asset);
        var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<UIAsset>(json)!.Instantiate(canvas);
        Check(restored.SortOrder == -7 && restored.Children[0].Name == "B");
        Check(restored.Children[0].SortOrder == -1 && restored.Children[1].SortOrder == 100);
        var old = Newtonsoft.Json.JsonConvert.DeserializeObject<UIAsset>("{\"Root\":{\"Type\":\"Widget\"}}")!;
        Check(old.Instantiate(canvas).SortOrder == 0);
    }
    finally { canvas.Destroy(); }
});
Run("Click Actions keep screen-local widget targets across JSON, rename and multiple instances", () =>
{
    UIActionFixtures.Reset();
    var label = new WidgetData { Type = "Text", Name = "Label" };
    var button = new WidgetData { Type = "Button", Name = "ActionButton" };
    var setText = UIActionCatalog.GetWidgetMethods(typeof(Text)).Single(method => method.Label.StartsWith("Text/Set text"));
    var record = UIActionCatalog.GetGlobalMethods().Single(method => method.Name == "Test/Record");
    button.ClickActions.Add(new UIActionCall { TargetId = label.Id, Method = setText.Id, Parameter = new Newtonsoft.Json.Linq.JValue("updated") });
    button.ClickActions.Add(new UIActionCall { Target = UIActionTarget.Global, Method = record.Id, Parameter = new Newtonsoft.Json.Linq.JValue("asset") });
    var asset = new UIAsset { Root = new WidgetData { Children = [button, label] } };
    var json = Newtonsoft.Json.JsonConvert.SerializeObject(asset);
    asset = Newtonsoft.Json.JsonConvert.DeserializeObject<UIAsset>(json)!;
    var canvas = new Canvas();
    canvas.Resize(new Vector2(800, 600));
    try
    {
        var first = asset.Instantiate(canvas);
        var second = asset.Instantiate(canvas);
        var firstButton = first.Find<Button>("ActionButton")!;
        var firstLabel = first.Find<Text>("Label")!;
        var secondLabel = second.Find<Text>("Label")!;
        firstLabel.Name = "Renamed";
        firstButton.Click.AddListener(() => UIActionFixtures.Values.Add("code"));
        firstButton.Click.Invoke();
        Check(firstLabel.Content == "updated" && secondLabel.Content == "");
        Check(UIActionFixtures.Values.SequenceEqual(new[] { "code", "asset" }));
        firstButton.Click.RemoveAllListeners();
        firstButton.Click.Invoke();
        Check(UIActionFixtures.Values.SequenceEqual(new[] { "code", "asset", "asset" }));
        asset.Root.Children[0].ClickActions[0].Parameter = new Newtonsoft.Json.Linq.JValue("changed template");
        firstButton.Click.Invoke();
        Check(firstLabel.Content == "updated");
        var captured = UIAsset.Capture(first);
        Check(captured.Root.Children[0].ClickActions.Count == 2 &&
            captured.Root.Children[0].ClickActions[0].TargetId == firstLabel.Id);
        var third = captured.Instantiate(canvas);
        third.Find<Button>("ActionButton")!.Click.Invoke();
        Check(third.Find<Text>("Renamed")!.Content == "updated");

        UIActionFixtures.Reset();
        var preview = asset.Instantiate(canvas, enableActions: false);
        preview.Find<Button>("ActionButton")!.Click.Invoke();
        Check(UIActionFixtures.Values.Count == 0 && preview.Find<Text>("Label")!.Content == "");
        var editor = typeof(Application).GetProperty(nameof(Application.IsEditor))!;
        var wasEditor = Application.IsEditor;
        try
        {
            editor.SetValue(null, true);
            second.Find<Button>("ActionButton")!.Click.Invoke();
            Check(UIActionFixtures.Values.Count == 0 && secondLabel.Content == "");
        }
        finally { editor.SetValue(null, wasEditor); }
    }
    finally { canvas.Destroy(); }
});

Run("UIAction discovery limits signatures and invokes typed constants without coercion", () =>
{
    UIActionFixtures.Reset();
    var globals = UIActionCatalog.GetGlobalMethods();
    Check(!globals.Any(method => method.Name.StartsWith("Unsupported/")));
    var button = new WidgetData { Type = "Button" };
    foreach (var (name, value) in new (string, object)[] { ("Test/Integer", 17), ("Test/Float", .25f), ("Test/Bool", true), ("Test/Record", "text") })
    {
        var method = globals.Single(method => method.Name == name);
        button.ClickActions.Add(new UIActionCall { Target = UIActionTarget.Global, Method = method.Id, Parameter = Newtonsoft.Json.Linq.JToken.FromObject(value) });
    }
    var asset = new UIAsset { Root = new WidgetData { Children = [button] } };
    var canvas = new Canvas();
    try
    {
        var screen = asset.Instantiate(canvas);
        screen.Children.OfType<Button>().Single().Click.Invoke();
        Check(UIActionFixtures.Number == 17 && UIActionFixtures.Amount == .25f && UIActionFixtures.Flag &&
            UIActionFixtures.Values.Single() == "text");

        UIActionFixtures.Reset();
        button.ClickActions[0].Parameter = new Newtonsoft.Json.Linq.JValue("17");
        asset.Instantiate(canvas).Children.OfType<Button>().Single().Click.Invoke();
        Check(UIActionFixtures.Number == 0 && UIActionFixtures.Values.Count == 0);
        button.ClickActions[0].Parameter = new Newtonsoft.Json.Linq.JValue(17);
        button.ClickActions[0].Method = "Unmarked method";
        asset.Instantiate(canvas).Children.OfType<Button>().Single().Click.Invoke();
        Check(UIActionFixtures.Values.Count == 0);
    }
    finally { canvas.Destroy(); }
});

Run("Closing a screen or a failed Action stops the remaining sequence without crashing a click", () =>
{
    var root = new WidgetData { Name = "Root" };
    var button = new WidgetData { Type = "Button", Name = "Close" };
    root.Children.Add(button);
    var destroy = UIActionCatalog.GetWidgetMethods(typeof(Widget)).Single(method => method.Name == "Widget/Destroy");
    var record = UIActionCatalog.GetGlobalMethods().Single(method => method.Name == "Test/Record");
    var fail = UIActionCatalog.GetGlobalMethods().Single(method => method.Name == "Test/Fail");
    var later = new UIActionCall { Target = UIActionTarget.Global, Method = record.Id, Parameter = new Newtonsoft.Json.Linq.JValue("must not run") };
    button.ClickActions.Add(new UIActionCall { TargetId = root.Id, Method = destroy.Id });
    button.ClickActions.Add(later);
    var canvas = new Canvas();
    try
    {
        UIActionFixtures.Reset();
        var screen = new UIAsset { Root = root }.Instantiate(canvas);
        screen.Find<Button>("Close")!.Click.Invoke();
        Check(screen.IsDestroyed && UIActionFixtures.Values.Count == 0);

        button.ClickActions[0] = new UIActionCall { Target = UIActionTarget.Global, Method = fail.Id };
        screen = new UIAsset { Root = root }.Instantiate(canvas);
        screen.Find<Button>("Close")!.Click.Invoke();
        Check(!screen.IsDestroyed && UIActionFixtures.Values.Count == 0);
        button.ClickActions[0] = new UIActionCall { TargetId = Guid.NewGuid(), Method = destroy.Id };
        new UIAsset { Root = root }.Instantiate(canvas).Find<Button>("Close")!.Click.Invoke();
        Check(UIActionFixtures.Values.Count == 0);
    }
    finally { canvas.Destroy(); }
});

Run("Duplicating UI branches regenerates IDs and remaps internal Action targets while preserving outside targets", () =>
{
    var outside = new WidgetData { Name = "Outside" };
    var label = new WidgetData { Type = "Text", Name = "BranchText" };
    var button = new WidgetData { Type = "Button", Name = "BranchButton" };
    var setText = UIActionCatalog.GetWidgetMethods(typeof(Text)).Single(method => method.Name == "Text/Set text");
    var enabled = UIActionCatalog.GetWidgetMethods(typeof(Widget)).Single(method => method.Name == "Widget/Set enabled");
    button.ClickActions.Add(new UIActionCall { TargetId = label.Id, Method = setText.Id, Parameter = new Newtonsoft.Json.Linq.JValue("copy") });
    button.ClickActions.Add(new UIActionCall { TargetId = outside.Id, Method = enabled.Id, Parameter = new Newtonsoft.Json.Linq.JValue(false) });
    var branch = new WidgetData { Children = [button, label] };
    var copy = Newtonsoft.Json.JsonConvert.DeserializeObject<WidgetData>(Newtonsoft.Json.JsonConvert.SerializeObject(branch))!;
    copy.RegenerateIds();
    Check(copy.Id != branch.Id && copy.Children[0].Id != button.Id &&
        copy.Children[0].ClickActions[0].TargetId == copy.Children[1].Id &&
        copy.Children[0].ClickActions[1].TargetId == outside.Id);
    var canvas = new Canvas();
    try
    {
        var screen = new UIAsset { Root = new WidgetData { Children = [outside, branch, copy] } }.Instantiate(canvas);
        screen.Children[2].Find<Button>("BranchButton")!.Click.Invoke();
        Check(screen.Children[1].Find<Text>("BranchText")!.Content == "" &&
            screen.Children[2].Find<Text>("BranchText")!.Content == "copy" && !screen.Children[0].IsEnabled);
        var duplicate = new WidgetData { Id = branch.Id };
        Throws<InvalidDataException>(() => new UIAsset { Root = new WidgetData { Children = [branch, duplicate] } }.Instantiate(canvas));
        Check(canvas.Children.Count == 1);
    }
    finally { canvas.Destroy(); }
});
Run("Disabling a clicked button preserves remaining listeners and Actions but blocks future clicks", () =>
{
    var record = UIActionCatalog.GetGlobalMethods().Single(method => method.Name == "Test/Record");
    var disable = UIActionCatalog.GetWidgetMethods(typeof(Widget)).Single(method => method.Name == "Widget/Set enabled");
    var buttonData = new WidgetData { Type = "Button", Name = "Next" };
    buttonData.ClickActions.Add(new UIActionCall { TargetId = buttonData.Id, Method = disable.Id,
        Parameter = new Newtonsoft.Json.Linq.JValue(false) });
    buttonData.ClickActions.Add(new UIActionCall { Target = UIActionTarget.Global, Method = record.Id,
        Parameter = new Newtonsoft.Json.Linq.JValue("asset") });
    var asset = new UIAsset { Root = new WidgetData { Children = [buttonData] } };
    var canvas = new Canvas();
    try
    {
        UIActionFixtures.Reset();
        var screen = asset.Instantiate(canvas);
        var button = screen.Find<Button>("Next")!;
        button.Click.AddListener(() => button.IsEnabled = false);
        button.Click.AddListener(() => UIActionFixtures.Values.Add("code"));
        button.Click.Invoke();
        Check(!button.IsEnabled && UIActionFixtures.Values.SequenceEqual(new[] { "code", "asset" }));
        button.Click.Invoke();
        Check(UIActionFixtures.Values.Count == 2);
        button.IsEnabled = true;
        button.Click.RemoveAllListeners();
        button.Click.Invoke();
        Check(!button.IsEnabled && UIActionFixtures.Values.SequenceEqual(new[] { "code", "asset", "asset" }));
        foreach (var close in new Action[] { () => screen.Visible = false, () => screen.IsEnabled = false, screen.Destroy })
        {
            screen.Visible = true;
            screen.IsEnabled = true;
            button.IsEnabled = true;
            button.Click.RemoveAllListeners();
            button.Click.AddListener(close);
            button.Click.AddListener(() => throw new Exception("Closed screen must stop dispatch"));
            button.Click.Invoke();
            Check(UIActionFixtures.Values.Count == 3);
        }
    }
    finally { canvas.Destroy(); }
});

Run("UIEvent dispatches a snapshot, removes listeners and propagates callback failures", () =>
{
    var signal = new UIEvent();
    var calls = new List<string>();
    Action second = () => calls.Add("second");
    Action added = () => calls.Add("added");
    Action first = () =>
    {
        calls.Add("first");
        signal.RemoveListener(second);
        signal.AddListener(added);
    };
    signal.AddListener(first);
    signal.AddListener(second);
    signal.Invoke();
    Check(calls.SequenceEqual(new[] { "first", "second" }));
    calls.Clear();
    signal.RemoveListener(first);
    signal.Invoke();
    Check(calls.SequenceEqual(new[] { "added" }));
    signal.RemoveAllListeners();
    signal.Invoke();
    Check(calls.Count == 1);
    Action failure = () => throw new InvalidOperationException("Expected listener failure");
    signal.AddListener(failure);
    signal.AddListener(second);
    Throws<InvalidOperationException>(signal.Invoke);
    Check(calls.Count == 1);
    signal.RemoveListener(failure);
    signal.Invoke();
    Check(calls.SequenceEqual(new[] { "added", "second" }));
    Throws<ArgumentNullException>(() => signal.AddListener(null!));
    Throws<ArgumentNullException>(() => signal.RemoveListener(null!));
});

Run("UIEvent<T> passes typed values and remains stable while listeners change", () =>
{
    var changed = new UIEvent<bool>();
    var values = new List<bool>();
    Action<bool> record = values.Add;
    changed.AddListener(record);
    changed.Invoke(true);
    changed.Invoke(false);
    changed.RemoveListener(record);
    changed.Invoke(true);
    Check(values.SequenceEqual(new[] { true, false }));

    var calls = new List<int>();
    var number = new UIEvent<int>();
    number.AddListener(value =>
    {
        calls.Add(value);
        number.RemoveAllListeners();
        number.AddListener(next => calls.Add(next * 10));
    });
    number.AddListener(value => calls.Add(-value));
    number.Invoke(3);
    number.Invoke(4);
    Check(calls.SequenceEqual(new[] { 3, -3, 40 }));
    number.RemoveAllListeners();
    number.Invoke(5);
    Check(calls.Count == 3);

    var enabled = true;
    var guarded = new UIEvent<string>(() => enabled);
    var text = "";
    guarded.AddListener(value => { text = value; enabled = false; });
    guarded.AddListener(_ => throw new Exception("Guard must stop the remaining listener"));
    guarded.Invoke("hello");
    Check(text == "hello");
    guarded.Invoke("ignored");
    Check(text == "hello");
    var scalar = new UIEvent<float>();
    var amount = 0f;
    scalar.AddListener(value => amount = value);
    scalar.Invoke(.75f);
    Check(amount == .75f);
    Throws<ArgumentNullException>(() => changed.AddListener(null!));
});

Run("Button.Click stays instance-local, excludes listeners from UI JSON and stops after disabling its screen", () =>
{
    var canvas = new Canvas();
    canvas.Resize(new Vector2(800, 600));
    var screen = canvas.Add<Widget>();
    var button = screen.Add<Button>();
    button.Name = "TestButton";
    var clicks = 0;
    Action listener = () => clicks++;
    button.Click.AddListener(listener);
    button.Click.RemoveListener(listener);
    button.Click.Invoke();
    Check(clicks == 0);
    button.Click.AddListener(listener);
    var json = Newtonsoft.Json.JsonConvert.SerializeObject(UIAsset.Capture(screen));
    Check(!json.Contains("Listeners") && !json.Contains("\"Click\""));
    var clone = Newtonsoft.Json.JsonConvert.DeserializeObject<UIAsset>(json)!.Instantiate(canvas);
    var cloneButton = clone.Find<Button>("TestButton")!;
    cloneButton.Click.Invoke();
    Check(clicks == 0 && !ReferenceEquals(button.Click, cloneButton.Click));
    button.Click.Invoke();
    Check(clicks == 1);
    button.Click.RemoveAllListeners();
    button.Click.AddListener(() => screen.IsEnabled = false);
    button.Click.AddListener(() => clicks++);
    button.Click.Invoke();
    Check(clicks == 1 && !screen.IsEnabled);
    screen.IsEnabled = true;
    button.Click.RemoveAllListeners();
    button.Click.AddListener(canvas.Destroy);
    button.Click.AddListener(() => clicks++);
    button.Click.Invoke();
    Check(canvas.IsDestroyed && button.IsDestroyed && clicks == 1);
    var uncreated = new Button();
    uncreated.Click.AddListener(() => clicks++);
    uncreated.Click.Invoke();
    Check(clicks == 1);
});
Run("Nine-slice preserves corners, crops tile UVs inside an atlas and handles small rects", () =>
{
    var image = new Image { DrawMode = ImageDrawMode.Sliced, Border = new Vector4(2), BorderScale = 2 };
    var method = typeof(Image).GetMethod("GetSlices", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    var uv = new Vector4(.25f, .125f, .5f, .25f);
    (Vector2 Position, Vector2 Size, Vector4 Uv)[] Parts(Vector2 size) =>
        ((IEnumerable<(Vector2 Position, Vector2 Size, Vector4 Uv)>)method.Invoke(image, [size, new Vector2(10), uv])!).ToArray();
    var parts = Parts(new Vector2(37, 33));
    Check(parts.Length == 9 && parts[0].Size == new Vector2(4));
    Check(Vector2.Distance(parts[4].Size, new Vector2(29, 25)) < .001f);
    image.EdgeMode = image.CenterMode = ImageSliceMode.Tile;
    parts = Parts(new Vector2(37, 33));
    Check(parts.Length == 25); // 4 corners + 6 horizontal + 6 vertical + 9 center.
    var last = parts.Single(p => Vector2.Distance(p.Position, new Vector2(28, 28)) < .001f);
    Check(Vector2.Distance(last.Size, new Vector2(5, 1)) < .001f);
    Check(Math.Abs(last.Uv.Z - .125f) < .001f && Math.Abs(last.Uv.W - .0125f) < .001f);
    Check(parts.All(p => p.Uv.X >= uv.X && p.Uv.Y >= uv.Y &&
        p.Uv.X + p.Uv.Z <= uv.X + uv.Z + .001f && p.Uv.Y + p.Uv.W <= uv.Y + uv.W + .001f));
    Check(Math.Abs(parts.Sum(p => p.Size.X * p.Size.Y) - 37 * 33) < .01f);
    image.FillCenter = false;
    Check(Parts(new Vector2(37, 33)).Length == 16);
    parts = Parts(new Vector2(3, 2));
    Check(parts.Length == 4 && Math.Abs(parts.Sum(p => p.Size.X * p.Size.Y) - 6) < .001f);
    image.Border = new Vector4(100, 100, 200, 200);
    parts = Parts(new Vector2(3, 2));
    Check(parts.All(p => float.IsFinite(p.Size.X) && p.Size.X >= 0));
    Check(Parts(Vector2.Zero).Length == 0);
    Throws<ArgumentOutOfRangeException>(() => image.Border = new Vector4(-1));
    Throws<ArgumentOutOfRangeException>(() => image.BorderScale = 0);
    Throws<ArgumentOutOfRangeException>(() => image.BorderScale = float.NaN);
});

Run("Nine-slice UI JSON roundtrip keeps settings and old assets keep Simple defaults", () =>
{
    var canvas = new Canvas();
    var root = canvas.Add<Widget>();
    var image = root.Add<Image>();
    image.Name = "Panel";
    image.DrawMode = ImageDrawMode.Sliced;
    image.Border = new Vector4(2, 3, 4, 5);
    image.BorderScale = 1.5f;
    image.FillCenter = false;
    image.EdgeMode = ImageSliceMode.Tile;
    image.CenterMode = ImageSliceMode.Stretch;
    var json = Newtonsoft.Json.JsonConvert.SerializeObject(UIAsset.Capture(root));
    var asset = Newtonsoft.Json.JsonConvert.DeserializeObject<UIAsset>(json)!;
    var copy = asset.Instantiate(canvas).Find<Image>("Panel")!;
    Check(copy.DrawMode == image.DrawMode && copy.Border == image.Border &&
        copy.BorderScale == image.BorderScale && !copy.FillCenter &&
        copy.EdgeMode == image.EdgeMode && copy.CenterMode == image.CenterMode);
    var old = new UIAsset { Root = new WidgetData { Type = "Image" } };
    var oldImage = (Image)old.Instantiate(canvas);
    Check(oldImage.DrawMode == ImageDrawMode.Simple && oldImage.FillCenter && oldImage.BorderScale == 1);
    canvas.Destroy();
});
Run("Button colors, left release, cancellation and callback mutation", () =>
{
    var canvas = new Canvas();
    canvas.Resize(new Vector2(800, 600));
    var button = canvas.Add<ProjectShockGL.UI.Button>();
    var image = button.Children.OfType<Image>().Single();
    var pointer = new Pointer(canvas);
    void Mouse(Vector2 position, params MouseButton[] buttons) =>
        pointer.Update(position, canvas.Size, buttons);
    void ColorIs(Color expected) => Check(image.Color.Equals(expected));

    var inside = new Vector2(20);
    var outside = new Vector2(200);
    var clicks = 0;
    button.Click.AddListener(() => clicks++);
    Mouse(inside);
    ColorIs(button.HoverColor);
    button.HoverColor = Color.Red;
    ColorIs(Color.Red);
    Mouse(inside, MouseButton.Right);
    ColorIs(Color.Red);
    Mouse(inside);
    Check(clicks == 0);
    Mouse(inside, MouseButton.Left);
    ColorIs(button.PressedColor);
    Check(clicks == 0);
    Mouse(outside, MouseButton.Left);
    ColorIs(button.NormalColor);
    Mouse(outside);
    Check(clicks == 0);
    Mouse(inside, MouseButton.Left);
    Mouse(inside);
    Check(clicks == 1);
    ColorIs(button.HoverColor);

    Mouse(inside, MouseButton.Left);
    button.IsEnabled = false;
    ColorIs(button.DisabledColor);
    Mouse(inside);
    ColorIs(button.DisabledColor);
    Check(clicks == 1);
    button.IsEnabled = true;
    Mouse(inside);
    button.HitTestVisible = false;
    Mouse(inside);
    ColorIs(button.DisabledColor);
    button.HitTestVisible = true;
    Mouse(inside);
    Mouse(inside, MouseButton.Left);
    pointer.Reset();
    Mouse(inside);
    Check(clicks == 1);
    Check(button.Text.HorizontalAlignment == HorizontalAlignment.Center &&
          button.Text.VerticalAlignment == VerticalAlignment.Center);

    button.Click.RemoveAllListeners();
    button.Click.AddListener(() => button.Click.RemoveAllListeners());
    button.Click.AddListener(() => clicks++);
    Mouse(inside, MouseButton.Left);
    Mouse(inside);
    Check(clicks == 2);
    button.Click.AddListener(button.Destroy);
    button.Click.AddListener(() => clicks++);
    Mouse(inside, MouseButton.Left);
    Mouse(inside);
    Check(button.IsDestroyed && clicks == 2);

    var uncreated = new ProjectShockGL.UI.Button();
    uncreated.NormalColor = Color.Blue;
    uncreated.IsEnabled = false;
    uncreated.Visible = false;
    uncreated.HitTestVisible = false;
    canvas.Destroy();
});
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
    // Game.Load applies the postprocess profile, so Content needs a live GL context first.
    var options = WindowOptions.Default;
    options.Size = new Vector2D<int>(800, 600);
    options.IsVisible = false;
    options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));
    using var window = Window.Create(options);
    window.Initialize();
    using var gl = window.CreateOpenGL();
    Content.Initialize(gl);

    var game = new TestGame();
    var loaded = false;
    try
    {
        Check(Content.Path == Path.Combine(AppContext.BaseDirectory, "TestContent"));
        game.LoadForTest();
        loaded = true;
        Check(PostProcessSettings.Enabled && PostProcessSettings.Shader is { Handle: not 0 });
        var canvas = (ConfiguredCanvas)game.UIManager.Canvas;
        var probe = canvas.Add<Probe>();
        game.UpdateForTest();
        Check(probe.Updates == 1);
        game.UnloadForTest();
        loaded = false;
        Check(canvas.IsDestroyed && probe.Destroys == 1);
    }
    finally
    {
        try
        {
            if (loaded) game.UnloadForTest();
        }
        finally
        {
            PostProcessSettings.Reset();
            Content.UnloadAll();
        }
    }
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
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "GameSettings.ini"),
        $"[Window]\nWidth=800\nHeight=600\n[Game]\nUICanvasClass={canvasClass}\n[Content]\nPath=TestContent\n");
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


public static class UIActionFixtures
{
    public static readonly List<string> Values = [];
    public static int Number;
    public static float Amount;
    public static bool Flag;
    public static void Reset() { Values.Clear(); Number = 0; Amount = 0; Flag = false; }

    [UIAction("Test/Record")] public static void Record(string value) => Values.Add(value);
    [UIAction("Test/Integer")] public static void Integer(int value) => Number = value;
    [UIAction("Test/Float")] public static void Float(float value) => Amount = value;
    [UIAction("Test/Bool")] public static void Bool(bool value) => Flag = value;
    [UIAction("Test/Fail")] public static void Fail() => throw new InvalidOperationException("Expected Action failure");
    [UIAction("Unsupported/Return")] public static int ReturnsValue() => 1;
    [UIAction("Unsupported/Two")] public static void Two(string a, string b) { }
    [UIAction("Unsupported/Generic")] public static void Generic<T>() { }
    [UIAction("Unsupported/Ref")] public static void ByReference(ref int value) { }
    public static void Unmarked() => Values.Add("unmarked");
}