// Full-screen colour grade (URP Full Screen Pass, before post-processing): pulls every pixel part of the way
// towards the nearest colour in the current era's palette, so 3D walls, sprites and tiles share one colour range.
// EraPalette.cs sets the palette and strength (globals) whenever the era changes.
Shader "Out Of Time/Era Palette"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "EraPalette"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Set by EraPalette.cs: a 1-pixel-tall strip of colours, how many there are, and how strongly to pull
            TEXTURE2D(_OOT_Palette);
            float _OOT_PaletteSize;
            float _OOT_PaletteStrength;

            // "Redmean" colour distance: cheap, and much closer to how we see colour than plain RGB distance
            float ColourDistance(float3 a, float3 b)
            {
                float r = (a.r + b.r) * 0.5;
                float3 d = a - b;
                return (2.0 + r) * d.r * d.r + 4.0 * d.g * d.g + (3.0 - r) * d.b * d.b;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);

                int count = (int)_OOT_PaletteSize;
                if (_OOT_PaletteStrength <= 0.0 || count <= 0)
                    return color;

                float3 best = color.rgb;
                float bestDistance = 1e9;
                [loop] for (int i = 0; i < min(count, 32); i++)
                {
                    float3 swatch = SAMPLE_TEXTURE2D_LOD(_OOT_Palette, sampler_PointClamp, float2((i + 0.5) / count, 0.5), 0).rgb;
                    float distance = ColourDistance(color.rgb, swatch);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = swatch;
                    }
                }

                color.rgb = lerp(color.rgb, best, _OOT_PaletteStrength);
                return color;
            }
            ENDHLSL
        }
    }
}
