#version 330 core
#define MAX_POINT_LIGHTS 16

in vec2 vTexCoord;
in vec3 vNormal;
in vec3 vWorldPos;

out vec4 FragColor;

uniform sampler2D uTexture;
uniform vec4 uColor;
uniform bool uForceUnlit;

uniform float uMainLightEnabled;
uniform vec3 uLightDirection; 
uniform vec3 uLightColor;     
uniform float uLightIntensity;
uniform vec3 uAmbientColor;

uniform vec3 uCameraPos;
uniform vec3 uFogColor;
uniform float uFogNear;
uniform float uFogFar;
uniform bool uFogEnabled;

uniform int uPointLightCount;
uniform vec3 uPointLightPositions[MAX_POINT_LIGHTS];
uniform vec3 uPointLightColors[MAX_POINT_LIGHTS];
uniform float uPointLightIntensities[MAX_POINT_LIGHTS];
uniform float uPointLightRadii[MAX_POINT_LIGHTS];
uniform vec3 uPointLightDirections[MAX_POINT_LIGHTS];
uniform vec2 uPointLightConeCosines[MAX_POINT_LIGHTS];

uniform float uRoughness = 0.6;
uniform float uMetallic = 0.0;

const float PI = 3.14159265359;

vec3 safeNormalize(vec3 value, vec3 fallback)
{
    float squaredLength = dot(value, value);
    return squaredLength > 0.0000001 ? value * inversesqrt(squaredLength) : fallback;
}

vec3 directLight(vec3 N, vec3 V, vec3 L, vec3 radiance, vec3 albedo, float roughness, float metallic)
{
    float nDotL = max(dot(N, L), 0.0);
    float nDotV = max(dot(N, V), 0.0);
    if (nDotL <= 0.0 || nDotV <= 0.0)
    {
        return vec3(0.0);
    }

    vec3 H = safeNormalize(V + L, N);
    float nDotH = max(dot(N, H), 0.0);
    float vDotH = max(dot(V, H), 0.0);
    vec3 f0 = mix(vec3(0.04), albedo, metallic);
    vec3 F = f0 + (1.0 - f0) * pow(1.0 - vDotH, 5.0);

    // GGX distribution and Schlick-GGX visibility for direct illumination.
    float alpha = roughness * roughness;
    float alphaSquared = alpha * alpha;
    float denominator = nDotH * nDotH * (alphaSquared - 1.0) + 1.0;
    float D = alphaSquared / max(PI * denominator * denominator, 0.0000001);
    float k = (roughness + 1.0) * (roughness + 1.0) / 8.0;
    float Gv = nDotV / (nDotV * (1.0 - k) + k);
    float Gl = nDotL / (nDotL * (1.0 - k) + k);
    vec3 specular = D * Gv * Gl * F / max(4.0 * nDotV * nDotL, 0.00001);
    vec3 diffuse = (1.0 - F) * (1.0 - metallic) * albedo / PI;
    return (diffuse + specular) * radiance * nDotL;
}

void main()
{
    vec4 texColor = texture(uTexture, vTexCoord);
    if (texColor.a < 0.5)
    {
        discard;
    }

    vec3 albedo = max(texColor.rgb * uColor.rgb, vec3(0.0));
    float roughness = clamp(uRoughness, 0.08, 1.0);
    float metallic = clamp(uMetallic, 0.0, 1.0);
    vec3 N = safeNormalize(vNormal, vec3(0.0, 1.0, 0.0));
    vec3 V = safeNormalize(uCameraPos - vWorldPos, N);

    // Simple diffuse ambient only: metallic surfaces have no environment reflections.
    vec3 finalColor = uAmbientColor * albedo * (1.0 - metallic);
    if (uMainLightEnabled > 0.5)
    {
        vec3 L = safeNormalize(-uLightDirection, N);
        finalColor += directLight(N, V, L, uLightColor * uLightIntensity, albedo, roughness, metallic);
    }

    for (int i = 0; i < min(uPointLightCount, MAX_POINT_LIGHTS); i++)
    {
        vec3 toLight = uPointLightPositions[i] - vWorldPos;
        float dist = length(toLight);
        if (dist <= 0.00001 || dist >= uPointLightRadii[i])
        {
            continue;
        }

        vec3 L = toLight / dist;
        float attenuation = clamp(1.0 - dist / uPointLightRadii[i], 0.0, 1.0);
        vec2 cone = uPointLightConeCosines[i];
        if (cone.y >= -1.0)
        {
            float cosine = dot(-L, safeNormalize(uPointLightDirections[i], -L));
            float coneFactor = cone.x - cone.y < 0.000001
                ? step(cone.y, cosine)
                : smoothstep(cone.y, cone.x, cosine);
            attenuation *= coneFactor;
        }

        vec3 radiance = uPointLightColors[i] * uPointLightIntensities[i] * attenuation;
        finalColor += directLight(N, V, L, radiance, albedo, roughness, metallic);
    }

    if (uForceUnlit)
    {
        finalColor = albedo;
    }
    if (uFogEnabled)
    {
        float distanceToCamera = length(vWorldPos - uCameraPos);
        float fogFactor = clamp((uFogFar - distanceToCamera) / max(uFogFar - uFogNear, 0.00001), 0.0, 1.0);
        finalColor = mix(uFogColor, finalColor, fogFactor);
    }
    FragColor = vec4(finalColor, texColor.a * uColor.a);
}
