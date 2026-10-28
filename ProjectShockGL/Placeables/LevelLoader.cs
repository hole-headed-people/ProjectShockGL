using ProjectShockGL.Core;

namespace ProjectShockGL.Placeables;

public class LevelLoader : Entity
{
    [Input("Load")]
    public void Load(string name)
    {
        LevelManager.Load(name);
    }
}