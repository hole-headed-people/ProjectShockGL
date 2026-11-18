using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public class CameraCaptureComponent : CameraComponent
{
    private RenderTexture? _target;
    
    private bool _created;
    
    private int _width = 256;
    private int _height = 192;
    
    private RenderTextureFilter _filter = RenderTextureFilter.Nearest;

    public int Width
    {
        get => _width;
        set => SetResolution(value, _height);
    }

    public int Height
    {
        get => _height;
        set => SetResolution(_width, value);
    }

    public RenderTextureFilter Filter
    {
        get => _filter;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            
            _target?.Resize(_width, _height, value);
            _filter = value;
        }
    }

    public RenderTexture Target
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDestroyed, this);

            if (!_created)
            {
                throw new InvalidOperationException("Add the capture component to an object first.");
            }
            
            return _target ??= Content.CreateRenderTexture(_width, _height, _filter);
        }
    }

    public Texture Texture => Target.Texture;

    public void SetResolution(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
        
        _target?.Resize(width, height, _filter);
        
        _width = width;
        _height = height;
    }

    public override void Create()
    {
        base.Create();
        
        _created = true;
    }

    internal void Capture(World world)
    {
        if (!CanActivate || !Owner.IsEditorVisible)
        {
            return;
        }
        
        Synchronize();
        Target.Capture(world, Camera);
    }

    public override void Destroy()
    {
        try { ReleaseTarget(); }
        finally { base.Destroy(); }
    }

    internal void ReleaseTarget()
    {
        _target?.Dispose();
        _target = null;
    }
}
