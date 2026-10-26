using ProjectShockGL.Core;

namespace ProjectDungeon.Placeables;

public class Message : Entity
{
    [Input("Print")]
    public void Print(string message)
    {
        Console.WriteLine(message);
    }
}