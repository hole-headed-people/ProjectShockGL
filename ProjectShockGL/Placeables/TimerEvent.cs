using ProjectShockGL.Core;
using Timer = ProjectShockGL.Core.Timer;

namespace ProjectShockGL.Placeables;

public class TimerEvent : Entity
{
    [Editable] public bool UseUnscaledTime { get; set; }
    
    private Timer _timer = null!;
    
    public override void Create()
    {
        base.Create();
        
        _timer = new Timer();
        _timer.Finished += OnFinished;
        _timer.ValueChanged += OnValueChanged;
    }

    public override void Destroy()
    {
        base.Destroy();
        
        _timer.Finished -= OnFinished;
        _timer.ValueChanged -= OnValueChanged;
        _timer.Dispose();
    }

    private void OnFinished()
    {
        InvokeEvent("OnFinished");
    }

    private void OnValueChanged(float value)
    {
        InvokeEvent("OnValueChanged", value);
    }

    [Input("Run")]
    public void Run(float seconds)
    {
        _timer.UseUnscaledTime = UseUnscaledTime;
        _timer.Run(seconds);
    }

    [Input("SetRemaining")]
    public void SetRemainingSeconds(float seconds)
    {
        _timer.SetRemainingSeconds(seconds);
    }

    [Input("SetPaused")]
    public void SetPaused(bool isPaused)
    {
        _timer.SetPaused(isPaused);
    }

    [Input("Stop")]
    public void Stop()
    {
        _timer.Stop();
    }

    [Input("Complete")]
    public void Complete()
    {
        _timer.Complete();
    }
}
