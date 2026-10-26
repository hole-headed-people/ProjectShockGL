# UI foundation

`Game` owns a sealed `UIManager`, which creates one `Canvas` using
`[Game] UICanvasClass` in `Settings.ini`. An empty setting uses the base canvas;
an invalid type logs a message and falls back to it. A valid canvas type must
have a public parameterless constructor. Exceptions inside its constructor or
`OnCreate` are allowed to surface so game initialization errors remain visible.

Create game UI by overriding `OnCreate` in the configured canvas. Create widget
behavior through `OnCreate`, `OnUpdate`, `OnDraw`, and `OnDestroy`. Tree traversal
is handled by the framework; these hooks do not need base calls.

```csharp
public class GameCanvas : ProjectShockGL.UI.Canvas
{
    protected override void OnCreate()
    {
        var group = Add<Widget>();
        group.Rect.Size = new Vector2(300, 100);
        group.Rect.Anchor = new Vector2(0, 1);
        group.Rect.Pivot = new Vector2(0, 1);
        group.Rect.Position = new Vector2(20, -20);

        var element = group.Add<ColoredWidget>();
        element.Rect.Position = new Vector2(10, 10);
        element.Rect.Size = new Vector2(100, 30);
    }
}

public class ColoredWidget : Widget
{
    protected override void OnDraw(Graphics graphics)
    {
        graphics.DrawUIRect(Rect.AbsolutePosition, Rect.Size, Color.Red);
    }
}
```

The example uses `System.Numerics`, `ProjectShockGL.UI`, and
`ProjectShockGL.Rendering`. Configure the assembly-qualified name of `GameCanvas`
in your game's settings. The existing `ProjectDungeon.UI.Canvas` is already
configured and can be extended directly.

## Hierarchy and coordinates

- `Canvas.Children` contains root widgets. `Widget.Parent == null` means its
  layout parent is the canvas. Every attached widget exposes the same `Canvas`.
- `Add<T>()` attaches and initializes a new widget. `SetParent(widget)` moves it
  within its canvas; `SetParent(null)` moves it back to the root. Local transform
  values are preserved. Cycles and transfers between canvases are rejected.
- `Children` is read-only. `Destroy()` detaches and recursively destroys a branch
  once. `Canvas.Clear()` destroys its root branches without replacing the canvas.
- Coordinates start at the top left; X goes right and Y goes down. Currently one
  canvas unit equals one window coordinate unit. The renderer uses the framebuffer
  viewport, so logical coordinates remain consistent on high-DPI displays.
- `Rect.Position` offsets the pivot from the parent's normalized `Anchor`.
  `Rect.AbsolutePosition` is the resulting top-left corner. `Size` is local and
  nonnegative. Parent movement and resizing are reflected immediately.
- Anchors and pivots default to `(0, 0)`; size defaults to `(100, 100)`. This first
  version has no stretch anchors, rotation, per-widget scaling, or clipping.

## Frame behavior

UI updates after the world and physics. UI draws after all application world
render handlers and before the editor's ImGui overlay. The UI pass uses an
orthographic projection without depth testing, lighting, or face culling, and
restores scene matrices and depth/culling states afterward.

Parents draw before children; later siblings draw on top. Reparenting during a
traversal never processes the same widget more than once in that traversal.
`Visible = false` hides a whole branch, but it may still update.
`IsEnabled = false` stops updates for the branch, but it remains drawable.

Input routing, focus, modal screens, text, and predefined controls are not part
of this foundation. UI state survives `World.Clear()` and level changes; it is
destroyed when the game unloads.

## Checks

From the solution directory:

```powershell
dotnet run --project tests/UIChecks/UIChecks.csproj
```

These checks exercise settings, ownership, transforms, tree traversal, destruction,
and game lifecycle integration without creating an OpenGL window. They do not
visually validate GPU rendering.
