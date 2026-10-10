Shader "AAR/Retro Screen"
{
    Properties
    {
        _Intensity ("Effect Blend", Range(0,1)) = 1
        _CurvatureX ("Horizontal Curvature", Range(0,0.6)) = 0.24
        _CurvatureY ("Vertical Curvature", Range(0,0.6)) = 0.2
        _Zoom ("Zoom", Range(0.75,1.3)) = 1
        _CornerRadius ("Corner Radius", Range(0,0.5)) = 0.1
        _BorderWidth ("Border Width", Range(0,0.2)) = 0.015
        _EdgeFeather ("Edge Feather (pixels)", Range(0.25,8)) = 1.5
        _BorderColor ("Border Color", Color) = (0.005,0.005,0.008,1)
        _VignetteStrength ("Vignette Strength", Range(0,1)) = 0.18
        _VignetteSoftness ("Vignette Softness", Range(0.05,1)) = 0.6
        _ScanlineStrength ("Scanline Strength", Range(0,1)) = 0.4
        _ScanlineCount ("Scanline Count", Range(40,600)) = 180
        _ScanlineWidth ("Dark Line Width", Range(0.05,0.8)) = 0.3
        _ChromaticAberration ("Color Separation (pixels)", Range(0,5)) = 0
        [ToggleUI] _PointSampling ("Crisp Pixel Sampling", Float) = 1
        _MinificationFilter ("Warp Minification Filtering", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "RetroScreen"
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment RetroScreenFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "RetroScreenCore.hlsl"

            float4 _RetroEffectsSuppression;
            float4 _RetroEffectsGeometry;
            CBUFFER_START(UnityPerMaterial)
                float4 _BorderColor;
                float _Intensity;
                float _CurvatureX;
                float _CurvatureY;
                float _Zoom;
                float _CornerRadius;
                float _BorderWidth;
                float _EdgeFeather;
                float _VignetteStrength;
                float _VignetteSoftness;
                float _ScanlineStrength;
                float _ScanlineCount;
                float _ScanlineWidth;
                float _ChromaticAberration;
                float _PointSampling;
                float _MinificationFilter;
            CBUFFER_END

            half4 RetroScreenSample(float2 uv)
            {
                // Resolve logical screen coordinates into URP's copied source texture.
                float2 sourceUV = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
                half4 sampled = half4(0, 0, 0, 0);
                if (_PointSampling > 0.5)
                {
                    sampled = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, sourceUV, 0);
                    // Integrate the warped output pixel's source footprint. One bilinear
                    // lookup cannot resolve a compressed Bayer lattice at curved edges.
                    float2 sourceSize = _BlitTexture_TexelSize.zw;
                    float2 dx = ddx(sourceUV);
                    float2 dy = ddy(sourceUV);
                    float footprint = max(length(dx * sourceSize), length(dy * sourceSize));
                    float filterBlend = smoothstep(1.02, 1.35, footprint) * _MinificationFilter;
                    if (filterBlend > 0.0)
                    {
                        float2 a = (dx + dy) * 0.25;
                        float2 b = (dx - dy) * 0.25;
                        half4 filtered = (
                            SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, sourceUV + a, 0) +
                            SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, sourceUV - a, 0) +
                            SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, sourceUV + b, 0) +
                            SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, sourceUV - b, 0)) * 0.25;
                        sampled = lerp(sampled, filtered, filterBlend);
                    }
                }
                else
                    sampled = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, sourceUV, 0);
                return sampled;
            }

            half4 RetroScreenFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // SV_POSITION is independent of render-target orientation and RTHandle scale.
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                half4 original = RetroScreenSample(uv);
                if (_Intensity <= 0.0 || _RetroEffectsSuppression.y > 0.5) return original;

                RetroScreenSettings settings;
                settings.curvature = _RetroEffectsGeometry.x > 0.5 ? float2(0,0) : float2(_CurvatureX, _CurvatureY);
                settings.zoom = _RetroEffectsGeometry.x > 0.5 ? 1.0 : _Zoom;
                settings.cornerRadius = _CornerRadius;
                settings.borderWidth = _BorderWidth;
                settings.edgeFeather = _EdgeFeather * 2.0 / _ScaledScreenParams.y;
                float2 warpedUV = RetroScreenWarp(uv, settings);
                half3 color = RetroScreenSample(warpedUV).rgb;

                if (_ChromaticAberration > 0.0 && _RetroEffectsGeometry.z < 0.5)
                {
                    float2 offset = (warpedUV * 2.0 - 1.0) * _ChromaticAberration / _ScaledScreenParams.xy;
                    color.r = RetroScreenSample(warpedUV + offset).r;
                    color.b = RetroScreenSample(warpedUV - offset).b;
                }

                float2 centered = warpedUV * 2.0 - 1.0;
                float vignette = smoothstep(1.0 - _VignetteSoftness, 1.0, dot(centered, centered) * 0.5);
                color *= 1.0 - vignette * _VignetteStrength * (1.0 - _RetroEffectsSuppression.z);
                float scanline = RetroScreenScanline(warpedUV, _ScaledScreenParams.y,
                    _ScanlineCount, _ScanlineWidth, settings);
                color *= 1.0 - scanline * _ScanlineStrength * (1.0 - _RetroEffectsSuppression.w);

                float mask = RetroScreenMask(warpedUV, _ScaledScreenParams.x / _ScaledScreenParams.y, settings);
                if (_RetroEffectsGeometry.y > 0.5) mask = 1.0;
                // Do not stretch the source's final row/column into the bezel.
                float valid = step(0.0, warpedUV.x) * step(warpedUV.x, 1.0)
                    * step(0.0, warpedUV.y) * step(warpedUV.y, 1.0);
                half3 result = lerp(_BorderColor.rgb, color, mask * valid);
                return half4(lerp(original.rgb, result, saturate(_Intensity)), original.a);
            }
            ENDHLSL
        }
    }
    CustomEditor "AAR.RetroScreen.Editor.RetroScreenShaderGUI"
    FallBack Off
}
