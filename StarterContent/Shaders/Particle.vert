#version 330 core
layout(location=0) in vec3 aPosition;
layout(location=2) in vec2 aTexCoord;
layout(location=9) in vec4 aColor;
uniform mat4 uProjection;
uniform mat4 uView;
uniform mat4 uModel;
out vec2 vTexCoord;
out vec3 vWorldPos;
out vec4 vColor;
void main()
{
    vec4 world=uModel*vec4(aPosition,1.0);
    vWorldPos=world.xyz;
    vTexCoord=aTexCoord;
    vColor=aColor;
    gl_Position=uProjection*uView*world;
}