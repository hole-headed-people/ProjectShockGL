using System.Numerics;

namespace ProjectShockGL.Rendering;

internal readonly record struct LightmapRayHit(int Triangle, float U, float V, float Distance);

internal sealed partial class LightmapOcclusion
{
    internal long IndirectRays { get; private set; }

    internal bool Nearest(Vector3 origin, Vector3 direction, int ignoreTriangle, float epsilon, out LightmapRayHit hit)
    {
        IndirectRays++;
        
        hit = new LightmapRayHit(-1,0,0,float.PositiveInfinity);

        if (_nodes.Count != 0)
        {
            NearestNode(0,origin,direction,ignoreTriangle,epsilon,ref hit);
        }
        
        return hit.Triangle >= 0;
    }

    private void NearestNode(int index, Vector3 origin, Vector3 direction, int ignoreTriangle, float epsilon, ref LightmapRayHit nearest)
    {
        var node=_nodes[index];
        var near=0f; var far=nearest.Distance;
        
        for(var axis=0;axis<3;axis++)
        {
            if(MathF.Abs(direction[axis])<1e-20f)
            {
                if (origin[axis] < node.Min[axis] || origin[axis] > node.Max[axis])
                {
                    return;
                }
                
                continue;
            }
            
            var a=(node.Min[axis]-origin[axis])/direction[axis];
            var b=(node.Max[axis]-origin[axis])/direction[axis];

            if (a > b)
            {
                (a,b)=(b,a);
            }
            
            near=MathF.Max(near,a); far=MathF.Min(far,b);

            if (near > far)
            {
                return;
            }
        }
        
        if(node.Count==0)
        {
            NearestNode(node.Left,origin,direction,ignoreTriangle,epsilon,ref nearest);
            NearestNode(node.Right,origin,direction,ignoreTriangle,epsilon,ref nearest);
            
            return;
        }
        
        for(var at=node.Start;at<node.Start+node.Count;at++)
        {
            var id=_order[at];

            if (id == ignoreTriangle)
            {
                continue;
            }
            
            TriangleTests++;
            
            var t=_triangles[id];
            
            var e1=t.B.Position-t.A.Position; var e2=t.C.Position-t.A.Position;
            
            var p=Vector3.Cross(direction,e2);
            var det=Vector3.Dot(e1,p);
            
            // GI rays travel from receiver toward visible reflecting surfaces.
            
            if (t.Material.BackfaceCulling ? det <= 1e-10f : MathF.Abs(det) <= 1e-10f)
            {
                continue;
            }
            
            var inverse=1f/det; var delta=origin-t.A.Position;
            
            var u=Vector3.Dot(delta,p)*inverse;

            if (u < 0 || u > 1)
            {
                continue;
            }
            
            var q=Vector3.Cross(delta,e1);
            var v=Vector3.Dot(direction,q)*inverse;

            if (v < 0 || u + v > 1)
            {
                continue;
            }
            
            var distance=Vector3.Dot(e2,q)*inverse;

            if (distance <= epsilon || distance >= nearest.Distance)
            {
                continue;
            }
            
            if(t.Material.SurfaceMode==SurfaceMode.Cutout)
            {
                var uv=t.A.TextureUV*(1-u-v)+t.B.TextureUV*u+t.C.TextureUV*v;

                if ((_alpha[id]?.Sample(uv) ?? 1) < t.Material.AlphaCutoff)
                {
                    continue;
                }
            }
            
            nearest=new LightmapRayHit(id,u,v,distance);
        }
    }
}