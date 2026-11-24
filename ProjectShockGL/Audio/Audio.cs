using Silk.NET.OpenAL;
using System.Numerics;
using System.Diagnostics;

namespace ProjectShockGL.Audio;

public static unsafe class Audio
{
    private static AL? _al;
    private static ALContext? _alc;

    private static Device* _device;
    private static Context* _context;

    private static int _thread;

    private static bool _attempted;

    private static readonly HashSet<AudioSource> Sources = [];
    private static readonly HashSet<AudioClip> Buffers = [];

    private static bool _listenerApplied;
    
    private static Vector3 _listenerPosition;
    private static Vector3 _listenerForward;
    private static Vector3 _listenerUp;
    
    internal static int ListenerWriteCount { get; private set; }
    internal static AL Api => _al ?? throw new InvalidOperationException("Audio is not initialized.");
    
    public static double UpdateTimeMs { get; private set; }
    public static double SpatialUpdateTimeMs { get; private set; }
    public static double TotalUpdateTimeMs => UpdateTimeMs + SpatialUpdateTimeMs;
    public static bool IsAvailable => _al != null;

    public static AudioGroup Master { get; } = new("Master", null);
    
    private static readonly Dictionary<string, AudioGroup> GroupLookup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Master"] = Master
    };
    
    public static IReadOnlyDictionary<string, AudioGroup> Groups { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, AudioGroup>(GroupLookup);

    public static AudioGroup CreateGroup(string name, AudioGroup? parent = null)
    {
        RequireGroupThread();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        
        name = name.Trim();
        if (parent != null) RequireRegisteredGroup(parent);
        var group = new AudioGroup(name, parent ?? Master);
        
        if (!GroupLookup.TryAdd(name, group))
        {
            throw new ArgumentException($"Audio group '{name}' already exists.", nameof(name));
        }
        
        RefreshGroupVolumes();
        return group;
    }

    public static void ApplyGroups(AudioGroupsAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        RequireGroupThread();
        var definitions = asset.GetValidatedGroups();
        var groups = new Dictionary<string, AudioGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions.Values)
        {
            groups.Add(definition.Name, GroupLookup.GetValueOrDefault(definition.Name)
                ?? new AudioGroup(definition.Name, null));
        }
        foreach (var definition in definitions.Values)
        {
            groups[definition.Name].Configure(definition.Volume,
                definition.Parent == null ? null : groups[definition.Parent]);
        }
        GroupLookup.Clear();
        foreach (var (name, group) in groups) GroupLookup.Add(name, group);
        RefreshGroupVolumes();
    }

    internal static void LoadGroupsFromSettings()
    {
        var name = Settings.GetString("Audio", "Groups", "");
        ApplyGroups(string.IsNullOrWhiteSpace(name) ? new AudioGroupsAsset() : Content.LoadAudioGroups(name));
    }

    public static AudioGroup? GetGroup(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? Master : GroupLookup.GetValueOrDefault(name.Trim());
    }

    internal static void RequireRegisteredGroup(AudioGroup group)
    {
        if (!ReferenceEquals(GetGroup(group.Name), group))
            throw new InvalidOperationException($"Audio group '{group.Name}' is no longer registered; get it again after applying a new configuration.");
    }

    internal static void RequireGroupThread()
    {
        if (IsAvailable) RequireThread();
    }

    internal static void RefreshGroupVolumes()
    {
        foreach (var source in Sources) source.RefreshGain();
    }

    public static bool Initialize()
    {
        if (_attempted)
        {
            if (IsAvailable)
            {
                RequireThread();
            }
            
            return IsAvailable;
        }

        _attempted = true;
        _thread = System.Environment.CurrentManagedThreadId;
        
        try
        {
            _alc = ALContext.GetApi(soft: true);
            _device = _alc.OpenDevice((string?)null);

            if (_device == null)
            {
                throw new InvalidOperationException("No audio output device is available.");
            }
            
            _context = _alc.CreateContext(_device, (int*)null);

            if (_context == null || !_alc.MakeContextCurrent(_context))
            {
                throw new InvalidOperationException("Cannot create an audio context.");
            }
            
            _al = AL.GetApi(soft: true);
            
            _al.DistanceModel(DistanceModel.LinearDistanceClamped);
            _al.DopplerFactor(0);
            CheckError("initialize audio");
            
            return true;
        }
        catch (Exception error)
        {
            Log.PrintWarning("Audio is unavailable; the game will run without sound. " + error.Message);
            ReleaseDevice();
            
            return false;
        }
    }

    public static AudioSource CreateSource(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ObjectDisposedException.ThrowIf(clip.IsDisposed, clip);

        if (IsAvailable)
        {
            RequireThread();
        }
        
        var source = new AudioSource(clip);
        Sources.Add(source);
        
        return source;
    }

    public static void Update(float unscaledDeltaSeconds)
    {
        if (!float.IsFinite(unscaledDeltaSeconds) || unscaledDeltaSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));
        }

        if (IsAvailable)
        {
            RequireThread();
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            foreach (var source in Sources.ToArray())
            {
                source.Update(unscaledDeltaSeconds);
            }
        }
        finally
        {
            UpdateTimeMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }
    }

    internal static void SynchronizeSpatialAudio()
    {
        SpatialUpdateTimeMs = 0;

        if (!IsAvailable)
        {
            return;
        }
        
        RequireThread();
        
        var started = Stopwatch.GetTimestamp();
        
        try
        {
            var camera = Rendering.Camera.Active;
            
            var position = camera.Transform.Position;
            var forward = camera.Transform.Forward;
            var up = camera.Transform.Up;
            
            var changed = false;
            
            if (!_listenerApplied || _listenerPosition != position)
            {
                Api.SetListenerProperty(ListenerVector3.Position, position);
                ListenerWriteCount++;
                changed = true;
            }
            
            if (!_listenerApplied || _listenerForward != forward || _listenerUp != up)
            {
                float* orientation = stackalloc float[6] { forward.X, forward.Y, forward.Z, up.X, up.Y, up.Z };
                
                Api.SetListenerProperty(ListenerFloatArray.Orientation, orientation);
                ListenerWriteCount++;
                
                changed = true;
            }
            
            if (changed)
            {
                CheckError("synchronize audio listener");
                
                _listenerPosition = position;
                _listenerForward = forward;
                _listenerUp = up;
                _listenerApplied = true;
            }

            foreach (var source in Sources.ToArray())
            {
                source.SynchronizePosition();
            }
        }
        finally
        {
            SpatialUpdateTimeMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }
    }
    
    internal static void RequireThread()
    {
        if (_thread != System.Environment.CurrentManagedThreadId)
        {
            throw new InvalidOperationException("Use audio sources on the engine thread.");
        }
    }

    internal static void CheckError(string operation)
    {
        var error = Api.GetError();

        if (error != AudioError.NoError)
        {
            throw new InvalidOperationException($"OpenAL failed to {operation}: {error}.");
        }
    }

    internal static void RegisterBuffer(AudioClip clip)
    {
        Buffers.Add(clip);
    }

    internal static void Unregister(AudioSource source)
    {
        Sources.Remove(source);
    }

    internal static void ReleaseClip(AudioClip clip)
    {
        foreach (var source in Sources.Where(source => ReferenceEquals(source.Clip, clip)).ToArray())
        {
            source.Dispose();
        }
        
        clip.ReleaseBuffer();
        Buffers.Remove(clip);
    }

    public static void Shutdown()
    {
        if (IsAvailable)
        {
            RequireThread();
        }
        
        try
        {
            Cleanup.Run(
                () => Cleanup.Run(Sources.ToArray().Select<AudioSource, Action>(source => source.Dispose).ToArray()),
                () => Cleanup.Run(Buffers.ToArray().Select<AudioClip, Action>(clip => clip.ReleaseBuffer).ToArray()));
        }
        finally
        {
            Sources.Clear();
            Buffers.Clear();
            
            ReleaseDevice();
            
            _listenerApplied = false;
            ListenerWriteCount = 0;
            UpdateTimeMs = 0;
            SpatialUpdateTimeMs = 0;
            _attempted = false;
            _thread = 0;
        }
    }

    private static void ReleaseDevice()
    {
        _al?.Dispose();
        _al = null;
        
        if (_alc != null)
        {
            _alc.MakeContextCurrent((Context*)null);

            if (_context != null)
            {
                _alc.DestroyContext(_context);
            }

            if (_device != null)
            {
                _alc.CloseDevice(_device);
            }
            
            _alc.Dispose();
        }

        _context = null;
        _device = null;
        _alc = null;
    }
}