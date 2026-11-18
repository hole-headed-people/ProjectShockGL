using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

public enum RenderTextureFilter { Nearest, Linear }

public sealed class RenderTexture : IDisposable
{
    private readonly GL _gl;

    private uint _framebuffer;
    private uint _outputFramebuffer;
    private uint _color; 
    private uint _depth;
    
    private Renderer? _renderer;
    private Graphics? _graphics;
    
    private readonly RenderQueue _queue = new();
    
    private bool _capturing;

    public Texture Texture { get; }
    public int Width => Texture.Width;
    public int Height => Texture.Height;
    public RenderTextureFilter Filter { get; private set; }
    public bool IsDisposed { get; private set; }
    public RenderStatistics Statistics => _graphics?.Statistics ?? default;

    public RenderTexture(GL gl, int width, int height, RenderTextureFilter filter = RenderTextureFilter.Nearest)
    {
        ArgumentNullException.ThrowIfNull(gl);
        
        _gl = gl;
        
        Texture = Texture.CreateRenderTarget(gl);
        
        try
        {
            using var state = new RenderTargetState(gl);
            
            _framebuffer = gl.GenFramebuffer();
            _outputFramebuffer = gl.GenFramebuffer();
            _color = gl.GenRenderbuffer();
            _depth = gl.GenRenderbuffer();
            
            Resize(width, height, filter);
        }
        catch
        {
            Dispose();
            
            throw;
        }
    }

    public void Resize(int width, int height, RenderTextureFilter? filter = null)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_capturing)
        {
            throw new InvalidOperationException("Cannot resize during capture.");
        }
        
        var nextFilter = filter ?? Filter;

        if (!Enum.IsDefined(nextFilter))
        {
            throw new ArgumentOutOfRangeException(nameof(filter));
        }
        
        var limit = Math.Min(_gl.GetInteger(GetPName.MaxTextureSize), _gl.GetInteger(GetPName.MaxRenderbufferSize));
        
        if (width <= 0 || height <= 0 || width > limit || height > limit)
        {
            throw new ArgumentOutOfRangeException(nameof(width), $"RenderTexture dimensions must be between 1 and {limit}.");
        }

        if (width == Width && height == Height && nextFilter == Filter)
        {
            return;
        }

        using var state = new RenderTargetState(_gl);
        
        Texture.ResizeRenderTarget(width, height, nextFilter);
        
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _outputFramebuffer);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, Texture.Handle, 0);
        
        CheckFramebuffer();

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _color);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Rgba8, (uint)width, (uint)height);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            RenderbufferTarget.Renderbuffer, _color);
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depth);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, (uint)width, (uint)height);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
            RenderbufferTarget.Renderbuffer, _depth);
        
        CheckFramebuffer();

        // A target can be shown before its first capture.
        _gl.Disable(EnableCap.ScissorTest);
        _gl.ColorMask(true, true, true, true);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _outputFramebuffer);
        _gl.ClearColor(0, 0, 0, 1);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        
        Filter = nextFilter;
        
        _renderer?.OnResize(width, height);
    }

    public void Capture(World world, Camera camera)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(camera);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ObjectDisposedException.ThrowIf(camera.IsDisposed, camera);

        if (_capturing)
        {
            throw new InvalidOperationException("Recursive RenderTexture capture is not supported.");
        }

        using var state = new RenderTargetState(_gl);
        
        var aspect = camera.AspectRatio;
        
        _capturing = true;
        
        try
        {
            _renderer ??= new Renderer(_gl, Width, Height) { ExcludedTexture = Texture };
            _graphics ??= new Graphics(_renderer);
            
            camera.AspectRatio = (float)Width / Height;
            
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
            _gl.Disable(EnableCap.ScissorTest);
            
            _gl.DepthMask(true);
            _gl.ColorMask(true, true, true, true);
            
            _graphics.BeginDraw(camera);
            
            // No BeginWorld: captures use their own resolution and no global postprocessing.
            
            world.Render(_queue, false);
            
            _queue.Flush(_graphics);
            
            _graphics.EndDraw();

            // Match loaded textures: UV (0,0) is the top-left of the captured image.
            _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _framebuffer);
            _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _outputFramebuffer);
            _gl.BlitFramebuffer(0, 0, Width, Height, 0, Height, Width, 0,
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        }
        finally
        {
            _queue.Clear();
            camera.AspectRatio = aspect;
            _capturing = false;
        }
    }

    private void CheckFramebuffer()
    {
        if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
        {
            throw new InvalidOperationException("RenderTexture framebuffer is incomplete.");
        }
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        if (_capturing)
        {
            throw new InvalidOperationException("Cannot dispose during capture.");
        }
        
        IsDisposed = true;
        
        Cleanup.Run(
            () => _renderer?.Dispose(),
            Texture.Dispose,
            () => { if (_framebuffer != 0) _gl.DeleteFramebuffer(_framebuffer); },
            () => { if (_outputFramebuffer != 0) _gl.DeleteFramebuffer(_outputFramebuffer); },
            () => { if (_color != 0) _gl.DeleteRenderbuffer(_color); },
            () => { if (_depth != 0) _gl.DeleteRenderbuffer(_depth); });
        _framebuffer = _outputFramebuffer = _color = _depth = 0;
    }
}
