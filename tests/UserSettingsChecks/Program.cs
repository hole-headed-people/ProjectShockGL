using System.Reflection;
using ProjectShockGL;

var root = Path.Combine(Path.GetTempPath(), "UserSettingsChecks-" + Guid.NewGuid().ToString("N"));
var ini = Path.Combine(AppContext.BaseDirectory, "DefaultSettings.ini");
var previous = File.Exists(ini) ? File.ReadAllText(ini) : null;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
void Reload()
{
    typeof(UserSettings).GetField("_profile", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, null);
    UserSettings.Initialize();
}
try
{
    Directory.CreateDirectory(Path.Combine(root, "Content"));
    var definition = Path.Combine(root, "Content", "Default.json");
    File.WriteAllText(definition, """
    {"Options":[
      {"Name":"Volume","Type":"Float","DefaultValue":1.0},
      {"Name":"Count","Type":"Int","DefaultValue":3},
      {"Name":"Language","Type":"String","DefaultValue":"ru"},
      {"Name":"Fullscreen","Type":"Bool","DefaultValue":false}
    ]}
    """);
    File.WriteAllText(ini, $"[Game]\nName=UserSettingsChecks\nSaveDirectory={Path.Combine(root, "Saves")}\nUserSettingsDefaultProfile=Default\nUserSettingsProfile=Preferences\n");
    Settings.Initialize();
    typeof(Content).GetProperty("Path")!.SetValue(null, Path.Combine(root, "Content"));
    UserSettings.Initialize();
    var volume = UserSettings.Profile.Get("Volume");
    Check(volume.Get<float>() == 1, "Defaults");
    var events = new List<IReadOnlyDictionary<string, UserSettingsOption>>();
    UserSettings.ProfileChanged += events.Add;
    volume.Set(.5f);
    volume.Set(.5f);
    Check(events.Count == 1 && events[0].TryGetValue("Volume", out var changedVolume) && changedVolume == volume && !events[0].ContainsKey("Language") && !events[0].ContainsKey("volume"), "Changed once");
    try { volume.Set("bad"); throw new Exception("Accepted wrong type"); } catch (ArgumentException) { }
    try { volume.Set(float.NaN); throw new Exception("Accepted NaN"); } catch (ArgumentException) { }
    UserSettings.Save();
    Check(events.Count == 1, "Save must not send change");
    var values = Path.Combine(root, "Preferences.json");
    Check(File.Exists(values) && !Directory.Exists(Path.Combine(root, "Saves")), "Adjacent path");
    Reload();
    Check(UserSettings.Profile.Get("Volume").Get<float>() == .5f, "Reload values");
    UserSettings.Profile.Get("Language").Set("en");
    var before = events.Count;
    UserSettings.Reset();
    Check(events.Count == before + 1 && events[^1].Count == 2 && events[^1].ContainsKey("Volume") && events[^1].ContainsKey("Language") && !events[^1].ContainsKey("Count"), "Reset batched");
    Check(File.ReadAllText(values).Contains("0.5"), "Reset does not write");
    UserSettings.Save();
    Reload();
    Check(UserSettings.Profile.Get("Volume").Get<float>() == 1, "Saved reset");
    File.WriteAllText(values, """{"Volume":"wrong","Count":2147483648,"Fullscreen":true,"Unknown":15}""");
    Reload();
    Check(UserSettings.Profile.Get("Volume").Get<float>() == 1 && UserSettings.Profile.Get("Count").Get<int>() == 3 &&
        UserSettings.Profile.Get("Fullscreen").Get<bool>(), "Invalid/new/unknown values");
    File.WriteAllText(values, "broken");
    Reload();
    Check(!UserSettings.Profile.Get("Fullscreen").Get<bool>(), "Corrupt file defaults");
    File.WriteAllText(definition, """{"Options":[{"Name":"X","Type":"Bool","DefaultValue":false},{"Name":"X","Type":"Bool","DefaultValue":true}]}""");
    try { UserSettingsProfile.Read(definition); throw new Exception("Accepted duplicate"); } catch (InvalidDataException) { }
    Console.WriteLine("All user settings checks passed.");
}
finally
{
    if (previous == null) File.Delete(ini); else File.WriteAllText(ini, previous);
    Directory.Delete(root, true);
}
