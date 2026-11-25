using System.Numerics;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Placeables;

public enum ReverbZoneShape { Box, Sphere }
public enum ReverbPreset { Room, Hall, Cave, Custom }

public class AudioReverbZone : Entity
{
    private static readonly List<AudioReverbZone> Zones = [];
    private ReverbPreset _preset = ReverbPreset.Room;
    private Vector3 _boxSize = new(10, 5, 10);
    private float _radius = 5;
    private float _strength = .5f, _decayTime = .8f, _decayHFRatio = .7f;
    private float _density = 1, _diffusion = 1, _gainHF = .7f;
    private float _transitionTime = 1;
    private ReverbZoneShape _shape;

    [Editable]
    public ReverbZoneShape Shape
    {
        get => _shape;
        set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); _shape = value; }
    }

    [Editable]
    public Vector3 BoxSize
    {
        get => _boxSize;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z) ||
                value.X <= 0 || value.Y <= 0 || value.Z <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            _boxSize = value;
        }
    }

    [Editable]
    public float Radius { get => _radius; set { Check(value, .001f, float.MaxValue); _radius = value; } }
    [Editable]
    public int Priority { get; set; }
    [Editable]
    public float TransitionTime { get => _transitionTime; set { Check(value, 0, 60); _transitionTime = value; } }

    [Editable]
    public ReverbPreset Preset
    {
        get => _preset;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (value != ReverbPreset.Custom)
            {
                (_strength, _decayTime, _decayHFRatio, _density, _diffusion, _gainHF) = value switch
                {
                    ReverbPreset.Hall => (.65f, 3f, .7f, 1f, 1f, .6f),
                    ReverbPreset.Cave => (.7f, 4f, 1.3f, 1f, .9f, .9f),
                    _ => (.5f, .8f, .7f, 1f, 1f, .7f)
                };
            }
            _preset = value;
        }
    }

    [Editable]
    public float Strength { get => _strength; set => Set(ref _strength, value, 0, 1); }
    [Editable]
    public float DecayTime { get => _decayTime; set => Set(ref _decayTime, value, .1f, 20); }
    [Editable]
    public float DecayHFRatio { get => _decayHFRatio; set => Set(ref _decayHFRatio, value, .1f, 2); }
    [Editable]
    public float Density { get => _density; set => Set(ref _density, value, 0, 1); }
    [Editable]
    public float Diffusion { get => _diffusion; set => Set(ref _diffusion, value, 0, 1); }
    [Editable]
    public float GainHF { get => _gainHF; set => Set(ref _gainHF, value, 0, 1); }

    private static void Check(float value, float min, float max)
    {
        if (!float.IsFinite(value) || value < min || value > max) throw new ArgumentOutOfRangeException(nameof(value));
    }

    private void Set(ref float field, float value, float min, float max)
    {
        Check(value, min, max);
        if (field == value) return;
        field = value;
        _preset = ReverbPreset.Custom;
    }

    public override void Create()
    {
        base.Create();
        Zones.Add(this);
    }

    public override void Destroy()
    {
        Zones.Remove(this);
        base.Destroy();
    }

    internal bool Contains(Vector3 position)
    {
        var delta = position - Transform.Position;
        var scale = Vector3.Abs(Transform.Scale);
        if (Shape == ReverbZoneShape.Sphere)
        {
            var radius = Radius * MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z));
            return delta.LengthSquared() <= radius * radius;
        }
        var local = Vector3.Transform(delta, Quaternion.Inverse(Transform.Rotation));
        var half = BoxSize * scale * .5f;
        return MathF.Abs(local.X) <= half.X && MathF.Abs(local.Y) <= half.Y && MathF.Abs(local.Z) <= half.Z;
    }

    internal static AudioReverbZone? Find(World? world, Vector3 position)
    {
        AudioReverbZone? selected = null;
        if (world == null) return null;
        foreach (var zone in Zones)
        {
            if (zone.IsDestroyed || !zone.IsActive || zone.OwningWorld != world || !zone.Contains(position)) continue;
            if (selected == null || zone.Priority > selected.Priority) selected = zone;
        }
        return selected;
    }

    public override void DebugRenderSelected(DebugDraw draw)
    {
        base.DebugRenderSelected(draw);
        var scale = Vector3.Abs(Transform.Scale);
        if (Shape == ReverbZoneShape.Sphere)
            draw.Sphere(Transform.Position, Radius * MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z)), Color.Yellow, ignoreDepth: true);
        else
            draw.Box(Transform.Position, Transform.Rotation, BoxSize * scale, Color.Yellow, ignoreDepth: true);
    }
}
