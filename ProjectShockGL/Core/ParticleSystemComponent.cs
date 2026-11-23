using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public class ParticleSystemComponent : RenderComponent
{
    private struct Particle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        
        public float Age;
        public float Lifetime;
        public float StartSize;
        public float Rotation;
        public float RotationSpeed;
        public Color StartColor;
        public Quaternion EmissionRotation;
        public Vector3 NoiseSeed;
    }

    private readonly List<Particle> _particles = [];
    
    private double _emission;
    
    private bool _playbackSet;
    
    private float _rate = 10;
    private float _lifetime = 3;
    private float _size = .1f;
    
    private int _maxParticles = 512;
    
    private Vector3 _velocity = Vector3.UnitY;
    private Vector3 _acceleration;
    
    private Mesh? _mesh;
    
    private float[] _vertices = [];
    
    private int _capacity;

    private ParticleRange? _startSize, _startLifetime;
    private ParticleRange _startRotation, _rotationSpeed;
    private ParticleCurveKey[] _sizeCurve = [new(0,1),new(1,1)];
    private ParticleColorKey[] _colorGradient = [new(0,Color.White),new(1,Color.White)];
    private Vector4[] _colors = [];
    public ParticleRange? StartSize
    {
        get => _startSize;
        set { value?.Validate(0); _startSize=value; }
    }
    public ParticleRange? StartLifetime
    {
        get => _startLifetime;
        set { value?.Validate(float.Epsilon); _startLifetime=value; }
    }
    public ParticleRange StartRotation
    {
        get => _startRotation;
        set { value.Validate(float.MinValue); _startRotation=value; }
    }
    public ParticleRange RotationSpeed
    {
        get => _rotationSpeed;
        set { value.Validate(float.MinValue); _rotationSpeed=value; }
    }
    public bool SizeOverLifetime { get; set; }
    public bool ColorOverLifetime { get; set; }
    public ParticleCurveKey[] SizeCurve
    {
        get => _sizeCurve.ToArray();
        set => _sizeCurve=ParticleAppearance.CopyCurve(value);
    }
    public ParticleColorKey[] ColorGradient
    {
        get => _colorGradient.ToArray();
        set => _colorGradient=ParticleAppearance.CopyGradient(value);
    }
    private ParticleBlendMode _blendMode;
    public ParticleBlendMode BlendMode
    {
        get => _blendMode;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(BlendMode));
            _blendMode = value;
        }
    }
    private ParticleEmissionMode _emissionMode;
    private int _burstCount = 10;
    public ParticleEmissionMode EmissionMode
    {
        get => _emissionMode;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(EmissionMode));
            _emissionMode=value;
            _emission=0;
        }
    }
    public int BurstCount
    {
        get => _burstCount;
        set
        {
            if(value<0 || value>100000) throw new ArgumentOutOfRangeException(nameof(BurstCount));
            _burstCount=value;
        }
    }
    private ParticleVelocityKey[] _velocityCurve=[new(0,Vector3.Zero),new(1,Vector3.Zero)];
    private Vector3 _noiseStrength=Vector3.One;
    private float _noiseFrequency=1, _noiseScrollSpeed=1;
    public bool VelocityOverLifetime { get; set; }
    public ParticleVelocityKey[] VelocityCurve
    {
        get=>_velocityCurve.ToArray();
        set=>_velocityCurve=ParticleMotion.CopyCurve(value);
    }
    public bool Noise { get; set; }
    public Vector3 NoiseStrength
    {
        get=>_noiseStrength;
        set
        {
            Finite(value,nameof(NoiseStrength));
            if(value.X<0 || value.Y<0 || value.Z<0) throw new ArgumentOutOfRangeException(nameof(NoiseStrength));
            _noiseStrength=value;
        }
    }
    public float NoiseFrequency
    {
        get=>_noiseFrequency;
        set {Finite(value,0,nameof(NoiseFrequency));_noiseFrequency=value;}
    }
    public float NoiseScrollSpeed
    {
        get=>_noiseScrollSpeed;
        set {Finite(value,0,nameof(NoiseScrollSpeed));_noiseScrollSpeed=value;}
    }
    public Texture? Texture { get; set; }
    public bool PlayOnStart { get; set; } = true;
    public bool IsPlaying { get; private set; }
    public int ParticleCount => _particles.Count;

    public float Rate
    {
        get => _rate;
        set
        {
            Finite(value, 0, nameof(Rate)); 
            _rate = value;
        }
    }

    public float Lifetime
    {
        get => _lifetime;
        set
        {
            Finite(value, float.Epsilon, nameof(Lifetime)); 
            
            _lifetime = value;
        }
    }

    public float Size
    {
        get => _size;
        set
        {
            Finite(value, 0, nameof(Size)); 
            
            _size = value;
        }
    }

    public int MaxParticles
    {
        get => _maxParticles;
        set
        {
            if (value < 0 || value > 100000)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxParticles));
            }
            
            _maxParticles = value;

            if (_particles.Count > value)
            {
                _particles.RemoveRange(value, _particles.Count - value);
            }
        }
    }

    // Local emitter direction; owner rotation is applied at birth. Owner scale is ignored.
    public Vector3 Velocity
    {
        get => _velocity;
        set
        {
            Finite(value, nameof(Velocity)); 
            
            _velocity = value;
        }
    }

    // World-space acceleration, applied to all living particles.
    public Vector3 Acceleration
    {
        get => _acceleration;
        set
        {
            Finite(value, nameof(Acceleration)); 
            
            _acceleration = value;
        }
    }

    private ParticleShape _shape;
    private Vector3 _boxSize = Vector3.One;
    private float _radius = 1;
    private float _coneAngle = 25;

    public ParticleShape Shape
    {
        get => _shape;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(Shape));
            _shape = value;
        }
    }

    public Vector3 BoxSize
    {
        get => _boxSize;
        set
        {
            Finite(value, nameof(BoxSize));
            if (value.X < 0 || value.Y < 0 || value.Z < 0) throw new ArgumentOutOfRangeException(nameof(BoxSize));
            _boxSize = value;
        }
    }

    public float Radius
    {
        get => _radius;
        set { Finite(value, 0, nameof(Radius)); _radius = value; }
    }

    public bool SphereSurface { get; set; }

    // Half-angle from the local -Z cone axis, in degrees.
    public float ConeAngle
    {
        get => _coneAngle;
        set
        {
            Finite(value, 0, nameof(ConeAngle));
            if (value >= 90) throw new ArgumentOutOfRangeException(nameof(ConeAngle));
            _coneAngle = value;
        }
    }

    public void Apply(ParticleAsset asset)
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);
        ArgumentNullException.ThrowIfNull(asset);
        asset.Validate();
        // Resolve resources before changing any live settings.
        var texture = asset.Texture == null ? null : asset.Texture == "WhitePixel" ? Content.GetWhitePixel() :
            Content.LoadTexture(asset.Texture, asset.TextureFormat) ?? throw new FileNotFoundException($"Particle texture '{asset.Texture}' was not found.");
        StartSize=asset.StartSize;
        StartLifetime=asset.StartLifetime;
        StartRotation=asset.StartRotation;
        RotationSpeed=asset.RotationSpeed;
        SizeOverLifetime=asset.SizeOverLifetime;
        ColorOverLifetime=asset.ColorOverLifetime;
        SizeCurve=asset.SizeCurve;
        ColorGradient=asset.ColorGradient;
        Shape = asset.Shape;
        BoxSize = asset.BoxSize;
        Radius = asset.Radius;
        SphereSurface = asset.SphereSurface;
        ConeAngle = asset.ConeAngle;
        PlayOnStart = asset.PlayOnStart;
        Rate = asset.Rate;
        MaxParticles = asset.MaxParticles;
        Lifetime = asset.Lifetime;
        Size = asset.Size;
        Velocity = asset.Velocity;
        Acceleration = asset.Acceleration;
        Offset = asset.Offset;
        Color = asset.Color;
        VelocityOverLifetime=asset.VelocityOverLifetime;
        VelocityCurve=asset.VelocityCurve;
        Noise=asset.Noise;
        NoiseStrength=asset.NoiseStrength;
        NoiseFrequency=asset.NoiseFrequency;
        NoiseScrollSpeed=asset.NoiseScrollSpeed;
        EmissionMode=asset.EmissionMode;
        BurstCount=asset.BurstCount;
        BlendMode = asset.BlendMode;
        RenderOrder = asset.RenderOrder;
        Texture = texture;
        Clear();
    }

    internal (Vector3 Position, Vector3 Velocity) SampleEmission()
    {
        var position = Vector3.Zero;
        var velocity = Velocity;
        float Unit() => System.Random.Shared.NextSingle();
        if (Shape == ParticleShape.Box)
        {
            position = new Vector3(Unit() - .5f, Unit() - .5f, Unit() - .5f) * BoxSize;
        }
        else if (Shape == ParticleShape.Sphere)
        {
            var z = 2 * Unit() - 1;
            var phi = 2 * MathF.PI * Unit();
            var circle = MathF.Sqrt(MathF.Max(0, 1 - z * z));
            var direction = new Vector3(circle * MathF.Cos(phi), circle * MathF.Sin(phi), z);
            var radius = SphereSurface ? Radius : Radius * MathF.Cbrt(Unit());
            position = direction * radius;
        }
        else if (Shape == ParticleShape.Cone)
        {
            var phi = 2 * MathF.PI * Unit();
            var radius = Radius * MathF.Sqrt(Unit());
            position = new Vector3(MathF.Cos(phi) * radius, MathF.Sin(phi) * radius, 0);
            var minCos = MathF.Cos(ConeAngle * MathF.PI / 180);
            var cos = minCos + (1 - minCos) * Unit();
            var sin = MathF.Sqrt(MathF.Max(0, 1 - cos * cos));
            phi = 2 * MathF.PI * Unit();
            velocity = new Vector3(MathF.Cos(phi) * sin, MathF.Sin(phi) * sin, -cos) * Velocity.Length();
        }
        return (position, velocity);
    }
    public ParticleSystemComponent()
    {
        SurfaceMode = SurfaceMode.Transparent;
    }

    public override void Start()
    {
        base.Start();

        if (!_playbackSet && PlayOnStart)
        {
            Play();
        }
    }

    public void Play()
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);
        
        _playbackSet = true;
        
        IsPlaying = true;
        if (EmissionMode == ParticleEmissionMode.Burst)
        {
            if (Owner == null) throw new InvalidOperationException("Add the particle component to an object before playing a burst.");
            var count=Math.Min(BurstCount,MaxParticles-_particles.Count);
            for(var i=0;i<count;i++) _particles.Add(CreateParticle());
            IsPlaying=_particles.Count>0;
        }
    }

    public void Stop()
    {
        _playbackSet = true;
        IsPlaying = false;
        
        _emission = 0;
    }

    public void Clear()
    {
        _particles.Clear();
        _emission = 0;
    }

    public override void Update(float deltaSeconds)
    {
        if (IsDestroyed || !IsEnabled || !Owner.IsActive)
        {
            return;
        }
        
        Finite(deltaSeconds, 0, nameof(deltaSeconds));

        if (deltaSeconds == 0)
        {
            return;
        }

        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var particle = _particles[i];
            
            Advance(ref particle, deltaSeconds);
            
            if (particle.Age >= particle.Lifetime)
            {
                _particles[i] = _particles[^1];
                _particles.RemoveAt(_particles.Count - 1);
            }
            else
            {
                _particles[i] = particle;
            }
        }

        if (EmissionMode == ParticleEmissionMode.Burst)
        {
            if (_particles.Count == 0) IsPlaying=false;
            return;
        }
        if (!IsPlaying || Rate == 0)
        {
            return;
        }
        
        var previous = _emission;
        
        var total = previous + Rate * (double)deltaSeconds;
        var births = Math.Floor(total);
        
        _emission = total - births;
        
        // Only the newest births that can still be alive need simulation. No backlog at capacity.
        
        var count = (int)Math.Min(births, MaxParticles - _particles.Count);
        
        for (var i = 0; i < count; i++)
        {
            var age = (float)((total - (births - i)) / Rate);

            if (age >= (StartLifetime?.Max ?? Lifetime))
            {
                break;
            }
            
            var particle = CreateParticle();
            if (age >= particle.Lifetime) continue;
            Advance(ref particle, age);
            
            _particles.Add(particle);
        }
    }

    private Particle CreateParticle()
    {
        var emission = SampleEmission();
        var particle = new Particle
            {
                Position = Owner.Transform.Position + Vector3.Transform(Offset + emission.Position, Owner.Transform.Rotation),
                Velocity = Vector3.Transform(emission.Velocity, Owner.Transform.Rotation),
                Lifetime = StartLifetime?.Sample() ?? Lifetime,
                StartSize = StartSize?.Sample() ?? Size,
                Rotation = StartRotation.Sample(),
                RotationSpeed = RotationSpeed.Sample(),
                EmissionRotation = Owner.Transform.Rotation,
                NoiseSeed = new Vector3(System.Random.Shared.NextSingle(),System.Random.Shared.NextSingle(),System.Random.Shared.NextSingle())*256,
                StartColor = Color
            };
            
        return particle;
    }

    private void Advance(ref Particle particle, float delta)
    {
        if(!VelocityOverLifetime && !Noise)
        {
            particle.Position += particle.Velocity * delta + Acceleration * (.5f * delta * delta);
            particle.Velocity += Acceleration * delta;
            particle.Age += delta;
            return;
        }
        // Substeps keep the smooth velocity field stable across ordinary frame rates.
        var duration=Math.Min(delta,Math.Max(0,particle.Lifetime-particle.Age));
        var steps=(int)Math.Clamp(Math.Ceiling(duration*60d),1,256);
        var step=duration/steps;
        var endAge=particle.Age+delta;
        for(var i=0;i<steps;i++)
        {
            var age=particle.Age+step*.5f;
            var baseVelocity=particle.Velocity+Acceleration*(step*.5f);
            var added=VelocityOverLifetime ? Vector3.Transform(ParticleMotion.EvaluateVelocity(_velocityCurve,
                Math.Clamp(age/particle.Lifetime,0,1)),particle.EmissionRotation) : Vector3.Zero;
            if(Noise)
            {
                var sample=(particle.Position+baseVelocity*(step*.5f))*NoiseFrequency + particle.NoiseSeed +
                    new Vector3(1,.73f,.37f)*(age*NoiseScrollSpeed);
                added+=ParticleMotion.Noise(sample)*NoiseStrength;
            }
            particle.Position+=(baseVelocity+added)*step;
            particle.Velocity+=Acceleration*step;
            particle.Age+=step;
        }
        particle.Age=endAge;
    }
    public override void Render(RenderQueue queue)
    {
        if (IsDestroyed || _particles.Count == 0 || Texture == null)
        {
            return;
        }
        
        queue.Add(new ParticleCommand(this)
        {
            SurfaceMode = Rendering.SurfaceMode.Transparent,
            RenderOrder = RenderOrder
        });
    }

    private void DrawParticles(Graphics graphics)
    {
        if (IsDestroyed || _particles.Count == 0 || Texture is not { Handle: not 0 } texture)
        {
            return;
        }
        
        var ordered = _particles.OrderByDescending(p => Vector3.DistanceSquared(graphics.CameraPosition, p.Position)).ToArray();
        
        EnsureMesh(ordered.Length);
        
        for (var i = 0; i < ordered.Length; i++)
        {
            var particle = ordered[i];
            var time = Math.Clamp(particle.Age / particle.Lifetime,0,1);
            var size = particle.StartSize * (SizeOverLifetime ? ParticleAppearance.EvaluateSize(_sizeCurve,time) : 1);
            var angle = (particle.Rotation + particle.RotationSpeed * particle.Age) * MathF.PI / 180;
            var model = Matrix4x4.CreateRotationZ(angle) * graphics.BillboardModel(particle.Position, new Vector2(size));
            var tint = ColorOverLifetime ? ParticleAppearance.EvaluateColor(_colorGradient,time) : Color.White;
            var color = particle.StartColor;
            var rgba = new Vector4(color.R*tint.R,color.G*tint.G,color.B*tint.B,color.A*tint.A);
            
            for (var corner = 0; corner < 4; corner++)
            {
                var x = corner is 1 or 2 ? 1f : 0f;
                var y = corner >= 2 ? 1f : 0f;
                
                var position = Vector3.Transform(new Vector3(x - .5f, y - .5f, 0), model);
                var offset = (i * 4 + corner) * 8;
                
                _vertices[offset] = position.X;
                _vertices[offset + 1] = position.Y;
                _vertices[offset + 2] = position.Z;
                
                _vertices[offset + 5] = 1;
                
                _vertices[offset + 6] = x;
                _vertices[offset + 7] = y;
                _colors[i*4+corner] = rgba;
            }
        }
        
        _mesh!.UpdateVertices(_vertices);
        _mesh.UpdateVertexColors(_colors);
        
        _mesh.DrawIndexCount = (uint)ordered.Length * 6;
        
        var parameters = new ShaderParameters();
        parameters.Set("uParticleBlendMode", (int)BlendMode);
        graphics.DrawMesh(_mesh, Vector3.Zero, Quaternion.Identity, Vector3.One, texture, Color.White,
            lit: false, ignoreDepth: false, shader: Content.LoadShaderProgram("Particle", "Particle"), shaderParameters: parameters, backfaceCulling: false, surfaceMode: Rendering.SurfaceMode.Transparent, particleBlendMode: BlendMode);
    }

    private void EnsureMesh(int count)
    {
        if (_mesh is { IsDisposed: false } && _capacity >= count)
        {
            return;
        }
        
        var capacity = Math.Max(count, Math.Min(MaxParticles, Math.Max(16, _capacity * 2)));
        
        var vertices = new float[capacity * 4 * 8];
        var indices = new uint[capacity * 6];
        
        for (var i = 0; i < capacity; i++)
        {
            var vertex = (uint)i * 4;
            var index = i * 6;
            
            indices[index] = vertex;
            indices[index + 1] = vertex + 1;
            indices[index + 2] = vertex + 2;
            indices[index + 3] = vertex;
            indices[index + 4] = vertex + 2;
            indices[index + 5] = vertex + 3;
        }
        
        var mesh = Content.CreateMesh(vertices, indices);
        
        _mesh?.Dispose();
        _mesh = mesh;
        _vertices = vertices;
        _colors = new Vector4[capacity * 4];
        _capacity = capacity;
    }

    public override void Destroy()
    {
        Stop();
        Clear();
        
        _mesh?.Dispose();
        _mesh = null;
        
        _vertices = [];
        _colors = [];
        _capacity = 0;
        
        base.Destroy();
    }

    private static void Finite(float value, float min, string name)
    {
        if (!float.IsFinite(value) || value < min)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void Finite(Vector3 value, string name)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private sealed class ParticleCommand(ParticleSystemComponent component) : RenderCommand
    {
        internal override Vector3 SortPosition
        {
            get
            {
                if (component._particles.Count == 0) return component.Owner.Transform.Position;
                var center = Vector3.Zero;
                foreach (var particle in component._particles) center += particle.Position / component._particles.Count;
                return center;
            }
        }
        
        public override void Execute(Graphics graphics)
        {
            component.DrawParticles(graphics);
        }
    }
}