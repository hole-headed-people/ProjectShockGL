#version 330 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoord;

uniform mat4 uProjection;
uniform mat4 uView;
uniform mat4 uModel;
uniform vec4 uUvRect;

out vec2 vTexCoord;
out vec3 vWorldPos;

void main()
{
    vTexCoord = uUvRect.xy + aTexCoord * uUvRect.zw;

    vec4 worldPos = uModel * vec4(aPosition, 1.0);
    vWorldPos = worldPos.xyz;

    gl_Position = uProjection * uView * worldPos;
}
