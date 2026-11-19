using System.Numerics;
using ImGuiNET;

namespace ProjectShockGLEditor;

internal static class SceneAxes
{
    internal static (string Name, Vector3 Direction, uint Color)[] GetAxes(Quaternion cameraRotation)
    {
        var inverse = Quaternion.Inverse(Quaternion.Normalize(cameraRotation));
        
        return new[]
        {
            ("X", Vector3.Transform(Vector3.UnitX, inverse), 0xFF5555EFu),
            ("Y", Vector3.Transform(Vector3.UnitY, inverse), 0xFF65CF65u),
            ("Z", Vector3.Transform(Vector3.UnitZ, inverse), 0xFFFFAA55u)
        }.OrderBy(axis => axis.Item2.Z).ToArray();
    }

    internal static void Draw(Vector2 sceneMin, Vector2 sceneMax, Quaternion cameraRotation)
    {
        if (UIEditor.Open || sceneMax.X - sceneMin.X < 120 || sceneMax.Y - sceneMin.Y < 120)
        {
            return;
        }

        var draw = ImGui.GetBackgroundDrawList();
        var center = new Vector2(sceneMax.X - 60, sceneMin.Y + 60);
        
        draw.PushClipRect(sceneMin, sceneMax, true);
        draw.AddCircleFilled(center, 48, 0xAA202020, 32);
        draw.AddCircleFilled(center, 2, 0xFFE0E0E0);

        foreach (var axis in GetAxes(cameraRotation))
        {
            var direction = new Vector2(axis.Direction.X, -axis.Direction.Y);
            var tip = center + direction * 34;
            var color = axis.Color;
            
            if (axis.Direction.Z < -.01f)
            {
                var shade = ImGui.ColorConvertU32ToFloat4(color);
                
                shade.X *= .55f; shade.Y *= .55f; shade.Z *= .55f;
                color = ImGui.ColorConvertFloat4ToU32(shade);
            }
            
            draw.AddLine(center, tip, color, 2);
            
            if (direction.LengthSquared() > .04f)
            {
                var unit = Vector2.Normalize(direction);
                var side = new Vector2(-unit.Y, unit.X);
                
                draw.AddTriangleFilled(tip, tip - unit * 6 + side * 3, tip - unit * 6 - side * 3, color);
            }
            
            var label = tip + (direction.LengthSquared() > .01f ? Vector2.Normalize(direction) * 9 : Vector2.Zero);
            
            draw.AddText(label - ImGui.CalcTextSize(axis.Name) * .5f, color, axis.Name);
        }

        draw.PopClipRect();
    }
}
