using Object = ProjectShockGL.Core.Object;

namespace ProjectDungeon.Core;

public class Controller : Object
{
    private Character? _character;
    
    protected virtual void OnCharacterRemoved(Character character){}
    protected virtual void OnCharacterAdded(Character character){}
    
    public void SetCharacter(Character character)
    {
        if (_character != null)
        {
            _character.ClearController();
            
            OnCharacterRemoved(_character);
        }
        
        _character = character;
        _character.SetController(this);
        
        OnCharacterAdded(_character);
    }

    public void ClearCharacter()
    {
        if (_character == null)
        {
            return;
        }
        
        _character.ClearController();
        
        OnCharacterRemoved(_character);

        _character = null;
    }
    
    public Character? GetCharacter()
    {
        return _character;
    }
}