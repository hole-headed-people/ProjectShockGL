using System.Collections;
using ProjectShockGL.Core;

namespace ProjectShockGL.Tools;

public static class CoroutinesTools
{
    public static IEnumerator Delay(float seconds, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        yield return new WaitForSeconds(seconds);
        
        action.Invoke();
    }
}