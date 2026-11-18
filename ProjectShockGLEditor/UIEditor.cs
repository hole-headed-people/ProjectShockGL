using System.Numerics;
using ImGuiNET;
using NativeFileDialogNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools;
using ProjectShockGL.UI;
using Silk.NET.OpenGL;
using Canvas = ProjectShockGL.UI.Canvas;

namespace ProjectShockGLEditor;

public static class UIEditor
{
    private static readonly string[] Types = ["Widget", "Image", "Text", "Button", "TextScrollView"];
    
    private static GL? _gl;
    
    private static uint _framebuffer;
    private static uint _texture;
    
    private static Vector2 _textureSize;
    private static Vector2 _previewOrigin;
    private static Vector2 _previewDisplay;
    
    private static Canvas? _preview;
    
    private static UIAsset _asset = new() { Root = new WidgetData { Name = "Panel", Size = new Vector2(800, 600) } };
    private static WidgetData _selected = _asset.Root;
    
    private static string _name = "Screen";
    private static string? _error;
    
    private static bool _rebuild = true;
    private static bool _dirty;
    
    private static Action? _pending;
    
    private static Vector2 _size = new(800, 600);
    
    private static readonly Dictionary<WidgetData, Widget> PreviewWidgets = [];
    
    private static WidgetData? _dragged;
    
    private static Vector2 _dragStartMouse;
    private static Vector2 _dragStartPosition;
    private static Vector2 _dragStartSize;
    private static Vector2 _resizeDirection;
    private static WidgetData? _hierarchySource;
    private static (WidgetData Source, WidgetData Target, int Placement)? _hierarchyDrop;
    private static readonly Vector2[] ResizeHandles =
    [
        new(-1, -1), new(0, -1), new(1, -1), new(1, 0),
        new(1, 1), new(0, 1), new(-1, 1), new(-1, 0)
    ];
    
    private static bool _dirtyBeforeDrag;
    private static bool _cancelledDrag;

    private sealed record Snapshot(string Json, string Selection);
    private sealed record Edit(Snapshot Before, Snapshot After);
    
    private static readonly Stack<Edit> UndoStack = [];
    private static readonly Stack<Edit> RedoStack = [];
    
    private static Edit? _pendingEdit;
    
    private static uint _editItem;
    private static uint _pendingItem;
    
    private static string _savedJson = SerializeAsset();

    private static string SerializeAsset() => JsonConvert.SerializeObject(_asset, JsonHelper.GetSettings());

    private static IEnumerable<(WidgetData Node, string Segment)> Nodes(WidgetData node)
    {
        if (node.ButtonImage != null)
        {
            yield return (node.ButtonImage, "Image");
        }

        if (node.ButtonText != null)
        {
            yield return (node.ButtonText, "Text");
        }

        if (node.Body != null)
        {
            yield return (node.Body, "Body");
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            yield return (node.Children[i], i.ToString());
        }
    }

    private static string SelectionPath(WidgetData node, WidgetData target, string path = "")
    {
        if (node == target)
        {
            return path;
        }
        
        foreach (var (child, segment) in Nodes(node))
        {
            var found = SelectionPath(child, target, path + "/" + segment);
            
            if (found != "#missing")
            {
                return found;
            }
        }
        
        return "#missing";
    }

    private static Snapshot Capture()
    {
        return new Snapshot(SerializeAsset(), SelectionPath(_asset.Root, _selected));
    }

    private static void Restore(Snapshot snapshot)
    {
        var asset = JsonConvert.DeserializeObject<UIAsset>(snapshot.Json, JsonHelper.GetSettings())!;
        var selected = asset.Root;

        foreach (var segment in snapshot.Selection.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            selected = Nodes(selected).FirstOrDefault(pair => pair.Segment == segment).Node ?? asset.Root;
        }
        
        _asset = asset;
        NormalizeRoot();
        _selected = selected;
        
        _dragged = null;
        
        PreviewWidgets.Clear();
        
        _rebuild = true;
        
        _dirty = snapshot.Json != _savedJson;
    }

    private static void CommitEdit()
    {
        if (_pendingEdit == null)
        {
            return;
        }
        
        if (_pendingEdit.Before.Json != _pendingEdit.After.Json)
        {
            UndoStack.Push(_pendingEdit);
            RedoStack.Clear();
        }
        
        _pendingEdit = null;
    }

    private static void RecordFrame(Snapshot before)
    {
        var after = Capture();

        if (_cancelledDrag)
        {
            _dirty = after.Json != _savedJson; 
            return;
        }
        
        if (before.Json != after.Json)
        {
            if (_pendingEdit != null && _pendingItem != _editItem)
            {
                CommitEdit();
            }
            
            _pendingEdit = new Edit(_pendingEdit?.Before ?? before, after);
            
            _pendingItem = _editItem;
        }

        if (_dragged == null && !ImGui.IsAnyItemActive())
        {
            CommitEdit();
        }
        
        _dirty = after.Json != _savedJson;
    }

