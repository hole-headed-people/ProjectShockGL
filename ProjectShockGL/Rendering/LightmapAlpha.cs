using System.Numerics;

namespace ProjectShockGL.Rendering;

// Immutable CPU copy of the alpha channel. No OpenGL access is needed during baking.
internal sealed class LightmapAlpha
{
    private readonly byte[] _alpha;
    private readonly int _width;
    private readonly int _height;
    private readonly bool _repeat;
    private readonly bool _linear;

    internal LightmapAlpha(int width, int height, ReadOnlySpan<byte> rgba, bool repeat, bool linear)
    {
        _width = width;
        _height = height;
        
        _repeat = repeat;
        _linear = linear;
        
        _alpha = new byte[checked(width * height)];

        for (var i = 0; i < _alpha.Length; i++)
        {
            _alpha[i] = rgba[i * 4 + 3];
        }
    }

    internal float Sample(Vector2 uv)
    {
        var u = _repeat ? uv.X - MathF.Floor(uv.X) : Math.Clamp(uv.X, 0, 1);
        var v = _repeat ? uv.Y - MathF.Floor(uv.Y) : Math.Clamp(uv.Y, 0, 1);
        
        float Pixel(int x, int y)
        {
            x = _repeat ? (x % _width + _width) % _width : Math.Clamp(x, 0, _width - 1);
            y = _repeat ? (y % _height + _height) % _height : Math.Clamp(y, 0, _height - 1);
            
            return _alpha[y * _width + x] / 255f;
        }

        if (!_linear)
        {
            return Pixel((int)(u * _width), (int)(v * _height));
        }
        
        var px = u * _width - .5f;
        var py = v * _height - .5f;
        
        var x0 = (int)MathF.Floor(px);
        var y0 = (int)MathF.Floor(py);
        
        var fx = px - x0;
        var fy = py - y0;
        
        var a = Pixel(x0, y0) * (1 - fx) + Pixel(x0 + 1, y0) * fx;
        var b = Pixel(x0, y0 + 1) * (1 - fx) + Pixel(x0 + 1, y0 + 1) * fx;
        
        return a * (1 - fy) + b * fy;
    }
}