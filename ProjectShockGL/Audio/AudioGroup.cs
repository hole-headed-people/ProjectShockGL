namespace ProjectShockGL.Audio;

public sealed class AudioGroup
{
    private float _volume = 1;
    
    private AudioGroup? _parent;

    public string Name { get; }
    private bool _muted;
    private bool _paused;
    public bool EffectiveMuted => Muted || Parent?.EffectiveMuted == true;
    public bool EffectivePaused => Paused || Parent?.EffectivePaused == true;
    public float EffectiveVolume => Muted ? 0 : Volume * (Parent?.EffectiveVolume ?? 1);

    public bool Muted
    {
        get => _muted;
        set
        {
            Audio.RequireGroupThread();
            Audio.RequireRegisteredGroup(this);
            if (_muted == value) return;
            _muted = value;
            Audio.RefreshGroupVolumes();
        }
    }

    public bool Paused
    {
        get => _paused;
        set
        {
            Audio.RequireGroupThread();
            Audio.RequireRegisteredGroup(this);
            if (_paused == value) return;
            _paused = value;
            Audio.RefreshGroupVolumes();
        }
    }

    internal AudioGroup(string name, AudioGroup? parent)
    {
        Name = name;
        _parent = parent;
    }

    internal void Configure(float volume, AudioGroup? parent)
    {
        _volume = volume;
        _parent = parent;
    }

    public float Volume
    {
        get => _volume;
        set
        {
            Audio.RequireGroupThread();
            Audio.RequireRegisteredGroup(this);
            
            if (!float.IsFinite(value) || value < 0 || value > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (_volume == value)
            {
                return;
            }
            
            _volume = value;
            
            Audio.RefreshGroupVolumes();
        }
    }

    public AudioGroup? Parent
    {
        get => _parent;
        set
        {
            Audio.RequireGroupThread();
            Audio.RequireRegisteredGroup(this);
            
            if (ReferenceEquals(this, Audio.Master))
            {
                if (value != null)
                {
                    throw new InvalidOperationException("Master cannot have a parent.");
                }
                
                return;
            }
            
            value ??= Audio.Master;
            Audio.RequireRegisteredGroup(value);
            
            for (var ancestor = value; ancestor != null; ancestor = ancestor.Parent)
            {
                if (ReferenceEquals(ancestor, this))
                {
                    throw new InvalidOperationException("Audio groups cannot form a cycle.");
                }
            }

            if (ReferenceEquals(_parent, value))
            {
                return;
            }
            
            _parent = value;
            
            Audio.RefreshGroupVolumes();
        }
    }
}
