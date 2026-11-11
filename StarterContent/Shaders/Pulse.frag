#version 330 core

in vec2 vTexCoord;
in vec3 vWorldPos;
out vec4 FragColor;

uniform sampler2D uTexture;
uniform vec4 uColor;
uniform float uTime;
uniform float uAlphaCutoff;

// Parameters supplied by the component's ShaderParameters.
uniform vec3 uPulseColor = vec3(0.2, 0.8, 1.0);
uniform float uPulseSpeed = 2.0;
uniform float uPulseStrength = 0.5;

// Optional: preserve the engine's normal fog behavior.
uniform vec3 uCameraPos;
uniform vec3 uFogColor;
uniform float uFogNear;
uniform float uFogFar;
uniform bool uFogEnabled;

void main()
{
    vec4 surface = texture(uTexture, vTexCoord) * uColor;
    if (surface.a < uAlphaCutoff) discard;
    float pulse = 0.5 + 0.5 * sin(uTime * uPulseSpeed);
    vec3 color = mix(surface.rgb, surface.rgb * uPulseColor,
        pulse * clamp(uPulseStrength, 0.0, 1.0));
    if (uFogEnabled)
    {
        float distanceToCamera = length(vWorldPos - uCameraPos);
        float fog = clamp((uFogFar - distanceToCamera) / max(uFogFar - uFogNear, 0.0001), 0.0, 1.0);
        color = mix(uFogColor, color, fog);
    }
    FragColor = vec4(color, surface.a);
}
