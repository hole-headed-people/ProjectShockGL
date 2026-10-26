using System.Numerics;
using ProjectShockGL;
using ProjectShockGL.Rendering;
using Plane = ProjectShockGL.Rendering.Plane;

namespace ProjectShockGLEditor;

public static class SceneGrid
{
    private const float GridHalfSize = 100f;
    
    public static bool Show { get; set; } = true;
    public static float Step { get; set; } = 1f;

    public static void Render(Graphics graphics)
    {
        if (!Show)
        {
            return;
        }

        var color     = new Color(0.25f, 0.25f, 0.25f);
        var axisColor = new Color(0.55f, 0.55f, 0.55f);
        
        const float thickness = 0.04f;
        
        var displayStep = MathF.Max(Step, 0.5f);

        for (var x = -GridHalfSize; x <= GridHalfSize + 0.0001f; x += displayStep)
        {
            var c = MathF.Abs(x) < 0.001f ? axisColor : color;
            graphics.DrawLine2D(new Vector3(x, 0, -GridHalfSize), new Vector3(x, 0,  GridHalfSize),
                Plane.Xz, Content.GetWhitePixel(), c, thickness);
        }

        for (var z = -GridHalfSize; z <= GridHalfSize + 0.0001f; z += displayStep)
        {
            var c = MathF.Abs(z) < 0.001f ? axisColor : color;
            
            graphics.DrawLine2D(new Vector3(-GridHalfSize, 0, z), new Vector3( GridHalfSize, 0, z),
                Plane.Xz, Content.GetWhitePixel(), c, thickness);
        }
    }
}
