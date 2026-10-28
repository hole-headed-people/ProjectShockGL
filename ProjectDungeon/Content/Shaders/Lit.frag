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

void main()
{
    vec4 texColor = texture(uTexture, vTexCoord);
    
    if (texColor.a < 0.5) discard;
    
    vec3 baseColor = texColor.rgb * uColor.rgb;

    vec3 N = normalize(vNormal);
    
    vec3 ambient = uAmbientColor * baseColor;
    vec3 finalColor = ambient;
    
    if(uMainLightEnabled > 0.5){
        vec3 L = normalize(-uLightDirection);
        float diff = max(dot(N, L), 0.0);
        vec3 diffuse = uLightColor * uLightIntensity * diff * baseColor;
        
        finalColor += diffuse;
    }

    for (int i = 0; i < uPointLightCount; i++)
    {
        vec3 toLight = uPointLightPositions[i] - vWorldPos;
        float dist = length(toLight);
        if (dist <= 0.00001 || dist >= uPointLightRadii[i]) continue;
        vec3 L = toLight / dist;

        float diff = max(dot(N, L), 0.0);
        float atten = clamp(1.0 - dist / uPointLightRadii[i], 0.0, 1.0);
        vec2 cone = uPointLightConeCosines[i];
        if (cone.y >= -1.0)
        {
            float cosine = dot(-L, normalize(uPointLightDirections[i]));
            float coneFactor = cone.x - cone.y < 0.000001
                ? step(cone.y, cosine)
                : smoothstep(cone.y, cone.x, cosine);
            atten *= coneFactor;
        }

        vec3 light = uPointLightColors[i] * uPointLightIntensities[i] * atten * diff * baseColor;
        finalColor += light;
    }

    if (uForceUnlit) finalColor = baseColor;

    if (uFogEnabled)
    {
        float dist = length(vWorldPos - uCameraPos);
        float fogFactor = clamp((uFogFar - dist) / (uFogFar - uFogNear), 0.0, 1.0);
        finalColor = mix(uFogColor, finalColor, fogFactor);
    }

    FragColor = vec4(finalColor, texColor.a * uColor.a);
}
