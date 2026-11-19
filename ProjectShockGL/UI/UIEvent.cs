namespace ProjectShockGL.UI;

public sealed class UIEvent
{
    private readonly List<Action> _listeners = [];
    private readonly Func<bool>? _canInvoke;
    internal Action? InvokeActions { get; set; }

    public UIEvent(Func<bool>? canInvoke = null)
    {
        _canInvoke = canInvoke;
    }

    public void AddListener(Action listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _listeners.Add(listener);
    }

    public void RemoveListener(Action listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _listeners.Remove(listener);
    }

    public void RemoveAllListeners()
    {
        _listeners.Clear();
    }

    public void Invoke()
    {
        foreach (var listener in _listeners.ToArray())
        {
            if (_canInvoke?.Invoke() == false)
            {
                break;
            }

            listener.Invoke();
        }

        if (InvokeActions != null && _canInvoke?.Invoke() != false) InvokeActions.Invoke();
    }
}

public sealed class UIEvent<T>
{
    private readonly List<Action<T>> _listeners = [];
    private readonly Func<bool>? _canInvoke;

    public UIEvent(Func<bool>? canInvoke = null)
    {
        _canInvoke = canInvoke;
    }

    public void AddListener(Action<T> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _listeners.Add(listener);
    }

    public void RemoveListener(Action<T> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _listeners.Remove(listener);
    }

    public void RemoveAllListeners()
    {
        _listeners.Clear();
    }

    public void Invoke(T value)
    {
        foreach (var listener in _listeners.ToArray())
        {
            if (_canInvoke?.Invoke() == false)
            {
                break;
            }

            listener.Invoke(value);
        }
    }
}
