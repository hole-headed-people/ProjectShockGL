using Silk.NET.OpenGL;
using StbImageSharp;

namespace ProjectShockGL.Rendering;

public class Texture : IDisposable
{
    private readonly GL _gl;

    public uint Handle { get; private set; }

    public int Width { get; }
    public int Height { get; }

    private Texture(GL gl, uint handle, int width, int height)
    {
        _gl = gl;

        Handle = handle;
        Width = width;
        Height = height;
    }

    public static unsafe Texture Load(GL gl, string path)
    {
        var handle = gl.GenTexture();
        try
        {
            gl.BindTexture(TextureTarget.Texture2D, handle);

            using var stream = File.OpenRead(path);
            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            fixed (byte* ptr = image.Data)
            {
                gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    InternalFormat.Rgba,
                    (uint)image.Width,
                    (uint)image.Height,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    ptr);
            }

            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);

            return new Texture(gl, handle, image.Width, image.Height);
        }
        catch
        {
            gl.DeleteTexture(handle);
            throw;
        }
    }

    public static unsafe Texture CreateSolid(GL gl, byte r, byte g, byte b, byte a)
    {
        var handle = gl.GenTexture();
        try
        {
            gl.BindTexture(TextureTarget.Texture2D, handle);

            byte[] pixel = [r, g, b, a];

            fixed (byte* ptr = pixel)
            {
                gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    InternalFormat.Rgba,
                    1, 1, 0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    ptr);
            }

            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

            return new Texture(gl, handle, 1, 1);
        }
        catch
        {
            gl.DeleteTexture(handle);
            throw;
        }
    }

    public void Dispose()
    {
        if (Handle == 0)
        {
            return;
        }

        _gl.DeleteTexture(Handle);

        Handle = 0;
    }

    public static unsafe Texture FromRgba(GL gl, int width, int height, ReadOnlySpan<byte> pixels)
    {
        if (width <= 0 || height <= 0 || pixels.Length != checked(width * height * 4))
        {
            throw new ArgumentException("RGBA data must match the texture dimensions.");
        }

        var handle = gl.GenTexture();

        try
        {
            gl.BindTexture(TextureTarget.Texture2D, handle);

            fixed (byte* ptr = pixels)
            {
                gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height,
                    0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            }

            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

            return new Texture(gl, handle, width, height);
        }
        catch
        {
            gl.DeleteTexture(handle);

            throw;
        }
    }
}
