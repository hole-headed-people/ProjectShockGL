using ImGuiNET;
using ProjectShockGL;
using ProjectShockGL.Core;

namespace ProjectShockGLEditor;

public static class Palette
{
    private static bool _isGeometryTab = true;
    
    private static int _selectedIndex;
    
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
            _selectedIndex = 0;
            _items = [];
            _currentType = "";
        }

        ImGui.SameLine();

        if (ImGui.Button("Entity"))
        {
            _isGeometryTab = false;
            _selectedIndex = 0;
            _items = [];
            _currentType = "";
        }

        ImGui.Separator();
        
        var expectedType = _isGeometryTab ? "Geometry" : "Entity";

        if (_currentType != expectedType)
        {
            _items = _isGeometryTab
                ? Content.GetAllGeometryNames()
                : Content.GetAllEntityNames();
            
            _currentType = expectedType;
            _selectedIndex = 0;
        }
        
        if (_items.Length > 0)
        {
            ImGui.SetNextItemWidth(-1);

            if (ImGui.ListBox("##items", ref _selectedIndex, _items, _items.Length)){}
        }
        else
        {
            ImGui.Text("No assets found");
        }

        ImGui.Separator();
        
        var canCreate = _items.Length > 0;

        if (!canCreate)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Create"))
        {
            Editor.Execute(() =>
            {
                ObjectManipulator.Commit();
                
                var name = _items[_selectedIndex];
                
                ProjectShockGL.Core.Object? obj = _isGeometryTab ? Geometry.Create(name) : Entity.Create(name);

                if (obj == null)
                {
                    throw new InvalidOperationException($"Cannot create asset '{name}'.");
                }
                
                //obj.Transform.Position = Camera.Active.Transform.Position + Camera.Active.Transform.Forward * 5;
                
                var names = ProjectShockGL.Core.Game.Instance.World.GetAllObjects(true).Select(item => item.Name).ToHashSet();
                var suffix = 1;

                while (names.Contains($"{name}_{suffix}"))
                {
                    suffix++;
                }
                
                obj.Name = $"{name}_{suffix}";
                
                Selection.Object = obj;
                UndoRedo.Clear();
                
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
