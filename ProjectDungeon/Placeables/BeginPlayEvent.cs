using ProjectShockGL;
using ProjectShockGL.Core;

namespace ProjectDungeon.Placeables;

public class BeginPlayEvent : Entity
{
    private void Invoke()
    {
        foreach (var output in Outputs)
        {
            if (output.Event != "OnBeginPlay")
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
    
    public static void InvokeAll()
    {
        var all = ProjectShockGL.Core.Game.Instance.World.FindAll<BeginPlayEvent>();

        if (all.Length <= 0)
        {
            return;
        }
        
        foreach (var e in all)
        {
            e.Invoke();
        }
    }
}