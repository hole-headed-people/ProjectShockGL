using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGLEditor;

internal static class SelectionBounds
{
    internal static void Draw(Graphics graphics, Vector2 screenSize, Vector2 sceneMin, Vector2 sceneMax)
    {
        if (UIEditor.Open || sceneMax.X <= sceneMin.X || sceneMax.Y <= sceneMin.Y)
        {
            return;
        }

        foreach (var selected in Selection.Targets.Where(target => target.IsEditorVisible))
        {
            var bounds = selected.GetLocalBounds();
            graphics.DrawSelectionBounds(bounds.min, bounds.max, selected.Transform.GetModelMatrix(),
                screenSize, sceneMin, sceneMax);
        }
    }
}
