using ImGuiNET;
using Newtonsoft.Json;
using ProjectShockGL;
using ProjectShockGL.Core;

namespace ProjectShockGLEditor;

public static class SceneFolders
{
    private static readonly List<SceneFolderData> Folders = [];
    private static EditorSelectionTarget[] _drag = [];
    private static string _name = "";
    private static string _rename = "";

    public static void Load(IEnumerable<SceneFolderData>? folders)
    {
        Folders.Clear();
        if (folders != null)
            Folders.AddRange(JsonConvert.DeserializeObject<SceneFolderData[]>(JsonConvert.SerializeObject(folders))!);
        _drag = [];
    }

    public static SceneFolderData[] Capture()
    {
        var world = ProjectShockGL.Core.Game.Instance.World;
        var objects = world.GetAllObjects(true).Where(obj => obj is Entity or Geometry or BrushBox).Select(obj => obj.Guid).ToHashSet();
        var details = world.Details.Select(detail => detail.Id).ToHashSet();
        return Folders.Select(folder => new SceneFolderData
        {
            Id = folder.Id, Name = folder.Name, Expanded = folder.Expanded,
            ObjectIds = folder.ObjectIds.Where(objects.Contains).ToArray(),
            DetailIds = folder.DetailIds.Where(details.Contains).ToArray()
        }).ToArray();
    }

    private static void Change(Action change)
    {
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();
        var before = JsonConvert.SerializeObject(Folders);
        try { change(); }
        catch { Restore(before); throw; }
        var after = JsonConvert.SerializeObject(Folders);
        if (before != after) UndoRedo.Register(new PropertyChangeAction<string>(Restore, before, after));
        EditorLevelManager.RefreshDirty();
    }

    private static void Restore(string json)
    {
        Load(JsonConvert.DeserializeObject<SceneFolderData[]>(json));
        EditorLevelManager.RefreshDirty();
    }

