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
    private bool _manualPaused;
    private bool _availabilityPaused;
    private bool _playRequested;
    private bool _nativeStarted;
    private float? _resumeOffset;
    
    public AudioClip Clip { get; }
    
    public bool IsDisposed { get; private set; }
    
    internal float EffectiveVolume => _volume * _fade * (Audio.GetGroup(GroupName) ?? Audio.Master).EffectiveVolume;
    internal uint Handle => _handle;
    internal bool DisposeWhenStopped { get; set; }

    internal AudioSource(AudioClip clip)
    {
        Clip = clip;
    }

    public AudioPlaybackState State
    {
        get
        {
            if (!IsDisposed && _handle != 0 && _nativeStarted)
            {
                Audio.RequireThread();
                Audio.Api.GetSourceProperty(_handle, GetSourceInteger.SourceState, out int state);
                
                if ((SourceState)state == SourceState.Stopped)
                {
                    _state = AudioPlaybackState.Stopped;
                    _playRequested = false;
                    _nativeStarted = false;
                    _manualPaused = false;

                    _duration = 0;
                }
            }

            return _state;
        }
    }

    public bool IsPlaying => State == AudioPlaybackState.Playing;
    public bool IsPaused => State == AudioPlaybackState.Paused;

    private bool _reverbEnabled = true;
    public bool ReverbEnabled
    {
        get => _reverbEnabled;
        set
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            Audio.RequireGroupThread();
            if (_reverbEnabled == value) return;
            _reverbEnabled = value;
            Audio.AttachReverb(this);
        }
    }

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
            RefreshGain();
        }
    }

    internal void RefreshGain()
    {
        if (IsDisposed) return;
        ApplyGain();
        SynchronizePlayback();
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
                Audio.AttachReverb(this);
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
            _nativeStarted = false;
            _resumeOffset = null;
            
            _fade = fadeIn > 0 ? 0 : 1;
        }

        SetFade(1, fadeIn, false);
        
        ApplyGain();

        _manualPaused = false;
        _playRequested = true;
        _state = AudioPlaybackState.Playing;
        SynchronizePlayback();
        Audio.CheckError("play source");
    }

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (State == AudioPlaybackState.Stopped) return;
        _manualPaused = true;
        SynchronizePlayback();
    }

    public void Resume()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _manualPaused = false;
        SynchronizePlayback();
    }

    internal void SetAvailabilityPaused(bool paused)
    {
        if (IsDisposed || _availabilityPaused == paused) return;
        _availabilityPaused = paused;
        SynchronizePlayback();
    }

    private void SynchronizePlayback()
    {
        if (!_playRequested || IsDisposed || _handle == 0 || State == AudioPlaybackState.Stopped) return;
        Audio.RequireThread();
        var group = Audio.GetGroup(GroupName) ?? Audio.Master;
        if (_manualPaused || _availabilityPaused || group.EffectivePaused)
        {
            if (_nativeStarted && _state == AudioPlaybackState.Playing) Audio.Api.SourcePause(_handle);
            _state = AudioPlaybackState.Paused;
        }
        else
        {
            if (!_nativeStarted || _state == AudioPlaybackState.Paused)
            {
                if (_resumeOffset is { } offset)
                {
                    Audio.Api.SetSourceProperty(_handle, SourceFloat.SecOffset, offset);
                    _resumeOffset = null;
                }
                Audio.Api.SourcePlay(_handle);
                _nativeStarted = true;
            }
            _state = AudioPlaybackState.Playing;
        }
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
        _playRequested = false;
        _nativeStarted = false;
        _manualPaused = false;
        _resumeOffset = null;
        
        _duration = 0;
        _fade = 0;
        
        ApplyGain();
        if (DisposeWhenStopped) Dispose();
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
        _playRequested = false;
        _nativeStarted = false;
        _manualPaused = false;
        _resumeOffset = null;
        _duration = 0;
        
        PositionProvider = null;
        IsDisposed = true;
        
        Audio.Unregister(this);
    }
}