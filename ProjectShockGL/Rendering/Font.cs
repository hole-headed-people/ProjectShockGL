using System.Numerics;
using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

public sealed class Font : IDisposable
{
    public FontAtlas Atlas { get; }
    public Texture Texture { get; }
    public bool IsDisposed => Texture.Handle == 0;

    private Font(FontAtlas atlas, Texture texture)
    {
        Atlas = atlas; 
        Texture = texture;
    }
    public static Font Load(GL gl, string path, int pixelHeight = 24)
    {
        var atlas = FontAtlas.Load(path, pixelHeight);
        
        return new Font(atlas, Texture.FromRgba(gl, atlas.Width, atlas.Height, atlas.Pixels.Span));
    }
    
    public Vector2 MeasureText(string text, float maxWidth = float.PositiveInfinity)
    {
        return Atlas.MeasureText(text, maxWidth);
    }

    public void Dispose()
    {
        Texture.Dispose();
    }
}
