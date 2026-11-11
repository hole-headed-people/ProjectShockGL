using System.Numerics;
using ProjectOxygenGL.Placeables;
using ProjectShockGL;
using ProjectShockGL.Core;

namespace ProjectOxygenGL.Core;

public class Game : ProjectShockGL.Core.Game
{
    public event Action? BeginPlay;
    public event Action? EndPlay;
    
    private Player? _player;
    private Character? _playerCharacter;
    private GameState? _loadingState;
    
    protected override void Load()
    {
        base.Load();
        
        LevelManager.Loaded += OnLevelLoaded;
        LevelManager.Unloading += OnLevelUnloading;
        SaveManager.Saving += OnSaving;
        SaveManager.Loaded += OnSaveLoaded;
        
        try
        {
            LevelManager.Load(Settings.GetString("Game", "StartupLevel", ""));
        }
        catch
        {
            LevelManager.Loaded -= OnLevelLoaded;
            LevelManager.Unloading -= OnLevelUnloading;
            SaveManager.Saving -= OnSaving;
            SaveManager.Loaded -= OnSaveLoaded;
            
            throw;
        }
    }

    protected override void Unload()
    {
        LevelManager.Loaded -= OnLevelLoaded;
        SaveManager.Saving -= OnSaving;
        SaveManager.Loaded -= OnSaveLoaded;

        try
        {
            base.Unload();
        }
        finally
        {
            LevelManager.Unloading -= OnLevelUnloading;
        }
    }

    private void OnLevelUnloading(Level level)
    {
        if (_loadingState == null && LevelManager.ActiveLevelName != null &&
            level.Placeables.All(placement => placement.Guid.GetValueOrDefault() != Guid.Empty))
        {
            SaveHelper.CaptureLevel();
        }

        EndPlay?.Invoke();
        
        _player = null;
        _playerCharacter = null;
    }

    private void OnLevelLoaded(Level level)
    {
        if (LevelManager.ActiveLevelName != null)
        {
            SaveHelper.RestoreLevel();
        }

        CreatePlayer();
        SpawnPlayerCharacter();

        var state = _loadingState;

        if (state != null && _playerCharacter != null)
        {
            _playerCharacter.Transform.Position = state.PlayerPosition;
            _playerCharacter.Transform.Rotation = state.PlayerRotation;
        }

        if (_player != null && _playerCharacter != null)
        {
            _player.SetCharacter(_playerCharacter);
            
            _player.SetInputEnabled(true);
            _player.SetInputMode(InputMode.Game);
        }

        if (state != null)
        {
            OnRestoreState(state);
        }
        
        BeginPlay?.Invoke();
        BeginPlayEvent.InvokeAll();
    }

    private void OnSaving()
    {
        if (LevelManager.ActiveLevelName is { } level)
        {
            SaveHelper.CaptureLevel();

            var state = (GameState)Activator.CreateInstance(GetGameStateType())!;
            state.Level = level;

            if (_playerCharacter is { IsDestroyed: false } character)
            {
                state.PlayerPosition = character.Transform.Position;
                state.PlayerRotation = character.Transform.Rotation;
            }

            OnCaptureState(state);
            state.Validate();
            
            SaveManager.GetData().SetGameState(state);
        }
    }

    private void OnSaveLoaded(LoadResult result)
    {
        if (result != LoadResult.Success || _loadingState != null ||
            SaveManager.GetData().GetGameState(GetGameStateType()) is not GameState state)
        {
            return;
        }

        state.Validate();
        _loadingState = state;

        try
        {
            LevelManager.Load(state.Level);
        }
        finally
        {
            _loadingState = null;
        }
    }

    protected virtual void OnCaptureState(GameState state){}
    protected virtual void OnRestoreState(GameState state){}

    private static Type GetGameStateType()
    {
        var name = Settings.GetString("Game", "GameStateClass", typeof(GameState).AssemblyQualifiedName!);
        var type = Type.GetType(name);

        if (type == null || !typeof(GameState).IsAssignableFrom(type) ||
            type.IsAbstract || type.ContainsGenericParameters || type.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new InvalidOperationException($"GameStateClass '{name}' must be a concrete OxygenGL GameState with a public parameterless constructor.");
        }

        return type;
    }

    private void CreatePlayer()
    {
        var template = Settings.GetString("Game", "PlayerClass", "");
        var type = string.IsNullOrWhiteSpace(template) ? null : Type.GetType(template);

        if (type == null)
        {
            return;
        }

        _player = World.Spawn<Player>(type);
    }

    private void SpawnPlayerCharacter()
    {
        var template =
            Settings.GetString("Game", "PlayerCharacterClass", "");
        var type = string.IsNullOrWhiteSpace(template) ? null : Type.GetType(template);

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
