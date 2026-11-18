using System.Collections;
using ProjectShockGL.Core;

namespace ProjectShockGL;

public sealed class DetailCollection : IReadOnlyList<DetailPlacement>
{
    private readonly List<DetailPlacement> _items = [];
    private readonly HashSet<Guid> _ids = [];
    
    private bool _sealed;

    public int Count => _items.Count;
    public int Revision { get; private set; }
    
    public DetailPlacement this[int index] => _items[index];

    public DetailPlacement? Find(Guid id) => _items.FirstOrDefault(item => item.Id == id);

    public DetailPlacement Add(DetailPlacement placement)
    {
        EnsureEditable();
        
        ArgumentNullException.ThrowIfNull(placement);
        var copy = placement.Copy();
        
        if (!_ids.Add(copy.Id))
        {
            throw new InvalidOperationException($"Detail '{copy.Id}' already exists in this world.");
        }

        _items.Add(copy);
        Revision++;
        
        return copy;
    }

    public void Update(DetailPlacement placement)
    {
        EnsureEditable();
        
        ArgumentNullException.ThrowIfNull(placement);
        
        var copy = placement.Copy();
        var index = _items.FindIndex(item => item.Id == copy.Id);
        
        if (index < 0)
        {
            throw new KeyNotFoundException($"Detail '{copy.Id}' was not found.");
        }
        
        _items[index] = copy;
        
        Revision++;
    }

    public bool Remove(Guid id)
    {
        EnsureEditable();

        if (!_ids.Remove(id))
        {
            return false;
        }
        
        _items.RemoveAll(item => item.Id == id);
        
        Revision++;
        
        return true;
    }

    public void MoveToIndex(Guid id, int index)
    {
        EnsureEditable();
        var current = _items.FindIndex(item => item.Id == id);
        if (current < 0) throw new KeyNotFoundException($"Detail '{id}' was not found.");
        index = Math.Clamp(index, 0, _items.Count - 1);
        if (current == index) return;
        var placement = _items[current];
        _items.RemoveAt(current);
        _items.Insert(index, placement);
        Revision++;
    }

    public void Clear()
    {
        EnsureEditable();
        Reset();
    }

    private void EnsureEditable()
    {
        if (_sealed && !Application.IsEditor)
        {
            throw new InvalidOperationException("Details cannot be changed after the level starts.");
        }
    }

    internal void Seal()
    {
        _sealed = true;
    }

    internal void Reset()
    {
        _sealed = false;

        if (_items.Count == 0)
        {
            return;
        }
        
        _items.Clear();
        _ids.Clear();
        
        Revision++;
    }

    internal void TakeFrom(DetailCollection source)
    {
        if (ReferenceEquals(this, source))
        {
            return;
        }
        
        Reset();
        
        _items.AddRange(source._items);
        _ids.UnionWith(source._ids);
        _sealed = source._sealed;

        if (_items.Count > 0)
        {
            Revision++;
        }
        
        source.Reset();
    }

    public IEnumerator<DetailPlacement> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}