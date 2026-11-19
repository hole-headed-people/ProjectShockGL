using ProjectShockGL.Core;
using ProjectShockGL.UI;

namespace ProjectShockGL.Placeables;

[OutputEvent("OnInvoke")]
public class UIEvent : Entity
{
    [Input("Invoke")]
    public void Invoke()
    {
        InvokeEvent("OnInvoke");
    }

    [UIAction("UIEvent/Invoke")]
    public static void Invoke(string name)
    {
        var e = Game.Instance.World.Find(name);

        if (e is not UIEvent uiEvent)
        {
            ProjectShockGL.Log.PrintWarning($"UIEvent with name {name} not found");
            
            return;
        }
        
        uiEvent.Invoke();
    }
}