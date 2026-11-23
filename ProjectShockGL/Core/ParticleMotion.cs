using System.Numerics;

namespace ProjectShockGL.Core;

public readonly record struct ParticleVelocityKey(float Time, Vector3 Velocity);

public static class ParticleMotion
{
    internal static ParticleVelocityKey[] CopyCurve(ParticleVelocityKey[]? keys)
    {
        if(keys==null || keys.Length==0 || keys.Length>128) throw new InvalidDataException("Velocity curve needs 1 to 128 keys.");
        var previous=-1f;
        foreach(var key in keys)
        {
            if(!float.IsFinite(key.Time) || key.Time<0 || key.Time>1 || key.Time<=previous || !Finite(key.Velocity))
                throw new InvalidDataException("Velocity curve needs finite vectors and strictly increasing times from 0 to 1.");
            previous=key.Time;
        }
        return keys.ToArray();
    }

    internal static bool Finite(Vector3 value)=>float.IsFinite(value.X)&&float.IsFinite(value.Y)&&float.IsFinite(value.Z);

    public static Vector3 EvaluateVelocity(IReadOnlyList<ParticleVelocityKey> keys,float time)
    {
        if(time<=keys[0].Time) return keys[0].Velocity;
        for(var i=1;i<keys.Count;i++)
            if(time<keys[i].Time)
                return Vector3.Lerp(keys[i-1].Velocity,keys[i].Velocity,(time-keys[i-1].Time)/(keys[i].Time-keys[i-1].Time));
        return keys[^1].Velocity;
    }

    // Continuous, periodic value noise. Independent fields for the three velocity axes.
    internal static Vector3 Noise(Vector3 point)=>new(
        Value(point),Value(point+new Vector3(37,17,53)),Value(point+new Vector3(101,73,29)));

    private static float Value(Vector3 point)
    {
        point=new Vector3(point.X%1024,point.Y%1024,point.Z%1024);
        var x=(int)MathF.Floor(point.X);var y=(int)MathF.Floor(point.Y);var z=(int)MathF.Floor(point.Z);
        float Smooth(float value)=>value*value*value*(value*(value*6-15)+10);
        var u=Smooth(point.X-x);var v=Smooth(point.Y-y);var w=Smooth(point.Z-z);
        float Hash(int a,int b,int c)
        {
            unchecked
            {
                var hash=(uint)(a&1023)*73856093u^(uint)(b&1023)*19349663u^(uint)(c&1023)*83492791u;
                hash^=hash>>16;hash*=0x7feb352du;hash^=hash>>15;hash*=0x846ca68bu;hash^=hash>>16;
                return (hash&0xffffff)/8388607.5f-1;
            }
        }
        float Lerp(float a,float b,float t)=>a+(b-a)*t;
        return Lerp(Lerp(Lerp(Hash(x,y,z),Hash(x+1,y,z),u),Lerp(Hash(x,y+1,z),Hash(x+1,y+1,z),u),v),
            Lerp(Lerp(Hash(x,y,z+1),Hash(x+1,y,z+1),u),Lerp(Hash(x,y+1,z+1),Hash(x+1,y+1,z+1),u),v),w);
    }
}