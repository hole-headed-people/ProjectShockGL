namespace ProjectDungeon.Game;

public class ShockGame : ProjectOxygenGL.Core.Game
{
    protected override void Load()
    {
        base.Load();
        
        ProjectShockGL.Rendering.PostProcessSettings.Apply("Default");
    }
}
