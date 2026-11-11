using ProjectShockGL.Core;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGL;

public class Coroutines : Object
{
    private static Coroutines? _instance;

    public static Coroutines Instance
    {
        get
        {
            if (_instance == null || _instance.IsDestroyed)
            {
                _instance = Game.Instance.World.Spawn<Coroutines>();
            }
            
            return _instance;
        }
    }
}