    public static void Undo()
    {
        EndDrag(false);
        CommitEdit();

        if (!UndoStack.TryPop(out var edit))
        {
            return;
        }
        
        Restore(edit.Before);
        RedoStack.Push(edit);
    }

    public static void Redo()
    {
        EndDrag(false);
        CommitEdit();

        if (!RedoStack.TryPop(out var edit))
        {
            return;
        }
        
        Restore(edit.After);
        UndoStack.Push(edit);
    }

    private static WidgetData? FindParent(WidgetData node, WidgetData target)
    {
        foreach (var (child, _) in Nodes(node))
        {
            if (node.Children.Contains(child) && child == target) return node;
            var parent = FindParent(child, target);
            if (parent != null) return parent;
        }
        return null;
    }

    public static void DuplicateSelected()
    {
        if (_dragged != null) return;
        var parent = FindParent(_asset.Root, _selected);
        if (parent == null) return;
        CommitEdit();
        var before = Capture();
        var json = JsonConvert.SerializeObject(_selected, JsonHelper.GetSettings());
        var copy = JsonConvert.DeserializeObject<WidgetData>(json, JsonHelper.GetSettings())!;
        var existingNames = new HashSet<string>(StringComparer.Ordinal);
        void CollectNames(WidgetData node)
        {
            existingNames.Add(node.Name);
            foreach (var (child, _) in Nodes(node)) CollectNames(child);
        }
        CollectNames(_asset.Root);
        var suffix = 1;
        var baseName = string.IsNullOrWhiteSpace(copy.Name) ? copy.Type : copy.Name;
        while (existingNames.Contains(baseName + "_" + suffix)) suffix++;
        copy.Name = baseName + "_" + suffix;
        parent.Children.Insert(parent.Children.IndexOf(_selected) + 1, copy);
        _selected = copy;
        _rebuild = true;
        _dirty = true;
        UndoStack.Push(new Edit(before, Capture()));
        RedoStack.Clear();
    }

    private static void ResetHistory()
    {
        UndoStack.Clear();
        RedoStack.Clear();
        
        _pendingEdit = null;
        _savedJson = SerializeAsset();
        
        _dirty = false;
    }

    [Flags]
    private enum Shortcut { None = 0, Undo = 1, Redo = 2, Duplicate = 4, Save = 8 }
    private static Shortcut _shortcuts;

    internal static void PollShortcuts()
    {
        if (!Open || !HasFocus || _pending != null) return;
        if (!Input.IsKey(Silk.NET.Input.Key.ControlLeft) && !Input.IsKey(Silk.NET.Input.Key.ControlRight)) return;
        if (Input.IsKeyDown(Silk.NET.Input.Key.Z)) _shortcuts |= Shortcut.Undo;
        if (Input.IsKeyDown(Silk.NET.Input.Key.Y)) _shortcuts |= Shortcut.Redo;
        if (Input.IsKeyDown(Silk.NET.Input.Key.D)) _shortcuts |= Shortcut.Duplicate;
        if (Input.IsKeyDown(Silk.NET.Input.Key.S)) _shortcuts |= Shortcut.Save;
    }

    public static bool Open;
    public static bool IsDirty => _dirty;
    public static bool HasFocus { get; private set; }

    public static void Initialize(GL gl) => _gl = gl;

    public static bool AllowClose()
    {
        if (!_dirty)
        {
            return true;
        }
        
        Open = true;
        
        Request(() =>
        {
            EndDrag(false);
            CommitEdit();
            _savedJson = SerializeAsset();
            _dirty = false;
            Application.Quit();
        });
        
        return false;
    }

    private static void Changed()
    {
        _editItem = _dragged != null ? uint.MaxValue : ImGui.GetItemID();
        _dirty = true;
        _rebuild = true;
    }

    private static void Request(Action action)
    {
        if (_dirty)
        {
            _pending = action;
        }
        else
        {
            action();
        }
    }

