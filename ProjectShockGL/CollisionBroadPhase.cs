using System.Numerics;
using BepuPhysics.Trees;
using BepuUtilities;
using BepuUtilities.Memory;
using ProjectShockGL.Core;
using ProjectShockGL.Tools;

namespace ProjectShockGL;

internal sealed class CollisionBroadPhase : IDisposable
{
    private Tree _moving;
    private Tree _stationary;
    
    private readonly BufferPool _pool = new();
    
    private readonly List<int> _movingIndices = [];
    private readonly List<int> _stationaryIndices = [];
    private readonly List<int> _unindexed = [];
    
    private readonly List<(int A, int B)> _pairs = [];
    
    private (Vector3 min, Vector3 max)[] _bounds = [];

    public CollisionBroadPhase()
    {
        _moving = new Tree(_pool, 64);
        _stationary = new Tree(_pool, 64);
    }

    public IReadOnlyList<(int A, int B)> FindPairs(Collider[] snapshot, Predicate<Collider> enabled)
    {
        _moving.Clear();
        _stationary.Clear();
        
        _movingIndices.Clear();
        _stationaryIndices.Clear();
        _unindexed.Clear();
        
        _pairs.Clear();

        if (_bounds.Length < snapshot.Length)
        {
            Array.Resize(ref _bounds, Math.Max(snapshot.Length, _bounds.Length * 2));
        }

        for (var index = 0; index < snapshot.Length; index++)
        {
            var collider = snapshot[index];
            
            if (!enabled(collider))
            {
                continue;
            }

            var (min, max) = _bounds[index] = collider.GetWorldBounds();
            
            if (!PhysicsHelper.IsFinite(min) || !PhysicsHelper.IsFinite(max) ||
                min.X > max.X || min.Y > max.Y || min.Z > max.Z)
            {
                _unindexed.Add(index);
                
                continue;
            }

            var bounds = new BoundingBox(min, max);
            
            if (collider.Owner.IsStatic)
            {
                _stationary.Add(ref bounds, _pool);
                _stationaryIndices.Add(index);
            }
            else
            {
                _moving.Add(ref bounds, _pool);
                _movingIndices.Add(index);
            }
        }

        if (_movingIndices.Count > 1)
        {
            var movingPairs = new PairCollector(_movingIndices, _movingIndices, _pairs);
            
            _moving.GetSelfOverlaps(ref movingPairs);
        }

        if (_movingIndices.Count > 0 && _stationaryIndices.Count > 0)
        {
            var mixedPairs = new PairCollector(_movingIndices, _stationaryIndices, _pairs);
            
            _moving.GetOverlaps(ref _stationary, ref mixedPairs);
        }
        
        for (var i = 0; i < _unindexed.Count; i++)
        {
            var index = _unindexed[i];
            
            foreach (var other in _movingIndices)
            {
                AddUnindexedPair(snapshot, index, other);
            }
            
            foreach (var other in _stationaryIndices)
            {
                AddUnindexedPair(snapshot, index, other);
            }
            
            for (var j = i + 1; j < _unindexed.Count; j++)
            {
                AddUnindexedPair(snapshot, index, _unindexed[j]);
            }
        }
        
        _pairs.Sort();
        
        return _pairs;
    }

    private void AddUnindexedPair(Collider[] snapshot, int a, int b)
    {
        if (!(snapshot[a].Owner.IsStatic && snapshot[b].Owner.IsStatic) && PhysicsHelper.AabbOverlap(_bounds[a], _bounds[b]))
        {
            _pairs.Add(a < b ? (a, b) : (b, a));
        }
    }

    public void Dispose()
    {
        _moving.Dispose(_pool);
        _stationary.Dispose(_pool);
        _pool.Clear();
    }

    private readonly struct PairCollector(List<int> left, List<int> right, List<(int, int)> pairs) : IOverlapHandler
    {
        public void Handle(int a, int b)
        {
            var first = left[a];
            var second = right[b];
            
            pairs.Add(first < second ? (first, second) : (second, first));
        }
    }
}
