namespace ProjectShockGL;

public static class Log
{
    private static readonly object Sync = new();
    private static string? _path;

    internal static void Initialize(string applicationName, bool isEditor)
    {
        lock (Sync)
        {
            _path = null;
            
            var folder = new string(applicationName.Select(character =>
                Path.GetInvalidFileNameChars().Contains(character) ? '_' : character).ToArray()).Trim().TrimEnd('.');

            if (folder.Length == 0)
            {
                folder = "ProjectShockGL";
            }

            try
            {
                var localData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);

                if (localData.Length == 0)
                {
                    throw new IOException("LocalAppData is unavailable.");
                }

                var directory = Path.Combine(localData, folder, "Logs");
                Directory.CreateDirectory(directory);
                
                var path = Path.Combine(directory, isEditor ? "Editor.log" : "Game.log");
                File.WriteAllText(path, string.Empty);
                
                _path = path;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                WriteConsole(Console.Error, $"Cannot initialize log file: {error.Message}");
            }
        }
    }

    public static void Print(string message)
    {
        Write("Info", message);
    }

    public static void PrintWarning(string message)
    {
        Write("Warning", message);
    }

    public static void PrintError(string message)
    {
        Write("Error", message);
    }

    public static void PrintError(string message, Exception error)
    {
        PrintError($"{message}{System.Environment.NewLine}{error}");
    }

    private static void Write(string level, string message)
    {
        lock (Sync)
        {
            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [{level}] {message}";
            
            WriteConsole(level == "Error" ? Console.Error : Console.Out, line);

            if (_path == null)
            {
                return;
            }

            try
            {
                File.AppendAllText(_path, line + System.Environment.NewLine);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                WriteConsole(Console.Error, $"Cannot write log file: {error.Message}");
            }
        }
    }

    private static void WriteConsole(TextWriter writer, string message)
    {
        try
        {
            writer.WriteLine(message);
        }
        catch (IOException) { }
    }
}
