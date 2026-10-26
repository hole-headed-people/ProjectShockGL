using System.Numerics;
using System.Text;
using static StbTrueTypeSharp.StbTrueType;

namespace ProjectShockGL.Rendering;

public readonly record struct FontGlyph(int Codepoint, Vector2 Offset, Vector2 Size, float Advance, Vector4 UvRect);
public readonly record struct TextGlyph(FontGlyph Glyph, Vector2 Position, int Line);

public sealed class FontAtlas
{
    private readonly Dictionary<int, FontGlyph> _glyphs;
    private readonly Dictionary<(int, int), float> _kerning;
    private readonly byte[] _pixels;
    public int PixelHeight { get; }
    public int Width { get; }
    public int Height { get; }
    public float Ascent { get; }
    public float LineHeight { get; }
    public ReadOnlyMemory<byte> Pixels => _pixels;
    public IReadOnlyDictionary<int, FontGlyph> Glyphs { get; }

    private FontAtlas(int size, int width, int height, float ascent, float lineHeight, byte[] pixels,
        Dictionary<int, FontGlyph> glyphs, Dictionary<(int, int), float> kerning)
    {
        PixelHeight = size;
        Width = width;
        Height = height;
        Ascent = ascent;
        LineHeight = lineHeight;
        _pixels = pixels;
        _glyphs = glyphs;
        Glyphs = new System.Collections.ObjectModel.ReadOnlyDictionary<int, FontGlyph>(glyphs);
        _kerning = kerning;
    }
    
