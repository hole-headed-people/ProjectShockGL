#version 330 core

in vec2 vTexCoord;
in vec3 vWorldPos;

out vec4 FragColor;

uniform sampler2D uTexture;
uniform vec4 uColor;
uniform float uAlphaCutoff;

uniform vec3 uCameraPos;
uniform vec3 uFogColor;
uniform float uFogNear;
uniform float uFogFar;
uniform bool uFogEnabled;

void main()
{
    vec4 texColor = texture(uTexture, vTexCoord);
    if (texColor.a < uAlphaCutoff) discard;
    vec3 finalColor = texColor.rgb * uColor.rgb;

    if (uFogEnabled)
    {
        float dist = length(vWorldPos - uCameraPos);
        float fogFactor = clamp((uFogFar - dist) / (uFogFar - uFogNear), 0.0, 1.0);
        finalColor = mix(uFogColor, finalColor, fogFactor);
    }
    
    FragColor = vec4(finalColor, texColor.a * uColor.a);
}
