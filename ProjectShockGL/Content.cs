using ProjectShockGL.Audio;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools;
using Silk.NET.OpenGL;
using Shader = ProjectShockGL.Rendering.Shader;
using Texture = ProjectShockGL.Rendering.Texture;

namespace ProjectShockGL;

public enum TextureFormat
{
    Png,
    Jpeg
}

public static class Content
{
    private static bool _isInitialized;
    
    private static GL _gl = null!;
    
    public static string Path { get; private set; } = null!;

    private static readonly Dictionary<string, Texture> Textures = new();
    private static readonly Dictionary<string, AudioClip> AudioClips = new();
    private static readonly Dictionary<string, TextureAtlas> TextureAtlases = new();
    private static readonly Dictionary<(string Name, int Size), Font> Fonts = new();
    private static readonly Dictionary<string, Shader> Shaders = new();
    private static readonly Dictionary<(string Vertex, string Fragment), ShaderProgram> Programs = new();
    private static readonly Dictionary<string, Mesh> Meshes = new();
    private static readonly Dictionary<string, GeometryData> Geometries = new();
    private static readonly Dictionary<string, DetailAsset> Details = new();
    private static readonly Dictionary<string, ParticleAsset> Particles = new();
    private static readonly Dictionary<string, EntityData> Entities = new();
    private static readonly Dictionary<string, Level> Levels = new();
    private static readonly Dictionary<string, PostProcessProfile> PostProcessProfiles = new();
    
    private static Texture? _whiteTexture;
    private static Mesh? _quadMesh;
    
    public static void Initialize(GL gl)
    {
        if (_isInitialized)
        {
            return;
        }
        
        _gl = gl;
        _isInitialized = true;
        
        var basePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Settings.GetString("Content", "Path", "Content"));
        var customPath = Settings.GetString("Content", "CustomPath", string.Empty);