    public static unsafe FontAtlas Load(string path, int pixelHeight = 24)
    {
        if (pixelHeight is < 4 or > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelHeight), "Raster size must be 4..128 pixels.");
        }
        
        var data = File.ReadAllBytes(path);
        
        ValidateTtf(data);
        
        using var info = CreateFont(data, 0) ?? throw new InvalidDataException("Unable to read TTF font.");
        
        var scale = stbtt_ScaleForPixelHeight(info, pixelHeight);
        
        int ascent;
        int descent;
        int gap;
        
        stbtt_GetFontVMetrics(info, &ascent, &descent, &gap);
        
        var codes = Enumerable.Range(32, 95).Concat(Enumerable.Range(160, 96))
            .Concat(Enumerable.Range(0x400, 256)).Concat(Enumerable.Range(0x2010, 24))
            .Concat([0x2116, 0xfffd]).Distinct().ToArray();
        
        var glyphs = new Dictionary<int, FontGlyph>();
        var bitmaps = new List<(int Code, byte[] Pixels, int X, int Y, int W, int H)>();
        
        const int width = 1024;
        
        var x = 1;
        var y = 1;
        
        var rowHeight = 0;
        
        foreach (var code in codes)
        {
            if (stbtt_FindGlyphIndex(info, code) == 0 && code != '?')
            {
                continue;
            }
            
            int advance, bearing, x0, y0, x1, y1;
            
            stbtt_GetCodepointHMetrics(info, code, &advance, &bearing);
            stbtt_GetCodepointBitmapBox(info, code, scale, scale, &x0, &y0, &x1, &y1);
            
            var w = x1 - x0;
            var h = y1 - y0;

            if (w + 2 > width || w < 0 || h < 0 || h > 1024)
            {
                throw new InvalidDataException("Invalid glyph dimensions.");
            }

            if (x + w + 1 > width)
            {
                x = 1; y += rowHeight + 2; 
                rowHeight = 0;
            }
            
            var bitmap = new byte[checked(w * h)];

            if (bitmap.Length > 0)
            {
                fixed (byte* target = bitmap)
                {
                    stbtt_MakeCodepointBitmap(info, target, w, h, w, scale, scale, code);
                }
            }
            
            bitmaps.Add((code, bitmap, x, y, w, h));
            glyphs.Add(code, new FontGlyph(code, new Vector2(x0, y0), new Vector2(w, h), advance * scale,
                new Vector4(x, y, w, h)));
            
            x += w + 2;
            
            rowHeight = Math.Max(rowHeight, h);
        }

        if (!glyphs.ContainsKey('?'))
        {
            throw new InvalidDataException("Font must contain a '?' fallback glyph.");
        }
        
        var height = 1;

        while (height < y + rowHeight + 1)
        {
            height *= 2;
        }

        if (height > 8192)
        {
            throw new InvalidDataException("Font atlas is too large.");
        }

        var rgba = new byte[checked(width * height * 4)];

        for (var i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = rgba[i + 1] = rgba[i + 2] = 255;
        }
        
        foreach (var bitmap in bitmaps)
        {
            for (var row = 0; row < bitmap.H; row++)
            {
                for (var column = 0; column < bitmap.W; column++)
                {
                    rgba[((bitmap.Y + row) * width + bitmap.X + column) * 4 + 3] =
                        bitmap.Pixels[row * bitmap.W + column];
                }
            }
            
            var glyph = glyphs[bitmap.Code];
            glyphs[bitmap.Code] = glyph with { UvRect = glyph.UvRect / new Vector4(width, height, width, height) };
        }
        
        var kern = new Dictionary<(int, int), float>();
        
        foreach (var left in glyphs.Keys)
        {
            foreach (var right in glyphs.Keys)
            {
                var amount = stbtt_GetCodepointKernAdvance(info, left, right);
                
                if (amount != 0)
                {
                    kern.Add((left, right), amount * scale);
                } 
            }
        }
        
        return new FontAtlas(pixelHeight, width, height, ascent * scale, (ascent - descent + gap) * scale, rgba, glyphs, kern);
    }

    private static void ValidateTtf(byte[] data)
    {
        if (data.Length < 12 || System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data) != 0x00010000)
        {
            throw new InvalidDataException("Expected a TrueType .ttf font.");
        }
        
        var count = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4));

        if (count == 0 || 12L + count * 16L > data.Length)
        {
            throw new InvalidDataException("Truncated font directory.");
        }
        
        for (var i = 0; i < count; i++)
        {
            var table = data.AsSpan(12 + i * 16, 16);
            var offset = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(table[8..]);
            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(table[12..]);

            if ((ulong)offset + length > (ulong)data.Length)
            {
                throw new InvalidDataException("Truncated font table.");
            }
        }
    }

    public FontGlyph GetGlyph(int codepoint)
    {
        return _glyphs.TryGetValue(codepoint, out var glyph) ? glyph : _glyphs['?'];
    }

    public float GetKerning(int left, int right)
    {
        return _kerning.GetValueOrDefault((left, right));
    }

    public Vector2 MeasureText(string text, float maxWidth = float.PositiveInfinity)
    {
        return LayoutText(text, maxWidth).Size;
    }
    
    public TextLayout LayoutText(string text, float maxWidth = float.PositiveInfinity)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (float.IsNaN(maxWidth) || maxWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxWidth));
        }

        if (text.Length == 0)
        {
            return new TextLayout([], [], Vector2.Zero);
        }
        
        var runes = text.Replace("\r\n", "\n").Replace('\r', '\n').EnumerateRunes().ToArray();
        var placed = new List<TextGlyph>();
        var widths = new List<float>();
        var pen = 0f;
        var previous = -1;
        var line = 0;

        void NewLine()
        {
            widths.Add(pen); 
            pen = 0; 
            previous = -1; 
            line++;
        }
        
        for (var i = 0; i < runes.Length; i++)
        {
            var code = runes[i].Value;

            if (code == '\n')
            {
                NewLine(); 
                
                continue;
            }

            if (Rune.IsControl(runes[i]) && code != '\t')
            {
                continue;
            }
            
            var white = Rune.IsWhiteSpace(runes[i]);
            
            if (!white && (i == 0 || Rune.IsWhiteSpace(runes[i - 1])))
            {
                var wordWidth = 0f;
                var last = previous;
                
                for (var j = i; j < runes.Length && !Rune.IsWhiteSpace(runes[j]); j++)
                {
                    var g = GetGlyph(runes[j].Value);
                    
                    wordWidth += (last < 0 ? 0 : GetKerning(last, g.Codepoint)) + g.Advance;
                    last = g.Codepoint;
                }

                if (pen > 0 && pen + wordWidth > maxWidth)
                {
                    NewLine();
                }
            }
            
            var glyph = GetGlyph(code == '\t' ? ' ' : code);
            var kern = previous < 0 ? 0 : GetKerning(previous, glyph.Codepoint);
            var advance = code == '\t' ? glyph.Advance * 4 : glyph.Advance;
            
            if (pen > 0 && pen + kern + advance > maxWidth)
            {
                NewLine();
                
                kern = 0;

                if (white)
                {
                    continue;
                }
            }

            if (glyph.Size.X > 0 && glyph.Size.Y > 0 && code != '\t')
            {
                placed.Add(new TextGlyph(glyph, new Vector2(pen + kern, line * LineHeight + Ascent) + glyph.Offset, line));
            }
            
            pen += kern + advance;
            previous = glyph.Codepoint;
        }
        
        widths.Add(pen);
        
        return new TextLayout(placed.AsReadOnly(), widths.AsReadOnly(), new Vector2(widths.Max(), widths.Count * LineHeight));
    }
}

public sealed record TextLayout(IReadOnlyList<TextGlyph> Glyphs, IReadOnlyList<float> LineWidths, Vector2 Size);
