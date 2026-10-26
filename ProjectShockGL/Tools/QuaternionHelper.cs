using System.Numerics;

namespace ProjectShockGL.Tools;

public static class QuaternionHelper
{
    public static Vector3 ToEulerDegrees(Quaternion q)
    {
        q = Quaternion.Normalize(q);
        
        var sinyCosp = 2f * (q.W * q.Y + q.Z * q.X);
        var cosyCosp = 1f - 2f * (q.Y * q.Y + q.X * q.X);
        
        var yaw = MathF.Atan2(sinyCosp, cosyCosp);
        
        var sinp = 2f * (q.W * q.X - q.Y * q.Z);
        sinp = Math.Clamp(sinp, -1f, 1f);
        
        var pitch = MathF.Asin(sinp);
        
        var sinrCosp = 2f * (q.W * q.Z + q.X * q.Y);
        var cosrCosp = 1f - 2f * (q.Z * q.Z + q.X * q.X);
        
        var roll = MathF.Atan2(sinrCosp, cosrCosp);

        if (MathF.Abs(sinp) > 0.9999f)
        {
            roll = 0f;
            yaw = MathF.Atan2(q.Z, q.W) * 2f;
        }
        
        return new Vector3(
            MathHelper.ToDegrees(pitch),
            MathHelper.ToDegrees(yaw),
            MathHelper.ToDegrees(roll)
        );
    }
    
    public static Quaternion FromEulerDegrees(Vector3 eulerDegrees)
    {
        return Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(eulerDegrees.Y),
            MathHelper.ToRadians(eulerDegrees.X), MathHelper.ToRadians(eulerDegrees.Z));
    }
}