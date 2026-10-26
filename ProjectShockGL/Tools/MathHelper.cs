namespace ProjectShockGL.Tools;

public static class MathHelper
{
    public const float Deg2Rad = MathF.PI / 180f;
    public const float Rad2Deg = 180f / MathF.PI;

    public static float ToRadians(float degrees) => degrees * Deg2Rad;
    public static float ToDegrees(float radians) => radians * Rad2Deg;
}