    public static void Draw()
    {
        if (!Open)
        {
            EndDrag(true); 
            CommitEdit(); 
            
            HasFocus = false; 
            
            return;
        }
        
        var screen = ImGui.GetIO().DisplaySize;
        
        ImGui.SetNextWindowSize(screen * 0.94f, ImGuiCond.Appearing);
        ImGui.SetNextWindowPos(screen * 0.5f, ImGuiCond.Appearing, new Vector2(0.5f));
        
        if (!ImGui.Begin($"UI Editor — {_name}{(_dirty ? " *" : "")}###UI Editor", ref Open))
        {
            ImGui.End();
            return;
        }

        NormalizeRoot();
        HasFocus = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        
        if (ImGui.Button("New"))
        {
            Request(() => { _name = "Screen"; _asset = new UIAsset(); _asset.Root.Size = _size; _selected = _asset.Root; ResetHistory(); _rebuild = true; });
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Open"))
        {
            Request(Load);
        }
        
        ImGui.SameLine();

        if (ImGui.Button("Save"))
        {
            Save();
        }
        
        ImGui.SameLine();

        ImGui.BeginDisabled(UndoStack.Count == 0 && _pendingEdit == null);

        if (ImGui.Button("Undo"))
        {
            Undo();
        }
        
        ImGui.EndDisabled();
        ImGui.SameLine();
        
        ImGui.BeginDisabled(RedoStack.Count == 0);

        if (ImGui.Button("Redo"))
        {
            Redo();
        }
        
        ImGui.EndDisabled();
        
        var shortcuts = _shortcuts;
        _shortcuts = Shortcut.None;
        var duplicateRequested = shortcuts.HasFlag(Shortcut.Duplicate);
        if (shortcuts.HasFlag(Shortcut.Undo)) Undo();
        if (shortcuts.HasFlag(Shortcut.Redo)) Redo();
        if (shortcuts.HasFlag(Shortcut.Save)) Save();
        if (HasFocus && ImGui.GetIO().KeyCtrl && _pending == null)
        {
            if (!shortcuts.HasFlag(Shortcut.Undo) && ImGui.IsKeyPressed(ImGuiKey.Z))
            {
                Undo();
                ImGui.SetKeyboardFocusHere(-1);
            }

            if (ImGui.IsKeyPressed(ImGuiKey.D, false)) duplicateRequested = true;
            if (!shortcuts.HasFlag(Shortcut.Redo) && ImGui.IsKeyPressed(ImGuiKey.Y))
            {
                Redo();
                ImGui.SetKeyboardFocusHere(-1);
            }
        }

        if (!shortcuts.HasFlag(Shortcut.Save) && HasFocus && ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.S))
        {
            Save();
        }
        
        ImGui.SetNextItemWidth(230);
        
        var size = _size;
        
        if (ImGui.InputFloat2("Preview size", ref size) && float.IsFinite(size.X) && float.IsFinite(size.Y)
            && size.X >= 1 && size.Y >= 1 && size.X <= 4096 && size.Y <= 4096)
        {
            _size = size;
            _rebuild = true;
        }

        if (_pending != null)
        {
            ImGui.OpenPopup("Discard UI changes?");
        }
        
        if (ImGui.BeginPopupModal("Discard UI changes?", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Discard unsaved UI changes?");
            
            if (ImGui.Button("Discard"))
            {
                var pending = _pending;
                
                _pending = null;
                pending?.Invoke();
                
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Cancel"))
            {
                _pending = null; 
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.EndPopup();
        }

        _cancelledDrag = false;
        
        var beforeEdit = Capture();
        
        if (_error != null)
        {
            ImGui.TextWrapped(_error);
        }
        
        var width = ImGui.GetContentRegionAvail().X;
        var treeWidth = Math.Min(240f, width * 0.2f);
        var inspectorWidth = Math.Min(340f, width * 0.28f);
        var previewWidth = Math.Max(1f, width - treeWidth - inspectorWidth - ImGui.GetStyle().ItemSpacing.X * 2);
        
        ImGui.BeginChild("UI left panels", new Vector2(treeWidth, -1));
        var leftHeight = ImGui.GetContentRegionAvail().Y;
        ImGui.BeginChild("Hierarchy", new Vector2(0, Math.Max(1, leftHeight * 0.65f)), ImGuiChildFlags.Border);
        ImGui.TextUnformatted("Hierarchy");
        Tree(_asset.Root, "Root");
        ImGui.BeginDisabled(FindParent(_asset.Root, _selected) == null || _dragged != null);
        if (ImGui.Button("Duplicate")) duplicateRequested = true;
        ImGui.EndDisabled();
        if (_selected != _asset.Root && ImGui.Button("Delete widget"))
        {
            if (Remove(_asset.Root, _selected)) { _selected = _asset.Root; Changed(); }
        }
        ImGui.EndChild();
        ImGui.BeginChild("Widgets", Vector2.Zero, ImGuiChildFlags.Border);
        ImGui.TextUnformatted("Widgets");
        foreach (var type in Types)
            if (ImGui.Button(type)) { _selected.Children.Add(Create(type)); Changed(); }
        ImGui.EndChild();
        ImGui.EndChild();
        ImGui.SameLine();

        ImGui.BeginChild("UI preview", new Vector2(previewWidth, -1), ImGuiChildFlags.Border);
        
        if (_texture != 0)
        {
            var available = ImGui.GetContentRegionAvail();
            var scale = Math.Min(available.X / _size.X, available.Y / _size.Y);
            var display = _size * Math.Max(0.01f, scale);
            var origin = ImGui.GetCursorScreenPos();
            
            _previewOrigin = origin;
            _previewDisplay = display;
            
            ImGui.InvisibleButton("##previewCanvas", display);
            ImGui.GetWindowDrawList().AddImage((nint)_texture, origin, origin + display,
                new Vector2(0, 1), new Vector2(1, 0));
            
            var mouse = (ImGui.GetIO().MousePos - origin) / display * _size;
            
            var hoveredHandle = ImGui.IsItemHovered() ? FindResizeHandle(ImGui.GetIO().MousePos) : Vector2.Zero;
            var cursorDirection = _dragged != null ? _resizeDirection : hoveredHandle;
            if (cursorDirection != Vector2.Zero)
            {
                var cursor = cursorDirection.X == 0 ? ImGuiMouseCursor.ResizeNS
                    : cursorDirection.Y == 0 ? ImGuiMouseCursor.ResizeEW
                    : cursorDirection.X == cursorDirection.Y ? ImGuiMouseCursor.ResizeNWSE : ImGuiMouseCursor.ResizeNESW;
                ImGui.SetMouseCursor(cursor);
            }
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                var hit = hoveredHandle != Vector2.Zero ? _selected : Pick(_asset.Root, mouse);
                if (hit != null)
                {
                    _selected = hit;
                    _dragged = hit == _asset.Root || hit.FillCanvas ? null : hit;
                    _dragStartMouse = mouse;
                    _dragStartPosition = hit.Position;
                    _dragStartSize = hit.Size;
                    _resizeDirection = hoveredHandle;
                    _dirtyBeforeDrag = _dirty;
                }
            }

            if (_dragged != null)
            {
                if (ImGui.IsKeyPressed(ImGuiKey.Escape) ||
                    !ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
                {
                    EndDrag(true);
                }
                else if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    MoveDrag(mouse);
                }
                else
                {
                    EndDrag(false);
                }
            }

            if (PreviewWidgets.TryGetValue(_selected, out var widget) && widget.Visible)
            {
                var min = origin + widget.Rect.AbsolutePosition / _size * display;
                var max = min + widget.Rect.Size / _size * display;
                
                var draw = ImGui.GetWindowDrawList();
                
                draw.PushClipRect(origin, origin + display, true);
                draw.AddRect(min, max, 0xFF40CCFF, 0, ImDrawFlags.None, 2);
                if (CanResize(_selected))
                {
                    foreach (var direction in ResizeHandles)
                    {
                        var handle = min + (max - min) * (direction + Vector2.One) * 0.5f;
                        draw.AddRectFilled(handle - new Vector2(4), handle + new Vector2(4), 0xFF40CCFF);
                    }
                }
                draw.PopClipRect();
            }
        }
        
        ImGui.EndChild();
        ImGui.SameLine();
        
        ImGui.BeginChild("UI properties", new Vector2(inspectorWidth, -1), ImGuiChildFlags.Border);
        
        DrawProperties(_selected);
        
        ImGui.EndChild();
        
        RecordFrame(beforeEdit);
        if (duplicateRequested) DuplicateSelected();
        if (_hierarchyDrop is { } drop)
        {
            _hierarchyDrop = null;
            try { MoveWidget(drop.Source, drop.Target, drop.Placement); }
            catch (Exception error) { _error = error.Message; }
        }
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) _hierarchySource = null;
        
        ImGui.End();
    }

    private static void Bind(WidgetData data, Widget widget, Dictionary<WidgetData, Widget>? map = null)
    {
        map ??= PreviewWidgets;
        map[data] = widget;
        
        var offset = 0;
        
        if (widget is ProjectShockGL.UI.Button button)
        {
            if (data.ButtonImage != null)
            {
                Bind(data.ButtonImage, widget.Children[0], map);
            }

            if (data.ButtonText != null)
            {
                Bind(data.ButtonText, button.Text, map);
            }
            
            offset = 2;
        }
        else if (widget is TextScrollView scroll)
        {
            if (data.Body != null)
            {
                Bind(data.Body, scroll.Body, map);
            }
            
            offset = 1;
        }

        for (var i = 0; i < data.Children.Count; i++)
        {
            Bind(data.Children[i], widget.Children[offset + i], map);
        }
    }

    private static WidgetData? Pick(WidgetData data, Vector2 position)
    {
        if (!PreviewWidgets.TryGetValue(data, out var widget) || !widget.Visible || widget.IsDestroyed)
            return null;
        
        var contains = widget.Rect.Contains(position);

        if (data.Type == "TextScrollView" && !contains)
        {
            return null;
        }
        
        for (var i = data.Children.Count - 1; i >= 0; i--)
        {
            var hit = Pick(data.Children[i], position);
            
            if (hit != null)
            {
                return hit;
            }
        }
        
        return contains ? data : null;
    }

    private static void MoveDrag(Vector2 mouse)
    {
        if (_dragged == null)
        {
            return;
        }
        
        var delta = mouse - _dragStartMouse;
        if (!float.IsFinite(delta.X) || !float.IsFinite(delta.Y)) return;
        var size = _dragStartSize;
        var shift = Vector2.Zero;
        if (_resizeDirection.X != 0)
        {
            size.X = Math.Max(1f, _dragStartSize.X + delta.X * _resizeDirection.X);
            if (_resizeDirection.X < 0) shift.X = _dragStartSize.X - size.X;
        }
        if (_resizeDirection.Y != 0)
        {
            size.Y = Math.Max(1f, _dragStartSize.Y + delta.Y * _resizeDirection.Y);
            if (_resizeDirection.Y < 0) shift.Y = _dragStartSize.Y - size.Y;
        }
        var position = _resizeDirection == Vector2.Zero
            ? _dragStartPosition + delta
            : _dragStartPosition + shift + (size - _dragStartSize) * _dragged.Pivot;
        if (position == _dragged.Position && size == _dragged.Size) return;
        _dragged.Position = position;
        _dragged.Size = size;
        Changed();
    }

    private static bool CanResize(WidgetData data)
    {
        if (data == _asset.Root || data.FillCanvas || !PreviewWidgets.TryGetValue(data, out var widget)) return false;
        return widget.Parent is not ProjectShockGL.UI.Button
            && !(widget.Parent is TextScrollView scroll && scroll.Body == widget);
    }

    private static Vector2 FindResizeHandle(Vector2 mouse)
    {
        if (!CanResize(_selected) || !PreviewWidgets.TryGetValue(_selected, out var widget) || !widget.Visible)
            return Vector2.Zero;
        var min = _previewOrigin + widget.Rect.AbsolutePosition / _size * _previewDisplay;
        var size = widget.Rect.Size / _size * _previewDisplay;
        var closest = 49f;
        var result = Vector2.Zero;
        foreach (var direction in ResizeHandles)
        {
            var center = min + size * (direction + Vector2.One) * 0.5f;
            var distance = Vector2.DistanceSquared(mouse, center);
            if (distance < closest)
            {
                closest = distance;
                result = direction;
            }
        }
        return result;
    }
    private static void EndDrag(bool cancel)
    {
        if (cancel && _dragged != null)
        {
            _cancelledDrag = true;
            
            _dragged.Position = _dragStartPosition;
            _dragged.Size = _dragStartSize;
            _dirty = _dirtyBeforeDrag;
            
            _rebuild = true;
            
            if (_pendingEdit != null && _pendingItem == uint.MaxValue)
            {
                _pendingEdit = _pendingEdit with { After = Capture() };
                CommitEdit();
            }
        }
        
        _dragged = null;
    }
    private static WidgetData Create(string type)
    {
        var data = new WidgetData { Type = type, Name = type };
        
        if (type == "Text")
        {
            data.HitTestVisible = false;
            data.Font = "NotoSans-Regular";
            data.Properties["Content"] = "Text";
        }
        
        if (type == "Button")
        {
            data.ButtonImage = Create("Image");
            data.ButtonImage.HitTestVisible = false;
            data.ButtonText = Create("Text");
            data.ButtonText.Properties["Content"] = "Button";
            data.ButtonText.Properties["HorizontalAlignment"] = (int)HorizontalAlignment.Center;
            data.ButtonText.Properties["VerticalAlignment"] = (int)VerticalAlignment.Center;
        }

        if (type == "TextScrollView")
        {
            data.Body = Create("Text");
        }
        
        return data;
    }

    private static bool CanDrop(WidgetData source, WidgetData target, int placement)
    {
        if (source == target || FindParent(_asset.Root, source) == null) return false;
        if (SelectionPath(source, target) != "#missing") return false;
        // Built-in roles remain attached to their owner.
        if (target != _asset.Root && FindParent(_asset.Root, target) == null) return false;
        return placement == 0 || FindParent(_asset.Root, target) != null;
    }

    internal static bool MoveWidget(WidgetData source, WidgetData target, int placement)
    {
        if (_dragged != null || !CanDrop(source, target, placement)) return false;
        var oldParent = FindParent(_asset.Root, source)!;
        var newParent = placement == 0 ? target : FindParent(_asset.Root, target)!;
        var oldIndex = oldParent.Children.IndexOf(source);
        var index = placement == 0 ? newParent.Children.Count
            : newParent.Children.IndexOf(target) + (placement > 0 ? 1 : 0);
        if (oldParent == newParent && oldIndex < index) index--;
        if (oldParent == newParent && oldIndex == index) return false;

        var position = source.Position;
        if (oldParent != newParent && !source.FillCanvas)
        {
            var canvas = new Canvas();
            canvas.Resize(_size);
            try
            {
                var root = _asset.Instantiate(canvas);
                canvas.Update(0);
                var map = new Dictionary<WidgetData, Widget>();
                Bind(_asset.Root, root, map);
                position = map[source].Rect.AbsolutePosition - map[newParent].Rect.AbsolutePosition
                    - map[newParent].Rect.Size * source.Anchor + source.Size * source.Pivot;
            }
            finally { canvas.Destroy(); }
        }
        CommitEdit();
        var before = Capture();
        oldParent.Children.RemoveAt(oldIndex);
        newParent.Children.Insert(index, source);
        source.Position = position;
        _selected = source;
        _rebuild = true;
        _dirty = true;
        UndoStack.Push(new Edit(before, Capture()));
        RedoStack.Clear();
        return true;
    }

    private static void HierarchyDragDrop(WidgetData data)
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        if (FindParent(_asset.Root, data) != null && ImGui.BeginDragDropSource())
        {
            _hierarchySource = data;
            ImGui.SetDragDropPayload("UI_WIDGET", IntPtr.Zero, 0);
            ImGui.TextUnformatted(data.Name);
            ImGui.EndDragDropSource();
        }
        if (_hierarchySource == null) return;
        var fraction = (ImGui.GetIO().MousePos.Y - min.Y) / Math.Max(1, max.Y - min.Y);
        var placement = fraction < 0.25f ? -1 : fraction > 0.75f ? 1 : 0;
        if (!CanDrop(_hierarchySource, data, placement) || !ImGui.BeginDragDropTarget()) return;
        ImGui.AcceptDragDropPayload("UI_WIDGET");
        ImGui.SetTooltip(placement == 0 ? "Make child" : placement < 0 ? "Place before" : "Place after");
        if (placement != 0)
        {
            var y = placement < 0 ? min.Y : max.Y;
            ImGui.GetWindowDrawList().AddLine(new Vector2(min.X, y), new Vector2(max.X, y), 0xFF40CCFF, 2);
        }
        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            _hierarchyDrop = (_hierarchySource, data, placement);
        ImGui.EndDragDropTarget();
    }

