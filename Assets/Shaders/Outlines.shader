// Full-screen outline pass (URP Full Screen Pass renderer feature, before transparents, needs Depth + Normal).
// Runs at the low render resolution, so every line is exactly one chunky pixel wide.
//  - Silhouette edges (a jump in depth): dark cartoon outline, drawn on the nearer object's pixel
//  - Inner edges (a change in surface direction, e.g. a wall's top edge): lightened where the edge
//    faces the sun, darkened where it faces away, which gives the pixel-art "lit edge" look
// Sprites and tiles are drawn after this pass, so they keep their own pixel edges.
// Walls cut by the see-through hole keep their full shape in the depth/normals textures, so their
// edges are drawn at full strength right across the hole, like a ghost outline of the wall.
Shader "Out Of Time/Outlines"
{
    Properties
    {
        _OutlineColor ("Silhouette Colour", Color) = (0.1, 0.08, 0.14, 1)
        _OutlineStrength ("Silhouette Strength", Range(0, 1)) = 0.85
        _DepthThreshold ("Silhouette Depth Threshold (fraction of distance)", Range(0.001, 0.2)) = 0.025
        _NormalThreshold ("Inner Edge Threshold", Range(0.01, 1)) = 0.35
        _HighlightStrength ("Inner Edge Highlight (sunlit side)", Range(0, 1)) = 0.35
        _CreaseStrength ("Inner Edge Darken (shaded side)", Range(0, 1)) = 0.25
        [Toggle] _HoleOnly ("Hole Only (second pass, after transparents: redraw the cut wall's edges over sprites)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "Outlines"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "OccluderCutout.hlsl"

            // Set by OccluderFader: the player's distance from the camera
            float _OOT_PlayerEyeDepth;

            float4 _OutlineColor;
            float _OutlineStrength;
            float _DepthThreshold;
            float _NormalThreshold;
            float _HighlightStrength;
            float _CreaseStrength;
            float _HoleOnly;

            // Distance from the camera, or a huge value where nothing opaque was drawn (sky, floor tiles)
            float EyeDepth(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                if (raw <= 0.0) return 1e6;
            #else
                if (raw >= 1.0) return 1e6;
            #endif
                return LinearEyeDepth(raw, _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                float2 texel = _CameraDepthTexture_TexelSize.xy;
                float2 offsets[4] = { float2(texel.x, 0), float2(-texel.x, 0), float2(0, texel.y), float2(0, -texel.y) };

                float depth = EyeDepth(uv);
                if (depth >= 1e5)
                    return color;  // nothing opaque here: leave it

                float3 normal = SampleSceneNormals(uv);

                // Hole-only pass: only the cut wall's own edges inside the see-through hole, i.e. surfaces
                // between the camera and the player. Everything else was already outlined by the first pass
                float holeMask = 1.0;
                if (_HoleOnly > 0.5)
                {
                    holeMask = OOTOvalInside(uv) * _OOT_CutoutCenter.w;
                    if (holeMask <= 0.001 || depth >= _OOT_PlayerEyeDepth - 0.1)
                        return color;
                }

                // Silhouette: a neighbour is much further away, so this pixel is the edge of the nearer object
                float silhouette = 0.0;
                // Inner edge: a neighbour at about the same distance faces a clearly different way.
                // Only count it on one side of the crease (the bias), so lines stay one pixel wide
                float crease = 0.0;
                // Unequal weights so no pair of axis-aligned faces cancels out. Y dominant: on a top/side edge
                // the top pixel is marked, which is the one that gets the sunlit highlight
                const float3 bias = float3(0.5, 1.0, 0.25);

                for (int i = 0; i < 4; i++)
                {
                    float2 nuv = uv + offsets[i];
                    float nDepth = EyeDepth(nuv);
                    if (nDepth - depth > depth * _DepthThreshold)
                        silhouette = 1.0;

                    if (abs(nDepth - depth) < depth * _DepthThreshold)
                    {
                        float3 nNormal = SampleSceneNormals(nuv);
                        float difference = dot(normal - nNormal, bias);
                        if (difference > 0.0 && (1.0 - dot(normal, nNormal)) > _NormalThreshold)
                            crease = 1.0;
                    }
                }

                if (silhouette > 0.0)
                    return half4(lerp(color.rgb, _OutlineColor.rgb, _OutlineStrength * holeMask), color.a);

                if (crease > 0.0)
                {
                    // Edges facing the sun catch the light; edges facing away read as a darker crease
                    half facing = dot(normal, GetMainLight().direction);
                    if (facing > 0.0)
                        color.rgb = lerp(color.rgb, saturate(color.rgb * 1.6 + 0.08), _HighlightStrength * holeMask);
                    else
                        color.rgb *= 1.0 - _CreaseStrength * holeMask;
                }
                return color;
            }
            ENDHLSL
        }
    }
}
