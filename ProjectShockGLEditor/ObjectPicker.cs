using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGLEditor;

public static class ObjectPicker
{
    private const float CycleTolerance = 5f;
    private static Vector2 _cyclePosition;
    private static Vector2 _cycleScreenSize;
    private static Matrix4x4 _cycleViewProjection;
    private static Guid[] _cycleCandidates = [];
    private static Guid? _lastPicked;
    private static int _cycleIndex;

    public static void PickAt(Vector2 mousePos, Vector2 screenSize, bool additive = false)
    {
        var camera = Camera.Active;
        var viewProjection = camera.GetViewMatrix() * camera.GetProjectionMatrix();

        if (!GizmoMath.ScreenRay(mousePos, screenSize, viewProjection, out var origin, out var direction))
        {
            ResetCycle();
            return;
        }

        var hits = PickAll(origin, direction, camera.Far,
            ProjectShockGL.Core.Game.Instance.World.GetAllObjects(includePending: true));

        if (additive)
        {
            ResetCycle();
            if (hits.Length > 0)
            {
                Selection.Toggle(hits[0]);
            }

            return;
        }

        if (hits.Length == 0)
        {
            ResetCycle();
            Selection.Object = null;
            return;
        }

        var candidates = hits.Select(obj => obj.Guid).ToArray();
        var selected = Selection.Objects;
        var continueCycle = _lastPicked.HasValue &&
                            selected.Length == 1 && selected[0].Guid == _lastPicked &&
                            Vector2.DistanceSquared(mousePos, _cyclePosition) <= CycleTolerance * CycleTolerance &&
                            screenSize == _cycleScreenSize && viewProjection == _cycleViewProjection &&
                            candidates.SequenceEqual(_cycleCandidates);

        if (continueCycle)
        {
            _cycleIndex = (_cycleIndex + 1) % hits.Length;
        }
        else
        {
            _cycleIndex = 0;
            _cyclePosition = mousePos;
            _cycleScreenSize = screenSize;
            _cycleViewProjection = viewProjection;
            _cycleCandidates = candidates;
        }

        var picked = hits[_cycleIndex];
        Selection.Object = picked;
        _lastPicked = picked.Guid;
    }

    private static void ResetCycle()
    {
        _cycleCandidates = [];
        _lastPicked = null;
        _cycleIndex = 0;
    }

    private static ProjectShockGL.Core.Object[] PickAll(Vector3 rayOrigin, Vector3 rayDir,
        float maxDist, IEnumerable<ProjectShockGL.Core.Object> objects)
    {
        var hits = new List<(ProjectShockGL.Core.Object Object, float Distance)>();

        foreach (var obj in objects)
        {
            if (!obj.IsActive || obj.IsDestroyed || !obj.IsEditorVisible)
            {
                continue;
            }

            var (min, max) = obj.GetWorldBounds();
            if (RayAabb(rayOrigin, rayDir, maxDist, min, max, out var distance))
            {
                hits.Add((obj, distance));
            }
        }

        // OrderBy keeps world order for equal distances, so overlapping bounds cycle predictably.
        return hits.OrderBy(hit => hit.Distance).Select(hit => hit.Object).ToArray();
    }
    private static bool RayAabb(Vector3 origin, Vector3 dir, float maxDist,
        Vector3 min, Vector3 max, out float distance)
    {
        distance = 0;

        var tMin = float.NegativeInfinity;
        var tMax = float.PositiveInfinity;

        for (var i = 0; i < 3; i++)
        {
            var o = i == 0 ? origin.X : i == 1 ? origin.Y : origin.Z;
            var d = i == 0 ? dir.X : i == 1 ? dir.Y : dir.Z;
            var mn = i == 0 ? min.X : i == 1 ? min.Y : min.Z;
            var mx = i == 0 ? max.X : i == 1 ? max.Y : max.Z;

            if (MathF.Abs(d) < 1e-8f)
            {
                if (o < mn || o > mx)
                {
                    return false;
                }
            }
            else
            {
                var t1 = (mn - o) / d;
                var t2 = (mx - o) / d;

                if (t1 > t2)
                {
                    (t1, t2) = (t2, t1);
                }

                tMin = MathF.Max(tMin, t1);
                tMax = MathF.Min(tMax, t2);

                if (tMin > tMax)
                {
                    return false;
                }
            }
        }

        if (tMax < 0)
        {
            return false;
        }

        if (tMin < 0)
        {
            tMin = tMax;
        }

        if (tMin > maxDist)
        {
            return false;
        }

        distance = tMin;
        
        return true;
    }
}
