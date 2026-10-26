using System.Numerics;
using ImGuiNET;
using ProjectShockGL.Rendering;
using Environment = ProjectShockGL.Environment;

namespace ProjectShockGLEditor;

public static class EnvironmentWindow
{
    public static void Draw(ref bool open)
    {
        if (!open)
        {
            return;
        }
        
        ImGui.SetNextWindowSize(new Vector2(360, 260), ImGuiCond.FirstUseEver);
        
        if (ImGui.Begin("Environment", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Lighting");
            
            var ambient = ToVector(Environment.AmbientColor);

            if (ImGui.ColorEdit3("Ambient color", ref ambient))
            {
                Environment.AmbientColor = new Color(ambient.X, ambient.Y, ambient.Z, Environment.AmbientColor.A);
            }

            ImGui.Separator();
            
            var fog = Environment.IsFogEnabled;

            if (ImGui.Checkbox("Enable fog", ref fog))
            {
                Environment.IsFogEnabled = fog;
            }

            var color = ToVector(Environment.FogColor);

            if (ImGui.ColorEdit3("Fog color", ref color))
            {
                Environment.FogColor = new Color(color.X, color.Y, color.Z, Environment.FogColor.A);
            }

            var near = Environment.FogNear;

            if (ImGui.DragFloat("Fog near", ref near, 0.1f) && float.IsFinite(near))
            {
                Environment.FogNear = Math.Clamp(near, 0, MathF.Max(0, Environment.FogFar - 0.01f));
            }
            
            var far = Environment.FogFar;

            if (ImGui.DragFloat("Fog far", ref far, 0.1f) && float.IsFinite(far))
            {
                Environment.FogFar = MathF.Max(far, Environment.FogNear + 0.01f);
            }

            ImGui.Separator();

            if (ImGui.Button("Reset to defaults"))
            {
                Environment.Reset();
            }
            
            ImGui.TextWrapped("Changes apply immediately and are saved with the level.");
        }
        
        ImGui.End();
    }

    private static Vector3 ToVector(Color color)
    {
        return new Vector3(color.R, color.G, color.B);
    }
}
