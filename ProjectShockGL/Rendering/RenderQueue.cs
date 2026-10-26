namespace ProjectShockGL.Rendering;

public class RenderQueue
{
    private readonly List<RenderCommand> _commands = [];
    
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
        _commands.Sort((a, b) => a.RenderOrder.CompareTo(b.RenderOrder));
        
        foreach (var command in _commands)
        {
            command.Execute(graphics);
        }
        
        Clear();
    }
}