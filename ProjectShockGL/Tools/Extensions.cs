using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Tools;

public static class Extensions
{
    public static Vector3 ToVector3(this Color color)
    {
        return new Vector3(color.R, color.G, color.B);
    }

    public static Vector4 ToVector4(this Color color)
    {
        return new Vector4(color.R, color.G, color.B, color.A);
    }
}