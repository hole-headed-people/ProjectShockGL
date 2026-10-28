namespace ProjectShockGLEditor;

public class Game : ProjectShockGL.Core.Game
{
    protected override void Load()
    {
        base.Load();
        ProjectShockGL.Rendering.PostProcessSettings.Apply("Default");
    }
}
