// Shows a wall's cut cross-section as a solid dark patch where OccluderFader has cut a hole in it,
// like the cut face of a cutaway, instead of seeing straight through to the floor.
// OccluderFader adds it as an extra material on a wall only while that wall is being cut.
// How it works: it draws only the inside of the mesh's bottom face (Cull Front), raised to the height where
// the hole starts (just above the player's feet), so it is visible through the hole on walls of any thickness.
// The wall's own front face hides it everywhere outside the hole. The wall's inner side faces are skipped.
Shader "Out Of Time/Cutout Cap"
{
    Properties
    {
        _CapColor ("Cap Colour", Color) = (0.2, 0.2, 0.23, 1)
        _CapLift ("Lift Above Floor (if the cut starts lower)", Float) = 0.05
        _PlayerClearance ("Keep Clear In Front Of Player (units)", Float) = 0.8
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
            #include "OccluderCutout.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CapColor;
                float _CapLift;
                float _PlayerClearance;
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
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                // Only the bottom face: raise it to where the hole starts (the cut's cross-section),
                // or at least just above the floor tiles so they can't draw over it
                output.down = normalWS.y < -0.5 ? 1.0 : 0.0;
                float capHeight = max(positionWS.y + _CapLift, _OOT_CutoutShape.z);
                positionWS.y = lerp(positionWS.y, capHeight, output.down);

                output.positionWS = positionWS;
                output.positionHCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Skip the inside of the side and top faces: they would hide the player behind the wall
                clip(input.down - 0.5);

                // Leave a gap just in front of the player, so the raised cap never covers their feet
                float2 towardsScene = normalize(-UNITY_MATRIX_V[2].xz);
                clip(-_PlayerClearance - dot(input.positionWS.xz - _OOT_CutoutPlayer.xz, towardsScene));
                return half4(_CapColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
