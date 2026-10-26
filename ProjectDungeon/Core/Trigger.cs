using ProjectDungeon.Game;
using ProjectShockGL;
using ProjectShockGL.Core;
using Object = ProjectShockGL.Core.Object;

namespace ProjectDungeon.Core;

public class Trigger : Entity
{
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

        if (other is not PlayerCharacter)
        {
            return;
        }

        foreach (var output in Outputs)
        {
            if (output.Event != "OnTriggerEnter")
            {
                continue;
            }

            var target = ProjectShockGL.Core.Game.Instance.World.Find(output.TargetName);

            if (target == null)
            {
                Console.WriteLine($"Trigger target not found: {output.TargetName}");
                
                continue;
            }
            
            InputInvoker.Invoke(target, output.Input, output.Parameter);
        }
    }
}