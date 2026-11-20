using ProjectOxygenGL.Core;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;

namespace ProjectOxygenGL.Placeables;

[OutputEvent("OnClick")]
public class Button : Interactive
{
    [Editable] public bool OnlyOnce { get; set; }

    [Editable]
    public float Radius
    {
        get => _sphereCollider.Radius;
        set => _sphereCollider.Radius = value;
    }
    
    private SphereCollider _sphereCollider = null!;

    public override void Create()
    {
        base.Create();
        
        _sphereCollider = AddComponent<SphereCollider>();
        _sphereCollider.IsTrigger = true;
    }

    public override void Interact(Character character)
    {
        OnClick(character);

        if (OnlyOnce)
        {
            IsInteractable = false;
        }
    }

    protected virtual void OnClick(Character character)
    {
        InvokeEvent("OnClick");
    }
}