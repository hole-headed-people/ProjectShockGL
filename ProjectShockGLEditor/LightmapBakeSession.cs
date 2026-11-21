using System.Diagnostics;
using ProjectShockGL;
using ProjectShockGL.Rendering;

namespace ProjectShockGLEditor;

public enum LightmapBakeState
{
    Idle, 
    Preparing, 
    Baking, 
    Cancelling, 
    Completed, 
    Cancelled, 
    Failed,
    Discarded
}

public sealed record LightmapBakeSummary(int Pages, int Surfaces, long ShadowRays, long IndirectRays, int MissedTriangles, TimeSpan Elapsed);

public sealed class LightmapBakeSession : IDisposable
{
    private Task<DirectLightBakeResult>? _task;
    
    private CancellationTokenSource? _cancellation;
    
    private World? _world;
    
    private Func<bool>? _isCurrent;
    private Func<string>? _signature;
    
    private string _capturedSignature = "";
    private string _assetName = "";
    private string? _previousAsset;
    
    private long _progress;
    
    private readonly Stopwatch _timer = new();
    
    private bool _disposed;

    public LightmapBakeState State { get; private set; }
    public bool IsBusy => _task != null;
    public string? Error { get; private set; }
    public LightmapBakeSummary? Summary { get; private set; }
    public string? PublishedAsset { get; private set; }
    public TimeSpan Elapsed => _timer.Elapsed;
    public float Progress
    {
        get
        {
            var value=Interlocked.Read(ref _progress);
            var total=(int)(value>>32);
            
            return total<=0?0:Math.Clamp((float)(uint)value/total,0,1);
        }
    }

    public void Start(World world, LightmapBakeSettings settings, string assetName,
        Func<bool>? isCurrent = null, Func<string>? signature = null)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);

        if (IsBusy)
        {
            throw new InvalidOperationException("A lighting bake is already running.");
        }
        
        Error=null;
        Summary=null;
        PublishedAsset=null;
        
        Interlocked.Exchange(ref _progress,0);
        
        State = LightmapBakeState.Preparing;
        _timer.Restart();
        
        try
        {
            ArgumentNullException.ThrowIfNull(world);
            
            settings.Validate();
            
            _world = world;
            _previousAsset = world.LightmapAsset;
            
            _assetName = assetName;
            _isCurrent = isCurrent;
            _signature = signature;
            
            _capturedSignature = signature?.Invoke() ?? "";
            
            var geometry=world.PrepareLightmapGeometry(settings.CreatePreparationOptions());

            if (!geometry.Surfaces.Any(surface => surface.Materials.Any(material => material.ReceivesLight)))
            {
                throw new InvalidOperationException("There are no static Lit surfaces to bake.");
            }

            if ((long)geometry.AtlasSize * geometry.AtlasSize * geometry.PageCount > 64L * 1024 * 1024)
            {
                throw new InvalidOperationException("This bake exceeds the lightmap package limit. Reduce surface resolution.");
            }
            
            var input = DirectLightBaker.Capture(world,geometry);
            
            _cancellation = new CancellationTokenSource();
            
            var token = _cancellation.Token;
            
            var options = settings.CreateBakeOptions();
            var progress = new BakeProgress(value=> Interlocked.Exchange(ref _progress, ((long)value.TotalTriangles << 32) | (uint)value.CompletedTriangles));
            
            _task = Task.Run(() => DirectLightBaker.Bake(input,options,token,progress), token);
            
            State = LightmapBakeState.Baking;
        }
        catch(Exception error)
        {
            State = LightmapBakeState.Failed;
            Error = error.Message;
            
            _timer.Stop();
            
            _cancellation?.Dispose();_cancellation=null;_world=null;_isCurrent=null;_signature=null;
        }
    }

    public void Clear(World world)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(IsBusy)throw new InvalidOperationException("Cancel the bake before clearing its lightmap.");
        world.ApplyLightmap(null);
        State=LightmapBakeState.Idle;Error=null;Summary=null;PublishedAsset=null;
        _timer.Reset();Interlocked.Exchange(ref _progress,0);
    }
    public void Cancel()
    {
        if (!IsBusy)
        {
            return;
        }
        
        _cancellation!.Cancel();
        
        State=LightmapBakeState.Cancelling;
    }

    // Called by the editor on its OpenGL thread, even when the window is closed.
    public bool Poll()
    {
        if (_task is not { IsCompleted: true })
        {
            return false;
        }
        
        try
        {
            _cancellation!.Token.ThrowIfCancellationRequested();
            
            var result = _task.GetAwaiter().GetResult();
            
            if(_isCurrent?.Invoke() == false || (_signature?.Invoke() ?? "") != _capturedSignature || _world!.LightmapAsset != _previousAsset)
            {
                State = LightmapBakeState.Discarded;
                Error = "The level changed during baking. The previous lightmap was kept.";
                
                return true;
            }

            var asset=Content.SaveLightmap(_assetName,result);
            
            // Save creates an immutable new package. Apply loads it completely before swapping the old resources.
            _world!.ApplyLightmap(asset);
            
            PublishedAsset = asset;
            Summary = new LightmapBakeSummary(result.Pages.Count, result.Geometry.Surfaces.Count, result.ShadowRays,
                result.IndirectRays, result.MissedTriangles, result.Elapsed);
            State = LightmapBakeState.Completed;
        }
        catch(OperationCanceledException)
        {
            State = LightmapBakeState.Cancelled;
        }
        catch(Exception error)
        {
            State = LightmapBakeState.Failed;
            Error = error.Message;
        }
        finally
        {
            // Observe a worker fault even if cancellation took priority.
            if (_task.IsFaulted)
            {
                _ = _task.Exception;
            }
            
            _task = null;
            
            _cancellation?.Dispose();
            _cancellation = null;
            
            _world = null;
            _signature = null;
            _isCurrent = null;
            
            _timer.Stop();
        }
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        
        _disposed = true;
        
        var task = _task;
        var cancellation = _cancellation;
        cancellation?.Cancel();
        
        _task = null;
        _cancellation = null;
        _world = null;
        _signature = null;
        _isCurrent = null;
        
        _timer.Stop();
        
        if(task != null)
        {
            State = LightmapBakeState.Cancelled;
            _ = task.ContinueWith(completed =>
                {
                    _ = completed.Exception;
                    
                    cancellation?.Dispose();
                },
                
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        else
        {
            cancellation?.Dispose();
        }
    }

    private sealed class BakeProgress(Action<LightmapBakeProgress> report) : IProgress<LightmapBakeProgress>
    {
        public void Report(LightmapBakeProgress value) => report(value);
    }
}