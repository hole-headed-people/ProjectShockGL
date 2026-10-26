using Newtonsoft.Json;
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
    private static readonly Dictionary<(string Name, int Size), Font> Fonts = new();
    private static readonly Dictionary<string, Shader> Shaders = new();
    private static readonly Dictionary<string, Mesh> Meshes = new();
    private static readonly Dictionary<string, GeometryData> Geometries = new();
    private static readonly Dictionary<string, EntityData> Entities = new();
    private static readonly Dictionary<string, Level> Levels = new();
    
    private static Texture? _whiteTexture;
    
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
        
        Console.WriteLine(Path);
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
        var key = $"{name}_{format}";
        
        if (Textures.TryGetValue(key, out var texture))
        {
            return texture;
        }

        var path = System.IO.Path.Combine(Path, "Textures", name + GetTextureFormat(format));

        if (!File.Exists(path))
        {
            return null;
        }
        
        var loadedTexture = Texture.Load(_gl, path);
        
        Textures.Add(key, loadedTexture);
        
        return loadedTexture;
    }
    
    public static Font? LoadFont(string name, int pixelHeight = 24)
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
            return null;
        }
        
        var font = Font.Load(_gl, path, pixelHeight);
        Fonts[(name, pixelHeight)] = font;
        
        return font;
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
        var key = $"{name}_{type}";
        
        if (Shaders.TryGetValue(key, out var shader))
        {
            return shader;
        }
        
        var path = System.IO.Path.Combine(Path, "Shaders", name + GetShaderFormat(type));
        
        if (!File.Exists(path))
        {
            return null;
        }
        
        var content = File.ReadAllText(path);

        if (string.IsNullOrEmpty(content))
        {
            return null;
        }
        
        var loadedShader = new Shader(_gl, type, content);
        
        Shaders.Add(key, loadedShader);
        
        return loadedShader;
    }
    
    public static Mesh? LoadMesh(string name)
    {
        if (Meshes.TryGetValue(name, out var mesh))
        {
            return mesh;
        }

        var path = System.IO.Path.Combine(Path, "Meshes", name + ".obj");

        if (!File.Exists(path))
        {
            return null;
        }

        var loadedMesh = ObjLoader.Load(_gl, path);
        Meshes.Add(name, loadedMesh);

        return loadedMesh;
    }

    public static GeometryData? LoadGeometry(string name)
    {
        if (Geometries.TryGetValue(name, out var geometry))
        {
            return geometry;
        }
        
        var path = System.IO.Path.Combine(Path, "Geometry", name + ".json");

        if (!File.Exists(path))
        {
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

    public static EntityData? LoadEntity(string name)
    {
        if (Entities.TryGetValue(name, out var entity))
        {
            return entity;
        }
        
        var path = System.IO.Path.Combine(Path, "Entities", name + ".json");

        if (!File.Exists(path))
        {
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
        if (Levels.TryGetValue(name, out var level))
        {
            return level;
        }
        
        var path = System.IO.Path.Combine(Path, "Levels", name + ".map");

        if (!File.Exists(path))
        {
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
    
    public static string[] GetAllGeometryNames()
    {
        var dir = System.IO.Path.Combine(Path, "Geometry");

        if (!Directory.Exists(dir))
        {
            return [];
        }

        return Directory.GetFiles(dir, "*.json")
            .Select(System.IO.Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .ToArray();
    }

    public static string[] GetAllEntityNames()
    {
        var dir = System.IO.Path.Combine(Path, "Entities");

        if (!Directory.Exists(dir))
        {
            return [];
        }

        return Directory.GetFiles(dir, "*.json")
            .Select(System.IO.Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .ToArray();
    }
    
    public static void UnloadAll()
    {
        var resources = Fonts.Values.Cast<IDisposable>()
            .Concat(Shaders.Values).Concat(Textures.Values).Concat(Meshes.Values).ToList();
        if (_whiteTexture != null) resources.Add(_whiteTexture);

        Fonts.Clear();
        Shaders.Clear();
        Textures.Clear();
        Meshes.Clear();
        Geometries.Clear();
        Entities.Clear();
        Levels.Clear();
        _whiteTexture = null;

        Cleanup.Run(resources.Distinct().Select<IDisposable, Action>(resource => resource.Dispose).ToArray());
    }
    internal static Mesh CreateMesh(float[] vertices, uint[] indices)
    {
        return new Mesh(_gl, vertices, indices);
    }
}