    private static void Tree(WidgetData data, string path)
    {
        ImGui.PushID(path);
        
        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.DefaultOpen;

        if (_selected == data)
        {
            flags |= ImGuiTreeNodeFlags.Selected;
        }
        
        var expanded = ImGui.TreeNodeEx($"{data.Name} ({data.Type})", flags);

        if (ImGui.IsItemClicked())
        {
            _selected = data;
        }
        
        HierarchyDragDrop(data);
        if (expanded)
        {
            if (data.ButtonImage != null)
            {
                Tree(data.ButtonImage, path + "/Image");
            }

            if (data.ButtonText != null)
            {
                Tree(data.ButtonText, path + "/Text");
            }

            if (data.Body != null)
            {
                Tree(data.Body, path + "/Body");
            }

            for (var i = 0; i < data.Children.Count; i++)
            {
                Tree(data.Children[i], path + "/" + i);
            }
            
            ImGui.TreePop();
        }
        
        ImGui.PopID();
    }

    private static bool Remove(WidgetData root, WidgetData target)
    {
        if (root.Children.Remove(target))
        {
            return true;
        }
        
        return root.Children.Any(child => Remove(child, target))
            || (root.ButtonImage != null && Remove(root.ButtonImage, target))
            || (root.ButtonText != null && Remove(root.ButtonText, target))
            || (root.Body != null && Remove(root.Body, target));
    }

