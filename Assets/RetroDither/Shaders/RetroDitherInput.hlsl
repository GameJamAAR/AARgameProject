#ifndef AAR_RETRO_DITHER_INPUT_INCLUDED
#define AAR_RETRO_DITHER_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "RetroDitherCore.hlsl"
#include "../../RetroScreen/Shaders/RetroScreenCore.hlsl"

float4 _RetroScreenDitherWarp;
float4 _RetroScreenDitherOutputSize;
float2 RetroDitherOutputDimensions()
{
    return _RetroScreenDitherOutputSize.x > 0.5 && _RetroScreenDitherOutputSize.y > 0.5
        ? _RetroScreenDitherOutputSize.xy : _ScaledScreenParams.xy;
}

float2 RetroDitherScreenContinuous(float2 screenPosition)
{
    float2 uv = screenPosition / _ScaledScreenParams.xy;
    if (_RetroScreenDitherWarp.w > 0.5)
        uv = RetroScreenUnwarp(uv, _RetroScreenDitherWarp.xy, _RetroScreenDitherWarp.z);
    return uv * RetroDitherOutputDimensions();
}

float2 RetroDitherScreenCoordinates(float2 screenPosition)
{
    // Snap to output pixel centers, while retaining the continuous mapping for
    // lighting derivatives. Nearest sampling then preserves final cell boundaries.
    return floor(RetroDitherScreenContinuous(screenPosition)) + 0.5;
}

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_PCBNormalMap); SAMPLER(sampler_PCBNormalMap);
TEXTURE2D(_PCBMaskMap); SAMPLER(sampler_PCBMaskMap);

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
    float _PatternScaleMode;
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
    float _NormalizePalette;
    float _DetailStability;
    float _SpecularAA;
    float _ReferenceHeight;
    float _DistanceFade;
    float _FadeStart;
    float _FadeEnd;
    float _PreserveDither;
    float _CellLighting;
    float4 _PatternOffset;
    float _ShadowOnly;
    float4 _GroundColor;
    float4 _GroundShadowColor;
    float _ShadowOpacity;
    float _PCBBaked;
    float _PCBContactBrightness;
    float _PCBContactSpecular;
    float4 _PCBLegendColor;
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
    settings.normalizePalette = _NormalizePalette;
    return settings;
}

// Optional scene-level look controller. Zero enabled flag preserves material behavior.
float4 _RetroLookFrom;
float4 _RetroLookTo;
float4 _RetroLookWave;
float4 _RetroLookCenter;
float RetroLookBlend(float3 worldPosition)
{
    if (_RetroLookWave.z < 0.5) return 1.0;
    float distanceToOrigin = length(worldPosition.xz - _RetroLookCenter.xz);
    float x = (_RetroLookWave.x - distanceToOrigin) / max(_RetroLookWave.y, 0.05);
    // Normal CDF via erf approximation: the derivative is a Gaussian bell.
    float a = abs(x) * 0.70710678;
    float t = 1.0 / (1.0 + 0.3275911 * a);
    float erf = 1.0 - (((((1.061405429*t - 1.453152027)*t + 1.421413741)*t
        - 0.284496736)*t + 0.254829592)*t) * exp(-a*a);
    return saturate(0.5 + 0.5 * (x < 0 ? -erf : erf));
}
float4 _RetroFromInk, _RetroFromShadow, _RetroFromLight, _RetroFromHighlight;
float4 _RetroToInk, _RetroToShadow, _RetroToLight, _RetroToHighlight;
RetroPalette RetroLookPalette(bool target)
{
    RetroPalette palette = RetroGetPalette();
    float enabled = _RetroLookWave.w > 0.5 ? (target ? _RetroLookTo.x : _RetroLookFrom.x) : 0;
    palette.shadow = lerp(palette.shadow, target ? _RetroToInk.rgb : _RetroFromInk.rgb, enabled);
    palette.dark = lerp(palette.dark, target ? _RetroToShadow.rgb : _RetroFromShadow.rgb, enabled);
    palette.light = lerp(palette.light, target ? _RetroToLight.rgb : _RetroFromLight.rgb, enabled);
    palette.highlight = lerp(palette.highlight, target ? _RetroToHighlight.rgb : _RetroFromHighlight.rgb, enabled);
    return palette;
}
float3 RetroLookColor(float tone, float2 pixel, float filterWidth, float visibility,
    float distanceFade, float3 worldPosition)
{
    float blend = RetroLookBlend(worldPosition);
    // Evaluate both palettes with identical tone, grid, thresholds and filtering.
    // Only the resulting colors are blended across the Gaussian front.
    float3 from = RetroQuantizeFiltered(tone,pixel,RetroLookPalette(false),RetroGetSettings(),filterWidth,visibility,distanceFade);
    float3 to = RetroQuantizeFiltered(tone,pixel,RetroLookPalette(true),RetroGetSettings(),filterWidth,visibility,distanceFade);
    return lerp(from,to,blend);
}
float3 RetroGroundLookColor(float coverage, bool target)
{
    float enabled = _RetroLookWave.w > 0.5 ? (target ? _RetroLookTo.x : _RetroLookFrom.x) : 0;
    float3 ink = target ? _RetroToInk.rgb : _RetroFromInk.rgb;
    float3 paper = target ? _RetroToHighlight.rgb : _RetroFromHighlight.rgb;
    float3 ground = lerp(_GroundColor.rgb,lerp(ink,paper,saturate(RetroLuminance(_GroundColor.rgb))),enabled);
    float3 shadow = lerp(_GroundShadowColor.rgb,lerp(ink,paper,saturate(RetroLuminance(_GroundShadowColor.rgb))),enabled);
    return lerp(ground,shadow,coverage);
}
float3 RetroGroundColor(float coverage,float3 worldPosition)
{
    return lerp(RetroGroundLookColor(coverage,false),RetroGroundLookColor(coverage,true),RetroLookBlend(worldPosition));
}
float3 RetroPCBComposite(float3 copper, float3 mask, float2 pixel, float3 positionWS, float shadowAttenuation)
{
    if (_PCBBaked < 0.5) return copper;
    float shadow = saturate((1-shadowAttenuation)*_ShadowOpacity);
    float threshold = lerp(0.5,RetroBayer(pixel,_PatternSize),saturate(_DitherStrength));
    float3 board = RetroGroundColor(step(threshold,shadow),positionWS);
    float coverage = saturate(mask.r+mask.g);
    return lerp(lerp(board,copper,coverage),_PCBLegendColor.rgb,saturate(mask.b));
}
#endif
