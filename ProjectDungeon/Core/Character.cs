using ProjectShockGL.Core;

namespace ProjectDungeon.Core;

public class Character : Entity
{
    private Controller? _controller;
    
    protected virtual void OnControlling(Controller controller){}
    protected virtual void OnStopControlling(Controller controller){}
    
    public void SetController(Controller controller)
    {
        if (_controller != null)
        {
            ClearController();
        }
        
        _controller = controller;
        
        OnControlling(controller);
    }

    public void ClearController()
    {
        if (_controller == null)
        {
            return;
        }
        
        OnStopControlling(_controller);
        
        _controller = null;
    }

    public Controller? GetController()
    {
        return _controller;
    }
}