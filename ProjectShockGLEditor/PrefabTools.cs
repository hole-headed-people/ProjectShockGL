using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL;
using ProjectShockGL.Tools;
using Object = ProjectShockGL.Core.Object;
using System.Numerics;
using ImGuiNET;

namespace ProjectShockGLEditor;

public static class PrefabTools
{
    private static string _name = "";
    public static void DrawSave()
    {
        if (ImGui.Button("Save prefab..."))
        {
            _name = Selection.Objects.Length == 1 ? Selection.Object!.Name : "Puzzle";
            
            ImGui.OpenPopup("Save prefab");
        }

        if (!ImGui.BeginPopup("Save prefab"))
        {
            return;
        }
        
        ImGui.TextUnformatted("Content/Prefabs/ (name without .json)");
        ImGui.InputText("Name / folder", ref _name, 256);
        ImGui.TextWrapped("Example: Puzzles/Bridge. Existing files are not overwritten.");
        
        ImGui.BeginDisabled(string.IsNullOrWhiteSpace(_name));
        
        if (ImGui.Button("Save"))
        {
            Editor.Execute(() => Save(Selection.Objects, _name.Trim()));
            ImGui.CloseCurrentPopup();
        }
        
        ImGui.EndDisabled();
        ImGui.EndPopup();
    }

    public static void Save(Object obj, string name)
    {
        Save([obj], name);
    }

    public static void Save(IEnumerable<Object> objects, string name)
    {
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();
        
        var selected = objects.Distinct().ToArray();
        var placements = EditorLevelManager.CaptureLevel(selected).Placeables;

        if (placements.Length == 0 || placements.Length != selected.Length)
        {
            throw new InvalidOperationException("Only placed objects can be saved as prefabs.");
        }
        
        var pivot = placements.Aggregate(Vector3.Zero, (sum, p) => sum + p.Position) / placements.Length;
        
        var path = Content.PrefabPath(name);
        var serializer = JsonSerializer.Create(JsonHelper.GetSettings());
        var members = new JArray();
        
        foreach (var placement in placements)
        {
            var member = JObject.FromObject(placement, serializer);
            member["Position"] = JToken.FromObject(placement.Position - pivot, serializer);

            if (placements.Length == 1)
            {
                member["Name"] = Path.GetFileName(name.Replace('\\', '/'));
            }
            
            member.Remove("EditorGroups");
            members.Add(member);
        }
        
        var data = new JObject { ["Pivot"] = JToken.FromObject(pivot, serializer), ["Placeables"] = members };
        
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            File.WriteAllText(temporary, data.ToString(Formatting.Indented));
            File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
        
        Editor.Report($"Prefab saved: {name}");
    }
}
