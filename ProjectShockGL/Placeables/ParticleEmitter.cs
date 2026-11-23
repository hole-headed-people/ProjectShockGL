using System.Numerics;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Placeables;

public class ParticleEmitter : Entity
{
    private string _particleAsset = "";
    private string? _appliedAsset;
    
    private bool _started;

    public ParticleSystemComponent Particles { get; private set; } = null!;

    [Editable(ContentDirectory = "Particles")]
    public string ParticleAsset
    {
        get => _particleAsset;
        set
        {
            value ??= "";
            
            if (_particleAsset == value)
            {
                return;
            }

            if (_started)
            {
                ApplyAsset(value);
            }
            
            _particleAsset = value;
        }
    }

    public override void Create()
    {
        base.Create();
        
        Particles = AddComponent<ParticleSystemComponent>();
        Particles.PlayOnStart = false;
    }

    public override void Start()
    {
        ApplyAsset(ParticleAsset);
        
        _started = true;
        
        base.Start();
    }

    public override void DebugRender(DebugDraw draw)
    {
        base.DebugRender(draw);
        
        draw.Line(Transform.Position, Transform.Position + Transform.Forward, Color.Yellow);
    }

    public override void DebugRenderSelected(DebugDraw draw)
    {
        base.DebugRenderSelected(draw);

        if (string.IsNullOrWhiteSpace(ParticleAsset))
        {
            return;
        }
        
        ParticleAsset? asset;
        
        try
        {
            asset = Content.LoadParticle(ParticleAsset);
        }
        catch (Exception error) when (error is IOException or ArgumentException or Newtonsoft.Json.JsonException)
        {
            return;
        }

        if (asset == null)
        {
            return;
        }
        
        foreach (var (from, to) in GetShapeLines(asset))
        {
            draw.Line(from, to, Color.Yellow, ignoreDepth: true, thickness: .012f);
        }
    }

    internal IEnumerable<(Vector3 From, Vector3 To)> GetShapeLines(ParticleAsset asset)
    {
        var lines = new List<(Vector3 From, Vector3 To)>();

        if (asset.Shape == ParticleShape.Box)
        {
            var half = asset.BoxSize * .5f;
            var corners = Enumerable.Range(0, 8).Select(i => new Vector3(
                    (i & 1) == 0 ? -half.X : half.X, (i & 2) == 0 ? -half.Y : half.Y, (i & 4) == 0 ? -half.Z : half.Z))
                .ToArray();
            
            for (var i = 0; i < 8; i++)
            {
                for (var axis = 0; axis < 3; axis++)
                {
                    if ((i & (1 << axis)) == 0)
                    {
                        Line(corners[i], corners[i | (1 << axis)]);
                    }
                }
            }
        }
        else if (asset.Shape == ParticleShape.Sphere)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                for (var i = 0; i < 48; i++)
                {
                    Line(Circle(i * MathF.Tau / 48), Circle((i + 1) * MathF.Tau / 48));
                    
                    continue;

                    Vector3 Circle(float angle) => axis switch
                    {
                        0 => new Vector3(0, MathF.Cos(angle), MathF.Sin(angle)) * asset.Radius,
                        1 => new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)) * asset.Radius,
                        _ => new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0) * asset.Radius
                    };
                }
            }
        }
        else if (asset.Shape == ParticleShape.Cone)
        {
            // This is a direction guide, not a simulation boundary. Match the Particle Editor preview.
            var length = Math.Clamp(asset.Velocity.Length() * (asset.StartLifetime?.Max ?? asset.Lifetime), .5f, 5);
            var spread = length * MathF.Tan(asset.ConeAngle * MathF.PI / 180);

            Vector3 Ring(float angle, float radius, float z) =>
                new(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, z);

            for (var i = 0; i < 48; i++)
            {
                var a = i * MathF.Tau / 48;
                var b = (i + 1) * MathF.Tau / 48;
                Line(Ring(a, asset.Radius, 0), Ring(b, asset.Radius, 0));
                Line(Ring(a, asset.Radius + spread, -length), Ring(b, asset.Radius + spread, -length));
                if (i % 12 == 0) Line(Ring(a, asset.Radius, 0), Ring(a, asset.Radius + spread, -length));
            }

            Line(Vector3.Zero, new Vector3(0, 0, -length));
        }
        else
        {
            Line(new Vector3(-.1f, 0, 0), new Vector3(.1f, 0, 0));
            Line(new Vector3(0, -.1f, 0), new Vector3(0, .1f, 0));
            Line(new Vector3(0, 0, -.1f), new Vector3(0, 0, .1f));
        }

        return lines;

        void Line(Vector3 from, Vector3 to)
        {
            lines.Add((WorldPoint(from), WorldPoint(to)));
        }

        Vector3 WorldPoint(Vector3 point)
        {
            return Transform.Position + Vector3.Transform(point + asset.Offset, Transform.Rotation);
        }
    }
    

    private bool ApplyAsset(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Particles.Stop();
            Particles.Clear();
            
            Particles.PlayOnStart = false;
            
            _appliedAsset = name;
            
            return false;
        }
        
        if (_appliedAsset != name)
        {
            var asset = Content.LoadParticle(name)
                ?? throw new FileNotFoundException($"ParticleEmitter '{Name}': particle asset '{name}' was not found.");
            
            Particles.Apply(asset);
            
            _appliedAsset = name;
        }
        
        return true;
    }

    [Input("Play")]
    public void Play()
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);

        if (!Application.IsEditor && ApplyAsset(ParticleAsset))
        {
            Particles.Play();
        }
    }

    [Input("Stop")]
    public void Stop()
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);
        
        Particles.Stop();
    }

    [Input("Clear")]
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);
        
        Particles.Clear();
    }
}