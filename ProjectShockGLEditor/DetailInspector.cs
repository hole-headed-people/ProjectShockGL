using System.Numerics;
using ImGuiNET;
using ProjectShockGL;
using ProjectShockGL.Tools;

namespace ProjectShockGLEditor;

internal static class DetailInspector
{
    private static string _search = "";

    internal static void Draw(EditorSelectionTarget target)
    {
        ImGui.PushID(target.Id.ToString());
        ImGui.TextUnformatted("Transform");
        var state = TransformState.Capture(target.Transform);
        var position = state.Position;
        if (ImGui.DragFloat3("Position", ref position, .1f))
            Editor.Execute(() => SceneDetails.SetTransform(target, state with { Position = position }, "Detail/Position", ImGui.IsItemActive()));
        Finish(target);
        state = TransformState.Capture(target.Transform);
        var rotation = QuaternionHelper.ToEulerDegrees(state.Rotation);
        if (ImGui.DragFloat3("Rotation", ref rotation, .1f))
            Editor.Execute(() => SceneDetails.SetTransform(target, state with { Rotation = QuaternionHelper.FromEulerDegrees(rotation) },
                "Detail/Rotation", ImGui.IsItemActive()));
        Finish(target);
        state = TransformState.Capture(target.Transform);
        var scale = state.Scale;
        if (ImGui.DragFloat3("Scale", ref scale, .1f))
        {
            for (var i = 0; i < 3; i++)
            {
                if (!float.IsFinite(scale[i]) || MathF.Abs(scale[i]) < .01f)
                    scale[i] = MathF.CopySign(.01f, state.Scale[i]);
            }
            Editor.Execute(() => SceneDetails.SetTransform(target, state with { Scale = scale }, "Detail/Scale", ImGui.IsItemActive()));
        }
        Finish(target);
        ImGui.Separator();
        ImGui.TextUnformatted("Properties");
        var detail = target.Detail!;
        if (ImGui.BeginCombo("Asset", detail.AssetPath))
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##detailSearch", "Search details...", ref _search, 256);
            AssetFolders.Draw(Content.GetAllDetailNames(), _search, path => path == detail.AssetPath,
                path => Editor.Execute(() => SceneDetails.SetAsset(target, path)));
            ImGui.EndCombo();
        }
        ImGui.TextWrapped("Static decoration. Appearance is configured in the Detail asset.");
        ImGui.Separator();
        if (ImGui.Button("Focus (F)")) SceneCamera.Focus(target.GetWorldBounds());
        ImGui.SameLine();
        ImGui.BeginDisabled(!Editor.CanDuplicateSelection || ObjectManipulator.IsDragging);
        var duplicate = ImGui.Button("Duplicate");
        ImGui.EndDisabled();
        if (duplicate)
        {
            Editor.DuplicateSelection();
            ImGui.PopID();
            return;
        }
        ImGui.SameLine();
        if (ImGui.Button("Delete"))
        {
            Editor.DeleteSelection();
        }
        ImGui.PopID();
    }

    private static void Finish(EditorSelectionTarget target)
    {
        if (ImGui.IsItemDeactivated()) UndoRedo.CommitPending(target);
    }
}