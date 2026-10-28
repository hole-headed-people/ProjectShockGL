namespace ProjectDungeon.Game;

public class ShockGame : Core.Game
{
    protected override void Load()
    {
        base.Load();
        
        ProjectShockGL.Rendering.PostProcessSettings.Apply("Default");
    }
}
