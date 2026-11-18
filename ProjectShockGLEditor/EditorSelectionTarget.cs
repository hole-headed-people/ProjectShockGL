using System.Numerics;
using ProjectShockGL;
using ProjectShockGL.Core;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

internal sealed class EditorSelectionTarget
{
    private EditorObjectReference? _object;
    private readonly World? _world;
    private readonly Guid _detailId;
    private readonly Transform _detailTransform = new();
    private DetailPlacement? _cachedDetail;

    private EditorSelectionTarget(Object obj) => _object = EditorObjectReference.Get(obj);
    private EditorSelectionTarget(World world, Guid id) { _world = world; _detailId = id; }

    internal static EditorSelectionTarget ForObject(Object obj) => new(obj);
    internal static EditorSelectionTarget ForDetail(World world, Guid id) => new(world, id);
    internal Object? Object
    {
        get
        {
            if (_object == null) return null;
            _object = EditorObjectReference.Get(_object.Current);
            return _object.Current;
        }
    }
    internal DetailPlacement? Detail => _world?.Details.Find(_detailId);
    internal World? World => _world;
    internal bool IsDetail => _object == null;
    internal Guid Id => Object?.Guid ?? _detailId;
    internal (Guid Id, bool Detail) Key => (Id, IsDetail);
    internal bool IsDestroyed => IsDetail ? Detail == null : Object!.IsDestroyed;
    internal bool IsEditorVisible => IsDetail
        ? Detail is { } detail && (!Application.IsEditor || !detail.EditorGroups.Any(ProjectShockGL.World.HiddenEditorGroups.Contains))
        : Object!.IsEditorVisible;
    internal bool IsActive => Object?.IsActive ?? true;

    internal bool Matches(EditorSelectionTarget other) => IsDetail == other.IsDetail &&
        (IsDetail ? ReferenceEquals(_world, other._world) && Id == other.Id : ReferenceEquals(Object, other.Object));

    internal Transform Transform
    {
        get
        {
            if (Object is { } obj) return obj.Transform;
            var detail = Detail;
            if (detail != null && !ReferenceEquals(detail, _cachedDetail))
            {
                new TransformState(detail.Position, detail.Rotation, detail.Scale).Apply(_detailTransform);
                _cachedDetail = detail;
            }
            return _detailTransform;
        }
    }

    internal void Apply(TransformState state)
    {
        if (IsDestroyed) return;
        if (Object is { } obj) { state.Apply(obj.Transform); return; }
        var detail = Detail!;
        _world!.Details.Update(new DetailPlacement
        {
            Id = detail.Id, AssetPath = detail.AssetPath, Position = state.Position,
            Rotation = state.Rotation, Scale = state.Scale, EditorGroups = detail.EditorGroups
        });
        _cachedDetail = null;
    }

    internal void FlushTransform()
    {
        if (!IsDetail || Detail is not { } detail) return;
        var state = TransformState.Capture(_detailTransform);
        if (state != new TransformState(detail.Position, detail.Rotation, detail.Scale)) Apply(state);
    }

    internal (Vector3 min, Vector3 max) GetLocalBounds()
    {
        if (Object is { } obj) return obj.GetLocalBounds();
        var asset = Content.LoadDetail(Detail!.AssetPath);
        var mesh = asset == null ? null : Content.LoadMesh(asset.Mesh);
        return mesh == null ? (new Vector3(-.5f), new Vector3(.5f)) : (mesh.BoundsMin, mesh.BoundsMax);
    }

    internal (Vector3 min, Vector3 max) GetWorldBounds()
    {
        if (Object is { } obj) return obj.GetWorldBounds();
        var (min, max) = GetLocalBounds();
        var model = Transform.GetModelMatrix();
        var worldMin = new Vector3(float.MaxValue);
        var worldMax = new Vector3(float.MinValue);
        for (var i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y,
                (i & 4) == 0 ? min.Z : max.Z);
            var point = Vector3.Transform(corner, model);
            worldMin = Vector3.Min(worldMin, point);
            worldMax = Vector3.Max(worldMax, point);
        }
        return (worldMin, worldMax);
    }
}