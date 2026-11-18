using ProjectShockGL.Core;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Placeables;

public class CameraCaptureEntity : CameraEntity
{
    public CameraCaptureComponent Capture => (CameraCaptureComponent)CameraComponent;
    public Texture Texture => Capture.Texture;

    [Editable]
    public int Width
    {
        get => Capture.Width;
        set => Capture.Width = Math.Max(1, value);
    }

    [Editable]
    public int Height
    {
        get => Capture.Height;
        set => Capture.Height = Math.Max(1, value);
    }

    [Editable]
    public RenderTextureFilter Filter
    {
        get => Capture.Filter;
        set => Capture.Filter = value;
    }

    protected override CameraComponent CreateCameraComponent() => AddComponent<CameraCaptureComponent>();
}
