namespace ProjectOxygenGL.Core;

public class Character : DamageableEntity
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

    public void Heal(float amount)
    {
        if (IsDestroyed || IsDead() || !float.IsFinite(amount) || amount <= 0f)
        {
            return;
        }
        
        SetCurrentHealth(CurrentHealth + MathF.Min(amount, MaxHealth - CurrentHealth));
    }

    public Controller? GetController()
    {
        return _controller;
    }
}
