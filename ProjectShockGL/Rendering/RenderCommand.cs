namespace ProjectShockGL.Rendering;

public abstract class RenderCommand
{
    public SurfaceMode SurfaceMode { get; set; } = SurfaceMode.Cutout;
    public float AlphaCutoff { get; set; } = 0.5f;
    internal virtual System.Numerics.Vector3 SortPosition => default;
    internal virtual bool IsOverlay => false;
    public int RenderOrder { get; set; }
    public abstract void Execute(Graphics graphics);
}