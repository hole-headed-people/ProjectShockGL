using ProjectShockGL.Rendering;
using ProjectShockGL.UI;

namespace ProjectShockGL.Core;

public class Game
{
    private RenderQueue _renderQueue = null!;
    private TimerManager _timerManager = null!;
    
    public World World { get; private set; } = null!;
    public UIManager UIManager { get; private set; } = null!;
    
    private static bool _isInitialized;
    public static Game Instance { get; private set; } = null!;
    
    public static void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        var name = Settings.GetString("Game", "Class", "ProjectShockGL.Core.Game, ProjectShockGL");

        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        var type = Type.GetType(name);
        
        if (type == null)
        {
            return;
        }

        if (Activator.CreateInstance(type) is not Game game)
        {
            return;
        }
        
        Instance = game;
        
        Application.Load += Instance.Load;
        Application.Update += Instance.Update;
        Application.Render += Instance.Render;
        Application.RenderOverlay += Instance.RenderDebug;
        Application.RenderUI += Instance.RenderUI;
        Application.Resized += Instance.OnResized;
        Application.Unload += Instance.Unload;
        
        _isInitialized = true;
    }

    protected virtual void Load()
    {
        _renderQueue = new RenderQueue();        
        _timerManager = TimerManager.Create();
        
        World = new World();
        
        Layers.Load();
        Physics.Initialize();

        PostProcessSettings.Apply(Settings.GetString("Game", "PostProcessingProfile", "Default"));
        
        UIManager = new UIManager();
        UIManager.Initialize();
    }

    protected virtual void Update(float deltaSeconds)
    {
        var gameDeltaSeconds = deltaSeconds * Time.DeltaSeconds;
        
        World.Update(gameDeltaSeconds, !Application.IsEditor, deltaSeconds);
        
        if (!Application.IsEditor)
        {
            _timerManager.Update(gameDeltaSeconds, deltaSeconds);

            if (gameDeltaSeconds > 0f)
            {
                Physics.Update();
            }
            
            UIManager.Update(deltaSeconds);
        }
    }
    
    protected virtual void Render(Graphics graphics)
    {
        World.Render(_renderQueue, false);
        _renderQueue.Flush(graphics);
    }

    private void RenderDebug(Graphics graphics)
    {
        if (!Application.IsDebug)
        {
            return;
        }
        
        World.Render(_renderQueue, true);
        _renderQueue.Flush(graphics);
    }
    
    protected virtual void Unload()
    {
        Cleanup.Run(() =>
        {
            LevelManager.Unload(World);
        }, UIManager.Destroy);
    }

    private void RenderUI(Graphics graphics)
    {
        UIManager.Draw(graphics);
    }

    private void OnResized(Silk.NET.Maths.Vector2D<int> size)
    {
        if (!UIManager.Canvas.IsDestroyed)
        {
            UIManager.Resize(new System.Numerics.Vector2(size.X, size.Y));
        }
    }
}
