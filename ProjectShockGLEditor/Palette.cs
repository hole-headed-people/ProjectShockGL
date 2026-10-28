using System.Numerics;
using ImGuiNET;
using ProjectShockGL;
using ProjectShockGL.Core;

namespace ProjectShockGLEditor;

public static class Palette
{
    private static string _tab = "Geometry";
    
    private static string _selected = "";
    private static string _search = "";
    
    private static string[] _items = [];
    private static string _currentType = "";

    private static string[] ReadItems() => _tab switch
    {
        "Geometry" => Content.GetAllGeometryNames(),
        "Entity" => Content.GetAllEntityNames(),
        _ => Content.GetAllPrefabNames()
    };
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
            _tab = "Geometry";
            _selected = "";
            _items = [];
            _currentType = "";
        }

        ImGui.SameLine();

        if (ImGui.Button("Entity"))
        {
            _tab = "Entity";
            _selected = "";
            _items = [];
            _currentType = "";
        }

        ImGui.SameLine();
        if (ImGui.Button("Prefab"))
        {
            _tab = "Prefab";
            _currentType = "";
            _selected = "";
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
        
        var expectedType = _tab;

        if (_currentType != expectedType)
        {
            _items = ReadItems();
            
            _currentType = expectedType;
            _selected = "";
        }
        
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##search", "Search assets...", ref _search, 256);
        if (ImGui.Button("Refresh"))
        {
            _items = ReadItems();
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
                if (_tab == "Prefab")
                {
                    var objects = SceneObjects.CreatePrefabSet(name, Vector3.Zero);
                    Editor.Report($"Created prefab: {name} ({objects.Length} objects)");
                    return;
                }
                var obj = SceneObjects.Create(_tab == "Geometry" ? PlaceableType.Geometry : PlaceableType.Entity, name);
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


