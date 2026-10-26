namespace ProjectShockGL.Rendering;

[Serializable]
public struct Color
{
    public float R { get; set; }
    public float G { get; set; }
    public float B { get; set; }
    public float A { get; set; }
    
    public static Color White => new(1f, 1f, 1f);
    public static Color Black => new(0f, 0f, 0f);
    public static Color Red => new(1f, 0f, 0f);
    public static Color Green => new(0f, 1f, 0f);
    public static Color Blue => new(0f, 0f, 1f);
    public static Color Yellow => new(1f, 1f, 0f);
    public static Color Cyan => new(0f, 1f, 1f);
    public static Color Magenta => new(1f, 0f, 1f);
    public static Color Gray => new(0.5f, 0.5f, 0.5f);
    public static Color Transparent => new(0f, 0f, 0f, 0f);

    public Color()
    {
        R = 0;
        G = 0;
        B = 0;
        A = 0;
    }

    public Color(float r, float g, float b, float a = 1f)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }
    
    public static Color FromBytes(byte r, byte g, byte b, byte a = 255)
    {
        return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
    }
    
    public static Color operator *(Color color, float scalar)
    {
        return new Color(color.R * scalar, color.G * scalar, color.B * scalar, color.A);
    }
    
    public static Color Lerp(Color a, Color b, float t)
    {
        return new Color(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t,
            a.A + (b.A - a.A) * t
        );
    }
}