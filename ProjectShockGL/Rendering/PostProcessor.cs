using System.Numerics;
using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

internal sealed class PostProcessor : IDisposable
{
    private readonly GL _gl;

    private uint _framebuffer;
    private uint _color;
    private uint _depth;
    private uint _vao;
    
    private int _width;
    private int _height;
    
    private int _destination;
    private int _readFramebuffer;
    
    private bool _capturing;
    private ShaderProgram _shader = null!;
    private readonly ShaderParameters _parameters = new();

    public PostProcessor(GL gl) => _gl = gl;

    public void Begin(int width, int height)
    {
        if (_capturing)
        {
            throw new InvalidOperationException("A world pass is already active.");
        }

        if (!PostProcessSettings.Enabled || width <= 0 || height <= 0)
        {
            return;
        }

        _shader = PostProcessSettings.Shader ?? Content.LoadShaderProgram("PostProcess");
        
        ObjectDisposedException.ThrowIf(_shader.Handle == 0, _shader);
        
        _parameters.CopyFrom(PostProcessSettings.Parameters);
        
        _destination = _gl.GetInteger(GetPName.DrawFramebufferBinding);
        _readFramebuffer = _gl.GetInteger(GetPName.ReadFramebufferBinding);

        if (_width != width || _height != height)
        {
            Resize(width, height);
        }
        
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        _gl.Viewport(0, 0, (uint)width, (uint)height);
        
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        
        _capturing = true;
    }

    private void Resize(int width, int height)
    {
        uint framebuffer = 0;
        uint color = 0;
        uint depth = 0;
        
        var texture = _gl.GetInteger(GetPName.TextureBinding2D);
        var renderbuffer = _gl.GetInteger(GetPName.RenderbufferBinding);
        
        try
        {
            framebuffer = _gl.GenFramebuffer();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            
            color = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, color);
            
            unsafe
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height,
                    0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            }
            
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, color, 0);
            
            depth = _gl.GenRenderbuffer();
            
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depth);
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)width, (uint)height);
            _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment,
                RenderbufferTarget.Renderbuffer, depth);

            if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
            {
                throw new InvalidOperationException("Cannot create the world postprocessing framebuffer.");
            }

            if (_vao == 0)
            {
                _vao = _gl.GenVertexArray();
            }

            ReleaseTarget();
            
            (_framebuffer, _color, _depth) = (framebuffer, color, depth);
            (_width, _height) = (width, height);
        }
        catch
        {
            if (framebuffer != 0)
            {
                _gl.DeleteFramebuffer(framebuffer);
            }

            if (color != 0)
            {
                _gl.DeleteTexture(color);
            }

            if (depth != 0)
            {
                _gl.DeleteRenderbuffer(depth);
            }
            
            throw;
        }
        finally
        {
            _gl.BindTexture(TextureTarget.Texture2D, (uint)texture);
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, (uint)renderbuffer);
            
            RestoreFramebuffer();
        }
    }

    public void End(float time)
    {
        if (!_capturing)
        {
            return;
        }
        
        _capturing = false;
        
        var depthTest = _gl.IsEnabled(EnableCap.DepthTest);
        var cull = _gl.IsEnabled(EnableCap.CullFace);
        var blend = _gl.IsEnabled(EnableCap.Blend);
        var scissor = _gl.IsEnabled(EnableCap.ScissorTest);
        var program = _gl.GetInteger(GetPName.CurrentProgram);
        var vao = _gl.GetInteger(GetPName.VertexArrayBinding);
        var activeTexture = _gl.GetInteger(GetPName.ActiveTexture);
        
        _gl.ActiveTexture(TextureUnit.Texture0);
        
        var texture = _gl.GetInteger(GetPName.TextureBinding2D);
        
        try
        {
            _gl.Disable(EnableCap.ScissorTest);
            
            _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _framebuffer);
            _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, (uint)_destination);
            _gl.BlitFramebuffer(0, 0, _width, _height, 0, 0, _width, _height,
                ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
            
            _gl.Disable(EnableCap.DepthTest);
            _gl.Disable(EnableCap.CullFace);
            _gl.Disable(EnableCap.Blend);
            
            _gl.UseProgram(_shader.Handle);
            
            _shader.Apply(_parameters);
            
            _shader.Write("uTexture", 0);
            _shader.Write("uTime", time);
            _shader.Write("uScreenSize", new Vector2(_width, _height));
            _shader.Write("uTexelSize", new Vector2(1f / _width, 1f / _height));
            
            _gl.BindTexture(TextureTarget.Texture2D, _color);
            _gl.BindVertexArray(_vao);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }
        finally
        {
            RestoreFramebuffer();
            
            _gl.BindVertexArray((uint)vao);
            _gl.BindTexture(TextureTarget.Texture2D, (uint)texture);
            
            _gl.ActiveTexture((TextureUnit)activeTexture);
            
            _gl.UseProgram((uint)program);
            
            Restore(EnableCap.DepthTest, depthTest);
            Restore(EnableCap.CullFace, cull);
            Restore(EnableCap.Blend, blend);
            Restore(EnableCap.ScissorTest, scissor);
        }
    }

    private void Restore(EnableCap capability, bool enabled)
    {
        if (enabled)
        {
            _gl.Enable(capability);
        }
        else
        {
            _gl.Disable(capability);
        }
    }

    private void RestoreFramebuffer()
    {
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, (uint)_destination);
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, (uint)_readFramebuffer);
    }

    private void ReleaseTarget()
    {
        if (_framebuffer != 0)
        {
            _gl.DeleteFramebuffer(_framebuffer);
        }

        if (_color != 0)
        {
            _gl.DeleteTexture(_color);
        }

        if (_depth != 0)
        {
            _gl.DeleteRenderbuffer(_depth);
        }
        
        _framebuffer = _color = _depth = 0;
    }

    public void Dispose()
    {
        if (_capturing)
        {
            RestoreFramebuffer(); 
            _capturing = false;
        }
        
        ReleaseTarget();

        if (_vao != 0)
        {
            _gl.DeleteVertexArray(_vao);
        }
        
        _vao = 0;
        _width = _height = 0;
    }
}
