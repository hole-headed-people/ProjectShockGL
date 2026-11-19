using System.Numerics;
using ImGuiNET;
using ProjectShockGL.Rendering;

namespace ProjectShockGLEditor;

internal static class SelectionBounds
{
    private static readonly (int A, int B)[] Edges =
    [
        (0, 1), (1, 3), (3, 2), (2, 0),
        (4, 5), (5, 7), (7, 6), (6, 4),
        (0, 4), (1, 5), (2, 6), (3, 7)
    ];

    private static readonly Vector4[] ClipPlanes =
    [
        new(1, 0, 0, 1), new(-1, 0, 0, 1),
        new(0, 1, 0, 1), new(0, -1, 0, 1),
        new(0, 0, 1, 0), new(0, 0, -1, 1)
    ];

    internal static void Draw(Vector2 screenSize, Vector2 sceneMin, Vector2 sceneMax)
    {
        if (UIEditor.Open || sceneMax.X <= sceneMin.X || sceneMax.Y <= sceneMin.Y) return;

        var camera = Camera.Active;
        var viewProjection = camera.GetViewMatrix() * camera.GetProjectionMatrix();
        var draw = ImGui.GetBackgroundDrawList();
        draw.PushClipRect(sceneMin, sceneMax, true);

        foreach (var selected in Selection.Targets.Where(target => target.IsEditorVisible))
        {
            var bounds = selected.GetLocalBounds();
            foreach (var edge in Project(bounds.min, bounds.max, selected.Transform.GetModelMatrix(), viewProjection, screenSize))
            {
                draw.AddLine(edge.A, edge.B, 0xFFFFFFFF, 1.5f);
            }
        }

        draw.PopClipRect();
    }

    internal static (Vector2 A, Vector2 B)[] Project(Vector3 min, Vector3 max, Matrix4x4 model,
        Matrix4x4 viewProjection, Vector2 screenSize)
    {
        if (screenSize.X <= 0 || screenSize.Y <= 0) return [];
        var matrix = model * viewProjection;
        var corners = new Vector4[8];
        for (var i = 0; i < corners.Length; i++)
        {
            var point = new Vector3((i & 1) == 0 ? min.X : max.X,
                (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
            corners[i] = Vector4.Transform(new Vector4(point, 1), matrix);
        }

        var result = new List<(Vector2 A, Vector2 B)>();
        foreach (var (a, b) in Edges)
        {
            if (!Clip(corners[a], corners[b], out var start, out var end)) continue;
            Vector2 Screen(Vector4 point) => new((point.X / point.W + 1) * 0.5f * screenSize.X,
                (1 - point.Y / point.W) * 0.5f * screenSize.Y);
            var from = Screen(start);
            var to = Screen(end);
            if (Vector2.DistanceSquared(from, to) > 0.0001f) result.Add((from, to));
        }
        return result.ToArray();
    }

    private static bool Clip(Vector4 a, Vector4 b, out Vector4 start, out Vector4 end)
    {
        start = end = default;
        if (!Finite(a) || !Finite(b)) return false;
        var first = 0f;
        var last = 1f;
        foreach (var plane in ClipPlanes)
        {
            var da = Vector4.Dot(a, plane);
            var db = Vector4.Dot(b, plane);
            if (da < 0 && db < 0) return false;
            if (da >= 0 && db >= 0) continue;
            var t = da / (da - db);
            if (da < 0) first = MathF.Max(first, t);
            else last = MathF.Min(last, t);
            if (first > last) return false;
        }
        start = Vector4.Lerp(a, b, first);
        end = Vector4.Lerp(a, b, last);
        return start.W > 0.000001f && end.W > 0.000001f;
    }

    private static bool Finite(Vector4 point) =>
        float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z) && float.IsFinite(point.W);
}
