using System.Numerics;
using ProjectDungeon.Game;
using ProjectDungeon.Placeables;
using ProjectShockGL;
using ProjectShockGL.Core;

namespace ProjectDungeon.Core;

public class Game : ProjectShockGL.Core.Game
{
    private Player? _player;
    private Character? _playerCharacter;
    
    protected override void Load()
    {
        base.Load();
        
        LevelManager.Loaded += OnLevelLoaded;
        
        try
        {
            LevelManager.Load(Settings.GetString("Game", "StartupLevel", ""));
        }
        catch
        {
            LevelManager.Loaded -= OnLevelLoaded;
            
            throw;
        }
    }

    protected override void Unload()
    {
        LevelManager.Loaded -= OnLevelLoaded;
        
        base.Unload();
    }

    private void OnLevelLoaded(Level level)
    {
        CreatePlayer();
        SpawnPlayerCharacter();

        if (_player != null && _playerCharacter != null)
        {
            _player.SetCharacter(_playerCharacter);
            
            _player.SetInputEnabled(true);
            _player.SetInputMode(InputMode.Game);
        }
        
        BeginPlayEvent.InvokeAll();
    }

    private void CreatePlayer()
    {
        var template = Settings.GetString("Game", "PlayerClass", "ProjectDungeon.Core.Player,ProjectDungeon");
        var type = Type.GetType(template);

        if (type == null)
        {
            return;
        }

        _player = World.Spawn<Player>(type);
    }

    private void SpawnPlayerCharacter()
    {
        var template =
            Settings.GetString("Game", "PlayerCharacterClass", "ProjectDungeon.Core.Character,ProjectDungeon");
        var type = Type.GetType(template);

        if (type == null)
        {
            return;
        }

        var playerStart = World.Find<PlayerStart>();

        var playerStartPosition = new Vector3(0f, 0f, 0f);
        var playerStartRotation = Quaternion.Identity;

        if (playerStart != null)
        {
            playerStartPosition = playerStart.Transform.Position;
            playerStartRotation = playerStart.Transform.Rotation;
        }

        _playerCharacter = World.Spawn<Character>(type);

        if (_playerCharacter != null)
        {
            _playerCharacter.Transform.Position = playerStartPosition;
            _playerCharacter.Transform.Rotation = playerStartRotation;
        }
    }

    public Player? GetPlayer()
    {
        return _player;
    }

    public Character? GetPlayerCharacter()
    {
        return _playerCharacter;
    }
}