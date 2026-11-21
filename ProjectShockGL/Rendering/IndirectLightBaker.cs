using System.Numerics;

namespace ProjectShockGL.Rendering;

public static partial class DirectLightBaker
{
    internal static Vector3 SampleWithIndirect(DirectLightBakeInput input, Vector3 position, Vector3 normal, DirectLightBakeOptions options)
    {
        options.Validate();
        normal = Vector3.Normalize(normal);
        var scene = new LightmapOcclusion(input.Triangles,input.Alpha);
        return Evaluate(input,scene,position,normal,-1,options.RayBias) +
            EvaluateIndirect(input,scene,position,normal,-1,options,0,CancellationToken.None);
    }

    private static Vector3 EvaluateIndirect(DirectLightBakeInput input, LightmapOcclusion scene, Vector3 position,
        Vector3 normal, int triangle, DirectLightBakeOptions options, uint seed, CancellationToken cancellation)
    {
        if (options.IndirectSamples == 0 || options.IndirectStrength == 0 || input.Lights.Count == 0) return Vector3.Zero;
        var tangent = Vector3.Normalize(Vector3.Cross(MathF.Abs(normal.Y) < .99f ? Vector3.UnitY : Vector3.UnitX,normal));
        var bitangent = Vector3.Cross(normal,tangent);
        var rotation = (Hash(seed) >> 8) / 16777216f;
        var origin = position + normal * options.RayBias;
        var sum = Vector3.Zero;

        for (var i = 0; i < options.IndirectSamples; i++)
        {
            if ((i & 15) == 0) cancellation.ThrowIfCancellationRequested();
            var u = (i + .5f) / options.IndirectSamples;
            var v = RadicalInverse((uint)i) + rotation;
            v -= MathF.Floor(v);
            var r = MathF.Sqrt(u);
            var angle = v * MathF.PI * 2;
            var direction = tangent * (r*MathF.Cos(angle)) + bitangent * (r*MathF.Sin(angle)) + normal * MathF.Sqrt(1-u);
            direction = Vector3.Normalize(direction);
            if (!scene.Nearest(origin,direction,triangle,options.RayBias*.1f,out var hit)) continue;

            var t = input.Triangles[hit.Triangle];
            // Unlit geometry can block rays, but is not an implicit emissive or reflective light source.
            if (!t.Material.ReceivesLight) continue;
            var wa = 1 - hit.U - hit.V;
            var hitNormal = t.A.Normal*wa + t.B.Normal*hit.U + t.C.Normal*hit.V;
            if (hitNormal.LengthSquared() < 1e-16f) hitNormal = Vector3.Cross(t.B.Position-t.A.Position,t.C.Position-t.A.Position);
            if (hitNormal.LengthSquared() < 1e-16f) continue;
            hitNormal = Vector3.Normalize(hitNormal);
            if (!t.Material.BackfaceCulling && Vector3.Dot(hitNormal,direction)>0) hitNormal = -hitNormal;
            if (Vector3.Dot(hitNormal,direction)>=0) continue;

            var hitPosition = t.A.Position*wa + t.B.Position*hit.U + t.C.Position*hit.V;
            var uv = t.A.TextureUV*wa + t.B.TextureUV*hit.U + t.C.TextureUV*hit.V;
            var color = t.Material.Color;
            var reflectance = new Vector3(color.R,color.G,color.B) * (input.Albedo[hit.Triangle]?.Sample(uv) ?? Vector3.One);
            reflectance = Vector3.Clamp(reflectance,Vector3.Zero,Vector3.One);
            if (reflectance == Vector3.Zero) continue;
            // Exactly one bounce: evaluate only direct Baked light here. No Ambient and no recursion.
            var incoming = Evaluate(input,scene,hitPosition,hitNormal,hit.Triangle,options.RayBias,includeAmbient:false);
            // Cosine-weighted hemisphere sampling cancels the Lambert 1/pi factor against its PDF.
            sum += incoming * reflectance;
        }
        var result = sum / options.IndirectSamples * options.IndirectStrength;
        if (!Finite(result)) throw new InvalidDataException("Indirect lighting overflowed.");
        return result;
    }

    private static uint Hash(uint value)
    {
        value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu;
        return value ^ (value >> 16);
    }

    private static float RadicalInverse(uint bits)
    {
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x55555555u) << 1) | ((bits & 0xaaaaaaaau) >> 1);
        bits = ((bits & 0x33333333u) << 2) | ((bits & 0xccccccccu) >> 2);
        bits = ((bits & 0x0f0f0f0fu) << 4) | ((bits & 0xf0f0f0f0u) >> 4);
        bits = ((bits & 0x00ff00ffu) << 8) | ((bits & 0xff00ff00u) >> 8);
        return bits * 2.3283064365386963e-10f;
    }
}