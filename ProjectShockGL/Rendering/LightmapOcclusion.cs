using System.Numerics;

namespace ProjectShockGL.Rendering;

internal sealed partial class LightmapOcclusion
{
    private readonly LightmapTriangle[] _triangles;
    private readonly LightmapAlpha?[] _alpha;
    
    private readonly int[] _order;
    
    private readonly List<Node> _nodes = [];
    
    private readonly CancellationToken _cancellation;
    
    internal long TriangleTests { get; private set; }
    internal long Rays { get; private set; }
    
    private readonly record struct Node(Vector3 Min, Vector3 Max, int Start, int Count, int Left, int Right);

    internal LightmapOcclusion(LightmapTriangle[] triangles, LightmapAlpha?[] alpha, CancellationToken cancellation = default)
    {
        _cancellation = cancellation;
        
        _triangles = triangles;
        _alpha = alpha;
        
        _order = Enumerable.Range(0, triangles.Length)
            .Where(index => triangles[index].Material.SurfaceMode != SurfaceMode.Transparent).ToArray();
        
        if (_order.Length != 0)
        {
            Build(0, _order.Length);
        }
    }

    private int Build(int start, int count)
    {
        _cancellation.ThrowIfCancellationRequested();
        
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        
        for (var i = start; i < start + count; i++)
        {
            var triangle = _triangles[_order[i]];
            
            min = Vector3.Min(min, Vector3.Min(triangle.A.Position, Vector3.Min(triangle.B.Position, triangle.C.Position)));
            max = Vector3.Max(max, Vector3.Max(triangle.A.Position, Vector3.Max(triangle.B.Position, triangle.C.Position)));
        }
        
        var index = _nodes.Count;
        
        _nodes.Add(default);
        
        if (count <= 8)
        {
            _nodes[index] = new Node(min, max, start, count, -1, -1);
            
            return index;
        }

        var size = max - min;
        var axis = size.X >= size.Y && size.X >= size.Z ? 0 : size.Y >= size.Z ? 1 : 2;
        
        float Center(int id)
        {
            var t = _triangles[id];
            
            return t.A.Position[axis] / 3f + t.B.Position[axis] / 3f + t.C.Position[axis] / 3f;
        }
        
        Array.Sort(_order, start, count, Comparer<int>.Create((a, b) =>
        {
            var result = Center(a).CompareTo(Center(b));
            return result == 0 ? a.CompareTo(b) : result;
        }));
        
        var half = count / 2;
        
        var left = Build(start, half);
        var right = Build(start + half, count - half);
        
        _nodes[index] = new Node(min, max, start, 0, left, right);
        
        return index;
    }

    internal bool Blocked(Vector3 origin, Vector3 direction, float distance, int ignoreTriangle, float epsilon)
    {
        Rays++;
        
        return _nodes.Count != 0 && Visit(0, origin, direction, distance, ignoreTriangle, epsilon);
    }

    private bool Visit(int index, Vector3 origin, Vector3 direction, float distance, int ignoreTriangle, float epsilon)
    {
        var node = _nodes[index];
        var near = 0f;
        var far = distance;
        
        for (var axis = 0; axis < 3; axis++)
        {
            if (MathF.Abs(direction[axis]) < 1e-20f)
            {
                if (origin[axis] < node.Min[axis] || origin[axis] > node.Max[axis])
                {
                    return false;
                }
                
                continue;
            }
            
            var a = (node.Min[axis] - origin[axis]) / direction[axis];
            var b = (node.Max[axis] - origin[axis]) / direction[axis];

            if (a > b)
            {
                (a, b) = (b, a);
            }
            
            near = MathF.Max(near, a);
            far = MathF.Min(far, b);

            if (near > far)
            {
                return false;
            }
        }

        if (node.Count == 0)
        {
            return Visit(node.Left, origin, direction, distance, ignoreTriangle, epsilon) ||
                Visit(node.Right, origin, direction, distance, ignoreTriangle, epsilon);
        }

        for (var at = node.Start; at < node.Start + node.Count; at++)
        {
            var id = _order[at];

            if (id == ignoreTriangle)
            {
                continue;
            }
            
            TriangleTests++;
            
            var t = _triangles[id];
            
            var e1 = t.B.Position - t.A.Position;
            var e2 = t.C.Position - t.A.Position;
            
            var p = Vector3.Cross(direction, e2);
            var det = Vector3.Dot(e1, p);
            
            // Rays travel from receiver to light. Cull surfaces facing away from the light.
            if (t.Material.BackfaceCulling ? det >= -1e-10f : MathF.Abs(det) <= 1e-10f)
            {
                continue;
            }
            
            var inverse = 1f / det;
            var delta = origin - t.A.Position;
            
            var u = Vector3.Dot(delta, p) * inverse;

            if (u < 0 || u > 1)
            {
                continue;
            }
            
            var q = Vector3.Cross(delta, e1);
            var v = Vector3.Dot(direction, q) * inverse;

            if (v < 0 || u + v > 1)
            {
                continue;
            }
            
            var hit = Vector3.Dot(e2, q) * inverse;
            
            if (hit <= epsilon || hit >= distance)
            {
                continue;
            }
            
            if (t.Material.SurfaceMode == SurfaceMode.Cutout)
            {
                var uv = t.A.TextureUV * (1 - u - v) + t.B.TextureUV * u + t.C.TextureUV * v;
                
                if ((_alpha[id]?.Sample(uv) ?? 1) < t.Material.AlphaCutoff)
                {
                    continue;
                }
            }
            
            return true;
        }
        
        return false;
    }
}