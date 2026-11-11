namespace ProjectShockGL.Tools;

public static class Random
{
    private const int FloatSteps = 1 << 24;

    public static int Range(int minInclusive, int maxExclusive)
    {
        if (minInclusive <= maxExclusive)
        {
            return System.Random.Shared.Next(minInclusive, maxExclusive);
        }

        return System.Random.Shared.Next(maxExclusive, minInclusive) + 1;
    }

    public static float Range(float minInclusive, float maxInclusive)
    {
        if (!float.IsFinite(minInclusive))
        {
            throw new ArgumentOutOfRangeException(nameof(minInclusive));
        }

        if (!float.IsFinite(maxInclusive))
        {
            throw new ArgumentOutOfRangeException(nameof(maxInclusive));
        }

        if (minInclusive == maxInclusive)
        {
            return minInclusive;
        }

        if (minInclusive > maxInclusive)
        {
            (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        }

        var fraction = System.Random.Shared.Next(FloatSteps + 1) / (double)FloatSteps;
        
        return (float)(minInclusive * (1d - fraction) + maxInclusive * fraction);
    }
}
