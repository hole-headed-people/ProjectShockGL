using System.Numerics;
using System.Collections.ObjectModel;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public class BrushBox : Object
{
    private Vector3 _size = Vector3.One;
    
    public bool Movable
    {
        get => !IsStatic;
        set
        {
            if (!ConfigureStatic(!value)) return;
            
            if (!value)
            {
                SimulatePhysics = false;
            }
        }
    }

    public bool SimulatePhysics
    {
        get => GetComponent<Rigidbody>() is { IsEnabled: true, IsDestroyed: false };
        set
        {
            if (IsRuntimeStaticLocked)
            {
                return;
            }
            
            if (!value)
            {
                RemoveComponent<Rigidbody>();
                return;
            }

            var body = GetComponent<Rigidbody>();
            
            if (body is { IsDestroyed: true })
            {
                RemoveComponent<Rigidbody>();
                body = null;
            }

            body ??= AddComponent<Rigidbody>();
            body.IsEnabled = true;
            
            Movable = true;
        }
    }
    
    private float _textureScale = 1;
    
    private BoxCollider? _box;
    
    private Mesh? _mesh;
    
    private Vector3 _meshSize;
    private Vector3 _meshScale;
    
    private float _meshTextureScale;
    
    private readonly Dictionary<BrushFace, BrushFaceData> _faces = [];
    private readonly HashSet<(string Name, TextureFormat Format)> _failedTextures = [];
    
    private FaceBatch[] _faceBatches = [];
    
    private int _facesVersion;
    private int _meshFacesVersion = -1;
    
    private (string Name, TextureFormat Format) _meshTexture;

    public IReadOnlyDictionary<BrushFace, BrushFaceData> Faces { get; }

    private sealed record FaceBatch(Mesh Mesh, (string Name, TextureFormat Format) Texture);

    public BrushBox()
    {
        Faces = new ReadOnlyDictionary<BrushFace, BrushFaceData>(_faces);
    }

    public void SetFace(BrushFace face, BrushFaceData? settings)
    {
        if (!Enum.IsDefined(face))
        {
            throw new ArgumentOutOfRangeException(nameof(face));
        }
        
        settings?.Validate();

        if (settings == null)
        {
            _faces.Remove(face);
        }
        else
        {
            _faces[face] = settings with { };
        }
        
        _facesVersion++;
        _failedTextures.Clear();
    }

    public void SetFaces(IReadOnlyDictionary<BrushFace, BrushFaceData> faces)
    {
        ArgumentNullException.ThrowIfNull(faces);
        
        var copy = faces.ToArray();
        
        foreach (var (face, settings) in copy)
        {
            if (!Enum.IsDefined(face) || settings == null)
            {
                throw new InvalidDataException("Invalid BrushBox face settings.");
            }
            
            settings.Validate();
        }
        
        _faces.Clear();

        foreach (var (face, settings) in copy)
        {
            _faces.Add(face, settings with { });
        }
        
        _facesVersion++;
        _failedTextures.Clear();
    }

    public Dictionary<BrushFace, BrushFaceData> CaptureFaces()
    {
        return _faces.ToDictionary(pair => pair.Key, pair => pair.Value with { });
    }
    
    public Vector3 Size
    {
        get => _size;
        set
        {
            if (IsRuntimeStaticLocked)
            {
                return;
            }
            
            BrushBoxMesh.Validate(value, nameof(Size));
            
            _size = value;
            
            if (_box != null)
            {
                _box.HalfExtents = value * 0.5f;
            }
        }
    }

    public string TextureName { get; set; } = "WhitePixel";
    
    public TextureFormat TextureFormat { get; set; } = TextureFormat.Jpeg;
    public Color Color { get; set; } = Color.White;
    
    public bool Lit { get; set; } = true;
    
    public float TextureScale
    {
        get => _textureScale;
        set
        {
            if (!float.IsFinite(value) || value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(TextureScale));
            }
            
            _textureScale = value;
        }
    }

    public override void Create()
    {
        base.Create();
        
        _box = AddComponent<BoxCollider>();
        _box.HalfExtents = Size * 0.5f;
        
        AddComponent<BrushRenderer>();
        
        IsStatic = !SimulatePhysics;
    }

    private void Prepare()
    {
        var textureKey = (TextureName, TextureFormat);

        if (_mesh != null && _meshSize == Size && _meshScale == Transform.Scale &&
            _meshTextureScale == TextureScale && _meshFacesVersion == _facesVersion && _meshTexture == textureKey)
        {
            return;
        }

        var data = BrushBoxMesh.Build(Size, Transform.Scale, TextureScale, _faces);
        var created = new List<Mesh>();
        
        try
        {
            var fullMesh = Content.CreateMesh(data.Vertices, data.Indices);
            created.Add(fullMesh);
            
            var groups = Enumerable.Range(0, 6).GroupBy(index =>
            {
                _faces.TryGetValue((BrushFace)index, out var settings);
                return BrushFaceData.GetTexture(settings, TextureName, TextureFormat);
            }).ToArray();
            
            var batches = groups.Select(group =>
            {
                var mesh = groups.Length == 1 ? fullMesh : Content.CreateMesh(data.Vertices,
                    group.SelectMany(index => data.Indices.Skip(index * 6).Take(6)).ToArray());
                if (groups.Length != 1)
                {
                    created.Add(mesh);
                }
                
                return new FaceBatch(mesh, group.Key);
            }).ToArray();

            ReleaseMeshes();
            
            _mesh = fullMesh;
            _faceBatches = batches;
            
            _meshSize = Size;
            _meshScale = Transform.Scale;
            _meshTextureScale = TextureScale;
            _meshFacesVersion = _facesVersion;
            
            _meshTexture = textureKey;
            
            _failedTextures.Clear();
        }
        catch
        {
            Cleanup.Run(created.Select<Mesh, Action>(mesh => mesh.Dispose).ToArray());
            
            throw;
        }
    }

    private Texture ResolveTexture((string Name, TextureFormat Format) key)
    {
        if (Application.IsEditor && _failedTextures.Contains(key))
        {
            return Content.GetWhitePixel();
        }
        
        try
        {
            return key.Name == "WhitePixel" ? Content.GetWhitePixel() : Content.LoadTexture(key.Name, key.Format)
                ?? throw new FileNotFoundException($"BrushBox texture '{key.Name}' ({key.Format}) was not found.");
        }
        catch (Exception error) when (Application.IsEditor)
        {
            Log.PrintWarning($"BrushBox '{Name}', texture '{key.Name}': {error.Message}");
            
            _failedTextures.Add(key);
            
            return Content.GetWhitePixel();
        }
    }

    private void ReleaseMeshes()
    {
        var owned = _faceBatches.Select(batch => batch.Mesh).Append(_mesh).OfType<Mesh>().Distinct().ToArray();
        
        _faceBatches = [];
        _mesh = null;
        
        Cleanup.Run(owned.Select<Mesh, Action>(mesh => mesh.Dispose).ToArray());
    }
    public override void Destroy()
    {
        try
        {
            base.Destroy();
        }
        finally
        {
            if (IsDestroyed)
            {
                ReleaseMeshes();
            }
        }
    }

    private sealed class BrushRenderer : MeshComponent
    {
        public override void Render(RenderQueue queue)
        {
            var brush = (BrushBox)Owner;
            brush.Prepare();
            
            try
            {
                foreach (var batch in brush._faceBatches)
                {
                    Mesh = batch.Mesh;
                    Texture = brush.ResolveTexture(batch.Texture);
                    Color = brush.Color;
                    Lit = brush.Lit;
                    
                    base.Render(queue);
                }
            }
            finally
            {
                Mesh = brush._mesh;
            }
        }
    }
}
