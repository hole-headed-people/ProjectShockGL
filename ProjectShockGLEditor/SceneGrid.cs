using ProjectShockGL.Rendering;

namespace ProjectShockGLEditor;

public static class SceneGrid
{
    public static bool Show { get; set; } = true;
    public static float Step { get; set; } = 1f;

    public static void Render(Graphics graphics)
    {
        if (!Show)
        {
            return;
        }
        
        var step = float.IsFinite(Step) ? MathF.Max(Step, .01f) : 1;
        
        graphics.DrawGrid(step, radius: 500, fadeStart: 50, fadeEnd: 500, plane: SceneCamera.GridPlane, planarFade: SceneCamera.ViewMode != SceneViewMode.Perspective);
    }
}
