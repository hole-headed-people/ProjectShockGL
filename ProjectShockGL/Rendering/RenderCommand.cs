namespace ProjectShockGL.Rendering;

public abstract class RenderCommand
{
    public int RenderOrder { get; set; }
    public abstract void Execute(Graphics graphics);
}