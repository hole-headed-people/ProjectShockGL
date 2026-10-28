using System.Runtime.InteropServices;
using ProjectShockGL.Core;

namespace ProjectShockGL.Placeables;

public class Message : Entity
{
    [Input("Print")]
    public void Print(string message)
    {
        Console.WriteLine(message);
    }

    [Input("PrintInt")]
    public void PrintInt(int value)
    {
        Console.WriteLine(value);
    }

    [Input("PrintFloat")]
    public void PrintFloat(float value)
    {
        Console.WriteLine(value);
    }
}