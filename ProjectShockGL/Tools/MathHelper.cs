namespace ProjectShockGL.Tools;

public static class MathHelper
{
    public const float Deg2Rad = MathF.PI / 180f;
    public const float Rad2Deg = 180f / MathF.PI;

    public static float ToRadians(float degrees) => degrees * Deg2Rad;
    public static float ToDegrees(float radians) => radians * Rad2Deg;

    public static System.Numerics.Vector3 MoveTowards(System.Numerics.Vector3 current,
        System.Numerics.Vector3 target, float maxDistanceDelta)
    {
        if (!float.IsFinite(maxDistanceDelta) || maxDistanceDelta < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDistanceDelta));
        }
        
        var delta = target - current;
        var distance = delta.Length();

        if (distance == 0f || distance <= maxDistanceDelta)
        {
            return target;
        }
        
        return current + delta / distance * maxDistanceDelta;
    }
}
