using System.Collections;

namespace ProjectShockGL.Core;

public sealed class WaitForSeconds
{
    public float Seconds { get; }
    public bool UseUnscaledTime { get; }
    
    public WaitForSeconds(float seconds, bool useUnscaledTime = false)
    {
        if (!float.IsFinite(seconds) || seconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }
        
        Seconds = seconds;
        UseUnscaledTime = useUnscaledTime;
    }
}

public sealed class Coroutine
{
    private readonly Stack<IEnumerator> _stack = new();
    
    private object? _waiting;
    
    private float _remaining;
    
    private bool _executing;
    private bool _cancelled;
    
    internal Object Owner { get; }
    
    public bool IsCompleted { get; private set; }
    public bool IsCancelled => _cancelled;

    internal Coroutine(Object owner, IEnumerator routine)
    {
        Owner = owner;
        
        _stack.Push(routine);
    }

    public void Stop()
    {
        if (IsCompleted)
        {
            return;
        }
        
        _cancelled = true;

        if (!_executing)
        {
            Finish();
        }
    }

    internal void Tick(float delta, float unscaled, bool first = false)
    {
        if (IsCompleted || _executing)
        {
            return;
        }

        if (Owner.IsDestroyed)
        {
            Stop(); 
            return;
        }

        if (!Owner.IsActive)
        {
            return;
        }
        
        if (!first)
        {
            if (_waiting is WaitForSeconds wait)
            {
                _remaining -= wait.UseUnscaledTime ? unscaled : delta;
                
                if (_remaining > 0)
                {
                    return;
                }
            }
            else if (_waiting is Coroutine other && !other.IsCompleted)
            {
                return;
            }
        }
        
        _waiting = null;
        _executing = true;
        
        try
        {
            for (var budget = 0; budget < 1024; budget++)
            {
                if (_cancelled || Owner.IsDestroyed || _stack.Count == 0)
                {
                    Finish(); 
                    return;
                }
                
                var iterator = _stack.Peek();
                
                if (!iterator.MoveNext())
                {
                    _stack.Pop();
                    
                    (iterator as IDisposable)?.Dispose();
                    
                    continue;
                }

                if (_cancelled || Owner.IsDestroyed)
                {
                    Finish(); 
                    return;
                }
                
                var yielded = iterator.Current;

                if (yielded is IEnumerator nested)
                {
                    _stack.Push(nested); 
                    continue;
                }

                if (yielded != null && yielded is not WaitForSeconds && yielded is not Coroutine)
                {
                    throw new InvalidOperationException($"Unsupported coroutine yield: {yielded.GetType().Name}");
                }

                if (ReferenceEquals(yielded, this))
                {
                    throw new InvalidOperationException("A coroutine cannot wait for itself.");
                }
                
                _waiting = yielded;

                if (yielded is WaitForSeconds delay)
                {
                    _remaining = delay.Seconds;
                }
                
                return;
            }
            
            throw new InvalidOperationException("Coroutine exceeded its synchronous execution budget.");
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Coroutine on '{Owner.Name}' failed: {error}");
            
            _cancelled = true;
            
            Finish();
        }
        finally
        {
            _executing = false;
        }
    }

    private void Finish()
    {
        IsCompleted = true;
        
        _waiting = null;
        
        while (_stack.TryPop(out var iterator))
        {
            try
            {
                (iterator as IDisposable)?.Dispose();
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Coroutine cleanup on '{Owner.Name}' failed: {error}");
            }
        }
        
        Owner.RemoveCoroutine(this);
    }
}