    private static void NormalizeRoot()
    {
        _asset.Root.Position = Vector2.Zero;
        _asset.Root.Anchor = Vector2.Zero;
        _asset.Root.Pivot = Vector2.Zero;
        _asset.Root.Size = _size;
    }

    private static void DrawProperties(WidgetData data)
    {
        var name = data.Name;

        if (ImGui.InputText("Name", ref name, 256, ImGuiInputTextFlags.NoUndoRedo))
        {
            data.Name = name; 
            Changed();
        }
        
        var fillCanvas = data == _asset.Root || data.FillCanvas;
        ImGui.BeginDisabled(data == _asset.Root);
        if (ImGui.Checkbox("Fill Canvas", ref fillCanvas))
        {
            data.FillCanvas = fillCanvas;
            Changed();
        }
        ImGui.EndDisabled();
        ImGui.BeginDisabled(data == _asset.Root || data.FillCanvas);
        var position = data.Position;

        if (ImGui.DragFloat2("Position", ref position))
        {
            data.Position = position; 
            Changed();
        }
        
        var size = data.Size;

        if (ImGui.DragFloat2("Size", ref size, 1, 0, 10000))
        {
            data.Size = Vector2.Max(Vector2.Zero, size); 
            Changed();
        }
        
        var anchor = data.Anchor;

        if (ImGui.DragFloat2("Anchor", ref anchor, 0.01f))
        {
            data.Anchor = anchor; 
            Changed();
        }
        
        var pivot = data.Pivot;

        if (ImGui.DragFloat2("Pivot", ref pivot, 0.01f))
        {
            data.Pivot = pivot; 
            Changed();
        }
        
        ImGui.EndDisabled();
        var visible = data.Visible;

        if (ImGui.Checkbox("Visible", ref visible))
        {
            data.Visible = visible; 
            Changed();
        }
        
        var enabled = data.IsEnabled;

        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            data.IsEnabled = enabled; 
            Changed();
        }
        
