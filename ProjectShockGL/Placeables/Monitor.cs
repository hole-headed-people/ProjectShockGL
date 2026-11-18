using System.Numerics;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Placeables;

public class Monitor : Entity
{
    private string _cameraName = "";
    private CameraCaptureEntity? _camera;
    private bool _needsResolve = true;

    public MeshComponent Screen { get; private set; } = null!;

    [Editable]
    public string CameraName
    {
        get => _cameraName;
        set
        {
            _cameraName = value ?? "";
            _camera = null;
            _needsResolve = true;
        }
    }

    public override void Create()
    {
        base.Create();
        
        Screen = AddComponent<ScreenComponent>();
        
        Screen.Lit = false;
        Screen.BackfaceCulling = true;
        Screen.UseParentScale = true;
        
        Screen.Color = Color.Black;
    }

    public override void Start()
    {
        base.Start();
        
        Setup(CameraName);
    }

    [Input("Setup")]
    public void Setup(string cameraName)
    {
        ObjectDisposedException.ThrowIf(IsDestroyed, this);
        
        CameraName = cameraName;
        ResolveCamera(warn: true);
    }

    private void ResolveCamera(bool warn)
    {
        if (!_needsResolve || OwningWorld == null)
        {
            return;
        }
        
        _needsResolve = false;
        _camera = string.IsNullOrWhiteSpace(CameraName) ? null : OwningWorld.Find(CameraName) as CameraCaptureEntity;
        
        if (warn && _camera == null && !string.IsNullOrWhiteSpace(CameraName))
        {
            ProjectShockGL.Log.PrintWarning($"Monitor '{Name}': CameraCapture '{CameraName}' was not found.");
        }
    }

    private Texture? GetCameraTexture()
    {
        ResolveCamera(warn: false);
        
        if (_camera == null || _camera.IsDestroyed || _camera.Capture.IsDestroyed || _camera.Camera.IsDisposed)
        {
            return null;
        }

        var target = _camera.Capture.Target;
        
        return target.IsDisposed || target.Texture.Handle == 0 ? null : target.Texture;
    }

    public override (Vector3 min, Vector3 max) GetLocalBounds()
    {
        return (new Vector3(-.5f, -.5f, -.01f), new Vector3(.5f, .5f, .01f));
    }

    private sealed class ScreenComponent : MeshComponent
    {
        public ScreenComponent() { }

        public override void Render(RenderQueue queue)
        {
            var texture = ((Monitor)Owner).GetCameraTexture();
            
            Mesh = Content.GetQuadMesh();
            Texture = texture ?? Content.GetWhitePixel();
            
            Color = texture == null ? Color.Black : Color.White;
            
            base.Render(queue);
        }
    }
}
