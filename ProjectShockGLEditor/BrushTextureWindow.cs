using System.Numerics;
using ImGuiNET;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;

namespace ProjectShockGLEditor;

public static class BrushTextureWindow
{
    private static readonly BrushFace[] Faces = Enum.GetValues<BrushFace>();
    private static readonly string[] FaceNames = ["+X", "-X", "+Y", "-Y", "+Z", "-Z"];
    
    private static EditorObjectReference? _selected;
    
    private static BrushFace _face;
    
    private static string _search = "";

    public static bool Open { get; set; }

    private static BrushBox? Current => Selection.Targets.Length == 1 && Selection.Object is BrushBox { IsDestroyed: false } brush
        ? brush : null;

    public static void PickAt(Vector2 mouse, Vector2 screen)
    {
        var camera = Camera.Active;

        if (!GizmoMath.ScreenRay(mouse, screen, camera.GetViewMatrix() * camera.GetProjectionMatrix(),
                out var origin, out var direction))
        {
            return;
        }
        
        BrushBox? nearest = null;
        
        var distance = camera.Far;
        var face = BrushFace.PositiveX;
        
        foreach (var brush in ProjectShockGL.Core.Game.Instance.World.GetAllObjects(true).OfType<BrushBox>())
        {
            if (!brush.IsActive || brush.IsDestroyed || !brush.IsEditorVisible)
            {
                continue;
            }

            if (!RayFace(brush, origin, direction, distance, out var hit, out var candidate))
            {
                continue;
            }
            
            nearest = brush;
            distance = hit;
            face = candidate;
        }

        if (nearest == null)
        {
            return;
        }
        
        UndoRedo.CommitPending();
        Selection.Object = nearest;
        
        _selected = EditorObjectReference.Get(nearest);
        _face = face;
    }

    public static bool RayFace(BrushBox brush, Vector3 origin, Vector3 direction, float maxDistance,
        out float distance, out BrushFace face)
    {
        distance = maxDistance;
        face = default;

        if (!Matrix4x4.Invert(brush.Transform.GetModelMatrix(), out var inverse))
        {
            return false;
        }
        
        var localOrigin = Vector3.Transform(origin, inverse);
        var localDirection = Vector3.TransformNormal(direction, inverse);
        
        var half = brush.Size * 0.5f;
        
        var found = false;
        
        foreach (var candidate in Faces)
        {
            var axis = (int)candidate / 2;
            var sign = (int)candidate % 2 == 0 ? 1 : -1;
            
            var o = Axis(localOrigin, axis);
            var d = Axis(localDirection, axis);

            if (MathF.Abs(d) < 1e-8f)
            {
                continue;
            }
            
            var t = (sign * Axis(half, axis) - o) / d;

            if (!float.IsFinite(t) || t < 0 || t > distance)
            {
                continue;
            }
            
            var point = localOrigin + localDirection * t;

            if (MathF.Abs(Axis(point, (axis + 1) % 3)) > Axis(half, (axis + 1) % 3) + 0.00001f ||
                MathF.Abs(Axis(point, (axis + 2) % 3)) > Axis(half, (axis + 2) % 3) + 0.00001f)
            {
                continue;
            }
            
            found = true;
            
            distance = t;
            face = candidate;
        }
        
        return found;
    }

    public static BrushFaceData Fit(BrushBox brush, BrushFace face)
    {
        var size = Vector3.Abs(brush.Size * brush.Transform.Scale);
        
        var span = face switch
        {
            BrushFace.PositiveX or BrushFace.NegativeX => new Vector2(size.Z, size.Y),
            BrushFace.PositiveY or BrushFace.NegativeY => new Vector2(size.X, size.Z),
            BrushFace.PositiveZ or BrushFace.NegativeZ => new Vector2(size.X, size.Y),
            _ => throw new ArgumentOutOfRangeException(nameof(face))
        };
        
        return Settings(brush, face) with { TextureScale = Vector2.One / span, TextureOffset = Vector2.Zero };
    }

