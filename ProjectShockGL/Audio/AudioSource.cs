using Silk.NET.OpenAL;

namespace ProjectShockGL.Audio;

public enum AudioPlaybackState
{
    Stopped, 
    Playing, 
    Paused
}

public sealed partial class AudioSource : IDisposable
{
    private uint _handle;
    private float? _appliedGain;
    
    private float _volume = 1;
    private float _fade = 1;
    
    private float _from;
    private float _to;

    private double _elapsed;
    private double _duration;

    private bool _stopAfterFade;
    private bool _loop;
    
    private AudioPlaybackState _state;
    
    public AudioClip Clip { get; }
    
    public bool IsDisposed { get; private set; }
    
    internal float EffectiveVolume => _volume * _fade * (Audio.GetGroup(GroupName) ?? Audio.Master).EffectiveVolume;
    internal uint Handle => _handle;

    internal AudioSource(AudioClip clip)
    {
        Clip = clip;
    }

    public AudioPlaybackState State
    {
        get
        {
            if (!IsDisposed && _handle != 0)
            {
                Audio.RequireThread();
                Audio.Api.GetSourceProperty(_handle, GetSourceInteger.SourceState, out int state);
                
                if ((SourceState)state == SourceState.Stopped)
                {
                    _state = AudioPlaybackState.Stopped;
                    _duration = 0;
                }
            }

            return _state;
        }
    }

    public bool IsPlaying => State == AudioPlaybackState.Playing;
    public bool IsPaused => State == AudioPlaybackState.Paused;

    private string _groupName = "";
    public string GroupName
    {
        get => _groupName;
        set
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            Audio.RequireGroupThread();
            value = value?.Trim() ?? "";
            if (_groupName == value) return;
            _groupName = value;
            ApplyGain();
        }
    }

    internal void RefreshGain()
    {
        if (!IsDisposed) ApplyGain();
    }

    public float Volume
    {
        get => _volume;
        set
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            
            if (!float.IsFinite(value) || value < 0 || value > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(Volume));
            }
            
            if (_volume == value) return;
            _volume = value;
            
            ApplyGain();
        }
    }

    public bool Loop
    {
        get => _loop;
        set
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            
            if (_loop == value) return;
            if (_handle != 0)
            {
                Audio.RequireThread();
                Audio.Api.SetSourceProperty(_handle, SourceBoolean.Looping, value);
            }

            _loop = value;
        }
    }

    public void Play(float fadeIn = 0)
    {
        ValidateDuration(fadeIn);
        
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ObjectDisposedException.ThrowIf(Clip.IsDisposed, Clip);

        if (!Audio.Initialize())
        {
            return;
        }
        
        if (_handle == 0)
        {
            var buffer = Clip.GetBuffer(SpatialMode == AudioSpatialMode.ThreeD);
            
            _handle = Audio.Api.GenSource();
            
            try
            {
                Audio.Api.SetSourceProperty(_handle, SourceInteger.Buffer, buffer);
                Audio.Api.SetSourceProperty(_handle, SourceBoolean.SourceRelative, true);
                Audio.Api.SetSourceProperty(_handle, SourceFloat.RolloffFactor, 0);
                Audio.Api.SetSourceProperty(_handle, SourceBoolean.Looping, Loop);
                
                SynchronizePosition();
                ApplySpatialSettings();
                Audio.CheckError("create audio source");
            }
            catch
            {
                Audio.Api.DeleteSource(_handle);
                
                _handle = 0;
                
                throw;
            }
        }

        var continuing = IsPlaying && fadeIn > 0;
        
        if (!continuing)
        {
            Audio.Api.SourceStop(_handle);
            
            _fade = fadeIn > 0 ? 0 : 1;
        }

        SetFade(1, fadeIn, false);
        
        ApplyGain();

        if (!continuing)
        {
            Audio.Api.SourcePlay(_handle);
        }
        
        Audio.CheckError("play source");
        
        _state = AudioPlaybackState.Playing;
    }

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (!IsPlaying)
        {
            return;
        }
        
        Audio.Api.SourcePause(_handle);
        
        _state = AudioPlaybackState.Paused;
    }

    public void Resume()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (!IsPaused)
        {
            return;
        }
        
        Audio.Api.SourcePlay(_handle);
        
        _state = AudioPlaybackState.Playing;
    }

    public void Stop(float fadeOut = 0)
    {
        ValidateDuration(fadeOut);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (fadeOut > 0 && IsPlaying)
        {
            SetFade(0, fadeOut, true);
        }
        else
        {
            StopNow();
        }
    }

    private void StopNow()
    {
        if (_handle != 0)
        {
            Audio.RequireThread();
            Audio.Api.SourceStop(_handle);
        }

        _state = AudioPlaybackState.Stopped;
        
        _duration = 0;
        _fade = 0;
        
        ApplyGain();
    }

    private void SetFade(float target, float seconds, bool stop)
    {
        _from = _fade;
        _to = target;
        
        _elapsed = 0;
        _duration = seconds;
        
        _stopAfterFade = stop;

        if (seconds == 0)
        {
            _fade = target;
        }
    }

    private void ApplyGain()
    {
        if (_handle == 0)
        {
            return;
        }
        
        Audio.RequireThread();
        var gain = EffectiveVolume;
        if (_appliedGain == gain) return;
        Audio.Api.SetSourceProperty(_handle, SourceFloat.Gain, gain);
        _appliedGain = gain;
    }

    internal void Update(float delta)
    {
        if (IsDisposed || State != AudioPlaybackState.Playing || _duration <= 0)
        {
            return;
        }
        
        _elapsed = Math.Min(_duration, _elapsed + delta);
        _fade = (float)(_from + (_to - _from) * (_elapsed / _duration));
        
        ApplyGain();
        
        if (_elapsed >= _duration)
        {
            _duration = 0;

            if (_stopAfterFade)
            {
                StopNow();
            }
        }
    }

    private static void ValidateDuration(float value)
    {
        if (!float.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Fade duration must be finite and nonnegative.");
        }
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }
        
        if (_handle != 0)
        {
            Audio.RequireThread();
            Audio.Api.SourceStop(_handle);
            Audio.Api.DeleteSource(_handle);
            
            _handle = 0;
        }

        _state = AudioPlaybackState.Stopped;
        _duration = 0;
        
        PositionProvider = null;
        IsDisposed = true;
        
        Audio.Unregister(this);
    }
}