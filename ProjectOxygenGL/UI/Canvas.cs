using ProjectShockGL;
using ProjectShockGL.Core;

namespace ProjectOxygenGL.UI;

public class Canvas : ProjectShockGL.UI.Canvas
{    
    public HUD? HUD { get; private set; }
    
    protected override void OnCreate()
    {
        base.OnCreate();

        if (Game.Instance is Core.Game game)
        {
            game.BeginPlay += OnBeginPlay;
            game.EndPlay += OnEndPlay;
        }
    }

    protected override void OnDestroy()
    {
        HUD = null;
        base.OnDestroy();
        
        if (Game.Instance is Core.Game game)
        {
            game.BeginPlay -= OnBeginPlay;
            game.EndPlay -= OnEndPlay;
        }
    }

    protected virtual void OnBeginPlay()
    {
        var hudType = Type.GetType(Settings.GetString("Game", "UIHUDClass", "ProjectOxygenGL.UI.HUD, ProjectOxygenGL"));

        if (hudType != null)
        {
            HUD = Add<HUD>(hudType);
        }
    }

    protected virtual void OnEndPlay()
    {
        var hud = HUD;
        HUD = null;
        hud?.Destroy();
    }
}
