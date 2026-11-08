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

            // Mask pixel: rgb = outline colour (black = the player), alpha = draw rank (far to near, from 1)
            // with the line's strength in the fraction (x 0.9). 0 = empty
            float4 Mask(int2 p)
            {
                p = clamp(p, int2(0, 0), int2(_OOT_OutlineMaskSize.xy) - 1);
                return LOAD_TEXTURE2D(_OOT_OutlineMask, p);
            }

            bool IsOutlined(float4 m) { return m.a >= 1.0 && max(m.r, max(m.g, m.b)) > 0.01; }

            half4 Frag(Varyings input) : SV_Target
            {
                // Same pixel in the mask (its size can differ by rounding, so map through UVs).
                // _ScaledScreenParams is the low-res render's size; _ScreenParams would be the full window
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                // The mask is drawn outside URP with a texture projection, so its rows run the other way
                uv.y = 1.0 - uv.y;
                int2 p = int2(uv * _OOT_OutlineMaskSize.xy);

                float4 centre = Mask(p);
                if (IsOutlined(centre))
                    discard;  // inside a silhouette: the line sits just outside it

                // The nearest outlined shape touching this pixel
                int2 offsets[4] = { int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1) };
                float4 best = 0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float4 n = Mask(p + offsets[i]);
                    if (IsOutlined(n) && n.a > best.a)
                        best = n;
                }
                if (best.a < 1.0)
                    discard;

                // The player covers this pixel and is nearer than that shape: the line goes behind them.
                // A shape nearer than the player keeps its line drawn over them
                if (centre.a >= 1.0 && floor(centre.a) > floor(best.a))
                    discard;

                return half4(best.rgb, frac(best.a) / 0.9);
            }
            ENDHLSL
        }
    }
}
