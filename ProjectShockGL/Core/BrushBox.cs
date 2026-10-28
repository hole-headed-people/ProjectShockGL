using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public class BrushBox : Object
{
    private Vector3 _size = Vector3.One;
    public bool Movable
    {
        get => !IsStatic;
        set => ConfigureStatic(!value);
    }
    
    private float _textureScale = 1;
    
    private BoxCollider? _box;
    
    private Mesh? _mesh;
    
    private Vector3 _meshSize;
    private Vector3 _meshScale;
    
    private float _meshTextureScale;
    private (string Name, TextureFormat Format)? _failedTexture;
    
    public Vector3 Size
    {
        get => _size;
        set
        {
            BrushBoxMesh.Validate(value, nameof(Size));
            _size = value;
            if (_box != null) _box.HalfExtents = value * 0.5f;
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
        
        IsStatic = true;
    }

    private void Prepare(BrushRenderer renderer)
    {
        if (_mesh == null || _meshSize != Size || _meshScale != Transform.Scale || _meshTextureScale != TextureScale)
        {
            var data = BrushBoxMesh.Build(Size, Transform.Scale, TextureScale);
            var mesh = Content.CreateMesh(data.Vertices, data.Indices);
            
            _mesh?.Dispose();
            
            _mesh = mesh;
            
            _meshSize = Size;
            _meshScale = Transform.Scale;
            
            _meshTextureScale = TextureScale;
        }
        
        renderer.Mesh = _mesh;
        
        var textureKey = (TextureName, TextureFormat);
        
        if (_failedTexture != textureKey)
        {
            try
            {
                renderer.Texture = string.IsNullOrWhiteSpace(TextureName) || TextureName == "WhitePixel"
                    ? Content.GetWhitePixel()
                    : Content.LoadTexture(TextureName, TextureFormat)
                        ?? throw new FileNotFoundException($"BrushBox texture '{TextureName}' ({TextureFormat}) was not found.");
                
                _failedTexture = null;
            }
            catch (Exception error) when (Application.IsEditor)
            {
                Console.Error.WriteLine($"BrushBox '{Name}', texture '{TextureName}': {error.Message}");
                renderer.Texture = Content.GetWhitePixel();
                
                _failedTexture = textureKey;
            }
        }
        
        renderer.Color = Color;
        renderer.Lit = Lit;
    }

    public override void Destroy()
    {
        try
        {
            base.Destroy();
        }
        finally
        {
            _mesh?.Dispose(); 
            _mesh = null;
        }
    }

    private sealed class BrushRenderer : MeshComponent
    {
        public override void Render(RenderQueue queue)
        {
            ((BrushBox)Parent).Prepare(this);
            
            base.Render(queue);
        }
    }
}
