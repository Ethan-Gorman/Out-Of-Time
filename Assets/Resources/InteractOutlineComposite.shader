// Draws InteractOutline's lines on top of everything, like an edge detection pass: a pixel outside every outlined
// silhouette that touches one (4 neighbours in the mask) takes that silhouette's colour. The mask matches the
// low-res render pixel for pixel, so every line is exactly one chunky pixel wide on every object.
// Drawn on a full-screen triangle (vertex positions are already clip space) in the Overlay queue.
// Lives in Resources so it is included in builds.
Shader "Hidden/Out Of Time/Interact Outline Composite"
{
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_OOT_OutlineMask);
            float4 _OOT_OutlineMaskSize;  // width, height, 1/width, 1/height

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = float4(input.positionOS.xy, 0.5, 1.0);
                return o;
            }

            half4 Mask(int2 p)
            {
                p = clamp(p, int2(0, 0), int2(_OOT_OutlineMaskSize.xy) - 1);
                return LOAD_TEXTURE2D(_OOT_OutlineMask, p);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Same pixel in the mask (its size can differ by rounding, so map through UVs).
                // _ScaledScreenParams is the low-res render's size; _ScreenParams would be the full window
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                // The mask is drawn outside URP with a texture projection, so its rows run the other way
                uv.y = 1.0 - uv.y;
                int2 p = int2(uv * _OOT_OutlineMaskSize.xy);

                if (Mask(p).a > 0.01)
                    discard;  // inside a silhouette: the line sits just outside it

                half4 c = Mask(p + int2(1, 0));
                half4 n = Mask(p + int2(-1, 0)); if (n.a > c.a) c = n;
                n = Mask(p + int2(0, 1)); if (n.a > c.a) c = n;
                n = Mask(p + int2(0, -1)); if (n.a > c.a) c = n;
                if (c.a <= 0.01)
                    discard;
                return c;
            }
            ENDHLSL
        }
    }
}