        var hit = data.HitTestVisible;

        if (ImGui.Checkbox("Hit test", ref hit))
        {
            data.HitTestVisible = hit; 
            Changed();
        }

        switch (data.Type)
        {
            case "Image":
                ColorField(data, "Color", Color.White);
                data.Texture = Resource("Texture", data.Texture);
                
                var format = (int)data.TextureFormat;
                
                if (ImGui.Combo("Format", ref format, "Png\0Jpeg\0"))
                {
                    data.TextureFormat = (TextureFormat)format; 
                    Changed();
                }
                
                data.Atlas = Resource("Atlas", data.Atlas);
                data.Region = Resource("Region", data.Region);
                EnumField(data, "DrawMode", "Simple\0Sliced\0");
                if ((data.Properties.Value<int?>("DrawMode") ?? 0) == (int)ImageDrawMode.Sliced)
                {
                    var border = data.Properties["Border"]?.ToObject<Vector4>() ?? Vector4.Zero;
                    if (ImGui.DragFloat4("Border (L T R B)", ref border, 1, 0, float.MaxValue))
                    {
                        data.Properties["Border"] = JObject.FromObject(Vector4.Max(border, Vector4.Zero));
                        Changed();
                    }
                    var borderScale = data.Properties.Value<float?>("BorderScale") ?? 1;
                    if (ImGui.DragFloat("Border scale", ref borderScale, .01f, .001f, 1000))
                    {
                        data.Properties["BorderScale"] = Math.Max(.001f, borderScale);
                        Changed();
                    }
                    BoolField(data, "FillCenter", true);
                    EnumField(data, "EdgeMode", "Stretch\0Tile\0");
                    if (data.Properties.Value<bool?>("FillCenter") ?? true)
                    {
                        EnumField(data, "CenterMode", "Stretch\0Tile\0");
                    }
                }
                
                break;
            case "Text":
                var text = data.Properties.Value<string>("Content") ?? "";

                if (ImGui.InputTextMultiline("Text", ref text, 16384, new Vector2(-1, 90), ImGuiInputTextFlags.NoUndoRedo))
                {
                    data.Properties["Content"] = text; 
                    Changed();
                }
                
                data.Font = Resource("Font", data.Font);
                
                var pixels = data.FontSize;

                if (ImGui.InputInt("Font size", ref pixels))
                {
                    data.FontSize = Math.Clamp(pixels, 1, 512); 
                    Changed();
                }
                
                ColorField(data, "Color", Color.White);
                BoolField(data, "WordWrap", true);
                BoolField(data, "ClipToRect", true);
                EnumField(data, "HorizontalAlignment", "Left\0Center\0Right\0");
                EnumField(data, "VerticalAlignment", "Top\0Center\0Bottom\0");
                
                break;
            case "Button":
                
                ColorField(data, "NormalColor", Color.White);
                ColorField(data, "HoverColor", Color.LightGray);
                ColorField(data, "PressedColor", Color.Gray);
                ColorField(data, "DisabledColor", Color.DarkGray);
                
                break;
            case "TextScrollView":
                var step = data.Properties.Value<float?>("WheelStep") ?? 40f;
                
                if (ImGui.DragFloat("Wheel step", ref step, 1, 0, 1000))
                {
                    data.Properties["WheelStep"] = step; 
                    
                    Changed();
                }
                
                break;
        }
    }

    private static string? Resource(string label, string? value)
    {
        var text = value ?? "";
        
        if (ImGui.InputText(label, ref text, 512, ImGuiInputTextFlags.NoUndoRedo))
        {
            Changed();
        }
        
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static void BoolField(WidgetData data, string name, bool fallback)
    {
        var value = data.Properties.Value<bool?>(name) ?? fallback;

        if (ImGui.Checkbox(name, ref value))
        {
            data.Properties[name] = value; 
            
            Changed();
        }
    }

    private static void EnumField(WidgetData data, string name, string items)
    {
        var value = data.Properties.Value<int?>(name) ?? 0;
        
        if (ImGui.Combo(name, ref value, items))
        {
            data.Properties[name] = value; 
            
            Changed();
        }
    }

    private static void ColorField(WidgetData data, string name, Color fallback)
    {
        var color = data.Properties[name]?.ToObject<Color>(JsonSerializer.Create(JsonHelper.GetSettings())) ?? fallback;
        var vector = new Vector4(color.R, color.G, color.B, color.A);
        
        if (ImGui.ColorEdit4(name, ref vector))
        {
            data.Properties[name] = JToken.FromObject(new Color(vector.X, vector.Y, vector.Z, vector.W),
                JsonSerializer.Create(JsonHelper.GetSettings()));
            
            Changed();
        }
    }

    private static string UIDirectory()
    {
        var directory = Path.GetFullPath(Path.Combine(Content.Path, "UI"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string AssetName(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(UIDirectory(), fullPath);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith("../")
            || relative.StartsWith(@"..\") || !Path.GetExtension(relative).Equals(".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a JSON file inside the project's Content/UI folder.");
        return relative[..^5].Replace('\\', '/');
    }

    private static void Load()
    {
        try
        {
            using var dialog = new NativeFileDialog().AddFilter("UI JSON", "json");
            var result = dialog.Open(out string? path, UIDirectory(), "");
            if (result != DialogResult.Okay || string.IsNullOrWhiteSpace(path)) return;
            var name = AssetName(path);
            var loaded = Content.LoadUI(name);
            var validation = new Canvas();
            
            validation.Resize(_size);

            try
            {
                loaded.Instantiate(validation);
            }
            finally
            {
                validation.Destroy();
            }
            
            _name = name;
            _asset = loaded;
            NormalizeRoot();
            _selected = loaded.Root;
            
            ResetHistory();
            
            _rebuild = true;
            _error = null;
        }
        catch (Exception error) { _error = error.Message; }
    }

    private static void Save()
    {
        try
        {
            using var dialog = new NativeFileDialog().SaveFile().AddFilter("UI JSON", "json");
            var folder = Path.GetDirectoryName(Path.Combine(UIDirectory(), _name + ".json"))!;
            if (!Directory.Exists(folder)) folder = UIDirectory();
            var result = dialog.Open(out string? path, folder, Path.GetFileName(_name) + ".json");
            if (result != DialogResult.Okay || string.IsNullOrWhiteSpace(path)) return;
            if (!Path.HasExtension(path)) path += ".json";
            var name = AssetName(path);
            CommitEdit();
            NormalizeRoot();
            Content.SaveUI(name, _asset);
            _name = name;

            _savedJson = SerializeAsset();

            _dirty = false;
            _error = null;
        }
        catch (Exception error)
        {
            _error = error.Message;
        }
    }

    public static void Render(Graphics graphics)
    {
        if (!Open || _gl == null)
        {
            return;
        }
        
        if (_rebuild)
        {
            var next = new Canvas();
            
            next.Resize(_size);

            try
            {
                var root = _asset.Instantiate(next);
                next.Update(0);

                _preview?.Destroy();
                _preview = next;

                PreviewWidgets.Clear();
                Bind(_asset.Root, root);

                _error = null;
            }
            catch (Exception error)
            {
                next.Destroy(); 
                _error = error.Message;
            }
            
            _rebuild = false;
        }

        if (_preview == null)
        {
            return;
        }
        
        var gl = _gl;
        gl.GetInteger(GetPName.DrawFramebufferBinding, out int previous);
        
        var viewport = new int[4];
        gl.GetInteger(GetPName.Viewport, viewport.AsSpan());
        
        if (_framebuffer == 0)
        {
            _framebuffer = gl.GenFramebuffer();
            _texture = gl.GenTexture();
        }
        try
        {
            gl.BindTexture(TextureTarget.Texture2D, _texture);
            
            if (_textureSize != _size)
            {
                gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)_size.X, (uint)_size.Y,
                    0, PixelFormat.Rgba, PixelType.UnsignedByte, new byte[(int)_size.X * (int)_size.Y * 4]);
                _textureSize = _size;
            }
            
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
            gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _texture, 0);

            if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
            {
                throw new InvalidOperationException("UI preview framebuffer is incomplete.");
            }
            
            gl.Viewport(0, 0, (uint)_size.X, (uint)_size.Y);
            // Clear the preview through the UI renderer to preserve the world's clear color.
            
            graphics.BeginUI(_size);

            try
            {
                graphics.DrawUIRect(Vector2.Zero, _size, new Color(0.12f, 0.12f, 0.12f));
                _preview.Draw(graphics);
            }
            finally
            {
                graphics.EndUI();
            }
        }
        finally
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)previous);
            gl.Viewport(viewport[0], viewport[1], (uint)viewport[2], (uint)viewport[3]);
        }
    }

    public static void Dispose()
    {
        _preview?.Destroy();
        _preview = null;

        if (_gl == null)
        {
            return;
        }

        if (_texture != 0)
        {
            _gl.DeleteTexture(_texture);
        }

        if (_framebuffer != 0)
        {
            _gl.DeleteFramebuffer(_framebuffer);
        }
        
        _texture = _framebuffer = 0;
    }
}
