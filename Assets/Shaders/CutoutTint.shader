// A faint see-through wash over the hole OccluderFader cuts in a wall, so the gap still reads as
// "wall here" (a few percent grey) instead of vanishing completely. No dither: plain alpha blending.
// OccluderFader adds it as an extra material on a wall only while that wall is being cut.
Shader "Out Of Time/Cutout Tint"
{
    Properties
    {
        _TintColor ("Tint (alpha = how strong in the middle of the hole)", Color) = (0.5, 0.5, 0.5, 0.05)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "OccluderCutout.hlsl"

            float _OOT_Strength; // per renderer, 0..1, set by OccluderFader

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Same shape as the hole, so the wash covers exactly the cut area and fades out at its edge
                float amount = OOTCutoutAmount(input.positionHCS, input.positionWS, _OOT_Strength);
                clip(amount - 0.001);
                return half4(_TintColor.rgb, _TintColor.a * amount);
            }
            ENDHLSL
        }
    }
}
