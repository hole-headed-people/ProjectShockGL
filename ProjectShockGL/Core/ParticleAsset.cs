using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public enum ParticleShape { Point, Box, Sphere, Cone }
public enum ParticleEmissionMode { Continuous, Burst }

public sealed class ParticleAsset
{
    public int Version { get; init; } = 1;
    
    public string? Texture { get; init; } = "WhitePixel";
    
    [JsonConverter(typeof(StringEnumConverter))]
    public TextureFormat TextureFormat { get; init; } = TextureFormat.Png;
    
    [JsonConverter(typeof(StringEnumConverter))]
    public ParticleShape Shape { get; init; }
    
    public Vector3 BoxSize { get; init; } = Vector3.One;
    
    public float ConeAngle { get; init; } = 25;
    public float Radius { get; init; } = 1;
    
    public bool SphereSurface { get; init; }
    public bool PlayOnStart { get; init; } = true;
    
    [JsonConverter(typeof(StringEnumConverter))]
    public ParticleEmissionMode EmissionMode { get; init; } = ParticleEmissionMode.Continuous;
    public int BurstCount { get; init; } = 10;

    public int MaxParticles { get; init; } = 512;
    
    public float Rate { get; init; } = 10;
    public float Lifetime { get; init; } = 3;
    public float Size { get; init; } = .1f;
    
    public Vector3 Velocity { get; init; } = Vector3.UnitY;
    public Vector3 Acceleration { get; init; }
    public Vector3 Offset { get; init; }
    
    public Color Color { get; init; } = Color.White;
    
    [JsonConverter(typeof(StringEnumConverter))]
    public ParticleBlendMode BlendMode { get; init; } = ParticleBlendMode.Alpha;

    public int RenderOrder { get; init; }

    public ParticleRange? StartSize { get; init; }
    public ParticleRange? StartLifetime { get; init; }
    public ParticleRange StartRotation { get; init; } = new(0,0);
    public ParticleRange RotationSpeed { get; init; } = new(0,0);
    public bool SizeOverLifetime { get; init; }
    public bool ColorOverLifetime { get; init; }
    public ParticleCurveKey[] SizeCurve { get; init; } = [new(0,1),new(1,1)];
    public ParticleColorKey[] ColorGradient { get; init; } = [new(0,Color.White),new(1,Color.White)];
    public bool VelocityOverLifetime { get; init; }
    public ParticleVelocityKey[] VelocityCurve { get; init; } = [new(0,Vector3.Zero),new(1,Vector3.Zero)];
    public bool Noise { get; init; }
    public Vector3 NoiseStrength { get; init; } = Vector3.One;
    public float NoiseFrequency { get; init; } = 1;
    public float NoiseScrollSpeed { get; init; } = 1;
    public void Validate()
    {
        if (!Enum.IsDefined(EmissionMode) || BurstCount < 0 || BurstCount > 100000)
            throw new InvalidDataException("Invalid particle emission mode or burst count.");
        ParticleMotion.CopyCurve(VelocityCurve);
        if(!ParticleMotion.Finite(NoiseStrength) || NoiseStrength.X<0 || NoiseStrength.Y<0 || NoiseStrength.Z<0 ||
            !float.IsFinite(NoiseFrequency) || NoiseFrequency<0 || !float.IsFinite(NoiseScrollSpeed) || NoiseScrollSpeed<0)
            throw new InvalidDataException("Noise settings must be finite and nonnegative.");
        StartSize?.Validate(0);
        StartLifetime?.Validate(float.Epsilon);
        StartRotation.Validate(float.MinValue);
        RotationSpeed.Validate(float.MinValue);
        ParticleAppearance.CopyCurve(SizeCurve);
        ParticleAppearance.CopyGradient(ColorGradient);
        if (Version != 1 || !Enum.IsDefined(TextureFormat) || !Enum.IsDefined(BlendMode))
        {
            throw new InvalidDataException("Unsupported particle asset version or texture format.");
        }

        if (Texture != null)
        {
            DetailAsset.ValidateReference(Texture, nameof(Texture));
        }
        
        _ = new ParticleSystemComponent
        {
            Shape = Shape, BoxSize = BoxSize, Radius = Radius, ConeAngle = ConeAngle,
            Rate = Rate, MaxParticles = MaxParticles, Lifetime = Lifetime, Size = Size,
            Velocity = Velocity, Acceleration = Acceleration
        };

        if (!float.IsFinite(Offset.X) || !float.IsFinite(Offset.Y) || !float.IsFinite(Offset.Z) ||
            !float.IsFinite(Color.R) || !float.IsFinite(Color.G) || !float.IsFinite(Color.B) ||
            !float.IsFinite(Color.A))
        {
            throw new InvalidDataException("Particle offset and color must be finite.");
        }
    }

    public static ParticleAsset Capture(ParticleSystemComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        
        var reference = component.Texture == null ? ((string Name, TextureFormat Format)?)null :
            ReferenceEquals(component.Texture, Content.GetWhitePixel()) ? ("WhitePixel", TextureFormat.Png) :
            Content.GetUITextureReference(component.Texture);
        
        var asset = new ParticleAsset
        {
            Texture = reference?.Name, TextureFormat = reference?.Format ?? TextureFormat.Png,
            Shape = component.Shape, BoxSize = component.BoxSize, Radius = component.Radius,
            SphereSurface = component.SphereSurface, ConeAngle = component.ConeAngle,
            PlayOnStart = component.PlayOnStart, Rate = component.Rate, MaxParticles = component.MaxParticles,
            Lifetime = component.Lifetime, Size = component.Size, Velocity = component.Velocity,
            Acceleration = component.Acceleration, Offset = component.Offset, Color = component.Color,
            StartSize=component.StartSize, StartLifetime=component.StartLifetime,
            StartRotation=component.StartRotation, RotationSpeed=component.RotationSpeed,
            SizeOverLifetime=component.SizeOverLifetime, ColorOverLifetime=component.ColorOverLifetime,
            SizeCurve=component.SizeCurve, ColorGradient=component.ColorGradient,
            EmissionMode = component.EmissionMode, BurstCount = component.BurstCount,
            VelocityOverLifetime=component.VelocityOverLifetime, VelocityCurve=component.VelocityCurve,
            Noise=component.Noise, NoiseStrength=component.NoiseStrength, NoiseFrequency=component.NoiseFrequency, NoiseScrollSpeed=component.NoiseScrollSpeed,
            BlendMode = component.BlendMode,
            RenderOrder = component.RenderOrder
        };
        
        asset.Validate();
        
        return asset;
    }
}