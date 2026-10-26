using System.Numerics;
using ImGuiNET;
using NativeFileDialogNET;
using ProjectShockGL;
using ProjectShockGL.Rendering;
using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace ProjectShockGLEditor;

public static class Editor
{
    private const ImGuiWindowFlags PanelFlags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize |
                                                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings;
    
    private static ImGuiController _imGui = null!;
    private static IWindow _window = null!;
    private static Vector2 _windowSize;
    
    private static float _dirtyTimer;
    
    private static string _status = "Ready";
    private static string? _error;
    
    private static Action? _pendingAction;
    
    private static bool _requestUnsavedPopup;
    private static bool _requestErrorPopup;
    
    private static bool _allowClose;
    private static bool _showEnvironment;

    public static Vector2 SceneMin => new(LeftWidth, 82);
    public static Vector2 SceneMax => new(_windowSize.X - RightWidth, _windowSize.Y - 28);
    private static float LeftWidth => MathF.Min(230, _windowSize.X * 0.23f);
    private static float RightWidth => MathF.Min(320, _windowSize.X * 0.28f);
    public static bool SceneHovered => InScene(Input.GetMousePosition());
    
    public static bool CanDuplicateSelection => Selection.Object is { IsDestroyed: false } &&
                                                Selection.Object is ProjectShockGL.Core.Geometry or ProjectShockGL.Core.Entity or ProjectShockGL.Core.BrushBox;

    public static void Initialize()
    {
        Application.LoadGl += OnLoad;
        Application.Load += EditorLevelManager.Initialize;
        Application.Load += SceneCamera.Initialize;
        Application.Update += OnUpdate;
        Application.BeforeRender += OnBeforeRender;
        Application.Render += OnRender;
        Application.AfterRender += OnAfterRender;
        Application.Resized += OnResized;
        Application.Unload += OnUnload;
        Application.Closing += OnWindowClosing;
    }

    private static void OnLoad(GL gl, IWindow window, IInputContext input)
    {
        _window = window;
        _windowSize = new Vector2(window.Size.X, window.Size.Y);
        _imGui = new ImGuiController(gl, window, input);
    }

    private static bool InScene(Vector2 point)
    {
        return point.X >= SceneMin.X && point.X < SceneMax.X && point.Y >= SceneMin.Y && point.Y < SceneMax.Y;
    }

    private static void OnUpdate(float deltaTime)
    {
        var io = ImGui.GetIO();
        
        if (!io.WantCaptureKeyboard && !ObjectManipulator.IsDragging)
        {
            var control = Input.IsKey(Key.ControlLeft) || Input.IsKey(Key.ControlRight);
            
            if (control)
            {
                if (Input.IsKeyDown(Key.S))
                {
                    Execute(() => Save(Input.IsKey(Key.ShiftLeft) || Input.IsKey(Key.ShiftRight)));
                }

                if (Input.IsKeyDown(Key.O))
                {
                    RequestChange(Open);
                }

                if (Input.IsKeyDown(Key.N))
                {
                    RequestChange(EditorLevelManager.NewLevel);
                }

                if (Input.IsKeyDown(Key.Z))
                {
                    UndoRedo.Undo();
                }

                if (Input.IsKeyDown(Key.Y))
                {
                    UndoRedo.Redo();
                }
                if (Input.IsKeyDown(Key.D) && _pendingAction == null && _error == null)
                {
                    DuplicateSelection();
                }
            }
            else if (SceneHovered && !Input.IsMouseButton(MouseButton.Right))
            {
                if (Input.IsKeyDown(Key.G))
                {
                    ObjectManipulator.Mode = ManipulationMode.Move;
                }

                if (Input.IsKeyDown(Key.R))
                {
                    ObjectManipulator.Mode = ManipulationMode.Rotate;
                }

                if (Input.IsKeyDown(Key.T))
                {
                    ObjectManipulator.Mode = ManipulationMode.Scale;
                }

                if (Input.IsKeyDown(Key.Delete))
                {
                    DeleteSelection();
                }
            }
        }

        var sceneInput = SceneHovered && !io.WantCaptureMouse && !io.WantCaptureKeyboard &&
                         !ObjectManipulator.IsDragging && _pendingAction == null && _error == null;
        
        SceneCamera.Update(deltaTime, sceneInput);
        
        _dirtyTimer += deltaTime;
        
        if (_dirtyTimer >= 0.25f)
        {
            _dirtyTimer = 0;

            if (_error == null)
            {
                Execute(EditorLevelManager.RefreshDirty);
            }
        }
    }

