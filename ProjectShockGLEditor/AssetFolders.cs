using ImGuiNET;

namespace ProjectShockGLEditor;

internal static class AssetFolders
{
    public static void Draw(IEnumerable<string> paths, string search, Func<string, bool> selected, Action<string> choose)
    {
        var entries = paths.Select(path => path.Replace('\\', '/'))
            .Where(path => path.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();

        if (entries.Length == 0)
        {
            ImGui.TextDisabled("No matching assets");
        }
        
        DrawFolder(entries, "");
        
        return;

        void DrawFolder(string[] items, string prefix)
        {
            foreach (var folder in items.Where(p => p[prefix.Length..].Contains('/'))
                         .Select(p => p[prefix.Length..].Split('/')[0]).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var child = prefix + folder + "/";
                
                ImGui.PushID(child);

                if (search.Length > 0)
                {
                    ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                }
                
                if (ImGui.TreeNode(folder))
                {
                    DrawFolder(items.Where(p => p.StartsWith(child, StringComparison.OrdinalIgnoreCase)).ToArray(), child);
                    ImGui.TreePop();
                }
                
                ImGui.PopID();
            }
            
            foreach (var path in items.Where(p => !p[prefix.Length..].Contains('/')))
            {
                ImGui.PushID(path);

                if (ImGui.Selectable(path[prefix.Length..], selected(path)))
                {
                    choose(path);
                }
                
                ImGui.PopID();
            }
        }
    }
}
