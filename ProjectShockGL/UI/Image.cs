using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.UI;

public enum ImageDrawMode { Simple, Sliced }
public enum ImageSliceMode { Stretch, Tile }

public class Image : Widget
{
    private Vector4 _border;
    private float _borderScale = 1;

    public Color Color { get; set; } = Color.White;
    
    public Texture? Texture { get; set; }
    public TextureRegion? Region { get; set; }
    
    public ImageDrawMode DrawMode { get; set; }
    public ImageSliceMode EdgeMode { get; set; }
    public ImageSliceMode CenterMode { get; set; }
    
    public bool FillCenter { get; set; } = true;
    
    public Vector4 Border
    {
        get => _border;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) ||
                !float.IsFinite(value.Z) || !float.IsFinite(value.W) ||
                value.X < 0 || value.Y < 0 || value.Z < 0 || value.W < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            _border = value;
        }
    }

    public float BorderScale
    {
        get => _borderScale;
        set
        {
            if (!float.IsFinite(value) || value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            
            _borderScale = value;
        }
    }

    protected override void OnDraw(Graphics graphics)
    {
        base.OnDraw(graphics);

        var texture = Region?.Texture ?? Texture;
        var uv = Region?.UvRect ?? new Vector4(0, 0, 1, 1);
        
        if (DrawMode == ImageDrawMode.Sliced && texture != null)
        {
            var sourceSize = new Vector2(texture.Width * uv.Z, texture.Height * uv.W);
            
            foreach (var part in GetSlices(Rect.Size, sourceSize, uv))
            {
                graphics.DrawUIRegion(Rect.AbsolutePosition + part.Position, part.Size, texture, part.Uv, Color);
            }
        }
        else if (Region is { } region)
        {
            graphics.DrawUIRegion(Rect.AbsolutePosition, Rect.Size, region.Texture, region.UvRect, Color);
        }
        else
        {
            graphics.DrawUIRect(Rect.AbsolutePosition, Rect.Size, Color, Texture);
        }
    }

    internal IEnumerable<(Vector2 Position, Vector2 Size, Vector4 Uv)> GetSlices(Vector2 size, Vector2 source, Vector4 uv)
    {
        if (size.X <= 0 || size.Y <= 0 || source.X <= 0 || source.Y <= 0)
        {
            yield break;
        }

        var border = Border;
        
        Fit(ref border.X, ref border.Z, source.X);
        Fit(ref border.Y, ref border.W, source.Y);
        
        var scaleX = Math.Min((double)BorderScale, size.X / Math.Max((double)border.X + border.Z, double.Epsilon));
        
        var left = (float)(border.X * scaleX);
        var right = (float)(border.Z * scaleX);
        
        var scaleY = Math.Min((double)BorderScale, size.Y / Math.Max((double)border.Y + border.W, double.Epsilon));
        
        var top = (float)(border.Y * scaleY);
        var bottom = (float)(border.W * scaleY);
        
        Fit(ref left, ref right, size.X);
        Fit(ref top, ref bottom, size.Y);

        float[] x = [0, left, size.X - right, size.X];
        float[] y = [0, top, size.Y - bottom, size.Y];
        float[] sx = [0, border.X, source.X - border.Z, source.X];
        float[] sy = [0, border.Y, source.Y - border.W, source.Y];

        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var center = row == 1 && column == 1;
                
                if (center && !FillCenter)
                {
                    continue;
                }
                
                var width = x[column + 1] - x[column];
                var height = y[row + 1] - y[row];
                
                var sourceWidth = sx[column + 1] - sx[column];
                var sourceHeight = sy[row + 1] - sy[row];

                if (width <= 0 || height <= 0 || sourceWidth <= 0 || sourceHeight <= 0)
                {
                    continue;
                }

                var tile = (center ? CenterMode : EdgeMode) == ImageSliceMode.Tile;
                
                var stepX = tile && column == 1 ? (double)sourceWidth * BorderScale : width;
                var stepY = tile && row == 1 ? (double)sourceHeight * BorderScale : height;
                
                // Malformed/extreme scales must not create an unbounded number of draw calls.
                
                if (Math.Ceiling(width / stepX) * Math.Ceiling(height / stepY) > 16384)
                {
                    stepX = width;
                    stepY = height;
                }
                
                var columns = (int)Math.Ceiling(width / stepX);
                var rows = (int)Math.Ceiling(height / stepY);
                
                for (var iy = 0; iy < rows; iy++)
                {
                    for (var ix = 0; ix < columns; ix++)
                    {
                        var offset = new Vector2((float)(ix * stepX), (float)(iy * stepY));
                        
                        var partSize = new Vector2((float)Math.Min(stepX, width - offset.X), (float)Math.Min(stepY, height - offset.Y));
                        var partUv = new Vector4(
                            uv.X + uv.Z * sx[column] / source.X,
                            uv.Y + uv.W * sy[row] / source.Y,
                            uv.Z * sourceWidth / source.X * partSize.X / (float)stepX,
                            uv.W * sourceHeight / source.Y * partSize.Y / (float)stepY);
                        
                        yield return (new Vector2(x[column], y[row]) + offset, partSize, partUv);
                    }
                }
            }
        }
    }

    private static void Fit(ref float first, ref float second, float available)
    {
        var sum = (double)first + second;
        
        if (sum <= available)
        {
            return;
        }
        
        first = (float)(first / sum * available);
        second = available - first;
    }
}
