using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public readonly record struct ParticleRange(float Min, float Max)
{
    internal void Validate(float minimum)
    {
        if (!float.IsFinite(Min) || !float.IsFinite(Max) || Min < minimum || Max < Min)
            throw new InvalidDataException("Particle range requires finite Min <= Max within its allowed bounds.");
    }
    internal float Sample() => Tools.Random.Range(Min, Max);
}

public readonly record struct ParticleCurveKey(float Time, float Value);
public readonly record struct ParticleColorKey(float Time, Color Color);

public static class ParticleAppearance
{
    internal static ParticleCurveKey[] CopyCurve(ParticleCurveKey[]? keys)
    {
        if (keys == null || keys.Length == 0 || keys.Length > 128) throw new InvalidDataException("Size curve needs 1 to 128 keys.");
        ValidateTimes(keys.Select(key => key.Time));
        if (keys.Any(key => !float.IsFinite(key.Value) || key.Value < 0)) throw new InvalidDataException("Size curve values must be finite and nonnegative.");
        return keys.ToArray();
    }

    internal static ParticleColorKey[] CopyGradient(ParticleColorKey[]? keys)
    {
        if (keys == null || keys.Length == 0 || keys.Length > 128) throw new InvalidDataException("Color gradient needs 1 to 128 keys.");
        ValidateTimes(keys.Select(key => key.Time));
        if (keys.Any(key => !float.IsFinite(key.Color.R) || !float.IsFinite(key.Color.G) || !float.IsFinite(key.Color.B) || !float.IsFinite(key.Color.A)))
            throw new InvalidDataException("Gradient colors must be finite.");
        return keys.ToArray();
    }

    private static void ValidateTimes(IEnumerable<float> times)
    {
        var previous = -1f;
        foreach (var time in times)
        {
            if (!float.IsFinite(time) || time < 0 || time > 1 || time <= previous)
                throw new InvalidDataException("Particle key times must be strictly increasing from 0 to 1.");
            previous = time;
        }
    }

    public static float EvaluateSize(IReadOnlyList<ParticleCurveKey> keys, float time)
    {
        if (time <= keys[0].Time) return keys[0].Value;
        for (var i = 1; i < keys.Count; i++)
            if (time < keys[i].Time)
            {
                var t = (time - keys[i-1].Time) / (keys[i].Time - keys[i-1].Time);
                return keys[i-1].Value * (1-t) + keys[i].Value * t;
            }
        return keys[^1].Value;
    }

    public static Color EvaluateColor(IReadOnlyList<ParticleColorKey> keys, float time)
    {
        if (time <= keys[0].Time) return keys[0].Color;
        for (var i = 1; i < keys.Count; i++)
            if (time < keys[i].Time)
            {
                var t = (time - keys[i-1].Time) / (keys[i].Time - keys[i-1].Time);
                var a=keys[i-1].Color; var b=keys[i].Color;
                return new Color(a.R*(1-t)+b.R*t,a.G*(1-t)+b.G*t,a.B*(1-t)+b.B*t,a.A*(1-t)+b.A*t);
            }
        return keys[^1].Color;
    }
}