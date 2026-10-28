using ProjectShockGL;
using Silk.NET.Input;
using Core_Object = ProjectShockGL.Core.Object;

namespace ProjectDungeon.Core;

public enum InputMode
{
    Game,
    UI
}

public class Player : Controller
{
    private InputMode _inputMode;
    private bool _isInputEnabled;

    public override void Update(float deltaSeconds)
    {
        base.Update(deltaSeconds);

        if (GetCharacter() == null || !_isInputEnabled)
        {
            return;
        }

        if (_inputMode == InputMode.Game)
        {
            OnGameInput();
        }
        else if (_inputMode == InputMode.UI)
        {
            OnUIInput();
        }
        
        OnGameOrUIInput();
    }
    
    protected virtual void OnGameInput(){}
    protected virtual void OnUIInput(){}
    protected virtual void OnGameOrUIInput(){}

    public void SetInputMode(InputMode mode)
    {
        _inputMode = mode;
        
        Input.SetCursorMode(mode == InputMode.Game ? CursorMode.Raw : CursorMode.Normal);
    }

    public void SetInputEnabled(bool isEnabled)
    {
        _isInputEnabled = isEnabled;
    }
}
