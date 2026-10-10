Shader "AAR/Retro Dither"
{
    Properties
    {
        [HideInInspector] _PCBBaked ("Baked PCB", Float) = 0
        [HideInInspector] _PCBNormalMap ("PCB Raw RGB Normal", 2D) = "bump" {}
        [HideInInspector] _PCBMaskMap ("PCB Surface Mask", 2D) = "black" {}
        [HideInInspector] _PCBContactBrightness ("Contact Brightness", Float) = -.08
        [HideInInspector] _PCBContactSpecular ("Contact Specular", Float) = .65
        [HideInInspector] _PCBLegendColor ("Legend Color", Color) = (.4,.4,.4,1)
        _BaseMap ("Tone Texture", 2D) = "white" {}
        _BaseColor ("Tone Tint", Color) = (1,1,1,1)
        _ShadowColor ("01 / Ink", Color) = (0.055,0.035,0.12,1)
        _DarkColor ("02 / Shadow", Color) = (0.29,0.18,0.48,1)
        _LightColor ("03 / Light", Color) = (0.95,0.37,0.40,1)
        _HighlightColor ("04 / Highlight", Color) = (1,0.91,0.67,1)
        _DitherStrength ("Dither Strength", Range(0,1)) = 1
        _PixelSize ("Pattern Pixel Size", Range(1,12)) = 3
        [Enum(OutputPixels,0,ReferenceHeight,1)] _PatternScaleMode ("Screen Pattern Sizing", Float) = 1
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
        [ToggleUI] _NormalizePalette ("Sort & Deduplicate Palette", Float) = 1
        _DetailStability ("Detail Filtering", Range(0,2)) = 0.75
        _SpecularAA ("Specular Antialiasing", Range(0,2)) = 1
        _ReferenceHeight ("Pattern Reference Height", Range(180,2160)) = 720
        _DistanceFade ("Distance Smoothing", Range(0,1)) = 0.65
        _FadeStart ("Distance Fade Start", Float) = 15
        _FadeEnd ("Distance Fade End", Float) = 40
        [ToggleUI] _PreserveDither ("Preserve Crisp Dither", Float) = 1
        _CellLighting ("Cell Lighting Stability", Range(0,1)) = 0.6
        _PatternOffset ("Pattern Phase (cells)", Vector) = (0,0,0,0)
        [ToggleUI] _ShadowOnly ("Dither Shadows Only", Float) = 0
        _GroundColor ("Lit Ground Color", Color) = (1,1,1,1)
        _GroundShadowColor ("Ground Shadow Color", Color) = (0.02,0.02,0.02,1)
        _ShadowOpacity ("Shadow Density", Range(0,1)) = 0.65
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
                float3 boardX : TEXCOORD3;
                float3 boardZ : TEXCOORD4;
                float topFace : TEXCOORD5;
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
                output.boardX = normalize(TransformObjectToWorldDir(float3(1,0,0)));
                output.boardZ = normalize(TransformObjectToWorldDir(float3(0,0,1)));
                output.topFace = input.normalOS.y > 0.9 ? 1.0 : 0.0;
                return output;
            }
            float RetroLightEnergy(Light light, float3 normal, float3 view, float normalVariance, float specularStrength)
            {
                float diffuse = saturate((dot(normal, light.direction) + _LightWrap) / (1.0 + _LightWrap));
                float3 halfway = SafeNormalize(light.direction + view);
                float exponent = max(_Shininess, 1.0);
                float filteredExponent = exponent / (1.0 + _SpecularAA * normalVariance * exponent);
                float specular = pow(saturate(dot(normal, halfway)), filteredExponent)
                    * (filteredExponent / exponent) * specularStrength * diffuse;
                return (diffuse + specular) * RetroLuminance(light.color)
                    * light.distanceAttenuation * light.shadowAttenuation;
            }
            half4 RetroFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal = normalize(input.normalWS);
                float3 pcbMask = 0;
                if (_PCBBaked > 0.5 && input.topFace > 0.5)
                {
                    pcbMask = SAMPLE_TEXTURE2D(_PCBMaskMap,sampler_PCBMaskMap,input.uv).rgb;
                    float3 bump = SAMPLE_TEXTURE2D(_PCBNormalMap,sampler_PCBNormalMap,input.uv).rgb * 2 - 1;
                    normal = normalize(input.boardX*bump.x + input.boardZ*bump.y + normal*bump.z);
                }
                float specularStrength = lerp(_Specular,_PCBContactSpecular,pcbMask.g);
                float3 normalDX = ddx(normal);
                float3 normalDY = ddy(normal);
                float normalVariance = max(dot(normalDX, normalDX), dot(normalDY, normalDY));
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #endif
                Light mainLight = GetMainLight(shadowCoord);
                if (_ShadowOnly > 0.5 || (_PCBBaked > 0.5 && dot(pcbMask,float3(1,1,1)) < .001))
                {
                    // A flat, readable ground: only the key light's shadow map
                    // affects the tone. Unshadowed pixels are exactly Ground Color.
                    float shadow = saturate((1.0 - mainLight.shadowAttenuation) * _ShadowOpacity);
                    // OutputPixels measures cells in final target pixels. ReferenceHeight
                    // deliberately scales their size; neither mode depends on render scale.
                    float cellPixels = max(round(_PixelSize * (_PatternScaleMode > 0.5 ? RetroDitherOutputDimensions().y / max(_ReferenceHeight, 1.0) : 1.0)), 1.0);
                    float2 groundPixel = _PatternSpace < 0.5
                        ? RetroDitherScreenCoordinates(input.positionCS.xy) / cellPixels
                        : input.uv * _UVDensity / max(round(_PixelSize), 1.0);
                    groundPixel += _PatternOffset.xy;
                    float threshold = lerp(0.5, RetroBayer(groundPixel, _PatternSize), saturate(_DitherStrength));
                    float coverage = step(threshold, shadow);
                    return half4(RetroGroundColor(coverage, input.positionWS), 1);
                }
                float energy = _Ambient + RetroLightEnergy(mainLight, normal, view, normalVariance, specularStrength);

                // URP's clustered light loop expects this local InputData.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                #if USE_CLUSTER_LIGHT_LOOP
                    for (uint lightIndex = 0u; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
                    {
                        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                        energy += RetroLightEnergy(GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1)), normal, view, normalVariance, specularStrength);
                    }
                #endif
                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        energy += RetroLightEnergy(GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1)), normal, view, normalVariance, specularStrength);
                    LIGHT_LOOP_END
                #endif

                float2 textureUV = TRANSFORM_TEX(input.uv, _BaseMap);
                float3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, textureUV).rgb * _BaseColor.rgb;
                energy *= RetroLuminance(albedo);
                energy += pow(1.0 - saturate(dot(normal, view)), _RimPower) * _RimStrength
                    / (1.0 + normalVariance * _DetailStability * 8.0);
                float tone = 1.0 - exp2(-max(energy, 0.0) * _Exposure);
                tone += pcbMask.g * (_PCBContactBrightness-_Brightness) / max(_Contrast,.01);
                float cellPixels = max(round(_PixelSize * (_PatternScaleMode > 0.5 ? RetroDitherOutputDimensions().y / max(_ReferenceHeight, 1.0) : 1.0)), 1.0);
                float2 patternPixel = _PatternSpace < 0.5
                    ? RetroDitherScreenCoordinates(input.positionCS.xy) / cellPixels
                    : input.uv * _UVDensity / max(round(_PixelSize), 1.0);
                patternPixel += _PatternOffset.xy;
                float footprint = max(length(ddx(patternPixel)), length(ddy(patternPixel)));
                float patternVisibility = 1.0 - smoothstep(0.5, 1.5, footprint);
                float filterWidth = min(0.49, fwidth(tone) * max(_Contrast, 0.01) * 3.0 * _DetailStability);
                float distanceToCamera = length(GetWorldSpaceViewDir(input.positionWS));
                float distanceFade = smoothstep(max(_FadeStart, 0.0), max(_FadeEnd, _FadeStart + 0.01), distanceToCamera)
                    * _DistanceFade;
                if (_PreserveDither > 0.5)
                {
                    if (_PatternSpace < 0.5)
                    {
                        // Reconstruct slowly varying lighting at the Bayer cell center.
                        // Limit the correction at steep changes so silhouettes and small
                        // facets keep their contrast instead of inheriting a neighbor's tone.
                        float cellSize = cellPixels;
                        float2 outputOffset = (0.5 - frac(patternPixel)) * cellSize;
                        float2 gridPosition = RetroDitherScreenContinuous(input.positionCS.xy);
                        float2 gridDX = ddx(gridPosition), gridDY = ddy(gridPosition);
                        float determinant = gridDX.x * gridDY.y - gridDX.y * gridDY.x;
                        // Lighting derivatives are measured in source pixels. Convert a
                        // final-grid offset through the inverse-warp Jacobian first.
                        float2 offset = abs(determinant) > 0.0001
                            ? float2(gridDY.y * outputOffset.x - gridDY.x * outputOffset.y,
                                -gridDX.y * outputOffset.x + gridDX.x * outputOffset.y) / determinant
                            : float2(0,0);
                        float2 gradient = float2(ddx(tone), ddy(tone));
                        float change = dot(gradient, offset);
                        float slope = abs(gradient.x) + abs(gradient.y);
                        float edgeGuard = 1.0 - smoothstep(0.03, 0.15, slope);
                        float stability = saturate(_CellLighting + distanceFade * (1.0 - _CellLighting));
                        tone = saturate(tone + clamp(change, -0.08, 0.08) * stability * edgeGuard);
                        // Keep Bayer thresholds binary, including on distant geometry.
                        return half4(RetroPCBComposite(RetroLookColor(tone, patternPixel, 0.0, 1.0, 0.0, input.positionWS),pcbMask,patternPixel,input.positionWS,mainLight.shadowAttenuation), 1);
                    }
                    // Subpixel UV cells still need integration; resolved UV cells stay crisp.
                    return half4(RetroPCBComposite(RetroLookColor(tone, patternPixel, 0.0, patternVisibility, 0.0, input.positionWS),pcbMask,patternPixel,input.positionWS,mainLight.shadowAttenuation), 1);
                }
                return half4(RetroPCBComposite(RetroLookColor(tone, patternPixel, filterWidth, patternVisibility, distanceFade, input.positionWS),pcbMask,patternPixel,input.positionWS,mainLight.shadowAttenuation), 1);
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
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "AAR.RetroDither.Editor.RetroDitherShaderGUI"
    FallBack Off
}
