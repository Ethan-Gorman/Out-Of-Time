// Sprite shader for characters (the player): lit by the same rules as the floor, so they sit in the scene.
//  - darkened with the same tinted shadow when standing in a wall's shadow
//  - the same banded torch rings (OOTLights.hlsl), which also cancel shadows
//  - the same base brightness as the floor
// The character casts its own shadow (SpriteShadowCaster), so shadows are checked a little towards the sun,
// just past that caster: only other objects' shadows land on the character, never its own.
Shader "Out Of Time/Sprite Lit"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        _BaseBrightness ("Base Brightness (match the floor)", Range(0, 1.5)) = 0.8

        [Header(Shadows)]
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.55
        _ShadowColor ("Shadow Tint", Color) = (0.25, 0.25, 0.4, 1)
        // The billboard leans back towards the camera, so its top sits behind the upright shadow quad: needs some margin
        _SelfShadowOffset ("Self-Shadow Offset (units towards the sun)", Range(0, 2)) = 0.8

        [Header(Torches and other lights)]
        _LightStrength ("Light Strength (match the floor)", Range(0, 2)) = 0.6
        _LightMax ("Max Brightening (match the floor)", Range(0, 4)) = 1.2
        _LightBands ("Light Rings (match the floor)", Range(0, 8)) = 4
        _LightBandSoftness ("Light Ring Edge Softness", Range(0, 0.5)) = 0.18
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

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
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
                float _ShadowStrength;
                float4 _ShadowColor;
                float _SelfShadowOffset;
                float _LightStrength;
                float _LightMax;
                float _LightBands;
                float _LightBandSoftness;
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

            half4 frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                c.rgb *= _BaseBrightness;

                // Torches: same banded sphere of light as the floor and walls
                half3 glow = OOTTorchGlow(input.positionWS, input.positionHCS, half4(1, 1, 1, 1), _LightStrength, _LightMax, _LightBands, _LightBandSoftness);

                // Sun shadow, checked a little towards the sun so the character's own shadow caster is skipped
                float3 sunward = input.positionWS + GetMainLight().direction * _SelfShadowOffset;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(sunward), sunward, half4(1, 1, 1, 1));
                half shadow = (1.0 - mainLight.shadowAttenuation) * _ShadowStrength;
                shadow *= 1.0 - OOTTorchCoverage(glow, _LightMax);

                c.rgb *= lerp(half3(1, 1, 1), _ShadowColor.rgb, shadow);
                c.rgb += c.rgb * glow;
                return c;
            }
            ENDHLSL
        }
    }
}
