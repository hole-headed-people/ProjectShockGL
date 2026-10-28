using ProjectShockGL;
using Timer = ProjectShockGL.Core.Timer;
using System.Reflection;

var manager = TimerManager.Create();
var checks = 0;
void Check(bool condition) { if (!condition) throw new Exception($"Check {checks + 1} failed"); checks++; }
using (var timer = new Timer())
{
    var finished = 0;
    var last = -1f;
    timer.Finished += () => finished++;
    timer.ValueChanged += value => last = value;
    timer.Run(2); timer.Run(2);
    manager.Update(.5f, .5f);
    Check(timer.RemainingSeconds == 1.5f);
    timer.SetPaused(true); manager.Update(1, 1);
    Check(timer.IsRunning() && timer.IsPaused && timer.RemainingSeconds == 1.5f);
    timer.SetPaused(false); manager.Update(2, 2);
    Check(finished == 1 && last == 0 && !timer.IsRunning());
    timer.Stop(); timer.Complete(); Check(finished == 1);
    timer.Run(1); timer.Stop(); manager.Update(2, 2); Check(finished == 1);
    timer.Run(1); timer.Complete(); Check(finished == 2);
    timer.Run(0); Check(finished == 3 && last == 0);
}
using (var scaled = new Timer())
using (var unscaled = new Timer(true))
{
    scaled.Run(2); unscaled.Run(2); manager.Update(0, 1);
    Check(scaled.RemainingSeconds == 2 && unscaled.RemainingSeconds == 1);
    manager.Update(.5f, 1);
    Check(scaled.RemainingSeconds == 1.5f && !unscaled.IsRunning());
}
using (var timer = new Timer())
{
    timer.Finished += () => timer.Run(3);
    timer.Run(1); manager.Update(1, 1); manager.Update(1, 1);
    Check(timer.IsRunning() && timer.RemainingSeconds == 2);
}
using (var timer = new Timer())
{
    timer.ValueChanged += value => { if (value == 0) timer.Run(4); };
    var finished = false; timer.Finished += () => finished = true;
    timer.Run(1); manager.Update(1, 1);
    Check(!finished && timer.RemainingSeconds == 4);
}
var disposed = new Timer(); disposed.Run(1); disposed.Dispose(); disposed.Dispose();
Check(!disposed.IsRunning());
try { disposed.Run(1); throw new Exception("Disposed timer accepted Run"); }
catch (ObjectDisposedException) { checks++; }
using (var timer = new Timer())
{
    timer.Run(3);
    try { timer.Run(float.NaN); throw new Exception("NaN accepted"); }
    catch (ArgumentOutOfRangeException) { Check(timer.RemainingSeconds == 3 && timer.IsRunning()); }
}
var advance = typeof(Application).GetMethod("AdvanceTime", BindingFlags.NonPublic | BindingFlags.Static)!;
var start = Application.Time; var rawStart = Application.UnscaledTime;
Application.TimeScale = .5f; advance.Invoke(null, [2d]);
Check(Application.Time == start + 1 && Application.UnscaledTime == rawStart + 2);
Check(Application.DeltaTime == 1 && Application.UnscaledDeltaTime == 2);
Application.TimeScale = 0; advance.Invoke(null, [1d]);
Check(Application.Time == start + 1 && Application.UnscaledTime == rawStart + 3 && Application.DeltaTime == 0);
try { Application.TimeScale = -1; throw new Exception("Negative scale accepted"); }
catch (ArgumentOutOfRangeException) { checks++; }
Application.TimeScale = 1;
Console.WriteLine($"PASS: {checks} timer/time checks");
