using System.Reflection;
using System.Runtime.ExceptionServices;
using ProjectShockGL;
using Silk.NET.Maths;
using Silk.NET.Windowing;

WindowOptions Options(string settings)
{
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Settings.ini"), settings);
    Settings.Initialize();
    var method = typeof(Application).GetMethod("CreateWindowOptions", BindingFlags.NonPublic | BindingFlags.Static)!;
    try { return (WindowOptions)method.Invoke(null, null)!; }
    catch (TargetInvocationException ex) when (ex.InnerException != null)
    {
        ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        throw;
    }
}
void Check(bool condition) { if (!condition) throw new Exception("Window mode check failed."); }
var defaults = Options("");
Check(defaults.Size == new Vector2D<int>(1024, 768) && defaults.WindowBorder == WindowBorder.Fixed);
var fixedWindow = Options("[Window]\nMode=Windowed\nWidth=800\nHeight=600\nCanResize=false\n");
Check(fixedWindow.Size == new Vector2D<int>(800, 600) && fixedWindow.WindowBorder == WindowBorder.Fixed);
var resizable = Options("[Window]\nMode=windowed\nWidth=900\nHeight=700\nCanResize=true\n");
Check(resizable.Size == new Vector2D<int>(900, 700) && resizable.WindowBorder == WindowBorder.Resizable);
Check(resizable.WindowState == WindowState.Normal);
Console.WriteLine("PASS: Defaults and Windowed preserve dimensions and CanResize, with case-insensitive mode names");
foreach (var bad in new[] { "[Window]\nMode=Fullscreen\n", "[Window]\nWidth=0\n", "[Window]\nHeight=-2\n" })
{
    var failed = false;
    try { Options(bad); } catch (InvalidDataException) { failed = true; }
    Check(failed);
}
Console.WriteLine("PASS: Invalid modes and nonpositive Windowed sizes report configuration errors");
if (args.Contains("--native"))
{
    // Query before any window exists, exactly as Application.Run does.
    var borderless = Options("[Window]\nMode=borderless\nWidth=ignored\nHeight=ignored\nCanResize=ignored\n");
    var monitor = Silk.NET.Windowing.Monitor.GetMainMonitor(null);
    Check(borderless.Size == monitor.VideoMode.Resolution);
    Check(borderless.WindowBorder == WindowBorder.Hidden && borderless.WindowState == WindowState.Normal);
    if (OperatingSystem.IsWindows()) Check(borderless.Position == Vector2D<int>.Zero);
    borderless.IsVisible = false;
    using var window = Window.Create(borderless);
    window.Initialize();
    Check(window.Size == borderless.Size && window.Position == borderless.Position);
    Check(window.WindowBorder == WindowBorder.Hidden && window.WindowState == WindowState.Normal);
    Console.WriteLine($"PASS: Hidden Borderless window matches primary monitor {window.Size}, at {window.Position}, without exclusive Fullscreen");
}
Console.WriteLine("All window checks passed.");
