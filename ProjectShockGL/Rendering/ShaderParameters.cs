using System.Numerics;

namespace ProjectShockGL.Rendering;

public sealed class ShaderParameters
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);
    internal IEnumerable<KeyValuePair<string, object>> Values => _values;

    public void Set(string name, float value)
    {
        Store(name, value);
    }

    public void Set(string name, int value)
    {
        Store(name, value);
    }

    public void Set(string name, bool value)
    {
        Store(name, value ? 1 : 0);
    }

    public void Set(string name, Vector2 value)
    {
        Store(name, value);
    }

    public void Set(string name, Vector3 value)
    {
        Store(name, value);
    }

    public void Set(string name, Vector4 value)
    {
        Store(name, value);
    }

    public void Set(string name, Matrix4x4 value)
    {
        Store(name, value);
    }

    public void Set(string name, Color value)
    {
        Set(name, new Vector4(value.R, value.G, value.B, value.A));
    }

    public bool Remove(string name)
    {
        return _values.Remove(name);
    }

    public void Clear()
    {
        _values.Clear();
    }

    private void Store(string name, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        
        if (ShaderProgram.IsEngineUniform(name))
        {
            throw new ArgumentException($"'{name}' is supplied by the renderer.", nameof(name));
        }
        
        _values[name] = value;
    }

    internal ShaderParameters Snapshot()
    {
        var copy = new ShaderParameters();
        
        foreach (var pair in _values)
        {
            copy._values.Add(pair.Key, pair.Value);
        }
        
        return copy;
    }

    internal void CopyFrom(ShaderParameters source)
    {
        if (ReferenceEquals(this, source))
        {
            return;
        }
        
        _values.Clear();

        foreach (var pair in source._values)
        {
            _values.Add(pair.Key, pair.Value);
        }
    }
}
