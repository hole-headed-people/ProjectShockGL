namespace ProjectShockGL.Rendering;

public class RenderQueue
{
    private readonly List<RenderCommand> _commands = [];
    internal IReadOnlyList<RenderCommand> Commands => _commands;
    
    public void Clear()
    {
        _commands.Clear();
    }

    public void Add(RenderCommand command)
    {
        _commands.Add(command);
    }
    
    public void Flush(Graphics graphics)
    {
        var ordered = _commands.OrderBy(command => command.RenderOrder)
            .ThenBy(command => command.IsOverlay ? 2 : command.SurfaceMode == SurfaceMode.Transparent ? 1 : 0)
            .ThenByDescending(command => command.SurfaceMode == SurfaceMode.Transparent
                ? System.Numerics.Vector3.DistanceSquared(graphics.CameraPosition, command.SortPosition) : 0)
            .ToArray();
        
        foreach (var command in ordered)
        {
            command.Execute(graphics);
        }
        
        Clear();
    }
}