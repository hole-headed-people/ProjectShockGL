using ProjectShockGL.Placeables;

namespace ProjectShockGL.Audio;

internal readonly record struct ReverbSettings(float Strength, float DecayTime, float DecayHFRatio, float Density, float Diffusion, float GainHF)
{
    public static ReverbSettings Silent => new(0, .8f, .7f, 1, 1, .7f);
    public static ReverbSettings From(AudioReverbZone zone) => new(zone.Strength, zone.DecayTime, zone.DecayHFRatio, zone.Density, zone.Diffusion, zone.GainHF);
    public static ReverbSettings Lerp(ReverbSettings a, ReverbSettings b, float t) => new(
        Mix(a.Strength,b.Strength,t), Mix(a.DecayTime,b.DecayTime,t), Mix(a.DecayHFRatio,b.DecayHFRatio,t),
        Mix(a.Density,b.Density,t), Mix(a.Diffusion,b.Diffusion,t), Mix(a.GainHF,b.GainHF,t));
    private static float Mix(float a,float b,float t) => a+(b-a)*t;
}

internal sealed class ReverbTransition
{
    private ReverbSettings _from = ReverbSettings.Silent, _target = ReverbSettings.Silent;
    private AudioReverbZone? _zone;
    private double _elapsed;
    private float _duration = 1;
    public ReverbSettings Current { get; private set; } = ReverbSettings.Silent;

    public void Update(AudioReverbZone? zone, float delta)
    {
        var target = zone == null ? _target with { Strength = 0 } : ReverbSettings.From(zone);
        if (!ReferenceEquals(_zone, zone) || target != _target)
        {
            _from = Current;
            _target = target;
            if (zone != null) _duration = zone.TransitionTime;
            _zone = zone;
            _elapsed = 0;
        }
        _elapsed = Math.Min(_duration, _elapsed + delta);
        var t = _duration <= 0 ? 1 : (float)(_elapsed / _duration);
        Current = ReverbSettings.Lerp(_from, _target, t);
    }
}
