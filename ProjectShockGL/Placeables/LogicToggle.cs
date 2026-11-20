using ProjectShockGL.Core;

namespace ProjectShockGL.Placeables;

[OutputEvent("OnTurnedOn")]
[OutputEvent("OnTurnedOff")]
[OutputEvent("OnStateChanged")]
public class LogicToggle : Entity
{
    [Editable] public bool InitialState { get; set; } = true;

    private bool _state;

    public override void Start()
    {
        base.Start();

        _state = InitialState;
    }

    [Input("SetState")]
    public void SetState(bool state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;

        InvokeEvent("OnStateChanged", _state);
        InvokeEvent(_state ? "OnTurnedOn" : "OnTurnedOff");
    }

    [Input("Toggle")]
    public void Toggle()
    {
        SetState(!_state);
    }
}
