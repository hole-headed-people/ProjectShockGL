using System.Numerics;
using Silk.NET.OpenAL;

namespace ProjectShockGL.Audio;

public enum AudioSpatialMode
{
    TwoD,
    ThreeD
}

public sealed partial class AudioSource
{
    private bool _spatialApplied;
    private AudioSpatialMode _appliedMode;
    private Vector3 _appliedPosition;
    private float _appliedMinDistance;
    private float _appliedMaxDistance;
    internal int SpatialWriteCount { get; private set; }
    private AudioSpatialMode _spatialMode;
    private Vector3 _position;
    private float _minDistance = 1;
    private float _maxDistance = 20;
    internal Func<Vector3>? PositionProvider { get; set; }

    public AudioSpatialMode SpatialMode
    {
        get => _spatialMode;
        set
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (_spatialMode == value) return;
            if (_handle != 0)
            {
                Audio.RequireThread();
                var buffer = Clip.GetBuffer(value == AudioSpatialMode.ThreeD);
                Audio.Api.GetSourceProperty(_handle, GetSourceInteger.Buffer, out int current);
                if ((uint)current != buffer)
                {
                    var state = State;
                    Audio.Api.GetSourceProperty(_handle, SourceFloat.SecOffset, out float offset);
                    Audio.Api.SourceStop(_handle);
                    Audio.Api.SetSourceProperty(_handle, SourceInteger.Buffer, buffer);
                    if (state != AudioPlaybackState.Stopped)
                    {
                        Audio.Api.SetSourceProperty(_handle, SourceFloat.SecOffset, offset);
                        Audio.Api.SourcePlay(_handle);
                        if (state == AudioPlaybackState.Paused) Audio.Api.SourcePause(_handle);
                    }
                }
            }
            _spatialMode = value;
            ApplySpatialSettings();
        }
    }

    public Vector3 Position
    {
        get => _position;
        set
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
                throw new ArgumentOutOfRangeException(nameof(value));
            _position = value;
            ApplySpatialSettings();
        }
    }

    public float MinDistance
    {
        get => _minDistance;
        set
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (!float.IsFinite(value) || value <= 0 || value >= _maxDistance)
                throw new ArgumentOutOfRangeException(nameof(value));
            _minDistance = value;
            ApplySpatialSettings();
        }
    }

    public float MaxDistance
    {
        get => _maxDistance;
        set
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (!float.IsFinite(value) || value <= _minDistance)
                throw new ArgumentOutOfRangeException(nameof(value));
            _maxDistance = value;
            ApplySpatialSettings();
        }
    }

    internal void SynchronizePosition()
    {
        if (IsDisposed) return;
        if (PositionProvider != null) Position = PositionProvider();
    }

    private void ApplySpatialSettings()
    {
        if (_handle == 0) return;
        Audio.RequireThread();
        var changed = false;
        var spatial = _spatialMode == AudioSpatialMode.ThreeD;
        if (!_spatialApplied || _appliedMode != _spatialMode)
        {
            Audio.Api.SetSourceProperty(_handle, SourceBoolean.SourceRelative, !spatial);
            Audio.Api.SetSourceProperty(_handle, SourceFloat.RolloffFactor, spatial ? 1f : 0f);
            SpatialWriteCount += 2;
            changed = true;
        }
        if (!_spatialApplied || _appliedMinDistance != _minDistance)
        {
            Audio.Api.SetSourceProperty(_handle, SourceFloat.ReferenceDistance, _minDistance);
            SpatialWriteCount++;
            changed = true;
        }
        if (!_spatialApplied || _appliedMaxDistance != _maxDistance)
        {
            Audio.Api.SetSourceProperty(_handle, SourceFloat.MaxDistance, _maxDistance);
            SpatialWriteCount++;
            changed = true;
        }
        var position = spatial ? _position : Vector3.Zero;
        if (!_spatialApplied || _appliedPosition != position)
        {
            Audio.Api.SetSourceProperty(_handle, SourceVector3.Position, position);
            SpatialWriteCount++;
            changed = true;
        }
        if (changed)
        {
            Audio.CheckError("configure spatial source");
            _appliedMode = _spatialMode;
            _appliedMinDistance = _minDistance;
            _appliedMaxDistance = _maxDistance;
            _appliedPosition = position;
            _spatialApplied = true;
        }
    }
}