using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ProjectShockGLEditor;

public sealed record UnusedLightmap(string FilePath, long Size, DateTime LastWriteTimeUtc);

public sealed class LightmapCleanupReport
{
    public string ContentPath { get; }
    public int LevelCount { get; }
    public IReadOnlyList<UnusedLightmap> Files { get; }
    public long TotalBytes => Files.Sum(file => file.Size);

    internal LightmapCleanupReport(string contentPath, int levelCount, List<UnusedLightmap> files)
    {
        ContentPath = contentPath;
        LevelCount = levelCount;
        Files = files.AsReadOnly();
    }
}

public static class LightmapCleanup
{
    private static void CheckLink(FileSystemInfo entry)
    {
        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Cleanup does not follow links: {entry.FullName}");
    }

    private static IEnumerable<FileInfo> Files(string root, string extension)
    {
        var directory = new DirectoryInfo(root);
        if (!directory.Exists) yield break;
        CheckLink(directory);
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            CheckLink(entry);
            if (entry is DirectoryInfo child)
            {
                foreach (var file in Files(child.FullName, extension)) yield return file;
            }
            else if (entry is FileInfo file && file.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase))
                yield return file;
        }
    }

    private static string ReferencePath(string root, string asset)
    {
        if (string.IsNullOrWhiteSpace(asset) || Path.IsPathRooted(asset) || asset.Contains(':') ||
            asset.Replace('\\', '/').Split('/').Any(part => string.IsNullOrWhiteSpace(part) || part is "." or ".."))
            throw new InvalidDataException($"Invalid lightmap reference '{asset}'.");
        var path = Path.GetFullPath(Path.Combine(root, asset.Replace('\\', '/') + ".lightmap"));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Lightmap reference escapes Content/Lightmaps: {asset}");
        return path;
    }

    public static LightmapCleanupReport Scan(string contentPath, string? activeLightmap = null)
    {
        var content = Path.GetFullPath(contentPath).TrimEnd(Path.DirectorySeparatorChar);
        var directory = new DirectoryInfo(content);
        if (!directory.Exists) throw new DirectoryNotFoundException(content);
        CheckLink(directory);
        var levels = Path.Combine(content, "Levels");
        if (!Directory.Exists(levels)) throw new DirectoryNotFoundException($"Cannot check level references: {levels}");
        var lightmaps = Path.Combine(content, "Lightmaps");
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (activeLightmap != null) referenced.Add(ReferencePath(lightmaps, activeLightmap));
        var levelCount = 0;
        foreach (var file in Files(levels, ".level"))
        {
            try
            {
                using var stream = file.OpenText();
                using var reader = new JsonTextReader(stream) { DateParseHandling = DateParseHandling.None };
                var json = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                while (reader.Read())
                    if (reader.TokenType != JsonToken.Comment) throw new InvalidDataException("Unexpected content after level JSON.");
                var references = json.Properties().Where(property => property.Name.Equals("Lightmap", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (references.Length > 1) throw new InvalidDataException("Duplicate Lightmap field.");
                if (references.Length == 1 && references[0].Value.Type != JTokenType.Null)
                {
                    if (references[0].Value.Type != JTokenType.String) throw new InvalidDataException("Lightmap must be a string or null.");
                    referenced.Add(ReferencePath(lightmaps, references[0].Value.Value<string>()!));
                }
                levelCount++;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                throw new InvalidDataException($"Cannot check level '{file.FullName}': {error.Message}. No files were deleted.", error);
            }
        }
        var unused = Files(lightmaps, ".lightmap").Where(file => !referenced.Contains(file.FullName))
            .OrderBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(file => new UnusedLightmap(file.FullName, file.Length, file.LastWriteTimeUtc)).ToList();
        return new LightmapCleanupReport(content, levelCount, unused);
    }

    public static int Delete(LightmapCleanupReport report, string? activeLightmap = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        // Recheck references and files after the user has reviewed the list.
        var current = Scan(report.ContentPath, activeLightmap).Files.ToDictionary(file => file.FilePath, StringComparer.OrdinalIgnoreCase);
        foreach (var file in report.Files)
        {
            if (!current.TryGetValue(file.FilePath, out var candidate) || candidate.Size != file.Size ||
                candidate.LastWriteTimeUtc != file.LastWriteTimeUtc)
                throw new InvalidOperationException("The cleanup list has changed. Scan again before deleting any files.");
        }
        var removed = 0;
        try
        {
            foreach (var file in report.Files)
            {
                File.Delete(file.FilePath);
                removed++;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Deleted {removed} of {report.Files.Count} lightmaps before an error: {error.Message}", error);
        }
        return removed;
    }
}
