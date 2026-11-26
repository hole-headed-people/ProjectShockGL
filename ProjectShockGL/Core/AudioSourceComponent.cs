using AudioSystem = ProjectShockGL.Audio.Audio;
using ProjectShockGL.Audio;

namespace ProjectShockGL.Core;

public class AudioSourceComponent : Component
{
    private AudioClip? _clip;
    private AudioSource? _source;

    private float _volume = 1;

    private bool _loop;
    private bool _enabled = true;

    private bool _explicitPlayback;
    public bool PlayOnStart { get; set; } = true;
    
    public bool IsPlaying => _source?.IsPlaying == true;
    public bool IsPaused => _source?.IsPaused == true;

    private string _groupName = "";
    public string GroupName
    {
        get => _groupName;
        set
        {
            ObjectDisposedException.ThrowIf(IsDestroyed, this);
            value = value?.Trim() ?? "";
            if (_source is { IsDisposed: false }) _source.GroupName = value;
            _groupName = value;
        }
    }

    private bool _reverbEnabled = true;
    public bool ReverbEnabled
    {
        get => _reverbEnabled;
        set
        {
            ObjectDisposedException.ThrowIf(IsDestroyed, this);
            if (_source is { IsDisposed: false }) _source.ReverbEnabled = value;
            _reverbEnabled = value;
        }
    }

    public AudioClip? Clip
    {
        get => _clip;
        set
        {
            ObjectDisposedException.ThrowIf(IsDestroyed, this);

            if (value?.IsDisposed == true)
            {
                throw new ObjectDisposedException(nameof(Clip));
            }

            if (ReferenceEquals(_clip, value))
            {
                return;
            }
            
            _source?.Dispose();
            _source = null;
            
            _clip = value;
        }
    }

    public float Volume
    {
        get => _volume;
        set
        {
            ObjectDisposedException.ThrowIf(IsDestroyed, this);

            if (!float.IsFinite(value) || value < 0 || value > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(Volume));
            }

            if (_source is { IsDisposed: false })
            {
                _source.Volume = value;
            }
            
            _volume = value;
        }
    }

    public bool Loop
    {
        get => _loop;
        set
        {
            ObjectDisposedException.ThrowIf(IsDestroyed, this);

            if (_source is { IsDisposed: false })
            {
                _source.Loop = value;
            }
            
            _loop = value;
        }
    }

    private AudioSpatialMode _spatialMode;
    private float _minDistance = 1;
    private float _maxDistance = 20;

    public AudioSpatialMode SpatialMode
    {
        get => _spatialMode;
        set
        {
            ObjectDisposedException.ThrowIf(IsDestroyed, this);
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (_source is { IsDisposed: false }) _source.SpatialMode = value;
            _spatialMode = value;
        }
    }

    public float MinDistance
    {
        get => _minDistance;
        set
        {
            ObjectDisposedException.ThrowIf(IsDestroyed, this);
            if (!float.IsFinite(value) || value <= 0 || value >= _maxDistance)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_source is { IsDisposed: false }) _source.MinDistance = value;
            _minDistance = value;
        }
    }

    public float MaxDistance
    {
        get => _maxDistance;
        set
        {
            ObjectDisposedException.ThrowIf(IsDestroyed, this);
            if (!float.IsFinite(value) || value <= _minDistance)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_source is { IsDisposed: false }) _source.MaxDistance = value;
            _maxDistance = value;
        }
    }
    public override bool IsEnabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            
            SyncAvailability();
        }
    }

    public override void Start()
    {
        base.Start();

        if (!Application.IsEditor && PlayOnStart && !_explicitPlayback && Clip != null)
        {
            Play();
        }
    }

    public void Play(float fadeIn = 0)
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);

        if (!float.IsFinite(fadeIn) || fadeIn < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fadeIn));
        }
        
        _explicitPlayback = true;

        if (Application.IsEditor || Clip == null)
        {
            return;
        }

        if (_source == null || _source.IsDisposed)
        {
            _source = AudioSystem.CreateSource(Clip);
            _source.PositionProvider = () => Owner.Transform.Position;
            // Set a valid intermediate range before applying the configured distances.
            _source.MaxDistance = Math.Max(20, MaxDistance);
            _source.MinDistance = MinDistance;
            _source.MaxDistance = MaxDistance;
            _source.SpatialMode = SpatialMode;
            _source.SynchronizePosition();
        }
        
        _source.ReverbEnabled = ReverbEnabled;
        _source.GroupName = GroupName;
        _source.Volume = Volume;
        _source.Loop = Loop;
        
        _source.SetAvailabilityPaused(!IsEnabled || !Owner.IsActive);
        _source.Play(fadeIn);
        
        SyncAvailability();
    }

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);
        
        _explicitPlayback = true;

        if (_source is { IsDisposed: false })
        {
            _source.Pause();
        }
    }

    public void Resume()
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);
        
        _explicitPlayback = true;

        if (_source is { IsDisposed: false })
        {
            _source.Resume();
        }
        
        SyncAvailability();
    }

    public void Stop(float fadeOut = 0)
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);

        if (!float.IsFinite(fadeOut) || fadeOut < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fadeOut));
        }
        
        _explicitPlayback = true;

        if (_source is { IsDisposed: false })
        {
            _source.Stop(fadeOut);
        }
    }

    internal override void OnOwnerActiveChanged(bool isActive)
    {
        SyncAvailability();
    }

    private void SyncAvailability()
    {
        if (IsDestroyed || _source is not { IsDisposed: false } || Owner == null)
        {
            return;
        }
        
        _source.SetAvailabilityPaused(!IsEnabled || !Owner.IsActive);
    }

    public override void Destroy()
    {
        _source?.Dispose();
        _source = null;
        
        _clip = null;
        
        base.Destroy();
    }
}