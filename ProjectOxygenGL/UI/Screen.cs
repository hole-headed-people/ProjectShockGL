using ProjectShockGL.UI;

namespace ProjectOxygenGL.UI;

public class Screen : Widget
{
    protected override void OnCreate()
    {
        base.OnCreate();
        
        Rect.Size = Canvas.Size;
    }
    
    protected override void OnUpdate(float deltaSeconds)
    {
        base.OnUpdate(deltaSeconds);
        
        Rect.Size = Canvas.Size;
    }
}