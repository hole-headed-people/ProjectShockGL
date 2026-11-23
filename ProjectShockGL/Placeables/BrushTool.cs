using ProjectShockGL.Core;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Placeables;

public class BrushTool : Entity
{
    [Editable] [ObjectName] public string TargetName { get; set; } = "";

    [Input("SetColor")]
    public void SetColor(float r, float g, float b, float a)
    {
        if (string.IsNullOrWhiteSpace(TargetName))
        {
            ProjectShockGL.Log.PrintWarning("TargetName is empty");
            
            return;
        }

        if (World.Find(TargetName) is not BrushBox target)
        {
            ProjectShockGL.Log.PrintWarning("TargetName is invalid");
            
            return;
        }
        
        r = Math.Clamp(r, 0f, 1f);
        g = Math.Clamp(g, 0f, 1f);
        b = Math.Clamp(b, 0f, 1f);
        a = Math.Clamp(a, 0f, 1f);
        
        target.Color = new Color(r, g, b, a);
    }

    [Input("SetTexture")]
    public void SetTexture(string name)
    {
        if (string.IsNullOrWhiteSpace(TargetName))
        {
            ProjectShockGL.Log.PrintWarning("TargetName is empty");
            
            return;
        }

        if (World.Find(TargetName) is not BrushBox target)
        {
            ProjectShockGL.Log.PrintWarning("TargetName is invalid");
            
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            ProjectShockGL.Log.PrintWarning("Texture Name is empty");
        }

        target.TextureName = name;
    }
}