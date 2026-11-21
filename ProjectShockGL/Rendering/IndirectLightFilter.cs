using System.Numerics;

namespace ProjectShockGL.Rendering;

public static partial class DirectLightBaker
{
    private static void AddFilteredIndirect(DirectLightBakeInput input, BakedLightmapPage[] pages, Vector3[][] indirect,
        int[][] triangles, int radius, CancellationToken cancellation)
    {
        var size=input.Geometry.AtlasSize;
        bool Frame(int id,int x,int y,out Vector3 position,out Vector3 normal,out float footprint)
        {
            var t=input.Triangles[id];
            var a=t.A.LightmapUV*size;var b=t.B.LightmapUV*size;var c=t.C.LightmapUV*size;
            var area=Cross(b-a,c-a);
            position=Vector3.Zero;normal=Vector3.Zero;footprint=0;
            if(MathF.Abs(area)<1e-12f)return false;
            var p=new Vector2(x+.5f,y+.5f);
            var wb=Cross(p-a,c-a)/area;var wc=Cross(b-a,p-a)/area;var wa=1-wb-wc;
            position=t.A.Position*wa+t.B.Position*wb+t.C.Position*wc;
            normal=t.A.Normal*wa+t.B.Normal*wb+t.C.Normal*wc;
            if(normal.LengthSquared()<1e-16f)normal=Vector3.Cross(t.B.Position-t.A.Position,t.C.Position-t.A.Position);
            if(normal.LengthSquared()<1e-16f)return false;
            normal=Vector3.Normalize(normal);
            var dx=((t.B.Position-t.A.Position)*(c.Y-a.Y)-(t.C.Position-t.A.Position)*(b.Y-a.Y))/area;
            var dy=((t.C.Position-t.A.Position)*(b.X-a.X)-(t.B.Position-t.A.Position)*(c.X-a.X))/area;
            footprint=MathF.Max(dx.Length(),dy.Length());
            return Finite(position)&&Finite(normal)&&float.IsFinite(footprint);
        }

        for(var page=0;page<pages.Length;page++)
        {
            for(var y=0;y<size;y++)
            {
                cancellation.ThrowIfCancellationRequested();
                for(var x=0;x<size;x++)
                {
                    var index=y*size+x;
                    var id=triangles[page][index]-1;
                    if(id<0)continue;
                    var sum=indirect[page][index];var weight=1f;
                    if(Frame(id,x,y,out var position,out var normal,out var footprint))
                    {
                        for(var dy=-radius;dy<=radius;dy++)for(var dx=-radius;dx<=radius;dx++)
                        {
                            if(dx==0&&dy==0)continue;
                            var nx=x+dx;var ny=y+dy;
                            if(nx<0||nx>=size||ny<0||ny>=size)continue;
                            var neighbour=ny*size+nx;
                            var other=triangles[page][neighbour]-1;
                            if(other<0||input.Triangles[id].Surface!=input.Triangles[other].Surface)continue;
                            if(!Frame(other,nx,ny,out var otherPosition,out var otherNormal,out var otherFootprint)||
                               Vector3.Dot(normal,otherNormal)<.95f)continue;
                            var limit=(radius+1)*MathF.Max(footprint,otherFootprint)*1.5f;
                            if(Vector3.DistanceSquared(position,otherPosition)>limit*limit)continue;
                            var w=1f/(1+dx*dx+dy*dy);
                            sum+=indirect[page][neighbour]*w;weight+=w;
                        }
                    }
                    pages[page].Values[index]+=sum/weight;
                    if(!Finite(pages[page].Values[index]))throw new InvalidDataException("Filtered indirect lighting overflowed.");
                }
            }
        }
    }
}