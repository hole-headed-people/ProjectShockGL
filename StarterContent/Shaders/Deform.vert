#version 330 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoord;

uniform mat4 uProjection;
uniform mat4 uView;
uniform mat4 uModel;
uniform vec4 uUvRect;
uniform float uTime;
uniform float uAmplitude = 0.2;
uniform float uSpeed = 2.0;
uniform float uFrequency = 3.0;

out vec2 vTexCoord;
out vec3 vWorldPos;
out vec3 vNormal;

void main()
{
    vec3 position = aPosition;
    
    float phase = position.y * uFrequency + uTime * uSpeed;
    position.x += sin(phase) * uAmplitude;
    
    vec3 normal = vec3(aNormal.x,
        aNormal.y - cos(phase) * uAmplitude * uFrequency * aNormal.x, aNormal.z);
    
    vec4 worldPosition = uModel * vec4(position, 1.0);
    
    vWorldPos = worldPosition.xyz;
    vNormal = transpose(inverse(mat3(uModel))) * normal;
    vTexCoord = uUvRect.xy + aTexCoord * uUvRect.zw;
    
    gl_Position = uProjection * uView * worldPosition;
}
