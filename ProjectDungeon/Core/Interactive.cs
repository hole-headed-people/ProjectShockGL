using ProjectShockGL.Core;

namespace ProjectDungeon.Core;

public abstract class Interactive : Entity
{
    [Editable]
    public bool IsInteractable { get; set; }
    
    public abstract void Interact(Character character);
}