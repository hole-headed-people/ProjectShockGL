using System.Globalization;
using ProjectShockGL;
using ProjectShockGL.Localization;

if (args.Length > 0)
{
    foreach (var path in Directory.EnumerateFiles(args[0], "*.json", SearchOption.AllDirectories))
    {
        LocalizationData.Read(path);
        Console.WriteLine("PASS: " + Path.GetFileName(path));
    }
}

var root = Path.Combine(Path.GetTempPath(), "LocalizationChecks-" + Guid.NewGuid().ToString("N"));
var ini = Path.Combine(AppContext.BaseDirectory, "DefaultSettings.ini");
var previous = File.Exists(ini) ? File.ReadAllText(ini) : null;
var culture = CultureInfo.CurrentCulture;
var originalError = Console.Error;
var originalOutput = Console.Out;
var errors = new StringWriter();
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
try
{
    var directory = Path.Combine(root, "Localization");
    Directory.CreateDirectory(directory);
    File.WriteAllText(Path.Combine(directory, "Valid.json"), """
    {"languages":{
      "en":{"Translations":{
        "Plain":["Hello"],"Number":["{0:F2}"],"Aligned":["[{0,5}]"],
        "Escaped":["{{value}} = {0}"],"Braces":["{{literal}}"],
        "Bad":["{0"],"MissingArg":["{2}"],"Fallback":["English"],
        "Variants":["A","B"],"VariantFormat":["First {0:F1}","Second {0:F1}"],"Empty":[""],"Date":["2026-11-26T00:00:00Z"]}},
      "ru":{"translations":{"Plain":["Привет"]}}
    }}
    """);
    File.WriteAllText(Path.Combine(directory, "Broken.json"), """{"Languages":{"en":{"Translations":{"Bad":[null]}}}}""");
    File.WriteAllText(ini, "[Game]\nName=LocalizationChecks\n[Localization]\nTables=Missing,Broken,Valid\nLanguage=ru\n");
    Settings.Initialize();
    typeof(Content).GetProperty("Path")!.SetValue(null, root);
    Console.SetError(errors);
    Console.SetOut(errors);
    LocalizationManager.Initialize();
    Check(LocalizationManager.GetString("Plain", "Valid") == "Привет", "Current language");
    Check(LocalizationManager.GetString("Fallback", "Valid") == "English", "English fallback");
    var length = errors.ToString().Length;
    LocalizationManager.GetString("Fallback", "Valid");
    Check(errors.ToString().Length == length, "Fallback warning once");
    Check(LocalizationManager.GetString("Unknown", "Valid") == "Unknown", "Key fallback");
    length = errors.ToString().Length;
    LocalizationManager.GetString("Unknown", "Valid");
    Check(errors.ToString().Length == length, "Missing key warning once");
    Check(LocalizationManager.GetString("Any", "Absent") == "Any", "Missing table fallback");
    length = errors.ToString().Length;
    LocalizationManager.GetString("Other", "Absent");
    Check(errors.ToString().Length == length, "Missing table warning once");
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
    LocalizationManager.SetLanguage("EN");
    Check(LocalizationManager.GetStringFormat("Number", "Valid", 1.25) == "1.25", "Numeric format");
    Check(LocalizationManager.GetStringFormat("Aligned", "Valid", 7) == "[    7]", "Alignment");
    Check(LocalizationManager.GetStringFormat("Escaped", "Valid", 5) == "{value} = 5", "Escaped braces");
    Check(LocalizationManager.GetStringFormat("Braces", "Valid") == "{literal}", "Escaped braces without arguments");
    Check(LocalizationManager.GetStringFormat("Plain", "Valid") == "Hello", "Plain string");
    Check(LocalizationManager.GetStringFormat("Bad", "Valid", 1) == "{0", "Broken format fallback");
    length = errors.ToString().Length;
    LocalizationManager.GetStringFormat("Bad", "Valid", 1);
    Check(errors.ToString().Length == length, "Format error once");
    Check(LocalizationManager.GetStringFormat("MissingArg", "Valid", 1) == "{2}", "Missing argument fallback");
    Check(LocalizationManager.GetString("Empty", "Valid") == "", "Empty translation is valid");
    Check(LocalizationManager.GetString("Date", "Valid") == "2026-11-26T00:00:00Z", "Date remains a string");
    for (var i = 0; i < 100; i++)
    {
        Check(LocalizationManager.GetString("Variants", "Valid") == "A", "Ordinary lookup uses first variant");
        Check(LocalizationManager.GetStringFormat("VariantFormat", "Valid", 2.5) == "First 2.5", "Ordinary formatting uses first variant");
        Check(LocalizationManager.GetRandomString("Variants", "Valid") is "A" or "B", "Random variants");
        Check(LocalizationManager.GetRandomStringFormat("VariantFormat", "Valid", 2.5) is "First 2.5" or "Second 2.5", "Random formatted variants");
    }
    LocalizationManager.SetLanguage("ru");
    Check(LocalizationManager.GetString("Variants", "Valid") == "A", "Fallback uses first variant");
    Check(LocalizationManager.GetRandomString("Variants", "Valid") is "A" or "B", "Random fallback");
    Check(LocalizationManager.GetRandomStringFormat("Bad", "Valid", 1) == "{0", "Random format error fallback");
    foreach (var invalid in new[] {
        "{}", "{\"Languages\":null}", "{\"Languages\":{}}",
        """{"Languages":{"en":{"Translations":{}}},"languages":{"ru":{"translations":{}}}}""",
        """{"Languages":{"en":null}}""",
        """{"Languages":{"en":{}}}""",
        """{"Languages":{"en":{"Translations":null}}}""",
        """{"Languages":{"en":{"Translations":{"": ["x"]}}}}""",
        """{"Languages":{"en":{"Translations":{"x":[]}}}}""",
        """{"Languages":{"en":{"Translations":{"x":[12]}}}}""",
        """{"Languages":{"en":{"Translations":{"x":[null]}}}}""",
        """{"Languages":{"en":{"Translations":{}},"EN":{"Translations":{}}}}""",
        """{"Languages":{"en":{"Translations":{"x":["a"],"x":["b"]}}}}"""
    })
    {
        var path = Path.Combine(directory, "Invalid.json");
        File.WriteAllText(path, invalid);
        var failed = false;
        try { LocalizationData.Read(path); }
        catch (Exception error) when (error is InvalidDataException or Newtonsoft.Json.JsonException) { failed = true; }
        Check(failed, "Invalid table was accepted: " + invalid);
    }
    var log = errors.ToString();
    Check(log.Contains("Missing.json") && log.Contains("Broken.json") && log.Contains("Valid/Bad") && log.Contains("Valid/MissingArg"), "Diagnostic context");
    Check(File.ReadAllText(Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
        "LocalizationChecks", "Logs", "Game.log")).Contains("Valid/Bad"), "Diagnostics reach file log");
    originalOutput.WriteLine("All localization checks passed.");
}
finally
{
    Console.SetError(originalError);
    Console.SetOut(originalOutput);
    CultureInfo.CurrentCulture = culture;
    if (previous == null) File.Delete(ini); else File.WriteAllText(ini, previous);
    Directory.Delete(root, true);
}
