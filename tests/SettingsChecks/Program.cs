using ProjectShockGL;

var root = AppContext.BaseDirectory;
var editorPath = Path.Combine(root, "EditorSettings.ini");
var defaultPath = Path.Combine(root, "DefaultSettings.ini");
var backup = new[] { editorPath, defaultPath }.ToDictionary(path => path, path => File.Exists(path) ? File.ReadAllText(path) : null);
var fixture = Path.Combine(root, "SettingsFixture-" + Guid.NewGuid().ToString("N"));
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
try
{
    Directory.CreateDirectory(fixture);
    var shared = Path.Combine(fixture, "DefaultSettings.ini");
    File.WriteAllText(defaultPath, "[Game]\nName=Game\n[Window]\nWidth=640\n[Layers]\n0=Default\n1=Player\n");
    File.WriteAllText(shared, "[Game]\nName=Shared\n[Content]\nPath=Assets\nCustomPath=OverrideAssets\n[Window]\nWidth=1280\nHeight=720\nIcon=Icons/Game.png\n[Layers]\n0=Default\n1=Player\n2=Trigger\n");
    File.WriteAllText(editorPath, $"[Settings]\nDefaultPath={Path.GetRelativePath(root, shared)}\n[Window]\nWidth=1920\nIcon=Icons/Editor.png\n[Content]\nCustomPath=\n[Layers]\n3=Items\n");
    Settings.Initialize(true);
    Check(Settings.GetString("Game", "Name", "") == "Shared", "Shared base");
    Check(Settings.GetInt("Window", "Width", 0) == 1920 && Settings.GetInt("Window", "Height", 0) == 720, "Key fallback");
    Check(Settings.GetString("Content", "CustomPath", "fallback") == "", "Explicit empty override");
    Check(Settings.GetPath("Content", "Path", "Content") == Path.Combine(fixture, "Assets"), "Inherited relative path origin");
    Check(Settings.GetPath("Window", "Icon", "") == Path.Combine(root, "Icons", "Editor.png"), "Override path origin");
    Check(Settings.GetPath("Content", "CustomPath", "fallback") == "", "Empty path");
    Layers.Load();
    Check(Layers.Get("Trigger") == 4 && Layers.Get("Items") == 8 && Layers.Get("Player") == 2, "Merged layers preserve bits");
    Settings.Initialize();
    Check(Settings.GetString("Game", "Name", "") == "Game", "Game ignores editor reference");
    Layers.Load();
    Check(Layers.Get("Player") == 2 && Layers.Get("Trigger") == 0 && Layers.Get("Items") == 0, "Layers reload clears stale entries");
    File.WriteAllText(editorPath, "[Window]\nWidth=900\n");
    Settings.Initialize(true);
    Check(Settings.GetString("Game", "Name", "") == "Game" && Settings.GetInt("Window", "Width", 0) == 900, "Adjacent default without link");
    File.Delete(editorPath);
    Settings.Initialize(true);
    Check(Settings.GetInt("Window", "Width", 0) == 640, "Optional editor settings");
    File.WriteAllText(editorPath, "[Settings]\nDefaultPath=Missing.ini\n");
    var rejected = false;
    try { Settings.Initialize(true); } catch (FileNotFoundException) { rejected = true; }
    Check(rejected, "Broken explicit link reports error");
    Console.WriteLine("All settings checks passed.");
}
finally
{
    foreach (var entry in backup)
    {
        if (entry.Value == null) File.Delete(entry.Key); else File.WriteAllText(entry.Key, entry.Value);
    }
    Directory.Delete(fixture, true);
}
