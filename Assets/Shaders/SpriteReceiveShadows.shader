// Unlit sprite/tilemap shader for the floor (with a hue shift to recolour it), plus three bits of lighting:
//  - shadows from the main light (walls, props and blocks cast visible shadows onto it)
//  - glow from point and spot lights, so torch flicker shows as warm pools on the ground
//  - slowly drifting cloud shadows
Shader "Out Of Time/Sprite Receive Shadows"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        _BaseBrightness ("Base Brightness (lower = darker floor, torches stand out more)", Range(0, 1.5)) = 0.8

        [Header(Colour)]
        _HueShift ("Hue Shift (degrees; -80 turns green grass brown)", Range(-180, 180)) = -80
        _SaturationScale ("Saturation (1 = unchanged)", Range(0, 2)) = 0.7

        [Header(Shadows)]
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.55
        _ShadowColor ("Shadow Tint", Color) = (0.25, 0.25, 0.4, 1)

        [Header(Torches and other lights)]
        _LightStrength ("Light Strength", Range(0, 2)) = 0.6
        _LightMax ("Max Brightening", Range(0, 4)) = 1.2
        _LightBands ("Light Rings (0 = smooth; match the walls' Toon Torch Rings)", Range(0, 8)) = 4
        _LightBandSoftness ("Light Ring Edge Softness", Range(0, 0.5)) = 0.18

        [Header(Clouds)]
        _CloudStrength ("Cloud Shadow Strength", Range(0, 1)) = 0.4
        _CloudScale ("Cloud Size (smaller = bigger clouds)", Float) = 0.07
        _CloudSpeed ("Cloud Drift (xy, units per second)", Vector) = (0.6, 0.25, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "CanUseSpriteAtlas" = "True" }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // The same shadow variants URP Lit uses for the main light
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            // Point/spot lights: per-object list (Forward) or the cluster loop (Forward+)
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "OOTLights.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _BaseBrightness;
                float _HueShift;
                float _SaturationScale;
                float _ShadowStrength;
                float4 _ShadowColor;
                float _LightStrength;
                float _LightMax;
                float _LightBands;
                float _LightBandSoftness;
                float _CloudStrength;
                float _CloudScale;
                float4 _CloudSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            // Smooth value noise, 0..1
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                c.rgb *= _BaseBrightness;

                // Recolour: rotate the hue (green -> brown by default) and tone down saturation.
                // Pale colours (white flowers, grey stones) barely change, since they have little hue to rotate
                float3 hsv = RgbToHsv(c.rgb);
                hsv.x = frac(hsv.x + _HueShift / 360.0);
                hsv.y = saturate(hsv.y * _SaturationScale);
                c.rgb = HsvToRgb(hsv);

                // 1 = fully lit, 0 = fully in shadow. Includes URP's fade at the shadow distance
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1, 1, 1, 1));
                half shadow = (1.0 - mainLight.shadowAttenuation) * _ShadowStrength;

                // Drifting clouds: two octaves of noise moving across the ground in world space
                float2 cloudPos = input.positionWS.xz * _CloudScale + _Time.y * _CloudSpeed.xy * _CloudScale;
                float cloud = ValueNoise(cloudPos) * 0.65 + ValueNoise(cloudPos * 2.3 + 17.0) * 0.35;
                shadow = saturate(shadow + smoothstep(0.35, 0.7, cloud) * _CloudStrength);

                // Torches and other lights: the same banded sphere of light as on the walls (OOTLights.hlsl).
                // Inside a torch's circle the shadows give way, so the circle always reads as lit
                half3 glow = OOTTorchGlow(input.positionWS, input.positionHCS, half4(1, 1, 1, 1), _LightStrength, _LightMax, _LightBands, _LightBandSoftness);
                shadow *= 1.0 - OOTTorchCoverage(glow, _LightMax);

                // Tint towards the shadow colour rather than plain black, which reads better with pixel art
                c.rgb *= lerp(half3(1, 1, 1), _ShadowColor.rgb, shadow);
                c.rgb += c.rgb * glow;
                return c;
            }
            ENDHLSL
        }
    }
}