    public static void Apply(BrushBox brush, BrushFace face, BrushFaceData? settings, string field, bool editing = false)
    {
        var reference = EditorObjectReference.Get(brush);
        var before = InspectorState.Capture(brush);
        
        brush.SetFace(face, settings);
        
        var after = InspectorState.Capture(brush);
        
        UndoRedo.RecordEdit(reference, $"Brush face {face}: {field}", before, after,
            state => InspectorState.Apply(reference.Current, state), editing);
        
        EditorLevelManager.RefreshDirty();
    }

    private static BrushFaceData Settings(BrushBox brush, BrushFace face)
    {
        return brush.Faces.TryGetValue(face, out var settings) ? settings : new BrushFaceData();
    }

    public static void Draw(Vector2 screen, Vector2 sceneMin, Vector2 sceneMax)
    {
        if (!Open)
        {
            return;
        }
        
        var open = Open;
        ImGui.SetNextWindowSize(new Vector2(430, 460), ImGuiCond.FirstUseEver);
        
        var visible = ImGui.Begin("Texture", ref open);
        
        Open = open;

        if (!open)
        {
            UndoRedo.CommitPending();
        }
        
        var brush = Current;
        
        if (visible)
        {
            ImGui.TextWrapped("Click a BrushBox face in SceneView to edit its texture.");

            if (brush == null)
            {
                ImGui.TextUnformatted("Select one BrushBox.");
            }
            else
            {
                var reference = EditorObjectReference.Get(brush);
                
                if (_selected != reference)
                {
                    UndoRedo.CommitPending();
                    
                    _selected = reference;
                    _face = BrushFace.PositiveZ;
                }
                
                ImGui.TextUnformatted(brush.Name);
                
                var index = (int)_face;
                
                if (ImGui.Combo("Face", ref index, FaceNames, FaceNames.Length))
                {
                    UndoRedo.CommitPending();
                    
                    _face = Faces[index];
                }
                
                DrawSettings(brush);
            }
        }
        
        ImGui.End();

        if (Open && brush != null)
        {
            DrawOutline(brush, screen, sceneMin, sceneMax);
        }
    }

    private static void DrawSettings(BrushBox brush)
    {
        var settings = Settings(brush, _face);
        var inherit = settings.Texture == null && settings.TextureFormat == null;
        
        if (ImGui.Checkbox("Use brush texture", ref inherit))
        {
            Apply(brush, _face, settings with
            {
                Texture = inherit ? null : brush.TextureName,
                TextureFormat = inherit ? null : brush.TextureFormat
            }, "Texture");
            
            settings = Settings(brush, _face);
        }
        
        var texture = settings.Texture ?? brush.TextureName;
        var format = settings.TextureFormat ?? brush.TextureFormat;
        
        if (ImGui.BeginCombo("Texture", $"{texture} ({format})"))
        {
            ImGui.InputTextWithHint("##search", "Search textures...", ref _search, 256);

            if ("WhitePixel".Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase) &&
                ImGui.Selectable("WhitePixel", texture == "WhitePixel"))
            {
                Apply(brush, _face, settings with { Texture = "WhitePixel", TextureFormat = TextureFormat.Jpeg }, "Texture");
            }
            
            var directory = Path.Combine(Content.Path, "Textures");
            var files = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                    .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg")
                    .Select(path => Path.GetRelativePath(directory, path).Replace('\\', '/')).ToArray() : [];
            
            AssetFolders.Draw(files, _search,
                path => path[..^Path.GetExtension(path).Length] == texture &&
                    (Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) ? TextureFormat.Png : TextureFormat.Jpeg) == format,
                path => Editor.Execute(() =>
                {
                    var extension = Path.GetExtension(path);
                    
                    var name = path[..^extension.Length];
                    var selectedFormat = extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ? TextureFormat.Png : TextureFormat.Jpeg;
                    
                    _ = Content.LoadTexture(name, selectedFormat) ?? throw new FileNotFoundException(path);
                    Apply(brush, _face, Settings(brush, _face) with { Texture = name, TextureFormat = selectedFormat }, "Texture"); 
                }));
            
