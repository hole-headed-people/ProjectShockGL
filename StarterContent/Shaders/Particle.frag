#version 330 core
in vec2 vTexCoord;
in vec3 vWorldPos;
in vec4 vColor;
out vec4 FragColor;
uniform sampler2D uTexture;
uniform vec4 uColor;
uniform int uParticleBlendMode = 0;
uniform vec3 uCameraPos;
uniform vec3 uFogColor;
uniform float uFogNear;
uniform float uFogFar;
uniform bool uFogEnabled;
void main()
{
    vec4 color=texture(uTexture,vTexCoord)*vColor*uColor;
    if(uFogEnabled)
    {
        float factor=clamp((uFogFar-length(vWorldPos-uCameraPos))/(uFogFar-uFogNear),0.0,1.0);
        vec3 neutral=uParticleBlendMode==1 ? vec3(0.0) : uParticleBlendMode==2 ? vec3(1.0) : uFogColor;
        color.rgb=mix(neutral,color.rgb,factor);
    }
    // DstColor / OneMinusSrcAlpha: destination * (color * alpha + 1 - alpha).
    if(uParticleBlendMode==2) color.rgb*=color.a;
    FragColor=color;
}