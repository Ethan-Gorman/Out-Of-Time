// Unlit sprite/tilemap shader for the floor, with three bits of lighting so it doesn't feel static:
//  - shadows from the main light (walls, props and blocks cast visible shadows onto it)
//  - glow from point and spot lights, so torch flicker shows as warm pools on the ground
//  - slowly drifting cloud shadows
Shader "Out Of Time/Sprite Receive Shadows"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}

        [Header(Shadows)]
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.55
        _ShadowColor ("Shadow Tint", Color) = (0.25, 0.25, 0.4, 1)

        [Header(Torches and other lights)]
        _LightStrength ("Light Strength", Range(0, 2)) = 0.35
        _LightMax ("Max Brightening", Range(0, 4)) = 1.2

        [Header(Clouds)]
        _CloudStrength ("Cloud Shadow Strength", Range(0, 1)) = 0.12
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
                float _ShadowStrength;
                float4 _ShadowColor;
                float _LightStrength;
                float _LightMax;
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

            // Sum of point/spot light colour reaching this bit of floor (the floor faces straight up)
            half3 AdditionalLightGlow(float3 positionWS, float4 positionHCS)
            {
                half3 glow = 0;
            #if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
                // URP's light loop macro expects a variable called inputData
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalWS = half3(0, 1, 0);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionHCS);

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
                    // Soft wrap so light just beside a torch still glows, not only directly below it
                    half facing = saturate(light.direction.y) * 0.5 + 0.5;
                    glow += light.color * (light.distanceAttenuation * light.shadowAttenuation * facing);
                LIGHT_LOOP_END
            #endif
                return glow;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                // 1 = fully lit, 0 = fully in shadow. Includes URP's fade at the shadow distance
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1, 1, 1, 1));
                half shadow = (1.0 - mainLight.shadowAttenuation) * _ShadowStrength;

                // Drifting clouds: two octaves of noise moving across the ground in world space
                float2 cloudPos = input.positionWS.xz * _CloudScale + _Time.y * _CloudSpeed.xy * _CloudScale;
                float cloud = ValueNoise(cloudPos) * 0.65 + ValueNoise(cloudPos * 2.3 + 17.0) * 0.35;
                shadow = saturate(shadow + smoothstep(0.45, 0.8, cloud) * _CloudStrength);

                // Tint towards the shadow colour rather than plain black, which reads better with pixel art
                c.rgb *= lerp(half3(1, 1, 1), _ShadowColor.rgb, shadow);

                // Torches and other lights brighten and warm the floor (capped so it never blows out)
                half3 glow = min(AdditionalLightGlow(input.positionWS, input.positionHCS) * _LightStrength, _LightMax);
                c.rgb += c.rgb * glow;
                return c;
            }
            ENDHLSL
        }
    }
}
