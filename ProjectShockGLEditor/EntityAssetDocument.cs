using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL;
using ProjectShockGL.Core;

namespace ProjectShockGLEditor;

public enum EntityParameterKind { String, Number, Boolean, Json }

public sealed class EntityAssetParameter
{
    public string Key = "";
    public EntityParameterKind Kind;
    public string Value = "";

    internal JToken Token()
    {
        if (Kind == EntityParameterKind.String) return new JValue(Value);
        if (Kind == EntityParameterKind.Boolean)
        {
            if (!bool.TryParse(Value, out var value)) throw new InvalidDataException($"Parameter '{Key}' needs true or false.");
            return new JValue(value);
        }
        var token = Parse(Value);
        if (Kind == EntityParameterKind.Number && (token.Type is not (JTokenType.Integer or JTokenType.Float) ||
            !double.IsFinite(token.Value<double>())))
            throw new InvalidDataException($"Parameter '{Key}' needs a finite JSON number.");
        return token;
    }

    internal static JToken Parse(string text)
    {
        using var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None };
        var token = JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        while (reader.Read())
            if (reader.TokenType != JsonToken.Comment) throw new InvalidDataException("Unexpected data after JSON value.");
        IEnumerable<JToken> tokens = token is JContainer container ? container.DescendantsAndSelf() : [token];
        if (tokens.Any(value => value.Type == JTokenType.Float && !double.IsFinite(value.Value<double>())))
            throw new InvalidDataException("JSON numbers must be finite.");
        return token;
    }

    internal static EntityAssetParameter From(JProperty property) => new()
    {
        Key = property.Name,
        Kind = property.Value.Type switch
        {
            JTokenType.String => EntityParameterKind.String,
            JTokenType.Integer or JTokenType.Float => EntityParameterKind.Number,
            JTokenType.Boolean => EntityParameterKind.Boolean,
            _ => EntityParameterKind.Json
        },
        Value = property.Value.Type == JTokenType.String ? property.Value.Value<string>()! : property.Value.ToString(Formatting.None)
    };
}

public sealed class EntityAssetDocument
{
    private JObject _root = new();
    private string _saved = "";
    public string DirectoryPath { get; }
    public string? FilePath { get; private set; }
    public Guid? AssetId { get; private set; }
    public string Class { get; set; } = "";
    public List<EntityAssetParameter> Parameters { get; } = [];
    public bool IsDirty => Draft() != _saved;

    public EntityAssetDocument(string directory)
    {
        DirectoryPath = Path.GetFullPath(directory);
        New();
    }

    public void New()
    {
        _root = new JObject();
        FilePath = null;
        AssetId = null;
        Class = "";
        Parameters.Clear();
        _saved = Draft();
    }

    public void Open(string path)
    {
        path = CheckedPath(path);
        var root = Read(path);
        var id = ReadId(root);
        if (root["Class"]?.Type != JTokenType.String ||
            root["Parameters"] != null && root["Parameters"] is not JObject)
            throw new InvalidDataException("An entity asset needs Class and a Parameters object.");
        var parameters = (JObject?)root["Parameters"] ?? new JObject();
        var rows = parameters.Properties().Select(EntityAssetParameter.From).ToArray();
        _root = root;
        AssetId = id;
        Class = root["Class"]!.Value<string>()!;
        Parameters.Clear();
        Parameters.AddRange(rows);
        FilePath = path;
        _saved = Draft();
    }

    public void Save(string? path = null)
    {
        path = CheckedPath(path ?? FilePath ?? throw new InvalidOperationException("Choose a file for the new asset."));
        if (FilePath != null && !path.Equals(FilePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Save updates the opened asset. Use New to create another asset.");
        var type = Type.GetType(Class.Trim());
        if (type == null || !typeof(Entity).IsAssignableFrom(type) || type.IsAbstract ||
            type.ContainsGenericParameters || type.GetConstructor(Type.EmptyTypes) == null)
            throw new InvalidDataException("Class must be a concrete Entity with a public parameterless constructor. Use 'Namespace.Class, Assembly'.");
        var parameters = new JObject();
        foreach (var row in Parameters)
        {
            if (string.IsNullOrWhiteSpace(row.Key) || parameters.ContainsKey(row.Key))
                throw new InvalidDataException($"Parameter keys must be nonempty and unique: '{row.Key}'.");
            parameters.Add(row.Key, row.Token());
        }

        var diskId = File.Exists(path) ? ReadId(Read(path)) : null;
        if (AssetId != null && diskId != null && AssetId != diskId)
            throw new InvalidDataException("The file on disk has another AssetId. It was not overwritten.");
        var id = AssetId ?? diskId ?? Guid.NewGuid();
        var output = (JObject)_root.DeepClone();
        output["AssetId"] = id;
        output["Class"] = Class.Trim();
        output["Parameters"] = parameters;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, output.ToString(Formatting.Indented));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        _root = output;
        AssetId = id;
        FilePath = path;
        Class = Class.Trim();
        _saved = Draft();
        Content.InvalidateEntity(Path.GetRelativePath(DirectoryPath, path)[..^5].Replace('\\', '/'));
    }

    private string CheckedPath(string path)
    {
        var full = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(DirectoryPath, full);
        if (Path.IsPathRooted(relative) || relative == ".." ||
            relative.StartsWith("../", StringComparison.Ordinal) || relative.StartsWith(@"..\", StringComparison.Ordinal) ||
            !Path.GetExtension(full).Equals(".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a JSON file inside Content/Entities.");
        return full;
    }

    private static JObject Read(string path) => EntityAssetParameter.Parse(File.ReadAllText(path)) as JObject
        ?? throw new InvalidDataException("An entity asset must be a JSON object.");

    private static Guid? ReadId(JObject root)
    {
        var token = root["AssetId"];
        if (token == null || token.Type == JTokenType.Null) return null;
        if (token.Type == JTokenType.String && Guid.TryParse(token.Value<string>(), out var id) && id != Guid.Empty) return id;
        throw new InvalidDataException("AssetId must be a nonempty GUID. The existing identifier was not changed.");
    }

    private string Draft() => JsonConvert.SerializeObject(new
    {
        Class, AssetId, Parameters = Parameters.Select(row => new { row.Key, row.Kind, row.Value })
    });
}
