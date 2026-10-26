using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

public class Shader : IDisposable
{
    private readonly GL _gl;

    public uint Handle { get; private set; }
    public ShaderType Type { get; }

    public Shader(GL gl, ShaderType type, string source)
    {
        _gl = gl;

        Type = type;
        Handle = _gl.CreateShader(type);

        try
        {
            _gl.ShaderSource(Handle, source);
            _gl.CompileShader(Handle);

            _gl.GetShader(Handle, ShaderParameterName.CompileStatus, out var status);

            if (status == 0)
            {
                var log = _gl.GetShaderInfoLog(Handle);
                
                throw new Exception($"{type} compile error: {log}");
            }
        }
        catch
        {
            Dispose();
            
            throw;
        }
    }

    public void Dispose()
    {
        if (Handle == 0)
        {
            return;
        }
        
        _gl.DeleteShader(Handle);
        
        Handle = 0;
    }
}
