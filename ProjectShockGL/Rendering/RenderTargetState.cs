using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

// Captures are separate passes, so their GL state must not leak into the caller.
internal sealed class RenderTargetState : IDisposable
{
    private readonly GL _gl;
    private readonly int _draw;
    
    private int _read;
    private int _renderbuffer;
    private int _program;
    private int _vao;
    private int _arrayBuffer;
    private int _activeTexture;
    private int _texture;
    
    private readonly int[] _viewport = new int[4];
    private readonly float[] _clearColor = new float[4];
    private readonly bool _depth, _cull, _blend, _scissor, _depthMask;
    private readonly bool[] _colorMask = new bool[4];

    internal RenderTargetState(GL gl)
    {
        _gl = gl;
        
        _draw = gl.GetInteger(GetPName.DrawFramebufferBinding);
        _read = gl.GetInteger(GetPName.ReadFramebufferBinding);
        _renderbuffer = gl.GetInteger(GetPName.RenderbufferBinding);
        _program = gl.GetInteger(GetPName.CurrentProgram);
        _vao = gl.GetInteger(GetPName.VertexArrayBinding);
        _arrayBuffer = gl.GetInteger(GetPName.ArrayBufferBinding);
        _activeTexture = gl.GetInteger(GetPName.ActiveTexture);
        
        gl.ActiveTexture(TextureUnit.Texture0);
        
        _texture = gl.GetInteger(GetPName.TextureBinding2D);
        
        gl.GetInteger(GetPName.Viewport, _viewport);
        gl.GetFloat(GetPName.ColorClearValue, _clearColor);
        gl.GetBoolean(GetPName.ColorWritemask, _colorMask);
        
        _depthMask = gl.GetBoolean(GetPName.DepthWritemask);
        _depth = gl.IsEnabled(EnableCap.DepthTest);
        _cull = gl.IsEnabled(EnableCap.CullFace);
        _blend = gl.IsEnabled(EnableCap.Blend);
        _scissor = gl.IsEnabled(EnableCap.ScissorTest);
    }

    public void Dispose()
    {
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, (uint)_draw);
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, (uint)_read);
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, (uint)_renderbuffer);
        _gl.Viewport(_viewport[0], _viewport[1], (uint)_viewport[2], (uint)_viewport[3]);
        _gl.ClearColor(_clearColor[0], _clearColor[1], _clearColor[2], _clearColor[3]);
        _gl.ColorMask(_colorMask[0], _colorMask[1], _colorMask[2], _colorMask[3]);
        _gl.DepthMask(_depthMask);
        
        Set(EnableCap.DepthTest, _depth);
        Set(EnableCap.CullFace, _cull);
        Set(EnableCap.Blend, _blend);
        Set(EnableCap.ScissorTest, _scissor);
        
        _gl.UseProgram((uint)_program);
        _gl.BindVertexArray((uint)_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, (uint)_arrayBuffer);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, (uint)_texture);
        _gl.ActiveTexture((TextureUnit)_activeTexture);
    }

    private void Set(EnableCap cap, bool enabled)
    {
        if (enabled)
        {
            _gl.Enable(cap);
        }
        else
        {
            _gl.Disable(cap);
        }
    }
}