            ImGui.EndCombo();
        }
        
        settings = Settings(brush, _face);
        
        var useScale = settings.TextureScale == null;
        
        if (ImGui.Checkbox("Use brush UV scale", ref useScale))
        {
            Apply(brush, _face, settings with { TextureScale = useScale ? null : new Vector2(brush.TextureScale) }, "Scale");
            settings = Settings(brush, _face);
        }
        
        var scale = settings.TextureScale ?? new Vector2(brush.TextureScale);

        if (ImGui.DragFloat2("UV scale", ref scale, 0.01f, 0.000001f, float.MaxValue, "%.4f"))
        {
            Apply(brush, _face, settings with { TextureScale = Vector2.Max(scale, new Vector2(0.000001f)) }, "Scale", ImGui.IsItemActive());
        }

        if (ImGui.IsItemDeactivated())
        {
            UndoRedo.CommitPending();
        }
        
        settings = Settings(brush, _face);
        
        var offset = settings.TextureOffset;

        if (ImGui.DragFloat2("UV offset", ref offset, 0.01f))
        {
            Apply(brush, _face, settings with { TextureOffset = offset }, "Offset", ImGui.IsItemActive());
        }

        if (ImGui.IsItemDeactivated())
        {
            UndoRedo.CommitPending();
        }
        
        ImGui.TextDisabled("Scale = repeats per world unit");

        if (ImGui.Button("Fit"))
        {
            Apply(brush, _face, Fit(brush, _face), "Fit");
        }
        
        ImGui.SameLine();

        if (ImGui.Button("Reset face"))
        {
            Apply(brush, _face, null, "Reset");
        }

        ImGui.Separator();
        settings = Settings(brush, _face);
        var inheritSurface = settings.SurfaceMode == null && settings.AlphaCutoff == null;
        if (ImGui.Checkbox("Use brush surface", ref inheritSurface))
        {
            Apply(brush, _face, settings with
            {
                SurfaceMode = inheritSurface ? null : brush.SurfaceMode,
                AlphaCutoff = inheritSurface ? null : brush.AlphaCutoff
            }, "Surface");
            settings = Settings(brush, _face);
        }
        var mode = (int)(settings.SurfaceMode ?? brush.SurfaceMode);
        if (ImGui.Combo("SurfaceMode", ref mode, new[] { "Opaque", "Cutout", "Transparent" }, 3))
        {
            Apply(brush, _face, settings with { SurfaceMode = (SurfaceMode)mode }, "SurfaceMode");
            settings = Settings(brush, _face);
        }
        if ((SurfaceMode)mode == SurfaceMode.Cutout)
        {
            var cutoff = settings.AlphaCutoff ?? brush.AlphaCutoff;
            if (ImGui.DragFloat("AlphaCutoff", ref cutoff, 0.01f, 0, 1))
                Apply(brush, _face, settings with { AlphaCutoff = Math.Clamp(cutoff, 0, 1) }, "AlphaCutoff", ImGui.IsItemActive());
            if (ImGui.IsItemDeactivated()) UndoRedo.CommitPending();
        }
    }

    private static void DrawOutline(BrushBox brush, Vector2 screen, Vector2 sceneMin, Vector2 sceneMax)
    {
        var data = BrushBoxMesh.Build(brush.Size, brush.Transform.Scale, brush.TextureScale);
        var model = brush.Transform.GetModelMatrix();
        var vp = Camera.Active.GetViewMatrix() * Camera.Active.GetProjectionMatrix();
        var points = new Vector2[4];
        
        for (var i = 0; i < 4; i++)
        {
            var at = ((int)_face * 4 + i) * 8;
            var point = Vector3.Transform(new Vector3(data.Vertices[at], data.Vertices[at + 1], data.Vertices[at + 2]), model);

            if (!GizmoMath.Project(point, vp, screen, out points[i]))
            {
                return;
            }
        }
        
        var draw = ImGui.GetBackgroundDrawList();
        draw.PushClipRect(sceneMin, sceneMax, true);
        
        var color = ImGui.GetColorU32(new Vector4(1, 0.8f, 0.15f, 1));

        for (var i = 0; i < 4; i++)
        {
            draw.AddLine(points[i], points[(i + 1) % 4], color, 2);
        }
        
        draw.PopClipRect();
    }

    private static float Axis(Vector3 value, int axis)
    {
        return axis == 0 ? value.X : axis == 1 ? value.Y : value.Z;
    }
}
