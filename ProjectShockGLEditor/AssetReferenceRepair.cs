using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Tools;

namespace ProjectShockGLEditor;

public sealed class AssetReferenceRepairReport
{
    public bool Succeeded { get; internal set; }
    public string? BackupDirectory { get; internal set; }
    public List<string> Changes { get; } = [];
    public List<string> Unchanged { get; } = [];
    public List<string> Errors { get; } = [];
}

public static class AssetReferenceRepair
{
    private sealed record Document(string Path, byte[] Original, JObject Json)
    {
        public bool Changed;
    }

    public static AssetReferenceRepairReport Run(string contentPath)
    {
        var report = new AssetReferenceRepairReport();
        var root = Path.GetFullPath(contentPath);
        var documents = new List<Document>();
        var names = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var ids = new Dictionary<Guid, string>();
        var proposed = new List<string>();

        string Relative(string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

        Document Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            using var stream = new MemoryStream(bytes);
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            var json = EntityAssetParameter.Parse(reader.ReadToEnd()) as JObject
                ?? throw new InvalidDataException("Expected a JSON object.");
            var document = new Document(path, bytes, json);
            documents.Add(document);
            return document;
        }

        void Scan(string directory, string extension, Action<string> action)
        {
            try
            {
                var folder = Path.Combine(root, directory);
                if (!Directory.Exists(folder)) return;

                foreach (var file in Directory.EnumerateFiles(folder, extension, SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                {
                    try { action(file); }
                    catch (Exception error) { report.Errors.Add($"{Relative(file)}: {error.Message}"); }
                }
            }
            catch (Exception error) { report.Errors.Add($"{directory}: {error.Message}"); }
        }

        if (!Directory.Exists(root))
        {
            report.Errors.Add($"Content directory does not exist: {root}");
            return report;
        }

        Scan("Entities", "*.json", path =>
        {
            var document = Read(path);
            var json = document.Json;
            var className = json["Class"]?.Type == JTokenType.String ? json["Class"]!.Value<string>() : null;
            var type = className == null ? null : Type.GetType(className);
            if (type == null || !typeof(Entity).IsAssignableFrom(type) || type.IsAbstract ||
                type.ContainsGenericParameters || type.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidDataException($"Class '{className}' must be a concrete Entity with a public parameterless constructor.");
            if (json["Parameters"] is { Type: not JTokenType.Object })
                throw new InvalidDataException("Parameters must be a JSON object.");

            var id = ReadId(json);
            if (id == null)
            {
                id = Guid.NewGuid();
                json["AssetId"] = id.Value;
                document.Changed = true;
                proposed.Add($"{Relative(path)}: assigned AssetId {id}.");
            }

            var name = Path.ChangeExtension(Path.GetRelativePath(Path.Combine(root, "Entities"), path), null)!.Replace('\\', '/');
            if (!names.TryAdd(name, id.Value))
                throw new InvalidDataException($"Duplicate asset path '{name}'.");
            if (!ids.TryAdd(id.Value, name))
                throw new InvalidDataException($"Duplicate AssetId '{id}': '{ids[id.Value]}' and '{name}'.");
        });

        void FixPlacement(Document document, JObject placement, string label)
        {
            var serializer = JsonSerializer.Create(JsonHelper.GetSettings());
            var data = placement.ToObject<Placeable>(serializer)
                ?? throw new InvalidDataException($"{label}: invalid placement.");
            if (!Enum.IsDefined(data.Type))
                throw new InvalidDataException($"{label}: unknown placement type '{data.Type}'.");
            if (data.Type != PlaceableType.Entity) return;

            var id = ReadId(placement);
            string name;
            if (id != null)
            {
                if (!ids.TryGetValue(id.Value, out var found))
                    throw new InvalidDataException($"{label}: entity AssetId '{id}' was not found.");
                name = found;
            }
            else
            {
                name = data.Path.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(name) || !names.TryGetValue(name, out var asset))
                    throw new InvalidDataException($"{label}: entity asset '{data.Path}' was not found.");
                id = asset;
            }

            if (data.AssetId == id && data.Path == name) return;
            placement["AssetId"] = id.Value;
            placement["Path"] = name;
            document.Changed = true;
            proposed.Add($"{Relative(document.Path)} / {label}: {name} -> {id}.");
        }

        void FixPlacements(Document document, bool prefab)
        {
            var property = document.Json.Property("Placeables", StringComparison.OrdinalIgnoreCase);
            if (property == null)
            {
                if (!prefab) throw new InvalidDataException("Map has no Placeables array.");
                FixPlacement(document, document.Json, "single-object prefab");
                return;
            }
            if (property.Value is not JArray placements)
                throw new InvalidDataException("Placeables must be an array.");
            for (var index = 0; index < placements.Count; index++)
            {
                if (placements[index] is not JObject placement)
                    throw new InvalidDataException($"Placeables[{index}] must be an object.");
                FixPlacement(document, placement, $"Placeables[{index}]");
            }
        }

        Scan("Levels", "*.map", path => FixPlacements(Read(path), prefab: false));
        Scan("Prefabs", "*.json", path => FixPlacements(Read(path), prefab: true));

        if (report.Errors.Count != 0)
        {
            // Proposed GUIDs exist only in memory. Even backups are not created on validation errors.
            return report;
        }

        var changes = documents.Where(document => document.Changed).ToArray();
        report.Unchanged.AddRange(documents.Where(document => !document.Changed).Select(document => Relative(document.Path)));
        var staged = new Dictionary<string, string>();
        var committed = new List<Document>();

        try
        {
            void CheckUnchanged()
            {
                foreach (var document in documents)
                    if (!File.ReadAllBytes(document.Path).AsSpan().SequenceEqual(document.Original))
                        throw new IOException($"File changed during repair: {Relative(document.Path)}. Run the check again.");
            }

            CheckUnchanged();
            if (changes.Length != 0)
            {
                var backup = Path.Combine(Path.GetDirectoryName(root)!, "AssetReferenceBackups",
                    DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(backup);
                report.BackupDirectory = backup;
                foreach (var document in changes)
                {
                    var file = Path.Combine(backup, Path.GetRelativePath(root, document.Path));
                    Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                    File.WriteAllBytes(file, document.Original);
                }

                foreach (var document in changes)
                {
                    var temporary = document.Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    staged.Add(document.Path, temporary);
                    File.WriteAllText(temporary, document.Json.ToString(Formatting.Indented));
                }

                CheckUnchanged();
                foreach (var document in changes)
                {
                    File.Move(staged[document.Path], document.Path, overwrite: true);
                    committed.Add(document);
                }
            }

            report.Succeeded = true;
            report.Changes.AddRange(proposed);
        }
        catch (Exception error)
        {
            report.Errors.Add($"Repair failed: {error.Message}");
            foreach (var document in committed.AsEnumerable().Reverse())
            {
                try { File.WriteAllBytes(document.Path, document.Original); }
                catch (Exception rollback)
                {
                    report.Errors.Add($"Could not restore '{Relative(document.Path)}': {rollback.Message}. Restore from '{report.BackupDirectory}'.");
                }
            }
        }
        finally
        {
            foreach (var temporary in staged.Values)
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception cleanup) { report.Errors.Add($"Cannot remove temporary file '{temporary}': {cleanup.Message}"); }
            }
        }

        if (report.Succeeded && !string.IsNullOrEmpty(Content.Path) && Path.GetFullPath(Content.Path).Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var name in names.Keys) Content.InvalidateEntity(name);
            Content.InvalidateLevels();
        }

        return report;
    }

    private static Guid? ReadId(JObject json)
    {
        var token = json["AssetId"];
        if (token == null || token.Type == JTokenType.Null) return null;
        if (token.Type == JTokenType.String && Guid.TryParse(token.Value<string>(), out var id) && id != Guid.Empty) return id;
        throw new InvalidDataException("AssetId must be a nonempty GUID.");
    }
}