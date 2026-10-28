namespace ProjectShockGL.Core;

public sealed class Timer : IDisposable
{
    public event Action? Finished;
    public event Action<float>? ValueChanged;
    
    private bool _isRunning;
    private bool _isDisposed;
    
    private int _revision;
    
    public bool UseUnscaledTime { get; set; }
    public bool IsPaused { get; private set; }
    public float RemainingSeconds { get; private set; }
    
    public Timer(bool useUnscaledTime = false)
    {
        UseUnscaledTime = useUnscaledTime;
    }

    public void Run(float seconds)
    {
        ThrowIfDisposed();
        
        Validate(seconds);
        Detach();
        
        _revision++;
        
        IsPaused = false;
        
        _isRunning = true;
        
        TimerManager.Tick += OnTick;
        SetRemainingSeconds(seconds);
    }

    private void OnTick(float scaled, float unscaled)
    {
        if (!_isRunning || IsPaused || _isDisposed)
        {
            return;
        }
        
        var delta = UseUnscaledTime ? unscaled : scaled;

        if (delta > 0f)
        {
            SetRemainingSeconds(RemainingSeconds - delta);
        }
    }

    public void SetRemainingSeconds(float seconds)
    {
        ThrowIfDisposed();
        Validate(seconds);

        if (seconds <= 0f && _isRunning)
        {
            Complete(); 
            return;
        }
        
        RemainingSeconds = Math.Max(0f, seconds);
        ValueChanged?.Invoke(RemainingSeconds);
    }

    public void Stop()
    {
        ThrowIfDisposed();
        
        _revision++;
        
        Detach();
        IsPaused = false;
    }

    public void Complete()
    {
        ThrowIfDisposed();

        if (!_isRunning)
        {
            return;
        }
        
        var revision = ++_revision;
        
        Detach();
        
        IsPaused = false;
        RemainingSeconds = 0f;
        
        ValueChanged?.Invoke(0f);

        if (!_isDisposed && revision == _revision)
        {
            Finished?.Invoke();
        }
    }

    public void SetPaused(bool isPaused)
    {
        ThrowIfDisposed();
        
        IsPaused = isPaused;
    }
    
    public bool IsRunning()
    {
        return _isRunning;
    }

    private void Detach()
    {
        TimerManager.Tick -= OnTick; 
        
        _isRunning = false;
    }
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        _revision++;
        
        Detach();
        
        IsPaused = false;
        
        Finished = null;
        ValueChanged = null;
    }
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private static void Validate(float seconds)
    {
        if (!float.IsFinite(seconds))
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }
    }
}
