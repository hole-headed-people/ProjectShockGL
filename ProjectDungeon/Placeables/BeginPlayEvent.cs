using ProjectShockGL;
using ProjectShockGL.Core;

namespace ProjectDungeon.Placeables;

public class BeginPlayEvent : Entity
{
    public static void InvokeAll()
    {
        var all = ProjectShockGL.Core.Game.Instance.World.FindAll<BeginPlayEvent>();

        if (all.Length <= 0)
        {
            return;
        }
        
        foreach (var e in all)
        {
            e.InvokeEvent("OnBeginPlay");
        }
    }
}