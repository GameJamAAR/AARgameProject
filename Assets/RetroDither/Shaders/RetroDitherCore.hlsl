#ifndef AAR_RETRO_DITHER_CORE_INCLUDED
#define AAR_RETRO_DITHER_CORE_INCLUDED

// Pure functions: no URP dependency, globals, or texture reads.
struct RetroPalette
{
    float3 shadow;
    float3 dark;
    float3 light;
    float3 highlight;
};

struct RetroDitherSettings
{
    float strength;
    float contrast;
    float brightness;
    float patternSize;
};

float RetroLuminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

// Recursive Bayer construction yields 2x2, 4x4, or 8x8 without a texture.
float RetroBayer(float2 pixel, float patternSize)
{
    uint size = patternSize < 3.0 ? 2u : (patternSize < 6.0 ? 4u : 8u);
    // Wrap before converting to uint so negative/tiled UVs repeat correctly too.
    uint2 p = (uint2)floor(pixel - floor(pixel / (float)size) * (float)size);
    uint rank = 0u;
    for (uint bit = 0u; (1u << bit) < size; ++bit)
    {
        uint x = (p.x >> bit) & 1u;
        uint y = (p.y >> bit) & 1u;
        rank = rank * 4u + ((x ^ y) * 2u + y);
    }
    return (rank + 0.5) / (float)(size * size);
}

float3 RetroPaletteColor(float index, RetroPalette palette)
{
    if (index < 0.5) return palette.shadow;
    if (index < 1.5) return palette.dark;
    if (index < 2.5) return palette.light;
    return palette.highlight;
}

float3 RetroQuantize(float tone, float2 pixel, RetroPalette palette, RetroDitherSettings settings)
{
    tone = saturate((tone - 0.5) * max(settings.contrast, 0.01) + 0.5 + settings.brightness);
    float ramp = tone * 3.0;
    float lower = floor(ramp);
    // Zero strength is ordinary nearest-band quantization; one uses the full Bayer threshold.
    float threshold = lerp(0.5, RetroBayer(pixel, settings.patternSize), saturate(settings.strength));
    float index = min(lower + step(threshold, frac(ramp)), 3.0);
    return RetroPaletteColor(index, palette);
}

#endif
