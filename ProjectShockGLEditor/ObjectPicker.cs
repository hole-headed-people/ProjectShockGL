using System.Numerics;
using ProjectShockGL;
using ProjectShockGL.Rendering;

namespace ProjectShockGLEditor;

public static class ObjectPicker
{
    public static void PickAt(Vector2 mousePos, Vector2 screenSize)
    {
        var camera = Camera.Active;

        if (!GizmoMath.ScreenRay(mousePos, screenSize, camera.GetViewMatrix() * camera.GetProjectionMatrix(),
                out var origin, out var direction))
        {
            return;
        }
        
        Selection.Object = Pick(origin, direction, camera.Far,
            ProjectShockGL.Core.Game.Instance.World.GetAllObjects(includePending: true));
    }
    
    private static ProjectShockGL.Core.Object? Pick(Vector3 rayOrigin, Vector3 rayDir, 
        float maxDist, IEnumerable<ProjectShockGL.Core.Object> objects)
    {
        ProjectShockGL.Core.Object? closest = null;
        
        var closestDist = float.MaxValue;

        foreach (var obj in objects)
        {
            if (!obj.IsActive || obj.IsDestroyed || !obj.IsEditorVisible)
            {
                continue;
            }

            var (min, max) = obj.GetWorldBounds();

            if (RayAabb(rayOrigin, rayDir, maxDist, min, max, out var dist))
            {
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = obj;
                }
            }
        }

        return closest;
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
