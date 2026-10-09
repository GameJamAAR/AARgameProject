Shader "AAR/Retro Dither"
{
    Properties
    {
        _BaseMap ("Tone Texture", 2D) = "white" {}
        _BaseColor ("Tone Tint", Color) = (1,1,1,1)
        _ShadowColor ("01 / Ink", Color) = (0.055,0.035,0.12,1)
        _DarkColor ("02 / Shadow", Color) = (0.29,0.18,0.48,1)
        _LightColor ("03 / Light", Color) = (0.95,0.37,0.40,1)
        _HighlightColor ("04 / Highlight", Color) = (1,0.91,0.67,1)
        _DitherStrength ("Dither Strength", Range(0,1)) = 1
        _PixelSize ("Pattern Pixel Size", Range(1,12)) = 3
        [Enum(Bayer2,2,Bayer4,4,Bayer8,8)] _PatternSize ("Bayer Matrix", Float) = 4
        [Enum(Screen,0,MeshUV,1)] _PatternSpace ("Pattern Coordinates", Float) = 0
        _UVDensity ("UV Pattern Density", Range(8,512)) = 128
        _Contrast ("Contrast", Range(0.25,3)) = 1.2
        _Brightness ("Brightness", Range(-1,1)) = 0
        _Exposure ("Light Exposure", Range(0.1,4)) = 1.2
        _Ambient ("Ambient Floor", Range(0,1)) = 0.12
        _LightWrap ("Diffuse Wrap", Range(0,1)) = 0.1
        _Specular ("Specular Strength", Range(0,2)) = 0.35
        _Shininess ("Specular Exponent", Range(4,128)) = 32
        _RimStrength ("Rim Strength", Range(0,2)) = 0.3
        _RimPower ("Rim Exponent", Range(0.5,8)) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "RetroDitherInput.hlsl"
        ENDHLSL

        Pass
        {
            Name "RetroForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RetroVertex
            #pragma fragment RetroFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings RetroVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }
            float RetroLightEnergy(Light light, float3 normal, float3 view)
            {
                float diffuse = saturate((dot(normal, light.direction) + _LightWrap) / (1.0 + _LightWrap));
                float3 halfway = SafeNormalize(light.direction + view);
                float specular = pow(saturate(dot(normal, halfway)), _Shininess) * _Specular * diffuse;
                return (diffuse + specular) * RetroLuminance(light.color)
                    * light.distanceAttenuation * light.shadowAttenuation;
            }
            half4 RetroFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal = normalize(input.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #endif
                Light mainLight = GetMainLight(shadowCoord);
                float energy = _Ambient + RetroLightEnergy(mainLight, normal, view);

                // URP's clustered light loop expects this local InputData.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                #if USE_CLUSTER_LIGHT_LOOP
                    for (uint lightIndex = 0u; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
                    {
                        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                        energy += RetroLightEnergy(GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1)), normal, view);
                    }
                #endif
                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        energy += RetroLightEnergy(GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1)), normal, view);
                    LIGHT_LOOP_END
                #endif

                float2 textureUV = TRANSFORM_TEX(input.uv, _BaseMap);
                float3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, textureUV).rgb * _BaseColor.rgb;
                energy *= RetroLuminance(albedo);
                energy += pow(1.0 - saturate(dot(normal, view)), _RimPower) * _RimStrength;
                float tone = 1.0 - exp2(-max(energy, 0.0) * _Exposure);
                float2 patternPixel = _PatternSpace < 0.5 ? input.positionCS.xy : input.uv * _UVDensity;
                patternPixel /= max(round(_PixelSize), 1.0);
                return half4(RetroQuantize(tone, patternPixel, RetroGetPalette(), RetroGetSettings()), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "AAR.RetroDither.Editor.RetroDitherShaderGUI"
    FallBack Off
}
