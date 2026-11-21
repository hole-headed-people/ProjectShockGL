using System.Numerics;
using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

internal sealed class LightmapDrawBinding : IDisposable
{
    private readonly GL _gl;
    
    private readonly int _unit;
    private readonly int _texture;

    internal LightmapDrawBinding(GL gl, uint program, Texture? texture, Vector4 region)
    {
        _gl = gl;
        _unit = gl.GetInteger(GetPName.ActiveTexture);
        
        gl.ActiveTexture(TextureUnit.Texture1);
        
        _texture = gl.GetInteger(GetPName.TextureBinding2D);
        
        var enabled = texture is { Handle: not 0 };
        
        gl.BindTexture(TextureTarget.Texture2D, enabled ? texture!.Handle : 0);
        
        gl.Uniform1(gl.GetUniformLocation(program,"uLightmap"),1);
        gl.Uniform1(gl.GetUniformLocation(program,"uLightmapEnabled"),enabled ? 1 : 0);
        gl.Uniform4(gl.GetUniformLocation(program,"uLightmapScaleOffset"),region.X,region.Y,region.Z,region.W);
        
        gl.ActiveTexture((TextureUnit)_unit);
    }

    public void Dispose()
    {
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D,(uint)_texture);
        _gl.ActiveTexture((TextureUnit)_unit);
    }
}