    private static void OnBeforeRender(float deltaTime)
    {
        _imGui.Update(deltaTime);
    }

    private static void OnRender(Graphics graphics)
    {
        SceneGrid.Render(graphics);
        
        if (Selection.Object is { IsDestroyed: false } selected)
        {
            var bounds = selected.GetWorldBounds();
            graphics.DrawBoxWireframe(bounds.min, bounds.max, Color.White);

            if (selected is ProjectShockGL.Core.Light light)
            {
                LightGizmo.Draw(graphics, light);
            }
        }
    }

    private static void OnAfterRender(float deltaTime)
    {
        if (_windowSize.X < 400 || _windowSize.Y < 200)
        {
            ObjectManipulator.Cancel();
            
            _imGui.Render();
            
            return;
        }
        
        DrawMenuBar();
        DrawToolbar();
        DrawHierarchy();
        
        ImGui.SetNextWindowPos(new Vector2(0, 82 + (_windowSize.Y - 110) * 0.48f));
        ImGui.SetNextWindowSize(new Vector2(LeftWidth, (_windowSize.Y - 110) * 0.52f));
        
        Palette.Draw();
        
        ImGui.SetNextWindowPos(new Vector2(_windowSize.X - RightWidth, 82));
        ImGui.SetNextWindowSize(new Vector2(RightWidth, _windowSize.Y - 110));
        
        Inspector.Draw();
        EnvironmentWindow.Draw(ref _showEnvironment);
        
        DrawStatus();
        DrawPopups();

        var mouse = ImGui.GetIO().MousePos;
        var allowScene = InScene(mouse) && !ImGui.GetIO().WantCaptureMouse &&
                         _pendingAction == null && _error == null &&
                         !ImGui.IsMouseDown(ImGuiMouseButton.Right) && !ImGui.IsMouseDown(ImGuiMouseButton.Middle);
        
        var owned = ObjectManipulator.Draw(_windowSize, SceneMin, SceneMax, allowScene);

        if (allowScene && !owned && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            ObjectPicker.PickAt(mouse, _windowSize);
        }
        
        _imGui.Render();
    }

    private static void OnResized(Vector2D<int> size)
    {
        _windowSize = new Vector2(size.X, size.Y);
    }

    private static void OnUnload()
    {
        ObjectManipulator.Cancel();
        _imGui.Dispose();
    }

    private static bool OnWindowClosing()
    {
        if (_allowClose)
        {
            return true;
        }
        
        var canClose = false;
        
        Execute(() =>
        {
            ObjectManipulator.Commit();
            EditorLevelManager.RefreshDirty();
            
            canClose = !EditorLevelManager.IsDirty;
            
            if (!canClose)
            {
                RequestChange(() => { _allowClose = true; Application.Quit(); });
            }
        });
        
        return canClose;
    }

    public static void Report(string message)
    {
        _status = message;
    }
    
