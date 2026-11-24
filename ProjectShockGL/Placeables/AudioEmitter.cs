using ProjectShockGL.Audio;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Placeables;

public class AudioEmitter : Entity
{
    private string _clipName = "";
    private bool _started;

    public AudioSourceComponent Source { get; private set; } = null!;

    [Editable(ContentDirectory = "Audio", ContentPattern = "*.wav")]
    public string ClipName
    {
        get => _clipName;
        set
        {
            value ??= "";
            
            if (_clipName == value)
            {
                return;
            }

            if (_started)
            {
                ApplyClip(value);
            }

            _clipName = value;
        }
    }

    [Editable]
    [AudioGroupName]
    public string GroupName
    {
        get => Source.GroupName;
        set => Source.GroupName = value;
    }

    [Editable]
    public AudioSpatialMode SpatialMode
    {
        get => Source.SpatialMode;
        set => Source.SpatialMode = value;
    }

    [Editable]
    public float Volume
    {
        get => Source.Volume;
        set => Source.Volume = float.IsFinite(value) ? Math.Clamp(value, 0, 1) : throw new ArgumentOutOfRangeException(nameof(value));
    }

    [Editable]
    public bool Loop
    {
        get => Source.Loop;
        set => Source.Loop = value;
    }

    [Editable]
    public bool PlayOnStart
    {
        get => Source.PlayOnStart;
        set => Source.PlayOnStart = value;
    }

    [Editable]
    public float MinDistance
    {
        get => Source.MinDistance;
        set
        {
            if (!float.IsFinite(value) || value <= 0 || value >= float.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            // Keep the range valid when properties are loaded in either order.
            if (value >= Source.MaxDistance)
            {
                Source.MaxDistance = MathF.BitIncrement(value);
            }

            Source.MinDistance = value;
        }
    }

    [Editable]
    public float MaxDistance
    {
        get => Source.MaxDistance;
        set
        {
            if (!float.IsFinite(value) || value <= 0 || value * .5f <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (value <= Source.MinDistance)
            {
                Source.MinDistance = value * .5f;
            }

            Source.MaxDistance = value;
        }
    }

    public override void Create()
    {
        base.Create();

        Source = AddComponent<AudioSourceComponent>();
        Source.SpatialMode = AudioSpatialMode.ThreeD;
    }

    public override void Start()
    {
        ApplyClip(ClipName);
        
        _started = true;

        base.Start();
    }

    public override void DebugRenderSelected(DebugDraw draw)
    {
        base.DebugRenderSelected(draw);
        
        draw.Sphere(Transform.Position, MinDistance, Color.Yellow, true);
        draw.Sphere(Transform.Position, MaxDistance, Color.DarkYellow, true);
    }

    private void ApplyClip(string name)
    {
        Source.Clip = string.IsNullOrWhiteSpace(name) ? null : Content.LoadAudioClip(name)
            ?? throw new FileNotFoundException($"AudioEmitter '{Name}': audio clip '{name}' was not found.");
    }

    [Input("Play")]
    public void Play()
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);

        if (!Application.IsEditor)
        {
            ApplyClip(ClipName);
            Source.Play();
        }
    }

    [Input("Pause")]
    public void Pause()
    {
        Source.Pause();
    }

    [Input("Resume")]
    public void Resume()
    {
        Source.Resume();
    }

    [Input("Stop")]
    public void Stop()
    {
        Source.Stop();
    }
}
