namespace ProjectShockGL;

public sealed class TimerManager
{
    internal static event Action<float, float>? Tick;
    
    private static readonly TimerManager Instance = new();
    
    private TimerManager() { }
    
    public static TimerManager Create()
    {
        return Instance;
    }

    public void Update(float deltaSeconds, float unscaledDeltaSeconds)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        if (!float.IsFinite(unscaledDeltaSeconds) || unscaledDeltaSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));
        }
        
        List<Exception>? errors = null;
        
        foreach (var callback in Tick?.GetInvocationList() ?? [])
        {
            try
            {
                ((Action<float, float>)callback)(deltaSeconds, unscaledDeltaSeconds);
            }
            catch (Exception error)
            {
                (errors ??= []).Add(error);
            }
        }

        if (errors != null)
        {
            throw new AggregateException("Timer update failed.", errors);
        }
    }
}
