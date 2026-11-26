#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoord;
layout (location = 3) in mat4 aInstanceModel;
layout (location = 7) in vec2 aLightmapUV;
layout (location = 8) in vec4 aLightmapRegion;
uniform mat4 uProjection;
uniform mat4 uView;
uniform float uTime;
uniform float uWindStrength = 0.15;
uniform float uWindSpeed = 2.0;
uniform float uWindHeight = 1.0;
uniform float uWindBase = 0.0;
uniform float uWindFrequency = 0.8;
uniform vec2 uWindDirection = vec2(1.0, 0.3);
out vec2 vTexCoord;
out vec2 vLightmapUV;
out vec3 vNormal;
out vec3 vWorldPos;
void main()
{
    float height = max(uWindHeight, 0.0001);
    float t = clamp((aPosition.y - uWindBase) / height, 0.0, 1.0);
    float phase = uTime * uWindSpeed + dot(aInstanceModel[3].xz, vec2(0.73, 1.17)) * uWindFrequency;
    vec2 direction = uWindDirection / max(length(uWindDirection), 0.0001);
    float wave = sin(phase) * uWindStrength;
    vec3 position = aPosition + vec3(direction.x, 0.0, direction.y) * wave * t * t;
    vec3 normal = aNormal;
    float derivative = t > 0.0 && t < 1.0 ? 2.0 * t / height : 0.0;
    normal.y -= dot(aNormal.xz, direction) * wave * derivative;
    vec4 worldPos = aInstanceModel * vec4(position, 1.0);
    vWorldPos = worldPos.xyz;
    vNormal = transpose(inverse(mat3(aInstanceModel))) * normal;
    vTexCoord = aTexCoord;
    vLightmapUV = aLightmapUV * aLightmapRegion.xy + aLightmapRegion.zw;
    gl_Position = uProjection * uView * worldPos;
}
