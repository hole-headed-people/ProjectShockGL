using System.Runtime.InteropServices;
using ProjectShockGL.Core;

namespace ProjectShockGL.Placeables;

public class Log : Entity
{
    [Input("Print")]
    public void Print(string message)
    {
        ProjectShockGL.Log.Print(message);
    }

    [Input("PrintInt")]
    public void PrintInt(int value)
    {
        ProjectShockGL.Log.Print(value.ToString());
    }

    [Input("PrintFloat")]
    public void PrintFloat(float value)
    {
        ProjectShockGL.Log.Print(value.ToString());
    }
}