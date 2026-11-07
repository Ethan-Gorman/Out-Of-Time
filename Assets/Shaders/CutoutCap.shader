// Shows the footprint of a wall as a solid dark patch where OccluderFader has cut a hole in it,
// like the cut face of a cutaway, instead of seeing straight through to the floor.
// OccluderFader adds it as an extra material on a wall only while that wall is being cut.
// How it works: it draws only the inside of the mesh's bottom face (Cull Front), lifted just above
// the floor. The wall's own front face normally hides it; where the front is cut away, it shows.
// The wall's inner side faces are skipped, so it never hides the player standing behind the wall.
Shader "Out Of Time/Cutout Cap"
{
    Properties
    {
        _CapColor ("Cap Colour", Color) = (0.2, 0.2, 0.23, 1)
        _CapLift ("Lift Above Floor", Float) = 0.05
    }

    SubShader
    {
        // Just after normal opaque geometry, so the wall's own (uncut) front face is already in the depth buffer
        Tags { "Queue" = "Geometry+1" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Cull Front
        ZWrite On
        ZTest LEqual

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CapColor;
                float _CapLift;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float down : TEXCOORD0; // 1 on the bottom face, 0 on the sides and top
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                // Only the bottom face: lift it above the floor tiles so they can't draw over it
                output.down = normalWS.y < -0.5 ? 1.0 : 0.0;
                positionWS.y += output.down * _CapLift;

                output.positionHCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Skip the inside of the side and top faces: they would hide the player behind the wall
                clip(input.down - 0.5);
                return half4(_CapColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
