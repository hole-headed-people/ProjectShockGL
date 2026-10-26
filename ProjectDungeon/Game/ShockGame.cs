using System.Numerics;
using ProjectDungeon.Core;
using ProjectShockGL;
using Silk.NET.Input;
using ProjectShockGL.Core;

namespace ProjectDungeon.Game;

public class ShockGame : ProjectShockGL.Core.Game
{
    protected override void Load()
    {
        base.Load();
        
        LevelManager.Loaded += OnLevelLoaded;
        
        try
        {
            LevelManager.Load("PolygonLevel");
        }
        catch
        {
            LevelManager.Loaded -= OnLevelLoaded;
            
            throw;
        }

        Input.SetCursorMode(CursorMode.Raw);
    }

    protected override void Unload()
    {
        LevelManager.Loaded -= OnLevelLoaded;
        
        base.Unload();
    }

    private void OnLevelLoaded(Level level)
    {
        var playerStart = World.Find<PlayerStart>();

        var playerStartPosition = new Vector3(0f, 0f, 0f);
        var playerStartRotation = Quaternion.Identity;
        
        if (playerStart != null)
        {
            playerStartPosition = playerStart.Transform.Position;
            playerStartRotation = playerStart.Transform.Rotation;
        }
        
        var playerCharacter = World.Spawn<PlayerCharacter>(playerStartPosition, playerStartRotation);
        var player = World.Spawn<Player>();
        
        player.SetCharacter(playerCharacter);
    }
}
