using Newtonsoft.Json;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Tools;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

internal sealed class SceneObjectAction : IUndoableAction
{
    private readonly World _world;
    private readonly EditorObjectReference _reference;
    private readonly EditorObjectReference? _previousSelection;
    private readonly string _snapshot;
    private readonly int _index;
    private readonly bool _creation;

    public SceneObjectAction(Object obj, Object? previousSelection, bool creation)
    {
        _world = ProjectShockGL.Core.Game.Instance.World;
        _index = Array.IndexOf(_world.GetAllObjects(true), obj);

        if (_index < 0 || obj.IsDestroyed)
        {
            throw new InvalidOperationException("The object is no longer in the scene.");
        }
        
        var placement = EditorLevelManager.CaptureLevel([obj]).Placeables.SingleOrDefault()
            ?? throw new InvalidOperationException("Only placed objects can be recorded in scene history.");
        
        _snapshot = JsonConvert.SerializeObject(placement, JsonHelper.GetSettings());
        
        _reference = EditorObjectReference.Get(obj);
        _previousSelection = previousSelection == null ? null : EditorObjectReference.Get(previousSelection);
        
        _creation = creation;
    }

    public void Undo()
    {
        if (_creation)
        {
            Remove(restorePreviousSelection: true);
        }
        
        else Restore();
    }

    public void Redo()
    {
        if (_creation)
        {
            Restore();
        }
        
        else Remove(restorePreviousSelection: false);
    }

    internal void Remove(bool restorePreviousSelection)
    {
        var obj = _reference.Current;
        
        try
        {
            _world.Destroy(obj);

            if (!obj.IsDestroyed)
            {
                throw new InvalidOperationException("Cannot delete this object from the editor world.");
            }
        }
        finally
        {
            if (obj.IsDestroyed && (Selection.Object == obj || restorePreviousSelection))
            {
                var previous = restorePreviousSelection ? _previousSelection?.Current : null;
                
                Selection.Object = previous is { IsDestroyed: false } && _world.GetAllObjects(true).Contains(previous)
                    ? previous : null;
            }
        }
    }

    private void Restore()
    {
        if (!_reference.Current.IsDestroyed)
        {
            Selection.Object = _reference.Current;
            
            return;
        }
        var placement = JsonConvert.DeserializeObject<Placeable>(_snapshot, JsonHelper.GetSettings())!;
        var restored = EditorLevelManager.CreatePlacement(placement);
        
        restored.Transform.Rotation = placement.Rotation;
        
        _world.MoveToIndex(restored, _index);
        _reference.Replace(restored);
        
        Selection.Object = restored;
    }
}
