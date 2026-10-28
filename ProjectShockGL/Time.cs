namespace ProjectShockGL;

public static class Time
{
    public static float DeltaSeconds { get; set; }
    public static float UnscaledDeltaSeconds { get; set; }
    
    public static double ElapsedTime { get; set; }
    public static double UnscaledElapsedTime { get; set; }

    private static float _timeScale = 1f;
    
    public static float TimeScale
    {
        get => _timeScale;
        set
        {
            if (!float.IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            
            _timeScale = value;
        }
    }
}