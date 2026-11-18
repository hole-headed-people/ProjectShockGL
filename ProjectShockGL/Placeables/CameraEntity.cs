using ProjectShockGL.Core;
using ProjectShockGL.Rendering;
using RenderCamera = ProjectShockGL.Rendering.Camera;

namespace ProjectShockGL.Placeables;

public class CameraEntity : Entity
{
    public CameraComponent CameraComponent { get; private set; } = null!;
    public RenderCamera Camera => CameraComponent.Camera;

    [Editable]
    public float FieldOfView
    {
        get => Camera.Fov * 180f / MathF.PI;
        set => Camera.Fov = Math.Clamp(Finite(value), 1f, 179f) * MathF.PI / 180f;
    }

    [Editable]
    public float NearClip
    {
        get => Camera.Near;
        set
        {
            Camera.Near = MathF.Max(.001f, Finite(value));
            Camera.Far = MathF.Max(Camera.Far, Camera.Near + .01f);
        }
    }

    [Editable]
    public float FarClip
    {
        get => Camera.Far;
        set => Camera.Far = MathF.Max(Camera.Near + .01f, Finite(value));
    }

    [Editable]
    public bool Orthographic
    {
        get => Camera.Orthographic;
        set => Camera.Orthographic = value;
    }

    [Editable]
    public float OrthoSize
    {
        get => Camera.OrthoSize;
        set => Camera.OrthoSize = MathF.Max(.01f, Finite(value));
    }

    [Editable]
    public Color ClearColor
    {
        get => Camera.ClearColor;
        set => Camera.ClearColor = value;
    }

    public override void Create()
    {
        base.Create();
        CameraComponent = CreateCameraComponent();
    }

    protected virtual CameraComponent CreateCameraComponent() => AddComponent<CameraComponent>();

    [Input("MakeActive")]
    public bool MakeActive() => CameraComponent.MakeActive();

    [Input("ReleaseActive")]
    public void ReleaseActive() => RenderCamera.ReleaseActive(Camera);

    public override void DebugRenderSelected(DebugDraw draw)
    {
        base.DebugRenderSelected(draw);

        var origin = Transform.Position;
        var forward = Transform.Forward;
        var right = Transform.Right;
        var tip = origin + forward * 2f;
        var head = tip - forward * .4f;
        draw.Line(origin, tip, Color.Yellow, ignoreDepth: true);
        draw.Line(tip, head + right * .2f, Color.Yellow, ignoreDepth: true);
        draw.Line(tip, head - right * .2f, Color.Yellow, ignoreDepth: true);
    }

    private static float Finite(float value)
    {
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return value;
    }
}
