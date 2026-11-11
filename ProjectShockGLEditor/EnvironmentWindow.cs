using System.Numerics;
using ImGuiNET;
using Newtonsoft.Json;
using ProjectShockGL.Core;
using ProjectShockGL.Tools;
using ProjectShockGL.Rendering;
using Environment = ProjectShockGL.Environment;

namespace ProjectShockGLEditor;

public static class EnvironmentWindow
{
    private static readonly object EditOwner = new();
    
    public static void Draw(ref bool open)
    {
        if (!open)
        {
            UndoRedo.CommitPending(EditOwner);
            return;
        }
        
        ImGui.SetNextWindowSize(new Vector2(360, 260), ImGuiCond.FirstUseEver);
        
        if (ImGui.Begin("Environment", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            var before = Capture();
            
            void Changed(string field, bool discrete = false)
            {
                var after = Capture();
                UndoRedo.RecordEdit(EditOwner, field, before, after, Apply, !discrete && ImGui.IsItemActive());
                before = after;
            }
            
            void Finish(string field)
            {
                if (ImGui.IsItemDeactivated()) UndoRedo.RecordEdit(EditOwner, field, before, before, Apply, false);
            }
            
            ImGui.TextUnformatted("Lighting");
            
            var ambient = ToVector(Environment.AmbientColor);

            if (ImGui.ColorEdit3("Ambient color", ref ambient))
            {
                Environment.AmbientColor = new Color(ambient.X, ambient.Y, ambient.Z, Environment.AmbientColor.A);
                Changed("Ambient");
            }

            Finish("Ambient");
            
            ImGui.Separator();
            var fog = Environment.IsFogEnabled;

            if (ImGui.Checkbox("Enable fog", ref fog))
            {
                Environment.IsFogEnabled = fog;
                Changed("FogEnabled", true);
            }

            var color = ToVector(Environment.FogColor);

            if (ImGui.ColorEdit3("Fog color", ref color))
            {
                Environment.FogColor = new Color(color.X, color.Y, color.Z, Environment.FogColor.A);
                Changed("FogColor");
            }

            Finish("FogColor");
            var near = Environment.FogNear;

            if (ImGui.DragFloat("Fog near", ref near, 0.1f) && float.IsFinite(near))
            {
                Environment.FogNear = Math.Clamp(near, 0, MathF.Max(0, Environment.FogFar - 0.01f));
                Changed("FogNear");
            }
            
            Finish("FogNear");
            var far = Environment.FogFar;

            if (ImGui.DragFloat("Fog far", ref far, 0.1f) && float.IsFinite(far))
            {
                Environment.FogFar = MathF.Max(far, Environment.FogNear + 0.01f);
                Changed("FogFar");
            }

            Finish("FogFar");
            ImGui.Separator();
            var killY = Game.Instance.World.KillY;

            if (ImGui.DragFloat("Kill Y", ref killY, 1f) && float.IsFinite(killY))
            {
                Game.Instance.World.KillY = killY;
                Changed("KillY");
            }

            Finish("KillY");

            var saveLevelState = Game.Instance.World.SaveLevelState;

            if (ImGui.Checkbox("Save Level State", ref saveLevelState))
            {
                Game.Instance.World.SaveLevelState = saveLevelState;
                Changed("SaveLevelState", true);
            }

            ImGui.Separator();
            if (ImGui.Button("Reset to defaults"))
            {
                var defaults = new Level();
                Environment.Reset();
                Game.Instance.World.KillY = defaults.KillY;
                Game.Instance.World.SaveLevelState = defaults.SaveLevelState;
                Changed("Reset", true);
            }
            
            ImGui.TextWrapped("Changes apply immediately and are saved with the level.");
        }
        
        ImGui.End();
    }

    private static string Capture() => JsonConvert.SerializeObject(new Level
    {
        Environment = Environment.Capture(),
        KillY = Game.Instance.World.KillY,
        SaveLevelState = Game.Instance.World.SaveLevelState
    }, JsonHelper.GetSettings());

    private static void Apply(string state)
    {
        var level = JsonConvert.DeserializeObject<Level>(state, JsonHelper.GetSettings())!;
        level.Validate();
        Environment.Apply(level.Environment ?? new EnvironmentData());
        Game.Instance.World.KillY = level.KillY;
        Game.Instance.World.SaveLevelState = level.SaveLevelState;
    }

    private static Vector3 ToVector(Color color)
    {
        return new Vector3(color.R, color.G, color.B);
    }
}
