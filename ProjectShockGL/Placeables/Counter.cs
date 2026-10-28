using ProjectShockGL.Core;

namespace ProjectShockGL.Placeables;

public class Counter : Entity
{
    [Editable] public int Number { get; set; } = 0;
    [Editable] public int StartCount { get; set; } = 0;
    
    private int _count;

    public override void Create()
    {
        base.Create();

        _count = StartCount;
    }

    [Input("SetCount")]
    public void SetCount(int count)
    {
        _count = count;
        
        InvokeEvent("OnCountChanged", count);

        if (_count == Number)
        {
            InvokeEvent("OnNumberReached");
        }
    }
    
    [Input("Add")]
    public void Add(int amount)
    {
        if (amount < 0)
        { 
            Console.WriteLine("Cannot add negative amount.");
        }
        
        SetCount(_count + amount);
        
        InvokeEvent("OnAddCount");
    }

    [Input("Subtract")]
    public void Subtract(int amount)
    {
        if (amount < 0)
        {
            Console.WriteLine("Cannot subtract negative amount.");
        }
        
        SetCount(_count - amount);
        
        InvokeEvent("OnSubtractCount");
    }
}