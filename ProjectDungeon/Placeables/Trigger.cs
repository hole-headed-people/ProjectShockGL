using ProjectDungeon.Core;
using ProjectShockGL;
using ProjectShockGL.Core;
using Object = ProjectShockGL.Core.Object;

namespace ProjectDungeon.Placeables;

public class Trigger : Entity
{
    [Editable] public bool CanTriggeredByPlayer { get; set; }
    [Editable] public bool CanTriggeredByObject { get; set; }
    [Editable] public string ObjectName { get; set; } = "";
    
    public override void Create()
    {
        base.Create();
        
        var boxCollider = AddComponent<BoxCollider>();
        boxCollider.IsTrigger = true;
        
        Layer = Layers.Get("Trigger");
    }

    public override void OnTriggerEnter(Object other)
    {
        base.OnTriggerEnter(other);

        if ((CanTriggeredByPlayer && other is Character character && character.GetController() is Player) ||
            (CanTriggeredByObject && !string.IsNullOrEmpty(ObjectName) && other.Name == ObjectName))
        {
            InvokeEvent("OnTriggerEnter");
        }
    }
}