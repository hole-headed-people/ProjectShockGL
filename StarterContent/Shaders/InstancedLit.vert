#version 330 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoord;

uniform mat4 uProjection;
uniform mat4 uView;
layout (location = 3) in mat4 aInstanceModel;

out vec2 vTexCoord;
out vec3 vNormal;
out vec3 vWorldPos;

void main()
{
    vTexCoord = aTexCoord;

    vec4 worldPos = aInstanceModel * vec4(aPosition, 1.0);
    vWorldPos = worldPos.xyz;
    
    vNormal = transpose(inverse(mat3(aInstanceModel))) * aNormal;

    gl_Position = uProjection * uView * worldPos;
}