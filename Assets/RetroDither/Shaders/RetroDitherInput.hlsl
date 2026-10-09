#ifndef AAR_RETRO_DITHER_INPUT_INCLUDED
#define AAR_RETRO_DITHER_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "RetroDitherCore.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

// Identical buffer in every pass keeps this shader compatible with the SRP Batcher.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float4 _ShadowColor;
    float4 _DarkColor;
    float4 _LightColor;
    float4 _HighlightColor;
    float _DitherStrength;
    float _PixelSize;
    float _PatternSize;
    float _PatternSpace;
    float _UVDensity;
    float _Contrast;
    float _Brightness;
    float _Exposure;
    float _Ambient;
    float _LightWrap;
    float _Specular;
    float _Shininess;
    float _RimStrength;
    float _RimPower;
CBUFFER_END

RetroPalette RetroGetPalette()
{
    RetroPalette palette;
    palette.shadow = _ShadowColor.rgb;
    palette.dark = _DarkColor.rgb;
    palette.light = _LightColor.rgb;
    palette.highlight = _HighlightColor.rgb;
    return palette;
}

RetroDitherSettings RetroGetSettings()
{
    RetroDitherSettings settings;
    settings.strength = _DitherStrength;
    settings.contrast = _Contrast;
    settings.brightness = _Brightness;
    settings.patternSize = _PatternSize;
    return settings;
}

#endif
