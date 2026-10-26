using ProjectShockGL;
using Silk.NET.Input;
using Core_Object = ProjectShockGL.Core.Object;

namespace ProjectDungeon.Core;

public enum InputMode
{
    Game,
    UI
}

public class Player : Core_Object
{
    private InputMode _inputMode;
    private bool _isInputEnabled;
    
    private Character? _character;

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        if (_character == null || !_isInputEnabled)
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
    
    protected virtual void OnCharacterRemoved(Character character){}
    protected virtual void OnCharacterAdded(Character character){}
    
    public void SetCharacter(Character character)
    {
        if (_character != null)
        {
            OnCharacterRemoved(_character);
        }
        
        _character = character;
        
        OnCharacterAdded(_character);
    }

    public void SetInputMode(InputMode mode)
    {
        _inputMode = mode;
        
        Input.SetCursorMode(mode == InputMode.Game ? CursorMode.Raw : CursorMode.Normal);
    }

    public void SetInputEnabled(bool isEnabled)
    {
        _isInputEnabled = isEnabled;
    }

    public Character? GetCharacter()
    {
        return _character;
    }
}