        if (string.IsNullOrEmpty(customPath))
        {
            Path = basePath;
        }
        else
        {
            if (System.IO.Path.IsPathRooted(customPath))
            {
                Path = customPath;
            }
            else
            {
                Path = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, customPath));
            }
        }
        
        Log.Print($"Content directory: {Path}");
    }
    
    // Runtime targets belong to their creator, not to the Content cache.
    public static RenderTexture CreateRenderTexture(int width, int height, RenderTextureFilter filter = RenderTextureFilter.Nearest)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Initialize Content before creating a RenderTexture.");
        }

        return new RenderTexture(_gl, width, height, filter);
    }
    private static string GetTextureFormat(TextureFormat format)
    {
        return format switch
        {
            TextureFormat.Png => ".png",
            TextureFormat.Jpeg => ".jpg",
            _ => ""
        };
    }
    
    public static Texture? LoadTexture(string name, TextureFormat format = TextureFormat.Jpeg)
    {
        try
        {
            var key = $"{name}_{format}";
        
            if (Textures.TryGetValue(key, out var texture))
            {
                return texture;
            }

            var path = System.IO.Path.Combine(Path, "Textures", name + GetTextureFormat(format));

            if (!File.Exists(path))
            {
                Log.PrintWarning($"Resource not found: {path}");
                return null;
            }
        
            var loadedTexture = Texture.Load(_gl, path);
        
            Textures.Add(key, loadedTexture);
        
            return loadedTexture;
        }
        catch (Exception error)
        {
            Log.PrintError($"LoadTexture failed for '{name}'.", error);
            throw;
        }
    }
    
    public static TextureAtlas? LoadTextureAtlas(string name)
    {
        try
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Initialize Content before loading texture atlases.");
            }

            if (TextureAtlases.TryGetValue(name, out var cached) && cached.Texture.Handle != 0)
            {
                return cached;
            }

            var path = System.IO.Path.Combine(Path, "Atlases", name);
            var descriptor = path + ".json";

            if (!File.Exists(descriptor))
            {
                Log.PrintWarning($"Resource not found: {descriptor}");
                return null;
            }

            var atlas = TextureAtlas.Load(_gl, descriptor, path + ".png");
        
            TextureAtlases[name] = atlas;
        
            return atlas;
        }
        catch (Exception error)
        {
            Log.PrintError($"LoadTextureAtlas failed for '{name}'.", error);
            throw;
        }
    }

    public static Font? LoadFont(string name, int pixelHeight = 24)
    {
        try
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Initialize Content before loading fonts.");
            }

            if (pixelHeight is < 4 or > 128)
            {
                throw new ArgumentOutOfRangeException(nameof(pixelHeight));
            }

            if (Fonts.TryGetValue((name, pixelHeight), out var cached) && !cached.IsDisposed)
            {
                return cached;
            }
        
            var path = System.IO.Path.Combine(Path, "Fonts", name + ".ttf");

            if (!File.Exists(path))
            {
                Log.PrintWarning($"Resource not found: {path}");
                return null;
            }
        
            var font = Font.Load(_gl, path, pixelHeight);
            
            Fonts[(name, pixelHeight)] = font;
        
            return font;
        }
        catch (Exception error)
        {
            Log.PrintError($"LoadFont failed for '{name}'.", error);
            throw;
        }
    }

    private static string GetShaderFormat(ShaderType type)
    {
        return type switch
        {
            ShaderType.VertexShader => ".vert",
            ShaderType.FragmentShader => ".frag",
            _ => ""
        };
    }

    public static Shader? LoadShader(string name, ShaderType type)
    {
        try
        {
            var key = $"{name}_{type}";
        
            if (Shaders.TryGetValue(key, out var shader))
            {
                return shader;
            }
        
            var path = System.IO.Path.Combine(Path, "Shaders", name + GetShaderFormat(type));
        
            if (!File.Exists(path))
            {
                Log.PrintWarning($"Resource not found: {path}");
                return null;
            }
        
            var content = File.ReadAllText(path);

            if (string.IsNullOrEmpty(content))
            {
                return null;
            }
        
            const string litDirective = "// @lit";
            
            if (type == ShaderType.FragmentShader && content.StartsWith(litDirective, StringComparison.Ordinal)
                && (content.Length == litDirective.Length || content[litDirective.Length] is '\r' or '\n'))
            {
                const string emissionMarker = "/* LIT_EMISSION */";
                var template = File.ReadAllText(System.IO.Path.Combine(Path, "Shaders", "Lit.frag"));
                
                if (!template.Contains(emissionMarker, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Lit.frag does not support emission effects. Update the Lit template.");
                }

                content = template.Replace(emissionMarker,
                    "#define LIT_HAS_EMISSION\n" + content[litDirective.Length..] + "\n",
                    StringComparison.Ordinal);
            }

            var loadedShader = new Shader(_gl, type, content);
        
            Shaders.Add(key, loadedShader);
        
            return loadedShader;
        }
        catch (Exception error)
        {
            Log.PrintError($"LoadShader failed for '{name}'.", error);
            throw;
        }
    }
    
    public static ShaderProgram LoadShaderProgram(string vertexName, string? fragmentName = null)
    {
        fragmentName ??= vertexName;
        
        var key = (vertexName, fragmentName);

        if (Programs.TryGetValue(key, out var cached) && cached.Handle != 0)
        {
            return cached;
        }
        
        var vertex = LoadShader(vertexName, ShaderType.VertexShader)
            ?? throw new FileNotFoundException($"Vertex shader '{vertexName}.vert' was not found.");
        
        var fragment = LoadShader(fragmentName, ShaderType.FragmentShader)
            ?? throw new FileNotFoundException($"Fragment shader '{fragmentName}.frag' was not found.");
        
        var program = new ShaderProgram(_gl, vertex, fragment);
        
        Programs[key] = program;
        
        return program;
    }

    public static PostProcessProfile? LoadPostProcessProfile(string name)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (PostProcessProfiles.TryGetValue(name, out var cached))
            {
                return cached;
            }
        
            var path = System.IO.Path.Combine(Path, "PostProcessProfiles", name + ".json");

            if (!File.Exists(path))
            {
                Log.PrintWarning($"Resource not found: {path}");
                return null;
            }
        
            try
            {
                var json = JObject.Parse(File.ReadAllText(path), new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
            
                var profile = json.ToObject<PostProcessProfile>(JsonSerializer.Create(new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Error,
                    DateParseHandling = DateParseHandling.None
                })) ?? throw new InvalidDataException("The profile is empty.");

                if (string.IsNullOrWhiteSpace(profile.VertexShader) || string.IsNullOrWhiteSpace(profile.FragmentShader) ||
                    profile.Parameters == null)
                {
                    throw new InvalidDataException("The profile needs shader names and a Parameters dictionary.");
                }
            
                PostProcessProfiles.Add(name, profile);
            
                return profile;
            }
            catch (Exception error) when (error is JsonException or InvalidDataException)
            {
                throw new InvalidDataException($"Cannot load postprocess profile '{path}': {error.Message}", error);
            }
        }
        catch (Exception error)
        {
            Log.PrintError($"LoadPostProcessProfile failed for '{name}'.", error);
            throw;
        }
    }

    public static Mesh? LoadMesh(string name)
    {
        try
        {
            if (Meshes.TryGetValue(name, out var mesh))
            {
                return mesh;
            }

            var path = System.IO.Path.Combine(Path, "Meshes", name + ".obj");

            if (!File.Exists(path))
            {
                Log.PrintWarning($"Resource not found: {path}");
                return null;
            }

            var loadedMesh = ObjLoader.Load(_gl, path);
            Meshes.Add(name, loadedMesh);

            return loadedMesh;
        }
        catch (Exception error)
        {
            Log.PrintError($"LoadMesh failed for '{name}'.", error);
            throw;
        }
    }

    public static AudioGroupsAsset LoadAudioGroups(string name)
    {
        try
        {
            return AudioGroupsAsset.Read(GetAudioGroupsPath(name));
        }
        catch (Exception error)
        {
            Log.PrintError($"LoadAudioGroups failed for '{name}'.", error);
            throw;
        }
    }

    public static string GetAudioGroupsPath(string name)
    {
        DetailAsset.ValidateReference(name, nameof(name));
        
        var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Path, "Audio"));
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, name.Replace('\\', '/') + ".json"));

        if (!path.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Audio Groups must be inside Content/Audio.");
        }
        
        return path;
    }

    public static AudioClip? LoadAudioClip(string name)
    {
        try
        {
            DetailAsset.ValidateReference(name, nameof(name));

            name = name.Replace('\\', '/');
            
            var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Path, "Audio"));
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, name + ".wav"));

            if (!path.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Audio clips must be inside Content/Audio.");
            }
            
            if (AudioClips.TryGetValue(name, out var cached))
            {
                if (!cached.IsDisposed)
                {
                    return cached;
                }
                
                AudioClips.Remove(name);
            }

            if (!File.Exists(path))
            {
                Log.PrintWarning($"Resource not found: {path}");
                
                return null;
            }

            var clip = AudioClip.Load(path);
            AudioClips.Add(name, clip);
            
            return clip;
        }
        catch (Exception error)
        {
            Log.PrintError($"LoadAudioClip failed for '{name}'.", error);
            
            throw;
        }
    }

    public static ParticleAsset? LoadParticle(string name)
    {
        try
        {
            var path = GetParticlePath(name);
            name = name.Replace('\\', '/');

            if (Particles.TryGetValue(name, out var cached))
            {
                return cached;
            }
            
            if (!File.Exists(path))
            {
                Log.PrintWarning($"Resource not found: {path}");
                
                return null;
            }
            
            var asset = JsonConvert.DeserializeObject<ParticleAsset>(File.ReadAllText(path), JsonHelper.GetSettings())
                ?? throw new InvalidDataException("Particle asset is empty or null.");
            
            asset.Validate();
            Particles.Add(name, asset);
            
            return asset;
        }
        catch (Exception error)
        {
            Log.PrintError($"LoadParticle failed for '{name}'.", error);
            throw;
        }
    }

    public static void SaveParticle(string name, ParticleAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        
        asset.Validate();
        
        var path = GetParticlePath(name);
        var json = JsonConvert.SerializeObject(asset, JsonHelper.GetSettings());
        
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        
        try
        {
            File.WriteAllText(temporary, json);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
        
        Particles.Remove(name.Replace('\\', '/'));
    }

    private static string GetParticlePath(string name)
    {
        DetailAsset.ValidateReference(name, nameof(name));
        
        var directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Path, "Particles"));
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(directory, name.Replace('\\', '/') + ".json"));

        if (!path.StartsWith(directory + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Particle assets must be inside Content/Particles.");
        }
        
        return path;
    }
    public static DetailAsset? LoadDetail(string name)
    {
        string? path = null;
        
        try
        {
            DetailAsset.ValidateReference(name, nameof(name));
            
            name = name.Replace('\\', '/');

            if (Details.TryGetValue(name, out var cached))
            {
                return cached;
            }
            
            path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Path, "Details", name + ".json"));
            
            if (!File.Exists(path))
            {
                Log.PrintWarning($"Resource not found: {path}");
                return null;
            }

            var data = JsonConvert.DeserializeObject<DetailAsset>(File.ReadAllText(path), JsonHelper.GetSettings())
                ?? throw new InvalidDataException("Detail asset is empty or null.");
            
            data.Validate();
            Details.Add(name, data);
            
            return data;
        }
        catch (Exception error)
        {
            var failure = new InvalidDataException($"Cannot load Detail '{path ?? name}': {error.Message}", error);
            Log.PrintError($"LoadDetail failed for '{name}'.", failure);
            
            throw failure;
        }
    }
    public static GeometryData? LoadGeometry(string name)
    {
        try
        {
            if (Geometries.TryGetValue(name, out var geometry))
            {
                return geometry;
            }
        
            var path = System.IO.Path.Combine(Path, "Geometry", name + ".json");

            if (!File.Exists(path))
            {
                Log.PrintWarning($"Resource not found: {path}");
                return null;
            }
        
            var json = File.ReadAllText(path);
        
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
        
            var data = JsonConvert.DeserializeObject<GeometryData>(json, JsonHelper.GetSettings());
        
            if (data == null)
            {
                return null;
            }
        
            Geometries.Add(name, data);
        
            return data;
        }
        catch (Exception error)
        {
            Log.PrintError($"LoadGeometry failed for '{name}'.", error);
            throw;
        }
    }

    public static string SaveLightmap(string name, DirectLightBakeResult result)
    {
        return LightmapStorage.Save(name, result);
    }

    internal static WorldLightmaps LoadLightmap(string name)
    {
        return new WorldLightmaps(_gl, name);
    }

    public static void InvalidateLevels()
    {
        Levels.Clear();
    }

    public static void InvalidateEntity(string name)
    {
        Entities.Remove(name);
        EntityAssetReferences.Refresh();
    }

    public static EntityData? LoadEntity(string name, Guid? assetId)
    {
        name = EntityAssetReferences.ResolvePath(name, assetId);
        
        var data = LoadEntity(name);
        
        if (assetId.HasValue && data?.AssetId != assetId)
        {
            InvalidateEntity(name);
            
            data = LoadEntity(name);
        }

        if (assetId.HasValue && data?.AssetId != assetId)
        {
            throw new InvalidDataException($"Entity asset '{name}' does not match GUID '{assetId}'.");
        }

        return data;
    }

    public static EntityData? LoadEntity(string name)
    {
        try
        {
            if (Entities.TryGetValue(name, out var entity))
            {
                return entity;
            }
        
            var path = System.IO.Path.Combine(Path, "Entities", name + ".json");

            if (!File.Exists(path))
            {
                Log.PrintWarning($"Resource not found: {path}");
                return null;
            }
        
            var json = File.ReadAllText(path);
        
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            var data = JsonConvert.DeserializeObject<EntityData>(json, JsonHelper.GetSettings());
        
            if (data == null)
            {
                return null;
            }
        
            Entities.Add(name, data);
        
            return data;
        }
        catch (Exception error)
        {
            Log.PrintError($"LoadEntity failed for '{name}'.", error);
            throw;
        }
    }
        
    internal static Mesh GetQuadMesh()
    {
        return _quadMesh ??= CreateMesh(
        [
            -.5f, -.5f, 0, 0, 0, 1, 0, 1,
             .5f, -.5f, 0, 0, 0, 1, 1, 1,
             .5f,  .5f, 0, 0, 0, 1, 1, 0,
            -.5f,  .5f, 0, 0, 0, 1, 0, 0
        ], [0, 1, 2, 0, 2, 3]);
    }
    public static Texture GetWhitePixel()
    {
        if (_whiteTexture != null)
        {
            return _whiteTexture;
        }

        _whiteTexture = Texture.CreateSolid(_gl, 255, 255, 255, 255);
        
        return _whiteTexture;
    }
    
    public static Level? LoadLevel(string name)
    {
        try
        {
            if (Levels.TryGetValue(name, out var level))
            {
                return level;
            }
        
            var path = System.IO.Path.Combine(Path, "Levels", name + ".map");

            if (!File.Exists(path))
            {
                Log.PrintWarning($"Resource not found: {path}");
                return null;
            }
        
            var json = File.ReadAllText(path);

            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
        
            var data = JsonConvert.DeserializeObject<Level>(json, JsonHelper.GetSettings());

            if (data == null)
            {
                return null;
            }
        
            Levels.Add(name, data);
        
            return data;
        }
        catch (Exception error)
        {
            Log.PrintError($"LoadLevel failed for '{name}'.", error);
            
            throw;
        }
    }
    
    public static string[] GetAllDetailNames()
    {
        var directory = System.IO.Path.Combine(Path, "Details");
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
            .Select(file => System.IO.Path.ChangeExtension(System.IO.Path.GetRelativePath(directory, file), null)!
                .Replace('\\', '/'))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string[] GetAllGeometryNames()
    {
        var dir = System.IO.Path.Combine(Path, "Geometry");

        if (!Directory.Exists(dir))
        {
            return [];
        }

        return Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories)
            .Select(file => System.IO.Path.ChangeExtension(System.IO.Path.GetRelativePath(dir, file), null)!.Replace('\\', '/'))
            .OfType<string>()
            .ToArray();
    }

    public static Placeable? LoadPrefab(string name)
    {
        var data = LoadPrefabData(name);
        
        if (data == null)
        {
            return null;
        }

        if (data.Placeables.Length != 1)
        {
            throw new InvalidDataException("Use LoadPrefabData for a multi-object prefab.");
        }
        
        var source = data.Placeables[0];
        
        return Prefab.WithTransform(source, source.Position + data.Pivot, source.Rotation);
    }

    public static PrefabData? LoadPrefabData(string name)
    {
        try
        {
            var path = GetPrefabPath(name);

            if (!File.Exists(path))
            {
                Log.PrintWarning($"Resource not found: {path}");
                
                return null;
            }
        
            var settings = JsonHelper.GetSettings();
            var json = JObject.Parse(File.ReadAllText(path));
        
            PrefabData data;

            if (json.Property("Placeables", StringComparison.OrdinalIgnoreCase) != null)
            {
                data = json.ToObject<PrefabData>(JsonSerializer.Create(settings))!;
            }
            else
            {
                var source = json.ToObject<Placeable>(JsonSerializer.Create(settings))!;
            
                data = new PrefabData { Pivot = source.Position,
                    Placeables = [Prefab.WithTransform(source, System.Numerics.Vector3.Zero, source.Rotation)] };
            }

            if (data.Placeables == null || data.Placeables.Length == 0 || data.Placeables.Any(p => p == null) ||
                !float.IsFinite(data.Pivot.X) || !float.IsFinite(data.Pivot.Y) || !float.IsFinite(data.Pivot.Z))
            {
                throw new InvalidDataException($"Prefab '{name}' has invalid placements or pivot.");
            }
        
            return data;
        }
        catch (Exception error)
        {
            Log.PrintError($"LoadPrefabData failed for '{name}'.", error);
            throw;
        }
    }

    public static string GetPrefabPath(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || System.IO.Path.IsPathRooted(name))
        {
            throw new ArgumentException("Use a relative prefab name without extension.", nameof(name));
        }
        
        var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Path, "Prefabs")) + System.IO.Path.DirectorySeparatorChar;
        var file = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, name + ".json"));

        if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Prefab must remain inside Content/Prefabs.", nameof(name));
        }
        
        return file;
    }

    public static string[] GetAllPrefabNames()
    {
        var root = System.IO.Path.Combine(Path, "Prefabs");
        
        return Directory.Exists(root) ? Directory.GetFiles(root, "*.json", SearchOption.AllDirectories)
            .Select(file => System.IO.Path.ChangeExtension(System.IO.Path.GetRelativePath(root, file), null)!.Replace('\\', '/'))
            .Order(StringComparer.OrdinalIgnoreCase).ToArray() : [];
    }

    public static string[] GetAllEntityNames()
    {
        var dir = System.IO.Path.Combine(Path, "Entities");

        if (!Directory.Exists(dir))
        {
            return [];
        }

        return Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories)
            .Select(file => System.IO.Path.ChangeExtension(System.IO.Path.GetRelativePath(dir, file), null)!.Replace('\\', '/'))
            .OfType<string>()
            .ToArray();
    }
    
    public static UI.UIAsset LoadUI(string name)
    {
        var path = GetUIPath(name);
        
        try
        {
            var asset = JsonConvert.DeserializeObject<UI.UIAsset>(File.ReadAllText(path), JsonHelper.GetSettings())
                ?? throw new InvalidDataException($"UI asset '{name}' is empty.");
            
            if (asset.Version != 1 || asset.Root == null)
            {
                throw new InvalidDataException($"UI asset '{name}' has an invalid root or version.");
            }
            
            return asset;
        }
        catch (Exception error)
        {
            Log.PrintError($"Cannot load UI '{path}'.", error);
            throw;
        }
    }

    public static void SaveUI(string name, UI.Widget root)
    {
        SaveUI(name, UI.UIAsset.Capture(root));
    }

    public static void SaveUI(string name, UI.UIAsset data)
    {
        ArgumentNullException.ThrowIfNull(data);
        
        var validation = new UI.Canvas();
        validation.Resize(new System.Numerics.Vector2(800, 600));

        try
        {
            data.Instantiate(validation);
        }
        finally
        {
            validation.Destroy();
        }
        
        var path = GetUIPath(name);
        var json = JsonConvert.SerializeObject(data, Formatting.Indented, JsonHelper.GetSettings());
        
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        
        try
        {
            File.WriteAllText(temporary, json);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static string GetUIPath(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        
        var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Path, "UI"));
        var file = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, name + ".json"));

        if (!file.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("UI assets must remain inside Content/UI.", nameof(name));
        }
        
        return file;
    }

    internal static (string Name, TextureFormat Format) GetUITextureReference(Texture texture)
    {
        foreach (var entry in Textures)
        {
            if (ReferenceEquals(entry.Value, texture))
            {
                var separator = entry.Key.LastIndexOf('_');
                return (entry.Key[..separator], Enum.Parse<TextureFormat>(entry.Key[(separator + 1)..]));
            }
        }
        
        throw new InvalidDataException("UI textures must be loaded through Content before capture.");
    }

    internal static (string Name, int Size) GetUIFontReference(Font font)
    {
        foreach (var entry in Fonts)
        {
            if (ReferenceEquals(entry.Value, font))
            {
                return entry.Key;
            }
        }
        
        throw new InvalidDataException("UI fonts must be loaded through Content before capture.");
    }

    internal static (string Atlas, string Region) GetUIRegionReference(TextureRegion region)
    {
        foreach (var entry in TextureAtlases)
        {
            var name = entry.Value.FindRegion(region);
            
            if (name != null)
            {
                return (entry.Key, name);
            }
        }
        
        throw new InvalidDataException("UI atlas regions must be loaded through Content before capture.");
    }
    public static void UnloadAll()
    {
        PostProcessSettings.Reset();
        
        PostProcessProfiles.Clear();
        
        var resources = Fonts.Values.Cast<IDisposable>()
            .Concat(AudioClips.Values).Concat(TextureAtlases.Values)
            .Concat(Programs.Values).Concat(Shaders.Values).Concat(Textures.Values).Concat(Meshes.Values).ToList();
        
        Programs.Clear();

        if (_whiteTexture != null)
        {
            resources.Add(_whiteTexture);
        }

        if (_quadMesh != null)
        {
            resources.Add(_quadMesh);
        }
        
        _quadMesh = null;

        AudioClips.Clear();
        Fonts.Clear();
        TextureAtlases.Clear();
        Shaders.Clear();
        Textures.Clear();
        Meshes.Clear();
        Geometries.Clear();
        Details.Clear();
        Particles.Clear();
        Entities.Clear();
        
        EntityAssetReferences.Refresh();
        
        Levels.Clear();
        
        _whiteTexture = null;

        Cleanup.Run(resources.Distinct().Select<IDisposable, Action>(resource => resource.Dispose).ToArray());
    }
    internal static Mesh CreateMesh(float[] vertices, uint[] indices)
    {
        return new Mesh(_gl, vertices, indices);
    }
}


