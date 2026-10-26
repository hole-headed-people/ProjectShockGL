using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

public class Shader : IDisposable
{
    private readonly GL _gl;
    
    public uint Handle { get; }
    public ShaderType Type { get; }

    public Shader(GL gl, ShaderType type, string source)
    {
        _gl = gl;
        
        Type = type;
        Handle = _gl.CreateShader(type);
        
        _gl.ShaderSource(Handle, source);
        _gl.CompileShader(Handle);

        _gl.GetShader(Handle, ShaderParameterName.CompileStatus, out var status);
        
        if (status == 0)
        {
            var log = _gl.GetShaderInfoLog(Handle);
            throw new Exception($"{type} compile error: {log}");
        }
    }

    public void Dispose()
    {
        _gl.DeleteShader(Handle);
    }
}