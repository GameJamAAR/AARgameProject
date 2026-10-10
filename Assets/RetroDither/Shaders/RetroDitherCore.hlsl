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
    float normalizePalette;
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

struct RetroPreparedPalette
{
    float3 color0;
    float3 color1;
    float3 color2;
    float3 color3;
    uint count;
};

float3 RetroPreparedColor(uint index, RetroPreparedPalette palette)
{
    if (index == 0u) return palette.color0;
    if (index == 1u) return palette.color1;
    if (index == 2u) return palette.color2;
    return palette.color3;
}

void RetroSortPair(inout float3 a, inout float3 b)
{
    if (RetroLuminance(a) > RetroLuminance(b))
    {
        float3 swap = a;
        a = b;
        b = swap;
    }
}

void RetroAppendUnique(inout RetroPreparedPalette palette, float3 color)
{
    float3 difference = color - RetroPreparedColor(palette.count - 1u, palette);
    if (dot(difference, difference) <= 0.000001) return;
    if (palette.count == 1u) palette.color1 = color;
    else if (palette.count == 2u) palette.color2 = color;
    else palette.color3 = color;
    palette.count++;
}

RetroPreparedPalette RetroPreparePalette(RetroPalette palette, float normalizePalette)
{
    RetroPreparedPalette prepared;
    prepared.color0 = palette.shadow;
    prepared.color1 = palette.dark;
    prepared.color2 = palette.light;
    prepared.color3 = palette.highlight;
    prepared.count = 4u;
    if (normalizePalette < 0.5) return prepared;

    // Fixed sorting network: no dynamic arrays or loops on mobile/Metal targets.
    RetroSortPair(prepared.color0, prepared.color1);
    RetroSortPair(prepared.color2, prepared.color3);
    RetroSortPair(prepared.color0, prepared.color2);
    RetroSortPair(prepared.color1, prepared.color3);
    RetroSortPair(prepared.color1, prepared.color2);
    float3 color1 = prepared.color1;
    float3 color2 = prepared.color2;
    float3 color3 = prepared.color3;
    prepared.count = 1u;
    RetroAppendUnique(prepared, color1);
    RetroAppendUnique(prepared, color2);
    RetroAppendUnique(prepared, color3);
    return prepared;
}

// Filtering inputs are explicit so the core can be reused in other fragment shaders.
float3 RetroQuantizeFiltered(float tone, float2 pixel, RetroPalette palette,
    RetroDitherSettings settings, float filterWidth, float patternVisibility, float distanceFade)
{
    RetroPreparedPalette prepared = RetroPreparePalette(palette, settings.normalizePalette);
    tone = saturate((tone - 0.5) * max(settings.contrast, 0.01) + 0.5 + settings.brightness);
    float ramp = tone * (float)(prepared.count - 1u);
    uint lower = (uint)floor(ramp);
    uint upper = min(lower + 1u, prepared.count - 1u);
    // Zero strength is ordinary nearest-band quantization; one uses the full Bayer threshold.
    float threshold = lerp(0.5, RetroBayer(pixel, settings.patternSize), saturate(settings.strength));
    float fraction = frac(ramp);
    float coverage = step(threshold, fraction);
    if (filterWidth > 0.0001)
        coverage = smoothstep(threshold - filterWidth, threshold + filterWidth, fraction);
    // When cells become subpixel or distant, use their average instead of unstable speckles.
    float resolved = saturate(patternVisibility) * (1.0 - saturate(distanceFade));
    coverage = lerp(fraction, coverage, resolved);
    return lerp(RetroPreparedColor(lower, prepared), RetroPreparedColor(upper, prepared), coverage);
}

float3 RetroQuantize(float tone, float2 pixel, RetroPalette palette, RetroDitherSettings settings)
{
    return RetroQuantizeFiltered(tone, pixel, palette, settings, 0.0, 1.0, 0.0);
}

#endif
