using System.Numerics;
using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools;

namespace ProjectShockGLEditor;

public static class LightmapBakeWindow
{
    private static LightmapBakeSession? _session;
    
    private static readonly object SettingsOwner=new();
    
    private static bool _isInitialized;
    
    private static int _levelVersion;
    private static string? _error;
    private static LightmapCleanupReport? _cleanupReport;
    private static string? _cleanupError;
    public static bool Open {get;set;}

    private static LightmapBakeSession Session => _session ??= new LightmapBakeSession();

    public static void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }
        
        _isInitialized=true;
        
        LevelManager.Loading+=LevelChanging;
        LevelManager.Unloading+=LevelChanging;
    }

    private static void LevelChanging(Level level)
    {
        _levelVersion++;
        _session?.Cancel();
    }

    public static void Update()
    {
        if (_session?.Poll() != true)
        {
            return;
        }

        if (_session.State == LightmapBakeState.Completed)
        {
            EditorLevelManager.RefreshDirty();
            Editor.Report("Lighting bake completed. Save the level to keep the new reference.");
        }
        else if (_session.State == LightmapBakeState.Cancelled)
        {
            Editor.Report("Lighting bake cancelled. Previous lightmap kept.");
        }
        else if (_session.Error != null)
        {
            Editor.Report(_session.Error);
            Log.PrintWarning("Lighting bake: " + _session.Error);
        }
    }

    private static string Signature()
    {
        var json = JObject.FromObject(EditorLevelManager.CaptureCurrentLevel(),
            JsonSerializer.Create(JsonHelper.GetSettings()));

        foreach (var key in new[] { "Groups", "Folders", "Lightmap", "KillY", "SaveLevelState" })
        {
            json.Remove(key);
        }

        foreach (var placement in json["Placeables"]?.OfType<JObject>() ?? [])
        {
            placement.Remove("EditorGroups");
        }

        foreach (var detail in json["Details"]?.OfType<JObject>() ?? [])
        {
            detail.Remove("EditorGroups");
        }

        json["Environment"] = JToken.FromObject(ProjectShockGL.Environment.AmbientColor,
            JsonSerializer.Create(JsonHelper.GetSettings()));
        
        return json.ToString(Formatting.None);
    }

    private static void Begin(World world)
    {
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();

        _error = null;

        var version = _levelVersion;
        var stem = Path.GetFileNameWithoutExtension(EditorLevelManager.CurrentPath ?? "Untitled");

        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = "Untitled";
        }

        Session.Start(world, world.LightmapSettings, stem,
            () => version == _levelVersion && ReferenceEquals(ProjectShockGL.Core.Game.Instance.World, world), Signature);
    }

    public static void Draw()
    {
        if (!Open)
        {
            UndoRedo.CommitPending(SettingsOwner);
            return;
        }
        
        Initialize();
        
        ImGui.SetNextWindowSize(new Vector2(540,620),ImGuiCond.FirstUseEver);
        
        var open=Open;

        if (ImGui.Begin("Bake Lighting", ref open))
        {
            var world = ProjectShockGL.Core.Game.Instance.World;
            var session = Session;

            ImGui.TextWrapped("Level: " + EditorLevelManager.DisplayName);
            ImGui.TextWrapped("Lightmap: " + (world.LightmapAsset ?? "None"));
            
            ImGui.Separator();
            ImGui.BeginDisabled(session.IsBusy || LevelManager.IsLoading);
            
            DrawSettings(world);
            
            ImGui.EndDisabled();
            ImGui.Separator();
            
            ImGui.BeginDisabled(session.IsBusy || LevelManager.IsLoading || ObjectManipulator.IsDragging);
            
            if (ImGui.Button("Bake"))
            {
                Begin(world);
            }
            
            ImGui.EndDisabled();
            
            ImGui.SameLine();
            
            ImGui.BeginDisabled(session.IsBusy || LevelManager.IsLoading || world.LightmapAsset == null);
            
            if (ImGui.Button("Clear"))
            {
                try
                {
                    UndoRedo.CommitPending();
                    
                    session.Clear(world);
                    
                    EditorLevelManager.RefreshDirty();
                    
                    _error = null;
                    Editor.Report("Lightmap cleared. Save the level to keep this change.");
                }
                catch (Exception error)
                {
                    _error = error.Message;
                }
            }

            ImGui.EndDisabled();
            
            ImGui.SameLine();
            ImGui.BeginDisabled(session.IsBusy || LevelManager.IsLoading);
            if (ImGui.Button("Clean unused lightmaps..."))
            {
                _cleanupReport = null;
                _cleanupError = null;
                try { _cleanupReport = LightmapCleanup.Scan(Content.Path, world.LightmapAsset); }
                catch (Exception error) { _cleanupError = error.Message; Log.PrintWarning(_cleanupError); }
                ImGui.OpenPopup("Unused lightmaps");
            }
            ImGui.EndDisabled();
            DrawCleanup(world);
            if (session.IsBusy)
            {
                ImGui.SameLine();
                ImGui.BeginDisabled(session.State == LightmapBakeState.Cancelling);
                
                if (ImGui.Button("Cancel"))
                {
                    session.Cancel();
                }
                
                ImGui.EndDisabled();
                
                ImGui.ProgressBar(session.Progress, new Vector2(-1, 0),
                    session.State == LightmapBakeState.Cancelling ? "Cancelling..." :
                    session.Progress >= 1 ? "Finishing calculation..." : $"{session.Progress:P0}");
                
                ImGui.TextUnformatted("Elapsed: " + session.Elapsed.ToString(@"hh\:mm\:ss"));
                ImGui.TextWrapped("The previous lightmap stays active. Closing this window does not cancel the bake.");
            }
            else
            {
                ImGui.TextUnformatted("Status: " + session.State);
                
                if (session.State == LightmapBakeState.Cancelled)
                {
                    ImGui.TextWrapped("Cancelled. Previous lightmap kept.");
                }
                
                if (session.Summary is { } summary)
                {
                    ImGui.TextWrapped(
                        $"Pages: {summary.Pages}, surfaces: {summary.Surfaces}, time: {summary.Elapsed.TotalSeconds:F1}s");
                    ImGui.TextWrapped($"Shadow rays: {summary.ShadowRays:N0}, GI rays: {summary.IndirectRays:N0}");
                    
                    if (summary.MissedTriangles > 0)
                    {
                        ImGui.TextWrapped("Triangles without accepted texel samples: " + summary.MissedTriangles);
                    }
                }
            }

            if (session.Error != null)
            {
                ImGui.TextWrapped(session.Error);
            }

            if (_error != null)
            {
                ImGui.TextWrapped(_error);
            }
            
            ImGui.Separator();
            ImGui.TextWrapped(
                "Bake updates the preview. Save the level to store its lightmap reference and settings. Clear removes the reference; saved packages are kept.");
        }

        Open = open;
        ImGui.End();
    }

    private static void DrawCleanup(World world)
    {
        ImGui.SetNextWindowSize(new Vector2(660, 420), ImGuiCond.Appearing);
        if (!ImGui.BeginPopupModal("Unused lightmaps", ImGuiWindowFlags.None)) return;
        ImGui.TextWrapped("Only files not referenced by any .level in Content/Levels are listed. The active lightmap is protected even before saving the level. Deletion is permanent.");
        if (_cleanupReport is { } report)
        {
            ImGui.TextWrapped($"Checked levels: {report.LevelCount}. Unused files: {report.Files.Count}. Size: {report.TotalBytes / 1048576.0:F2} MiB.");
            ImGui.BeginChild("Lightmap files", new Vector2(0, 220), ImGuiChildFlags.Border);
            foreach (var file in report.Files)
                ImGui.TextUnformatted($"{Path.GetRelativePath(report.ContentPath, file.FilePath)} ({file.Size / 1024.0:F1} KiB)");
            ImGui.EndChild();
        }
        if (_cleanupError != null) ImGui.TextWrapped(_cleanupError);
        ImGui.BeginDisabled(_cleanupReport == null || _cleanupReport.Files.Count == 0 || _cleanupError != null ||
            Session.IsBusy || LevelManager.IsLoading);
        if (ImGui.Button("Delete listed files"))
        {
            try
            {
                if (!Path.GetFullPath(Content.Path).TrimEnd(Path.DirectorySeparatorChar).Equals(_cleanupReport!.ContentPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Content directory changed. Scan again.");
                var count = LightmapCleanup.Delete(_cleanupReport, world.LightmapAsset);
                Editor.Report($"Deleted {count} unused lightmap files.");
                _cleanupReport = null;
                ImGui.CloseCurrentPopup();
            }
            catch (Exception error) { _cleanupError = error.Message; Log.PrintWarning(_cleanupError); }
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            _cleanupReport = null;
            _cleanupError = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }
    private static void DrawSettings(World world)
    {
        var settings = world.LightmapSettings;

        void Changed(string field, LightmapBakeSettings next, bool discrete = false)
        {
            var before = JsonConvert.SerializeObject(world.LightmapSettings);
            world.LightmapSettings = next;
            
            var after = JsonConvert.SerializeObject(next);
            
            UndoRedo.RecordEdit(SettingsOwner, field, before, after,
                value => world.LightmapSettings = JsonConvert.DeserializeObject<LightmapBakeSettings>(value)!,
                !discrete && ImGui.IsItemActive());
            
            settings = next;
        }

        void Finish(string field)
        {
            if (!ImGui.IsItemDeactivated())
            {
                return;
            }
            
            var current = JsonConvert.SerializeObject(world.LightmapSettings);
            
            UndoRedo.RecordEdit(SettingsOwner, field, current, current,
                value => world.LightmapSettings = JsonConvert.DeserializeObject<LightmapBakeSettings>(value)!, false);
        }

        var preset = settings.SurfaceSize == 64 && settings.SamplesPerTexel == 1 && settings.IndirectSamples == 16 ? 0 :
            settings.SurfaceSize == 128 && settings.SamplesPerTexel == 1 && settings.IndirectSamples == 64 ? 1 :
            settings.SurfaceSize == 256 && settings.SamplesPerTexel == 4 && settings.IndirectSamples == 128 ? 2 : 3;
        
        if (ImGui.Combo("Quality", ref preset, new[] { "Draft", "Standard", "High", "Custom" }, 4) && preset < 3)
        {
            Changed("Preset", settings with
            {
                SurfaceSize = Math.Min(settings.AtlasSize, new[] { 64, 128, 256 }[preset]), Padding = 2,
                SamplesPerTexel = preset == 2 ? 4 : 1, IndirectSamples = new[] { 16, 64, 128 }[preset],
                IndirectFilterRadius = 1
            }, true);
        }
        
        var atlas = settings.AtlasSize;
        
        if (ImGui.InputInt("Atlas size (px)", ref atlas))
        {
            atlas = Math.Clamp(atlas, 8, 8192);
            
            var surface = Math.Min(settings.SurfaceSize, atlas);
            
            Changed("AtlasSize",
                settings with
                {
                    AtlasSize = atlas, SurfaceSize = surface,
                    Padding = Math.Min(settings.Padding, (surface - 1) / 2)
                });
        }

        Finish("AtlasSize");
        
        var surfaceSize = settings.SurfaceSize;
        
        if (ImGui.InputInt("Surface resolution (px)", ref surfaceSize))
        {
            surfaceSize = Math.Clamp(surfaceSize, 8, settings.AtlasSize);
            
            Changed("SurfaceSize",
                settings with
                {
                    SurfaceSize = surfaceSize, Padding = Math.Min(settings.Padding, (surfaceSize - 1) / 2)
                });
        }

        Finish("SurfaceSize");
        
        var padding = settings.Padding;

        if (ImGui.InputInt("UV padding (px)", ref padding))
        {
            Changed("Padding", settings with { Padding = Math.Clamp(padding, 1, (settings.SurfaceSize - 1) / 2) });
        }
        
        Finish("Padding");
        
        var samples = settings.SamplesPerTexel == 4 ? 1 : 0;

        if (ImGui.Combo("Samples per texel", ref samples, new[] { "1", "4" }, 2))
        {
            Changed("SamplesPerTexel", settings with { SamplesPerTexel = samples == 0 ? 1 : 4 }, true);
        }
        
        var bias = settings.RayBias;

        if (ImGui.InputFloat("Shadow bias", ref bias, .0001f, .001f, "%.5f") && float.IsFinite(bias))
        {
            Changed("RayBias", settings with { RayBias = MathF.Max(.000001f, bias) });
        }
        
        Finish("RayBias");
        
        var gi = settings.IndirectEnabled;

        if (ImGui.Checkbox("Diffuse GI (one bounce)", ref gi))
        {
            Changed("GI", settings with { IndirectEnabled = gi }, true);
        }
        
        ImGui.BeginDisabled(!settings.IndirectEnabled);
        
        var giSamples = settings.IndirectSamples;

        if (ImGui.InputInt("GI samples", ref giSamples))
        {
            Changed("IndirectSamples", settings with { IndirectSamples = Math.Clamp(giSamples, 1, 4096) });
        }
        
        Finish("IndirectSamples");
        
        var strength = settings.IndirectStrength;

        if (ImGui.InputFloat("GI strength", ref strength, .1f, 1) && float.IsFinite(strength))
        {
            Changed("IndirectStrength", settings with { IndirectStrength = MathF.Max(0, strength) });
        }
        
        Finish("IndirectStrength");
        
        var filter = settings.IndirectFilterRadius;

        if (ImGui.Combo("GI filter radius", ref filter, new[] { "Off", "1", "2" }, 3))
        {
            Changed("IndirectFilterRadius", settings with { IndirectFilterRadius = filter }, true);
        }
        
        ImGui.EndDisabled();
    }

    public static void Dispose()
    {
        if (_isInitialized)
        {
            LevelManager.Loading -= LevelChanging;
            LevelManager.Unloading -= LevelChanging;

            _isInitialized = false;
        }

        _session?.Dispose();
        _session = null;
        _cleanupReport = null;
        _cleanupError = null;
        
        Open = false;
        UndoRedo.CommitPending(SettingsOwner);
    }
}