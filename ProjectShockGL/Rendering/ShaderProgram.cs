using System.Numerics;
using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

public sealed class ShaderProgram : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, int> _locations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> _defaults = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (UniformType Type, int Size)> _uniforms = new(StringComparer.Ordinal);
    public uint Handle { get; private set; }

    internal ShaderProgram(GL gl, Shader vertex, Shader fragment)
    {
        _gl = gl;
        
        Handle = gl.CreateProgram();

        try
        {
            gl.AttachShader(Handle, vertex.Handle);
            gl.AttachShader(Handle, fragment.Handle);

            gl.LinkProgram(Handle);

            gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out var status);

            if (status == 0)
            {
                throw new InvalidOperationException($"Shader link error: {gl.GetProgramInfoLog(Handle)}");
            }

            gl.DetachShader(Handle, vertex.Handle);
            gl.DetachShader(Handle, fragment.Handle);

            gl.GetProgram(Handle, ProgramPropertyARB.ActiveUniforms, out var count);

            for (uint i = 0; i < count; i++)
            {
                var name = gl.GetActiveUniform(Handle, i, out var size, out var type);
                _uniforms[name] = (type, size);
            }
        }
        catch
        {
            Dispose(); 
            
            throw;
        }
    }

    internal int Location(string name)
    {
        ObjectDisposedException.ThrowIf(Handle == 0, this);

        if (!_locations.TryGetValue(name, out var location))
        {
            _locations.Add(name, location = _gl.GetUniformLocation(Handle, name));
        }
        
        return location;
    }

    internal void Apply(ShaderParameters? parameters)
    {
        foreach (var pair in _defaults)
        {
            Write(pair.Key, pair.Value);
        }
        
        if (parameters == null)
        {
            return;
        }
        
        foreach (var pair in parameters.Values)
        {
            if (Location(pair.Key) < 0)
            {
                continue;
            }
            
            Validate(pair.Key, pair.Value);

            if (!_defaults.ContainsKey(pair.Key))
            {
                _defaults.Add(pair.Key, ReadDefault(pair.Key, pair.Value));
            }

            if (_defaults[pair.Key].GetType() != pair.Value.GetType())
            {
                throw new ArgumentException($"Uniform '{pair.Key}' must use the same parameter type for every object.");
            }
            
            Write(pair.Key, pair.Value);
        }
    }

    private void Validate(string name, object value)
    {
        var expected = value switch
        {
            float => UniformType.Float,
            int => UniformType.Int,
            Vector2 => UniformType.FloatVec2,
            Vector3 => UniformType.FloatVec3,
            Vector4 => UniformType.FloatVec4,
            Matrix4x4 => UniformType.FloatMat4,
            _ => throw new ArgumentException("Unsupported uniform type.")
        };

        if (!_uniforms.TryGetValue(name, out var uniform) || uniform.Size != 1 ||
            (uniform.Type != expected && !(value is int && uniform.Type == UniformType.Bool)))
        {
            throw new ArgumentException($"Uniform '{name}' does not match the supplied parameter type; arrays are not supported.");
        }
    }

    internal ShaderParameters ConvertParameters(IReadOnlyDictionary<string, object> values)
    {
        ObjectDisposedException.ThrowIf(Handle == 0, this);
        
        var result = new ShaderParameters();
        
        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || IsEngineUniform(pair.Key))
            {
                throw new InvalidDataException($"Postprocess parameter '{pair.Key}' is reserved or invalid.");
            }

            if (Location(pair.Key) < 0)
            {
                continue;
            }

            if (!_uniforms.TryGetValue(pair.Key, out var uniform) || uniform.Size != 1)
            {
                throw new InvalidDataException($"Postprocess parameter '{pair.Key}': uniform arrays are not supported.");
            }
            
            ShaderParameterJson.Add(result, pair.Key, pair.Value, uniform.Type);
        }
        
        return result;
    }

    private unsafe object ReadDefault(string name, object value)
    {
        var location = Location(name);
        
        if (value is int)
        {
            int result;
            _gl.GetUniform(Handle, location, &result);
            
            return result;
        }
        
        var data = stackalloc float[16];
        
        _gl.GetUniform(Handle, location, data);
        
        return value switch
        {
            float => data[0],
            Vector2 => new Vector2(data[0], data[1]),
            Vector3 => new Vector3(data[0], data[1], data[2]),
            Vector4 => new Vector4(data[0], data[1], data[2], data[3]),
            Matrix4x4 => *(Matrix4x4*)data,
            _ => throw new ArgumentException("Unsupported uniform type.")
        };
    }

    internal unsafe void Write(string name, object value)
    {
        var location = Location(name);
        
        switch (value)
        {
            case float f: 
                _gl.Uniform1(location, f); 
                break;
            case int i: 
                _gl.Uniform1(location, i); 
                break;
            case Vector2 v: 
                _gl.Uniform2(location, v.X, v.Y); 
                break;
            case Vector3 v: 
                _gl.Uniform3(location, v.X, v.Y, v.Z); 
                break;
            case Vector4 v: 
                _gl.Uniform4(location, v.X, v.Y, v.Z, v.W); 
                break;
            case Matrix4x4 m: 
                _gl.UniformMatrix4(location, 1, false, (float*)&m); 
                break;
        }
    }

    internal static bool IsEngineUniform(string name)
    {
        return name is
                   "uProjection" or "uView" or "uModel" or "uTexture" or "uColor" or "uUvRect" or
                   "uAlphaCutoff" or "uCameraPos" or "uTime" or "uFogColor" or "uFogNear" or "uFogFar" or
                   "uFogEnabled" or "uForceUnlit" or "uScreenSize" or "uTexelSize" or "uAmbientColor" or "uMainLightEnabled" or
                   "uLightDirection" or "uLightColor" or "uLightIntensity" or "uPointLightCount" ||
               name.StartsWith("uPointLight", StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Handle == 0)
        {
            return;
        }
        
        _gl.DeleteProgram(Handle);
        
        Handle = 0;
    }
}