    public static void Execute(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _error = ex.GetBaseException().Message;
            _requestErrorPopup = true;
            _status = "Operation failed";
            
            Console.WriteLine(ex);
        }
    }

    private static void RequestChange(Action action)
    {
        Execute(() =>
        {
            ObjectManipulator.Commit();
            EditorLevelManager.RefreshDirty();
            if (!EditorLevelManager.IsDirty) { action(); return; }
            _pendingAction = action;
            _requestUnsavedPopup = true;
        });
    }

    private static bool Save(bool saveAs = false)
    {
        var path = EditorLevelManager.CurrentPath;
        
        if (saveAs || path == null)
        {
            using var dialog = new NativeFileDialog().SaveFile().AddFilter("Map Files", "map");
            var result = dialog.Open(out string? selected, DefaultDirectory(), EditorLevelManager.DisplayName);

            if (result != DialogResult.Okay || string.IsNullOrWhiteSpace(selected))
            {
                return false;
            }
            
            path = Path.HasExtension(selected) ? selected : selected + ".map";
        }
        
        EditorLevelManager.SaveCurrentLevel(path);
        _status = $"Saved: {path}";
        
        return true;
    }

    private static string DefaultDirectory()
    {
        if (EditorLevelManager.CurrentPath != null)
        {
            return Path.GetDirectoryName(EditorLevelManager.CurrentPath)!;
        }
        
        var directory = Path.Combine(Content.Path, "Levels");
        Directory.CreateDirectory(directory);
        
        return directory;
    }

    private static void Open()
    {
        using var dialog = new NativeFileDialog().AddFilter("Map Files", "map");
        var result = dialog.Open(out string? path, DefaultDirectory(), "");
        
        if (result != DialogResult.Okay || string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        
        EditorLevelManager.OpenLevel(path);
        
        _status = $"Opened: {path}";
    }

    public static void DuplicateSelection()
    {
        if (!CanDuplicateSelection || ObjectManipulator.IsDragging)
        {
            return;
        }
        
        Execute(() =>
        {
            ObjectManipulator.Commit();
            
            var duplicate = EditorLevelManager.DuplicateObject(Selection.Object!);
            
            Selection.Object = duplicate;
            UndoRedo.Clear();
            EditorLevelManager.RefreshDirty();
            
            _status = $"Duplicated: {duplicate.Name}";
        });
    }

    public static void DeleteSelection()
    {
        if (Selection.Object is not { IsDestroyed: false } selected)
        {
            return;
        }
        
        ObjectManipulator.Cancel();
        ProjectShockGL.Core.Game.Instance.World.Destroy(selected);
        
        Selection.Object = null;
        UndoRedo.Clear();
        
        _status = "Object deleted";
    }

    private static void DrawHierarchy()
    {
        ImGui.SetNextWindowPos(new Vector2(0, 82));
        ImGui.SetNextWindowSize(new Vector2(LeftWidth, (_windowSize.Y - 110) * 0.48f));
        
        if (ImGui.Begin("Scene", PanelFlags))
        {
            var objects = ProjectShockGL.Core.Game.Instance.World.GetAllObjects(true);
            
            for (var i = 0; i < objects.Length; i++)
            {
                var obj = objects[i];
                
                ImGui.PushID(i);
                
                var label = string.IsNullOrEmpty(obj.Name) ? obj.GetType().Name : obj.Name;
                
                if (!obj.IsActive)
                {
                    label += " (inactive)";
                }
                
                if (ImGui.Selectable(label, Selection.Object == obj))
                {
                    ObjectManipulator.Commit();
                    Selection.Object = obj;
                }
                
                ImGui.PopID();
            }

            if (objects.Length == 0)
            {
                ImGui.TextWrapped("Create an object from the Palette below.");
            }
        }
        
        ImGui.End();
    }

    private static void DrawMenuBar()
    {
        if (!ImGui.BeginMainMenuBar())
        {
            return;
        }
        
        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.MenuItem("New", "Ctrl+N"))
            {
                RequestChange(EditorLevelManager.NewLevel);
            }

            if (ImGui.MenuItem("Open...", "Ctrl+O"))
            {
                RequestChange(Open);
            }
            
            ImGui.Separator();

            if (ImGui.MenuItem("Save", "Ctrl+S"))
            {
                Execute(() => Save());
            }

            if (ImGui.MenuItem("Save As...", "Ctrl+Shift+S"))
            {
                Execute(() => Save(true));
            }
            
            ImGui.Separator();

            if (ImGui.MenuItem("Exit"))
            {
                RequestChange(() => { _allowClose = true; Application.Quit(); });
            }
            
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("Edit"))
        {
            if (ImGui.MenuItem("Undo transform", "Ctrl+Z", false, UndoRedo.CanUndo))
            {
                UndoRedo.Undo();
            }

            if (ImGui.MenuItem("Redo transform", "Ctrl+Y", false, UndoRedo.CanRedo))
            {
                UndoRedo.Redo();
            }

            if (ImGui.MenuItem("Duplicate object", "Ctrl+D", false, CanDuplicateSelection && !ObjectManipulator.IsDragging))
            {
                DuplicateSelection();
            }

            if (ImGui.MenuItem("Delete object", "Delete", false, Selection.Object != null))
            {
                DeleteSelection();
            }
            
            if (ImGui.MenuItem("Environment", "", _showEnvironment))
            {
                _showEnvironment = !_showEnvironment;
            }
            
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("View"))
        {
            var grid = SceneGrid.Show;

            if (ImGui.MenuItem("Grid", "", grid))
            {
                SceneGrid.Show = !grid;
            }
            
            var speed = SceneCamera.Speed;

            if (ImGui.SliderFloat("Camera speed", ref speed, 1, 30))
            {
                SceneCamera.Speed = speed;
            }
            
            var sensitivity = SceneCamera.MouseSensitivity;

            if (ImGui.SliderFloat("Mouse sensitivity", ref sensitivity, 0.02f, 1))
            {
                SceneCamera.MouseSensitivity = sensitivity;
            }
            
            ImGui.EndMenu();
        }
        
        ImGui.TextUnformatted($"   {EditorLevelManager.DisplayName}{(EditorLevelManager.IsDirty ? " *" : "")}");
        ImGui.EndMainMenuBar();
    }

    private static void DrawToolbar()
    {
        ImGui.SetNextWindowPos(new Vector2(0, 24));
        ImGui.SetNextWindowSize(new Vector2(_windowSize.X, 58));
        
        if (ImGui.Begin("##toolbar", PanelFlags | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.BeginDisabled(ObjectManipulator.IsDragging);
            
            foreach (var mode in Enum.GetValues<ManipulationMode>())
            {
                if (mode != ManipulationMode.Move)
                {
                    ImGui.SameLine();
                }
                
                var hotkey = mode == ManipulationMode.Move ? "G" : mode == ManipulationMode.Rotate ? "R" : "T";

                if (ImGui.RadioButton($"{mode} ({hotkey})", ObjectManipulator.Mode == mode))
                {
                    ObjectManipulator.Mode = mode;
                }
            }
            
            ImGui.SameLine();
            
            var local = ObjectManipulator.Local;

            if (ImGui.Checkbox("Local", ref local))
            {
                ObjectManipulator.Local = local;
            }
            
            ImGui.SameLine();
            
            var snap = ObjectManipulator.SnapEnabled;

            if (ImGui.Checkbox("Snap", ref snap))
            {
                ObjectManipulator.SnapEnabled = snap;
            }
            
            ImGui.SameLine();
            ImGui.SetNextItemWidth(70);
            
            var step = SceneGrid.Step;

            if (ImGui.InputFloat("Move step", ref step))
            {
                SceneGrid.Step = float.IsFinite(step) ? Math.Clamp(step, 0.01f, 100) : 1;
            }
            
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60);
            
            var angle = ObjectManipulator.RotationStep;

            if (ImGui.InputFloat("Angle", ref angle))
            {
                ObjectManipulator.RotationStep = float.IsFinite(angle) ? Math.Clamp(angle, 1, 180) : 15;
            }
            
            ImGui.EndDisabled();
            
            var preview = Application.EditorPreview;

            if (ImGui.RadioButton("Lit", !preview.Unlit))
            {
                preview.Unlit = false;
            }
            
            ImGui.SameLine();

            if (ImGui.RadioButton("Unlit", preview.Unlit))
            {
                preview.Unlit = true;
            }
            
            ImGui.SameLine();
            
            var environmentEnabled = preview.EnvironmentEnabled;

            if (ImGui.Checkbox("Environment", ref environmentEnabled))
            {
                preview.EnvironmentEnabled = environmentEnabled;
            }
            
            ImGui.SameLine();
            
            ImGui.TextUnformatted("| Shift: snap | Esc: cancel | RMB + WASD/QE: camera | F: focus");
        }
        
        ImGui.End();
    }

    private static void DrawStatus()
    {
        ImGui.SetNextWindowPos(new Vector2(0, _windowSize.Y - 28));
        ImGui.SetNextWindowSize(new Vector2(_windowSize.X, 28));

        if (ImGui.Begin("##status", PanelFlags | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.TextUnformatted(_status);
        }
        
        ImGui.End();
    }

    private static void DrawPopups()
    {
        if (_requestUnsavedPopup)
        {
            ImGui.OpenPopup("Unsaved changes"); _requestUnsavedPopup = false;
        }
        
        if (ImGui.BeginPopupModal("Unsaved changes", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Save changes before continuing?");

            if (ImGui.Button("Save"))
            {
                Execute(() => { if (Save()) CompletePending(); });
            }
            
            ImGui.SameLine();

            if (ImGui.Button("Discard"))
            {
                Execute(CompletePending);
            }
            
            ImGui.SameLine();

            if (ImGui.Button("Cancel"))
            {
                _pendingAction = null; ImGui.CloseCurrentPopup();
            }
            
            ImGui.EndPopup();
        }

        if (_requestErrorPopup)
        {
            ImGui.OpenPopup("Editor error"); _requestErrorPopup = false;
        }
        
        if (ImGui.BeginPopupModal("Editor error", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextWrapped(_error ?? "");

            if (ImGui.Button("OK"))
            {
                _error = null; ImGui.CloseCurrentPopup();
            }
            
            ImGui.EndPopup();
        }
    }

    private static void CompletePending()
    {
        var action = _pendingAction;
        
        _pendingAction = null;
        ImGui.CloseCurrentPopup();
        
        action?.Invoke();
    }
}

