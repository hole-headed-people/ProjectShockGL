using ImGuiNET;
using ProjectShockGL;
using ProjectShockGL.Core;

namespace ProjectShockGLEditor;

public static class Palette
{
    private static bool _isGeometryTab = true;
    
    private static string _selected = "";
    private static string _search = "";
    
    private static string[] _items = [];
    private static string _currentType = "";

    public static void Draw()
    {
        if (!ImGui.Begin("Palette", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.End();
            
            return;
        }
        
        if (ImGui.Button("Geometry"))
        {
            _isGeometryTab = true;
            _selected = "";
            _items = [];
            _currentType = "";
        }

        ImGui.SameLine();

        if (ImGui.Button("Entity"))
        {
            _isGeometryTab = false;
            _selected = "";
            _items = [];
            _currentType = "";
        }

        ImGui.Separator();
        if (ImGui.Button("Create BrushBox"))
        {
            Editor.Execute(() =>
            {
                var brush = SceneObjects.Create(PlaceableType.BrushBox);
                Editor.Report($"Created: {brush.Name}");
            });
        }

        ImGui.Separator();
        
        var expectedType = _isGeometryTab ? "Geometry" : "Entity";

        if (_currentType != expectedType)
        {
            _items = _isGeometryTab
                ? Content.GetAllGeometryNames()
                : Content.GetAllEntityNames();
            
            _currentType = expectedType;
            _selected = "";
        }
        
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##search", "Search assets...", ref _search, 256);
        if (ImGui.Button("Refresh"))
        {
            _items = _isGeometryTab ? Content.GetAllGeometryNames() : Content.GetAllEntityNames();
            if (!_items.Contains(_selected)) _selected = "";
        }
        ImGui.BeginChild("##assets", new System.Numerics.Vector2(0, Math.Max(80, ImGui.GetContentRegionAvail().Y - 65)));
        AssetFolders.Draw(_items, _search, path => path == _selected, path => _selected = path);
        ImGui.EndChild();
        ImGui.Separator();
        
        var canCreate = _items.Contains(_selected);

        if (!canCreate)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Create"))
        {
            Editor.Execute(() =>
            {
                var name = _selected;
                var obj = SceneObjects.Create(_isGeometryTab ? PlaceableType.Geometry : PlaceableType.Entity, name);
                Editor.Report($"Created: {obj.Name}");
            });
        }

        if (!canCreate)
        {
            ImGui.EndDisabled();
        }

        ImGui.End();
    }
}

