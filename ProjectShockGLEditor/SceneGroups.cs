using ImGuiNET;
using Newtonsoft.Json;
using ProjectShockGL;
using ProjectShockGL.Core;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

public static class SceneGroups
{
    private static readonly List<EditorGroupData> Groups = [];
    private static string _name = "";

    public static EditorGroupData[] Capture()
    {
        return Groups.Select(g => new EditorGroupData
            { Id = g.Id, Name = g.Name, Visible = g.Visible }).ToArray();
    }

    public static void Load(IEnumerable<EditorGroupData>? groups)
    {
        Groups.Clear();
        
        if (groups != null)
        {
            Groups.AddRange(groups.Where(g => !string.IsNullOrWhiteSpace(g.Id))
                .DistinctBy(g => g.Id).Select(g => new EditorGroupData { Id = g.Id, Name = g.Name, Visible = g.Visible }));
        }
        
        UpdateVisibility();
    }

    private static void UpdateVisibility()
    {
        World.HiddenEditorGroups.Clear();

        foreach (var group in Groups.Where(g => !g.Visible))
        {
            World.HiddenEditorGroups.Add(group.Id);
        }
    }

    public static void Change(Action change)
    {
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();
        
        var objects = ProjectShockGL.Core.Game.Instance.World.GetAllObjects(true).Where(o => !o.IsDestroyed)
            .Select(EditorObjectReference.Get).ToArray();

        var before = Snapshot();
        change();
        
        UpdateVisibility();
        
        var after = Snapshot();

        if (before != after)
        {
            UndoRedo.Register(new PropertyChangeAction<string>(Restore, before, after));
        }
        
        EditorLevelManager.RefreshDirty();
        return;

        void Restore(string json)
        {
            var data = Newtonsoft.Json.Linq.JObject.Parse(json);
            
            Load(data["Groups"]!.ToObject<EditorGroupData[]>());
            
            var members = data["Members"]!.ToObject<string[][]>()!;
            
            for (var i = 0; i < objects.Length; i++)
            {
                objects[i].Current.EditorGroups.Clear();
                objects[i].Current.EditorGroups.AddRange(members[i]);
            }
            
            EditorLevelManager.RefreshDirty();
        }

        string Snapshot() => JsonConvert.SerializeObject(new
        {
            Groups = Capture(), Members = objects.Select(o => o.Current.EditorGroups.ToArray()).ToArray()
        });
    }

    public static void Draw(Object[] objects)
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##groupName", "New group name", ref _name, 128);
        ImGui.BeginDisabled(string.IsNullOrWhiteSpace(_name));
        
        if (ImGui.Button("Create group"))
        {
            Change(() => Groups.Add(new EditorGroupData { Name = _name.Trim() }));
            _name = "";
        }
        
        ImGui.EndDisabled();
        
        foreach (var group in Groups.ToArray())
        {
            ImGui.PushID(group.Id);
            
            var visible = group.Visible;

            if (ImGui.Checkbox("##visible", ref visible))
            {
                Change(() => group.Visible = visible);
            }
            
            ImGui.SameLine();
            
            var members = objects.Where(o => !o.IsDestroyed && o.EditorGroups.Contains(group.Id)).ToArray();
            var open = ImGui.TreeNode($"{group.Name} ({members.Length})");
            
            if (open)
            {
                var selected = Selection.Objects;
                
                ImGui.BeginDisabled(selected.Length == 0);

                if (ImGui.Button("Add selected"))
                {
                    Change(() =>
                    {
                        foreach (var obj in selected)
                        {
                            if (!obj.EditorGroups.Contains(group.Id))
                            {
                                obj.EditorGroups.Add(group.Id);
                            }
                        }
                    });
                }
                
                ImGui.EndDisabled();
                
                foreach (var obj in members)
                {
                    ImGui.PushID(obj.GetHashCode());

                    if (ImGui.SmallButton("Remove"))
                    {
                        Change(() => obj.EditorGroups.Remove(group.Id));
                    }
                    
                    ImGui.SameLine();

                    if (ImGui.Selectable(obj.Name, Selection.Object == obj))
                    {
                        Selection.Object = obj;
                    }
                    
                    ImGui.PopID();
                }
                
                if (ImGui.Button("Delete group")) Change(() =>
                {
                    Groups.Remove(group);

                    foreach (var obj in objects)
                    {
                        obj.EditorGroups.Remove(group.Id);
                    }
                });
                
                ImGui.TreePop();
            }
            
            ImGui.PopID();
        }
        
        ImGui.Separator();
    }
}
