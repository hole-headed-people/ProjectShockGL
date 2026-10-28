#version 330 core

in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D uTexture;
uniform float uTime;
uniform vec2 uScreenSize;
uniform vec2 uTexelSize;

uniform float uVignetteStrength = 0.45;
uniform float uVignetteRadius = 0.3;
uniform float uVignetteSoftness = 0.5;

uniform float uGrainStrength = 0.0;
uniform float uGrainSize = 1.0;
uniform float uChromaticAberrationPixels = 0.0;

float grainNoise(vec2 cell, float frame)
{
    uint hash = uint(cell.x) * 1973u + uint(cell.y) * 9277u + uint(frame) * 26699u + 911u;
    hash = (hash ^ (hash >> 16)) * 2246822519u;
    hash = (hash ^ (hash >> 13)) * 3266489917u;
    hash ^= hash >> 16;
    return float(hash & 0x00ffffffu) / 16777215.0;
}

void main()
{
    vec4 scene = texture(uTexture, vTexCoord);
    if (uChromaticAberrationPixels > 0.0)
    {
        vec2 centered = vTexCoord * 2.0 - 1.0;
        vec2 radialPixels = (vTexCoord - 0.5) * uScreenSize;
        vec2 direction = radialPixels / max(length(radialPixels), 0.0001);
        float edgeWeight = clamp(dot(centered, centered) * 0.5, 0.0, 1.0);
        vec2 offset = direction * uTexelSize * uChromaticAberrationPixels * edgeWeight;
        scene.r = texture(uTexture, vTexCoord + offset).r;
        scene.b = texture(uTexture, vTexCoord - offset).b;
    }
    float distanceFromCenter = length((vTexCoord - 0.5) * 2.0) / sqrt(2.0);
    float radius = clamp(uVignetteRadius, 0.0, 1.0);
    float edge = smoothstep(radius, radius + max(uVignetteSoftness, 0.0001), distanceFromCenter);
    float brightness = 1.0 - edge * clamp(uVignetteStrength, 0.0, 1.0);
    scene.rgb *= brightness;

    if (uGrainStrength > 0.0)
    {
        vec2 cell = floor(gl_FragCoord.xy / max(uGrainSize, 1.0));
        float noise = grainNoise(cell, floor(max(uTime, 0.0) * 24.0)) - 0.5;
        scene.rgb = clamp(scene.rgb + noise * clamp(uGrainStrength, 0.0, 1.0), 0.0, 1.0);
    }
    FragColor = scene;
}
