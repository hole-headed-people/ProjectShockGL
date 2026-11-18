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
    public static Color VeryDarkRed => FromBytes(102, 0, 0);
    public static Color DarkRed => FromBytes(179, 0, 0);
    public static Color Red => new(1f, 0f, 0f);
    public static Color LightRed => FromBytes(255, 102, 102);
    public static Color VeryLightRed => FromBytes(255, 204, 204);

    public static Color VeryDarkOrange => FromBytes(102, 51, 0);
    public static Color DarkOrange => FromBytes(179, 90, 0);
    public static Color Orange => FromBytes(255, 128, 0);
    public static Color LightOrange => FromBytes(255, 179, 102);
    public static Color VeryLightOrange => FromBytes(255, 230, 204);

    public static Color VeryDarkYellow => FromBytes(102, 102, 0);
    public static Color DarkYellow => FromBytes(179, 179, 0);
    public static Color Yellow => new(1f, 1f, 0f);
    public static Color LightYellow => FromBytes(255, 255, 102);
    public static Color VeryLightYellow => FromBytes(255, 255, 204);

    public static Color VeryDarkGreen => FromBytes(0, 102, 0);
    public static Color DarkGreen => FromBytes(0, 179, 0);
    public static Color Green => new(0f, 1f, 0f);
    public static Color LightGreen => FromBytes(102, 255, 102);
    public static Color VeryLightGreen => FromBytes(204, 255, 204);

    public static Color VeryDarkCyan => FromBytes(0, 102, 102);
    public static Color DarkCyan => FromBytes(0, 179, 179);
    public static Color Cyan => new(0f, 1f, 1f);
    public static Color LightCyan => FromBytes(102, 255, 255);
    public static Color VeryLightCyan => FromBytes(204, 255, 255);

    public static Color VeryDarkBlue => FromBytes(0, 0, 102);
    public static Color DarkBlue => FromBytes(0, 0, 179);
    public static Color Blue => new(0f, 0f, 1f);
    public static Color LightBlue => FromBytes(102, 102, 255);
    public static Color VeryLightBlue => FromBytes(204, 204, 255);

    public static Color VeryDarkViolet => FromBytes(51, 0, 102);
    public static Color DarkViolet => FromBytes(90, 0, 179);
    public static Color Violet => FromBytes(128, 0, 255);
    public static Color LightViolet => FromBytes(179, 102, 255);
    public static Color VeryLightViolet => FromBytes(230, 204, 255);

    public static Color Magenta => new(1f, 0f, 1f);
    public static Color VeryDarkGray => FromBytes(51, 51, 51);
    public static Color DarkGray => FromBytes(89, 89, 89);
    public static Color Gray => new(0.5f, 0.5f, 0.5f);
    public static Color LightGray => FromBytes(179, 179, 179);
    public static Color VeryLightGray => FromBytes(230, 230, 230);
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

    public static Color FromDrawing(System.Drawing.Color color)
    {
        return FromBytes(color.R, color.G, color.B, color.A);
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