    public static string Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Folder name cannot be empty.", nameof(name));
        var folder = new SceneFolderData { Name = name.Trim() };
        Change(() => Folders.Add(folder));
        return folder.Id;
    }

    public static void Rename(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Folder name cannot be empty.", nameof(name));
        var folder = Find(id);
        Change(() => folder.Name = name.Trim());
    }

    public static void Delete(string id)
    {
        var folder = Find(id);
        Change(() => Folders.Remove(folder));
    }

    private static SceneFolderData Find(string id) =>
        Folders.FirstOrDefault(folder => folder.Id == id) ?? throw new KeyNotFoundException($"Folder '{id}' was not found.");

    private static EditorSelectionTarget[] Targets()
    {
        var world = ProjectShockGL.Core.Game.Instance.World;
        return world.GetAllObjects(true).Where(obj => obj is Entity or Geometry or BrushBox).Select(EditorSelectionTarget.ForObject)
            .Concat(world.Details.Select(detail => EditorSelectionTarget.ForDetail(world, detail.Id))).ToArray();
    }

    private static bool Contains(SceneFolderData folder, EditorSelectionTarget target) =>
        (target.IsDetail ? folder.DetailIds : folder.ObjectIds).Contains(target.Id);

    public static void Select(string id)
    {
        var folder = Find(id);
        Selection.SetTargets(Targets().Where(target => Contains(folder, target)));
    }

    public static void MoveSelected(string? id) => Move(Selection.Targets, id);

    internal static void Move(IEnumerable<EditorSelectionTarget> targets, string? id)
    {
        var destination = id == null ? null : Find(id);
        var selected = targets.Where(target => !target.IsDestroyed && (target.IsDetail || target.Object is Entity or Geometry or BrushBox)).DistinctBy(target => target.Key).ToArray();
        if (selected.Length == 0) return;
        Change(() =>
        {
            foreach (var target in selected)
            {
                foreach (var folder in Folders)
                {
                    if (target.IsDetail) folder.DetailIds = folder.DetailIds.Where(value => value != target.Id).ToArray();
                    else folder.ObjectIds = folder.ObjectIds.Where(value => value != target.Id).ToArray();
                }
                if (destination == null) continue;
                if (target.IsDetail) destination.DetailIds = destination.DetailIds.Append(target.Id).ToArray();
                else destination.ObjectIds = destination.ObjectIds.Append(target.Id).ToArray();
            }
        });
    }

    internal static void Copy(Guid source, Guid copy, bool detail)
    {
        var folder = Folders.FirstOrDefault(item => (detail ? item.DetailIds : item.ObjectIds).Contains(source));
        if (folder == null) return;
        if (detail) folder.DetailIds = folder.DetailIds.Append(copy).Distinct().ToArray();
        else folder.ObjectIds = folder.ObjectIds.Append(copy).Distinct().ToArray();
    }

    public static void Draw()
    {
        var targets = Targets();
        Action? pending = null;
        if (Folders.Count > 0)
        {
            ImGui.Selectable("Scene root (drop to move out)", false);
            Drop(null);
        }
        foreach (var folder in Folders.ToArray())
        {
            var members = targets.Where(target => Contains(folder, target)).ToArray();
            ImGui.PushID(folder.Id);
            ImGui.SetNextItemOpen(folder.Expanded);
            var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
            if (members.Length > 0 && members.All(Selection.Contains)) flags |= ImGuiTreeNodeFlags.Selected;
            var open = ImGui.TreeNodeEx($"{folder.Name} ({members.Length})###folder", flags);
            if (open != folder.Expanded)
            {
                folder.Expanded = open;
                EditorLevelManager.RefreshDirty();
            }
            if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) Select(folder.Id);
            Drop(folder.Id);
            if (ImGui.BeginPopupContextItem("##folderMenu"))
            {
                if (ImGui.IsWindowAppearing()) _rename = folder.Name;
                if (ImGui.InputText("Name", ref _rename, 128, ImGuiInputTextFlags.EnterReturnsTrue) && !string.IsNullOrWhiteSpace(_rename))
                {
                    var renamed = _rename;
                    pending = () => Rename(folder.Id, renamed);
                    ImGui.CloseCurrentPopup();
                }
                if (ImGui.MenuItem("Move selected here")) pending = () => MoveSelected(folder.Id);
                if (ImGui.MenuItem("Delete folder (keep objects)")) pending = () => Delete(folder.Id);
                ImGui.EndPopup();
            }
            if (open)
            {
                foreach (var member in members) Row(member);
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
        foreach (var target in targets.Where(target => !Folders.Any(folder => Contains(folder, target)))) Row(target);
        if (targets.Length == 0 && Folders.Count == 0) ImGui.TextWrapped("Create an object from the Palette below.");
        ImGui.Separator();
        if (ImGui.Button("New folder"))
        {
            _name = "";
            ImGui.OpenPopup("New Scene folder");
        }
        if (ImGui.BeginPopup("New Scene folder"))
        {
            ImGui.InputText("Name", ref _name, 128);
            ImGui.BeginDisabled(string.IsNullOrWhiteSpace(_name));
            if (ImGui.Button("Create"))
            {
                var created = _name;
                pending = () => Create(created);
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndDisabled();
            ImGui.EndPopup();
        }
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) _drag = [];
        if (pending != null) Editor.Execute(pending);
        return;

        void Row(EditorSelectionTarget target)
        {
            ImGui.PushID((target.IsDetail ? "Detail/" : "Object/") + target.Id);
            var label = target.Detail is { } detail ? Path.GetFileName(detail.AssetPath) + " (Detail)" :
                string.IsNullOrWhiteSpace(target.Object!.Name) ? target.Object.GetType().Name : target.Object.Name;
            if (!target.IsActive) label += " (inactive)";
            if (!target.IsEditorVisible) label += " (hidden)";
            if (ImGui.Selectable(label, Selection.Contains(target)))
            {
                if (ImGui.GetIO().KeyCtrl) Selection.ToggleTarget(target);
                else Selection.SetTarget(target);
            }
            if (ImGui.BeginDragDropSource())
            {
                _drag = Selection.Contains(target) ? Selection.Targets : [target];
                ImGui.SetDragDropPayload("SCENE_MEMBERS", IntPtr.Zero, 0);
                ImGui.TextUnformatted($"Move {_drag.Length} item(s)");
                ImGui.EndDragDropSource();
            }
            ImGui.PopID();
        }

        void Drop(string? folder)
        {
            if (_drag.Length == 0 || !ImGui.BeginDragDropTarget()) return;
            ImGui.AcceptDragDropPayload("SCENE_MEMBERS");
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                var dragged = _drag.ToArray();
                pending = () => Move(dragged, folder);
            }
            ImGui.EndDragDropTarget();
        }
    }
}
