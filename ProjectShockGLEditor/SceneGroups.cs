using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL;
using ProjectShockGL.Core;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

public static class SceneGroups
{
    private static readonly List<EditorGroupData> Groups = [];
    private static string _name = "";

    public static EditorGroupData[] Capture() => Groups.Select(group => new EditorGroupData
        { Id = group.Id, Name = group.Name, Visible = group.Visible }).ToArray();

    public static void Load(IEnumerable<EditorGroupData>? groups)
    {
        Groups.Clear();
        if (groups != null)
        {
            Groups.AddRange(groups.Where(group => !string.IsNullOrWhiteSpace(group.Id))
                .DistinctBy(group => group.Id).Select(group => new EditorGroupData
                    { Id = group.Id, Name = group.Name, Visible = group.Visible }));
        }
        UpdateVisibility();
    }

    private static void UpdateVisibility()
    {
        World.HiddenEditorGroups.Clear();
        foreach (var group in Groups.Where(group => !group.Visible)) World.HiddenEditorGroups.Add(group.Id);
    }

    public static void Change(Action change)
    {
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();
        var world = ProjectShockGL.Core.Game.Instance.World;
        var objects = world.GetAllObjects(true).Where(obj => !obj.IsDestroyed).Select(EditorObjectReference.Get).ToArray();
        var before = Snapshot();
        try { change(); UpdateVisibility(); }
        catch { Restore(before); throw; }
        var after = Snapshot();
        if (before != after) UndoRedo.Register(new PropertyChangeAction<string>(Restore, before, after));
        EditorLevelManager.RefreshDirty();
        return;

        void Restore(string json)
        {
            var data = JObject.Parse(json);
            Load(data["Groups"]!.ToObject<EditorGroupData[]>());
            var members = data["Members"]!.ToObject<string[][]>()!;
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i].Current.IsDestroyed) continue;
                objects[i].Current.EditorGroups.Clear();
                objects[i].Current.EditorGroups.AddRange(members[i]);
            }
            foreach (var member in data["DetailMembers"]!)
            {
                var id = member["Id"]!.ToObject<Guid>();
                if (world.Details.Find(id) == null) continue;
                SetGroups(EditorSelectionTarget.ForDetail(world, id), member["Groups"]!.ToObject<string[]>()!);
            }
            EditorLevelManager.RefreshDirty();
        }

        string Snapshot() => JsonConvert.SerializeObject(new
        {
            Groups = Capture(), Members = objects.Select(obj => obj.Current.EditorGroups.ToArray()).ToArray(),
            DetailMembers = world.Details.Select(detail => new { detail.Id, Groups = detail.EditorGroups.ToArray() }).ToArray()
        });
    }

    private static EditorGroupData Find(string id) =>
        Groups.FirstOrDefault(group => group.Id == id) ?? throw new KeyNotFoundException($"Group '{id}' was not found.");

    private static string[] GetGroups(EditorSelectionTarget target) =>
        target.Detail is { } detail ? detail.EditorGroups.ToArray() : target.Object!.EditorGroups.ToArray();

    private static void SetGroups(EditorSelectionTarget target, IEnumerable<string> groups)
    {
        if (target.IsDestroyed) return;
        if (target.Detail is { } detail)
        {
            target.World!.Details.Update(new DetailPlacement
            {
                Id = detail.Id, AssetPath = detail.AssetPath, Position = detail.Position, Rotation = detail.Rotation,
                Scale = detail.Scale, EditorGroups = groups.ToArray()
            });
        }
        else
        {
            target.Object!.EditorGroups.Clear();
            target.Object.EditorGroups.AddRange(groups);
        }
    }

    private static void Assign(EditorSelectionTarget target, string id, bool add)
    {
        var groups = GetGroups(target);
        var changed = add ? groups.Append(id).Distinct(StringComparer.Ordinal).ToArray()
            : groups.Where(group => group != id).ToArray();
        if (!groups.SequenceEqual(changed)) SetGroups(target, changed);
    }

    public static void AddSelected(string id) => AssignSelected(id, true);
    public static void RemoveSelected(string id) => AssignSelected(id, false);

    private static void AssignSelected(string id, bool add)
    {
        _ = Find(id);
        var selected = Selection.Targets;
        if (selected.Length == 0) return;
        Change(() => { foreach (var target in selected) Assign(target, id, add); });
    }

    public static void SetVisible(string id, bool visible)
    {
        var group = Find(id);
        Change(() => group.Visible = visible);
    }

    public static void Delete(string id)
    {
        var group = Find(id);
        var world = ProjectShockGL.Core.Game.Instance.World;
        var targets = world.GetAllObjects(true).Where(obj => !obj.IsDestroyed).Select(EditorSelectionTarget.ForObject)
            .Concat(world.Details.Select(detail => EditorSelectionTarget.ForDetail(world, detail.Id))).ToArray();
        Change(() =>
        {
            Groups.Remove(group);
            foreach (var target in targets) Assign(target, id, false);
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
        var world = ProjectShockGL.Core.Game.Instance.World;
        var targets = objects.Where(obj => !obj.IsDestroyed).Select(EditorSelectionTarget.ForObject)
            .Concat(world.Details.Select(detail => EditorSelectionTarget.ForDetail(world, detail.Id))).ToArray();
        foreach (var group in Groups.ToArray())
        {
            ImGui.PushID(group.Id);
            var visible = group.Visible;
            if (ImGui.Checkbox("##visible", ref visible)) SetVisible(group.Id, visible);
            ImGui.SameLine();
            var members = targets.Where(target => GetGroups(target).Contains(group.Id)).ToArray();
            if (ImGui.TreeNode($"{group.Name} ({members.Length})###members"))
            {
                ImGui.BeginDisabled(Selection.Targets.Length == 0);
                if (ImGui.Button("Add selected")) AddSelected(group.Id);
                if (ImGui.Button("Remove selected")) RemoveSelected(group.Id);
                ImGui.EndDisabled();
                foreach (var target in members)
                {
                    ImGui.PushID((target.IsDetail ? "Detail/" : "Object/") + target.Id);
                    if (ImGui.SmallButton("Remove")) Change(() => Assign(target, group.Id, false));
                    ImGui.SameLine();
                    var label = target.Detail is { } detail ? Path.GetFileName(detail.AssetPath) + " (Detail)" : target.Object!.Name;
                    if (ImGui.Selectable(label, Selection.Contains(target)))
                    {
                        if (ImGui.GetIO().KeyCtrl || Input.IsKey(Silk.NET.Input.Key.ControlLeft) ||
                            Input.IsKey(Silk.NET.Input.Key.ControlRight)) Selection.ToggleTarget(target);
                        else Selection.SetTarget(target);
                    }
                    ImGui.PopID();
                }
                if (ImGui.Button("Delete group")) Delete(group.Id);
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
        ImGui.Separator();
    }
}