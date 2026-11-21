namespace ProjectShockGL.Rendering;

public sealed record LightmapBakeSettings
{
    public int AtlasSize { get; init; } = 1024;
    public int SurfaceSize { get; init; } = 128;
    public int Padding { get; init; } = 2;
    public int SamplesPerTexel { get; init; } = 1;
    public float RayBias { get; init; } = .001f;
    public bool IndirectEnabled { get; init; } = true;
    public int IndirectSamples { get; init; } = 64;
    public float IndirectStrength { get; init; } = 1f;
    public int IndirectFilterRadius { get; init; } = 1;

    public LightmapPreparationOptions CreatePreparationOptions() => new()
    {
        AtlasSize=AtlasSize,SurfaceSize=SurfaceSize,Padding=Padding
    };

    public DirectLightBakeOptions CreateBakeOptions() => new()
    {
        SamplesPerTexel=SamplesPerTexel,RayBias=RayBias,
        IndirectSamples=IndirectEnabled?IndirectSamples:0,
        IndirectStrength=IndirectStrength,IndirectFilterRadius=IndirectFilterRadius
    };

    public void Validate()
    {
        CreatePreparationOptions().Validate();
        CreateBakeOptions().Validate();
        if(IndirectSamples<1 || IndirectSamples>4096)
            throw new ArgumentOutOfRangeException(nameof(IndirectSamples),"Use 1..4096 indirect samples, or disable GI.");
    }
}