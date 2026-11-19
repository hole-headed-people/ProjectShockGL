using ProjectShockGL.Core;

namespace ProjectShockGL.Placeables;

public class LevelLoader : Entity
{
    [Editable] public int EntryIndex { get; set; } = -1;
    
    [Input("Load")]
    public void Load(string name)
    {
        LevelManager.Load(name, EntryIndex);
    }
}