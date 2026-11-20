using System.Numerics;
using ImGuiNET;
using NativeFileDialogNET;
using ProjectShockGL;

namespace ProjectShockGLEditor;

public static class EntityAssetEditor
{
    private static EntityAssetDocument? _document;
    private static string? _error;
    private static Action? _pending;
    private static bool _askUnsaved;
    private static AssetReferenceRepairReport? _repairReport;
    private static bool _showRepairReport;
    public static bool Open { get; set; }
    private static EntityAssetDocument Document => _document ??= new EntityAssetDocument(Path.Combine(Content.Path, "Entities"));

    public static void Toggle()
    {
        if (Open) Request(() => Open = false);
        else Open = true;
    }

    public static bool AllowClose()
    {
        if (_document?.IsDirty != true) return true;
        Open = true;
        Request(Application.Quit);
        return false;
    }

    private static void Request(Action action)
    {
        if (Document.IsDirty)
        {
            _pending = action;
            _askUnsaved = true;
        }
        else Execute(action);
    }

    private static void Execute(Action action)
    {
        try { action(); _error = null; }
        catch (Exception error) { _error = error.Message; }
    }

    private static void Load()
    {
        Directory.CreateDirectory(Document.DirectoryPath);
        using var dialog = new NativeFileDialog().AddFilter("Entity JSON", "json");
        if (dialog.Open(out string? path, Document.DirectoryPath, "") == DialogResult.Okay && !string.IsNullOrWhiteSpace(path))
            Document.Open(path);
    }

    private static bool Save()
    {
        try
        {
            var path = Document.FilePath;
            if (path == null)
            {
                Directory.CreateDirectory(Document.DirectoryPath);
                using var dialog = new NativeFileDialog().SaveFile().AddFilter("Entity JSON", "json");
                if (dialog.Open(out path, Document.DirectoryPath, "Entity.json") != DialogResult.Okay || string.IsNullOrWhiteSpace(path))
                    return false;
                if (!Path.HasExtension(path)) path += ".json";
            }
            Document.Save(path);
            _error = null;
            return true;
        }
        catch (Exception error) { _error = error.Message; return false; }
    }

    public static void Draw()
    {
        if (!Open) return;
        ImGui.SetNextWindowSize(new Vector2(720, 500), ImGuiCond.FirstUseEver);
        var open = true;
        var visible = ImGui.Begin("Entity Asset", ref open);
        if (!open) Request(() => Open = false);
        if (visible)
        {
            if (ImGui.Button("New")) Request(Document.New);
            ImGui.SameLine();
            if (ImGui.Button("Open")) Request(Load);
            ImGui.SameLine();
            if (ImGui.Button("Save")) Save();
            ImGui.SameLine();
            if (ImGui.Button("Fix Asset References")) Request(FixReferences);
            ImGui.SameLine();
            ImGui.TextUnformatted(Document.IsDirty ? "Modified" : Document.FilePath == null ? "New" : "Saved");
            ImGui.TextWrapped(Document.FilePath ?? "New entity asset");
            ImGui.TextUnformatted(Document.AssetId is { } id ? "AssetId: " + id : "AssetId: assigned on first save");
            if (Document.AssetId is { } assetId)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("Copy ID")) ImGui.SetClipboardText(assetId.ToString());
            }
            ImGui.TextUnformatted("Class");
            var type = Document.Class;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint("##class", "Namespace.Class, Assembly", ref type, 2048)) Document.Class = type;
            ImGui.Separator();
            ImGui.TextUnformatted("Parameters (key / type / value)");
            var remove = -1;
            for (var index = 0; index < Document.Parameters.Count; index++)
            {
                var row = Document.Parameters[index];
                ImGui.PushID(index);
                ImGui.SetNextItemWidth(170);
                ImGui.InputTextWithHint("##key", "Key", ref row.Key, (uint)Math.Max(256, row.Key.Length + 128));
                ImGui.SameLine();
                var kind = (int)row.Kind;
                ImGui.SetNextItemWidth(100);
                if (ImGui.Combo("##type", ref kind, new[] { "String", "Number", "Boolean", "JSON" }, 4))
                {
                    row.Kind = (EntityParameterKind)kind;
                    row.Value = row.Kind switch
                    {
                        EntityParameterKind.String => "",
                        EntityParameterKind.Number => "0",
                        EntityParameterKind.Boolean => "false",
                        _ => "{}"
                    };
                }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(MathF.Max(80, ImGui.GetContentRegionAvail().X - 35));
                if (row.Kind == EntityParameterKind.Boolean)
                {
                    var value = bool.TryParse(row.Value, out var boolean) && boolean;
                    if (ImGui.Checkbox("##value", ref value)) row.Value = value ? "true" : "false";
                }
                else ImGui.InputText("##value", ref row.Value, (uint)Math.Max(4096, row.Value.Length + 1024));
                ImGui.SameLine();
                if (ImGui.SmallButton("X")) remove = index;
                ImGui.PopID();
            }
            if (remove >= 0) Document.Parameters.RemoveAt(remove);
            if (ImGui.Button("Add parameter")) Document.Parameters.Add(new EntityAssetParameter());
            if (_error != null) ImGui.TextWrapped(_error);
        }
        if (_askUnsaved)
        {
            ImGui.OpenPopup("Unsaved entity asset");
            _askUnsaved = false;
        }
        if (ImGui.BeginPopupModal("Unsaved entity asset", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Save changes to this entity asset?");
            if (ImGui.Button("Save") && Save()) Complete();
            ImGui.SameLine();
            if (ImGui.Button("Discard"))
            {
                Document.New();
                Complete();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                _pending = null;
                ImGui.CloseCurrentPopup();
            }
            if (_error != null) ImGui.TextWrapped(_error);
            ImGui.EndPopup();
        }
        DrawRepairReport();
        ImGui.End();
    }

    private static void FixReferences()
    {
        _repairReport = AssetReferenceRepair.Run(Content.Path);
        _showRepairReport = true;
        if (_repairReport.Succeeded && Document.FilePath is { } path)
        {
            Document.Open(path);
        }
    }

    private static void DrawRepairReport()
    {
        if (_showRepairReport)
        {
            ImGui.OpenPopup("Asset References Report");
            _showRepairReport = false;
        }

        ImGui.SetNextWindowSize(new Vector2(800, 520), ImGuiCond.Appearing);
        if (!ImGui.BeginPopupModal("Asset References Report")) return;

        var report = _repairReport!;
        ImGui.TextWrapped(report.Succeeded
            ? $"Completed. Changes: {report.Changes.Count}. Unchanged files: {report.Unchanged.Count}."
            : "Repair failed. Validation errors prevent all writes; write failures trigger rollback.");
        if (report.BackupDirectory != null)
        {
            ImGui.TextWrapped("Backups: " + report.BackupDirectory);
            if (ImGui.SmallButton("Copy backup path")) ImGui.SetClipboardText(report.BackupDirectory);
        }

        ImGui.BeginChild("Repair details", new Vector2(0, -40));
        foreach (var error in report.Errors) ImGui.TextWrapped("ERROR: " + error);
        foreach (var change in report.Changes) ImGui.TextWrapped(change);
        foreach (var unchanged in report.Unchanged) ImGui.TextWrapped("Unchanged: " + unchanged);
        ImGui.EndChild();
        if (ImGui.Button("Close")) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private static void Complete()
    {
        var action = _pending;
        _pending = null;
        ImGui.CloseCurrentPopup();
        if (action != null) Execute(action);
    }
}
