using Silk.NET.OpenGL;
using StbImageSharp;

namespace ProjectShockGL.Rendering;

public class Texture : IDisposable
{
    private readonly GL _gl;
    
    private LightmapAlpha? _bakeAlpha;
    
    internal bool IsOpaque { get; }
    
    public uint Handle { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    private Texture(GL gl, uint handle, int width, int height, bool isOpaque)
    {
        _gl = gl;

        Handle = handle;
        IsOpaque = isOpaque;
        Width = width;
        Height = height;
    }
    
    internal LightmapAlpha? CaptureBakeAlpha()
    {
        ObjectDisposedException.ThrowIf(Handle == 0, this);
        
        return _bakeAlpha;
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

            return new Texture(gl, handle, image.Width, image.Height, IsFullyOpaque(image.Data))
            {
                _bakeAlpha = IsFullyOpaque(image.Data) ? null : new LightmapAlpha(image.Width, image.Height, image.Data, true, false)
            };
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

            return new Texture(gl, handle, 1, 1, a == 255)
            {
                _bakeAlpha = a == 255 ? null : new LightmapAlpha(1, 1, pixel, false, false)
            };
        }
        catch
        {
            gl.DeleteTexture(handle);
            throw;
        }
    }

    internal static unsafe Texture FromLinearRgb(GL gl, int size, System.Numerics.Vector3[] pixels)
    {
        if (pixels.Length != checked(size * size)) throw new ArgumentException("Lightmap pixels do not match dimensions.");
        var previous = gl.GetInteger(GetPName.TextureBinding2D);
        var handle = gl.GenTexture();
        try
        {
            gl.BindTexture(TextureTarget.Texture2D, handle);
            fixed (System.Numerics.Vector3* data = pixels)
                gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgb32f, (uint)size, (uint)size, 0,
                    PixelFormat.Rgb, PixelType.Float, data);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            return new Texture(gl, handle, size, size, true);
        }
        catch { gl.DeleteTexture(handle); throw; }
        finally { gl.BindTexture(TextureTarget.Texture2D, (uint)previous); }
    }

    internal unsafe LightmapAlbedo CaptureBakeAlbedo()
    {
        ObjectDisposedException.ThrowIf(Handle == 0, this);
        if (Width <= 0 || Height <= 0) throw new InvalidOperationException("Allocate the surface texture before capturing lighting.");
        var previous = _gl.GetInteger(GetPName.TextureBinding2D);
        var packBuffer = _gl.GetInteger(GetPName.PixelPackBufferBinding);
        var alignment = _gl.GetInteger(GetPName.PackAlignment);
        var rowLength = _gl.GetInteger(GetPName.PackRowLength);
        var skipPixels = _gl.GetInteger(GetPName.PackSkipPixels);
        var skipRows = _gl.GetInteger(GetPName.PackSkipRows);
        try
        {
            _gl.BindBuffer(BufferTargetARB.PixelPackBuffer,0);
            _gl.PixelStore(PixelStoreParameter.PackAlignment,1);
            _gl.PixelStore(PixelStoreParameter.PackRowLength,0);
            _gl.PixelStore(PixelStoreParameter.PackSkipPixels,0);
            _gl.PixelStore(PixelStoreParameter.PackSkipRows,0);
            _gl.BindTexture(TextureTarget.Texture2D,Handle);
            var repeat = _gl.GetTexParameterI(GLEnum.Texture2D,GLEnum.TextureWrapS) == (int)GLEnum.Repeat;
            var repeatV = _gl.GetTexParameterI(GLEnum.Texture2D,GLEnum.TextureWrapT) == (int)GLEnum.Repeat;
            var linear = _gl.GetTexParameterI(GLEnum.Texture2D,GLEnum.TextureMagFilter) == (int)GLEnum.Linear;
            var pixels = new byte[checked(Width * Height * 4)];
            fixed(byte* data=pixels) _gl.GetTexImage(TextureTarget.Texture2D,0,PixelFormat.Rgba,PixelType.UnsignedByte,data);
            return new LightmapAlbedo(Width,Height,pixels,repeat,linear,repeatV);
        }
        finally
        {
            _gl.PixelStore(PixelStoreParameter.PackAlignment,alignment);
            _gl.PixelStore(PixelStoreParameter.PackRowLength,rowLength);
            _gl.PixelStore(PixelStoreParameter.PackSkipPixels,skipPixels);
            _gl.PixelStore(PixelStoreParameter.PackSkipRows,skipRows);
            _gl.BindBuffer(BufferTargetARB.PixelPackBuffer,(uint)packBuffer);
            _gl.BindTexture(TextureTarget.Texture2D,(uint)previous);
        }
    }
    internal static Texture CreateRenderTarget(GL gl)
    {
        return new Texture(gl, gl.GenTexture(), 0, 0, true);
    }

    internal unsafe void ResizeRenderTarget(int width, int height, RenderTextureFilter filter)
    {
        _gl.BindTexture(TextureTarget.Texture2D, Handle);
        _gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height,
            0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        var mode = filter == RenderTextureFilter.Nearest ? GLEnum.Nearest : GLEnum.Linear;
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)mode);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)mode);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        Width = width;
        Height = height;
    }
    public void Dispose()
    {
        if (Handle == 0)
        {
            return;
        }

        _gl.DeleteTexture(Handle);

        Handle = 0;
        _bakeAlpha = null;
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

            return new Texture(gl, handle, width, height, IsFullyOpaque(pixels))
            {
                _bakeAlpha = IsFullyOpaque(pixels) ? null : new LightmapAlpha(width, height, pixels, false, true)
            };
        }
        catch
        {
            gl.DeleteTexture(handle);

            throw;
        }
    }
    private static bool IsFullyOpaque(ReadOnlySpan<byte> pixels)
    {
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 255) return false;
        }

        return true;
    }}
