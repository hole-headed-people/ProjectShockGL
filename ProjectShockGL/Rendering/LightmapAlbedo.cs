using System.Numerics;

namespace ProjectShockGL.Rendering;

// Owned snapshot of GPU texture colors, sampled with the texture's current filtering and wrap rules.
internal sealed class LightmapAlbedo
{
    private readonly byte[] _pixels;
    
    private readonly int _width;
    private readonly int _height;
    
    private readonly bool _repeatU;
    private readonly bool _repeatV;
    private readonly bool _linear;

    internal LightmapAlbedo(int width, int height, byte[] pixels, bool repeat, bool linear, bool? repeatV = null)
    {
        _width = width;
        _height = height;
        
        _pixels = pixels;
        
        _repeatU = repeat;
        _repeatV = repeatV ?? repeat;
        _linear = linear;
    }

    internal Vector3 Sample(Vector2 uv)
    {
        var u = _repeatU ? uv.X - MathF.Floor(uv.X) : Math.Clamp(uv.X,0,1);
        var v = _repeatV ? uv.Y - MathF.Floor(uv.Y) : Math.Clamp(uv.Y,0,1);
        
        Vector3 Pixel(int x, int y)
        {
            x = _repeatU ? (x % _width + _width) % _width : Math.Clamp(x,0,_width-1);
            y = _repeatV ? (y % _height + _height) % _height : Math.Clamp(y,0,_height-1);
            
            var at = (y * _width + x) * 4;
            
            return new Vector3(_pixels[at],_pixels[at+1],_pixels[at+2]) / 255f;
        }

        if (!_linear)
        {
            return Pixel((int)(u*_width),(int)(v*_height));
        }
        
        var px=u*_width-.5f; var py=v*_height-.5f;
        
        var x0=(int)MathF.Floor(px); var y0=(int)MathF.Floor(py);
        
        var a=Vector3.Lerp(Pixel(x0,y0),Pixel(x0+1,y0),px-x0);
        var b=Vector3.Lerp(Pixel(x0,y0+1),Pixel(x0+1,y0+1),px-x0);
        
        return Vector3.Lerp(a,b,py-y0);
    }
}