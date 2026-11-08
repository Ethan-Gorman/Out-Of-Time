// Generated from URP Lit (com.unity.render-pipelines.universal 17.6). Adds to the ForwardLit pass:
// the OccluderCutout.hlsl see-through hole, optional world-space UVs, and a toon shading mode
// (_OOT_Toon, on by default). Same properties as Lit, so Lit materials switch over with no changes.
// OccluderFader swaps walls to this shader while they block the player.
Shader "Out Of Time/Lit Cutout"
{
    Properties
    {
        // Specular vs Metallic workflow
        _WorkflowMode("WorkflowMode", Float) = 1.0

        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _OOT_WorldUV("World Space UVs (1 = on, Tiling = repeats per unit)", Range(0, 1)) = 0
        _OOT_Toon("Toon Shading (1 = on, 0 = normal Lit)", Range(0, 1)) = 1
        _OOT_ToonBands("Toon Light Bands", Range(1, 6)) = 3
        _OOT_ToonSoftness("Toon Band Edge Softness", Range(0, 0.5)) = 0.12
        _OOT_ShadowTint("Toon Shadow Tint", Color) = (0.32, 0.3, 0.48, 1)
        _OOT_AmbientStrength("Toon Ambient Strength", Range(0, 2)) = 0.25
        _OOT_TorchStrength("Toon Torch Strength (match the floor's Light Strength)", Range(0, 2)) = 0.6
        _OOT_TorchMax("Toon Torch Max Brightening (match the floor's)", Range(0, 4)) = 1.2
        _OOT_TorchBands("Toon Torch Rings (0 = smooth; match the floor's Light Rings)", Range(0, 8)) = 4
        _OOT_TorchBandSoftness("Toon Torch Ring Edge Softness", Range(0, 0.5)) = 0.18
        _OOT_TopAtlas("Top Faces: Tile Atlas (e.g. Tiles.png; leave empty for the normal texture)", 2D) = "white" {}
        _OOT_TopCount("Top Faces: Number of Tiles Used", Range(0, 6)) = 0
        _OOT_TopRect0("Top Faces: Tile 0 Rect (UV x, y, w, h)", Vector) = (0, 0, 1, 1)
        _OOT_TopRect1("Top Faces: Tile 1 Rect", Vector) = (0, 0, 1, 1)
        _OOT_TopRect2("Top Faces: Tile 2 Rect", Vector) = (0, 0, 1, 1)
        _OOT_TopRect3("Top Faces: Tile 3 Rect", Vector) = (0, 0, 1, 1)
        _OOT_TopRect4("Top Faces: Tile 4 Rect", Vector) = (0, 0, 1, 1)
        _OOT_TopRect5("Top Faces: Tile 5 Rect", Vector) = (0, 0, 1, 1)
        _OOT_TopBrightness("Top Faces: Brightness", Range(0, 2)) = 0.9
        // Opt-in looks for flat colour-sheet models (e.g. the Slavic village kit); 0 = off, so nothing else changes
        _OOT_PaletteSnap("Palette Snap (0 = off; pulls colours towards the era palette)", Range(0, 1)) = 0
        _OOT_PixelGrain("Pixel Grain (0 = off; 2x2 texel speckle at 16 px per unit)", Range(0, 0.1)) = 0

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
        [ToggleOff] _ScreenSpaceReflections("Screen Space Reflections", Float) = 1.0
        [ToggleOff] _ScreenSpaceReflectionsContributeTransparent("Screen Space Reflections Contribute Transparent", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _Parallax("Scale", Range(0.005, 0.08)) = 0.005
        _ParallaxMap("Height Map", 2D) = "black" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}
        _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0
        [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}

        // SRP batching compatibility for Clear Coat (Not used in Lit)
        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0

        // Blending state
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0
        [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        // Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
        // this Subshader will fail. One can add a subshader below or fallback to Standard built-in to make this
        // material work with both Universal Render Pipeline and Builtin Unity Pipeline
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass. Shades all light in a single pass. GI + emission + Fog
        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            // -------------------------------------
            // Render State Commands
            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex LitPassVertex
            #pragma fragment CutoutLitFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma multi_compile_local_fragment _ _EMISSION // always built: emissive props (crystal, fire, machine) keep their glow while cut
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SCREENSPACEREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP

            // -------------------------------------
            // Universal Pipeline keywords
#if defined(UNITY_PLATFORM_META_QUEST)
            #pragma multi_compile _ META_QUEST_LIGHTUNROLL
#endif
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_REFLECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#if defined(UNITY_PLATFORM_META_QUEST)
            #pragma multi_compile _ META_QUEST_ORTHO_PROJ
            #pragma multi_compile _ META_QUEST_NO_SPOTLIGHTS_LIGHT_LOOP
#endif
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"


            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "OccluderCutout.hlsl"
            float _OOT_Strength; // per renderer, 0..1, set by OccluderFader through a MaterialPropertyBlock
            float _OOT_WorldUV;  // 1 = map the base texture in world space (see Properties)
            float _OOT_Toon;
            float _OOT_ToonBands;
            float _OOT_ToonSoftness;
            float4 _OOT_ShadowTint;
            float _OOT_AmbientStrength;
            float _OOT_TorchStrength;
            float _OOT_TorchMax;
            float _OOT_TorchBands;
            float _OOT_TorchBandSoftness;
            TEXTURE2D(_OOT_TopAtlas);
            SAMPLER(sampler_OOT_TopAtlas);
            float _OOT_TopCount;
            float4 _OOT_TopRect0, _OOT_TopRect1, _OOT_TopRect2, _OOT_TopRect3, _OOT_TopRect4, _OOT_TopRect5;
            float _OOT_TopBrightness;
            float _OOT_PaletteSnap;
            float _OOT_PixelGrain;
            // Set by EraPalette.cs (globals): a 1-pixel-tall strip of the era's colours, how many there are, and the
            // grade strength (0 when this era has no palette, so a stale one from the last era is never used).
            // Sampled with the shared point sampler, like EraPalette.shader, to stay inside the sampler limit
            TEXTURE2D(_OOT_Palette);
            float _OOT_PaletteSize;
            float _OOT_PaletteStrength;
            #include "OOTLights.hlsl"

            // Same "redmean" colour distance as EraPalette.shader, so both pick the same swatch
            float OOTColourDistance(float3 a, float3 b)
            {
                float r = (a.r + b.r) * 0.5;
                float3 d = a - b;
                return (2.0 + r) * d.r * d.r + 4.0 * d.g * d.g + (3.0 - r) * d.b * d.b;
            }

            // Pulls a colour part of the way to the nearest era palette swatch (no palette yet = unchanged)
            half3 OOTPaletteSnap(half3 color, half amount)
            {
                int count = (int)_OOT_PaletteSize;
                if (count <= 0 || _OOT_PaletteStrength <= 0.0)
                    return color;
                float3 best = color;
                float bestDistance = 1e9;
                [loop] for (int i = 0; i < min(count, 32); i++)
                {
                    float3 swatch = SAMPLE_TEXTURE2D_LOD(_OOT_Palette, sampler_PointClamp, float2((i + 0.5) / count, 0.5), 0).rgb;
                    float gap = OOTColourDistance(color, swatch);
                    if (gap < bestDistance)
                    {
                        bestDistance = gap;
                        best = swatch;
                    }
                }
                return lerp(color, (half3)best, amount);
            }

            // -1..1 per world-space cell of 2x2 texels at 16 px per unit, on the face's main plane
            // (like the world UVs), so flat faces get chunky pixel grain instead of single-pixel noise
            half OOTGrain(float3 positionWS, half3 normalWS)
            {
                float3 n = abs(normalWS);
                float2 p = n.y > max(n.x, n.z) ? positionWS.xz : (n.x > n.z ? positionWS.zy : positionWS.xy);
                float2 cell = floor(p * 8.0 + 0.001);
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453) * 2.0 - 1.0;
            }

            // Light quantised into flat bands with slightly soft edges (pixel art / cartoon shading)
            half OOTBand(half x)
            {
                half bands = max(1.0, _OOT_ToonBands);
                half f = saturate(x) * bands;
                half i = floor(f);
                half edge = smoothstep(0.5 - _OOT_ToonSoftness, 0.5 + _OOT_ToonSoftness, f - i);
                return saturate((i + edge) / bands);
            }

            // Toon version of Lit: same surface inputs (colour, texture, emission), but flat banded light,
            // a tinted shadow colour instead of black, flat ambient, and banded torch/point lights
            half4 OOTToonShade(Varyings input)
            {
                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);
                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                InitializeBakedGIData(input, inputData);

                half3 albedo = surfaceData.albedo;

                // Top faces: random tiles from the atlas, greyscale, like the border's wall tops.
                // The grid starts at this block's own corner and stretches so a whole number of tiles
                // fits, so tops are never cropped mid-tile (assumes an unrotated box, like StoneBlock)
                if (_OOT_TopCount >= 1.0 && inputData.normalWS.y > 0.7)
                {
                    float4x4 toWorld = GetObjectToWorldMatrix();
                    float2 extent = float2(length(toWorld._m00_m10_m20), length(toWorld._m02_m12_m22));
                    float2 corner = toWorld._m03_m23 - 0.5 * extent;
                    float2 count = max(1.0, round(extent));
                    float2 grid = (input.positionWS.xz - corner) / (extent / count);
                    float2 cell = floor(grid);
                    float pick = frac(sin(dot(cell + corner, float2(127.1, 311.7))) * 43758.5453);
                    int index = min((int)(pick * _OOT_TopCount), (int)_OOT_TopCount - 1);
                    float4 rects[6] = { _OOT_TopRect0, _OOT_TopRect1, _OOT_TopRect2, _OOT_TopRect3, _OOT_TopRect4, _OOT_TopRect5 };
                    float4 rect = rects[index];
                    // Stay just inside the tile so neighbouring atlas tiles never bleed in
                    float2 local = clamp(frac(grid), 0.01, 0.99);
                    half3 tile = SAMPLE_TEXTURE2D(_OOT_TopAtlas, sampler_OOT_TopAtlas, rect.xy + local * rect.zw).rgb;
                    albedo = Luminance(tile) * _OOT_TopBrightness;
                }

                // Opt-in (both 0 by default): palette snap first, then grain, so the grain survives as light/dark specks
                if (_OOT_PaletteSnap > 0.0)
                    albedo = OOTPaletteSnap(albedo, _OOT_PaletteSnap);
                if (_OOT_PixelGrain > 0.0)
                    albedo *= 1.0 + _OOT_PixelGrain * OOTGrain(input.positionWS, inputData.normalWS);

                // Torches: the same banded sphere of light as on the floor (OOTLights.hlsl), so walls and floor match
                half3 glow = OOTTorchGlow(inputData.positionWS, input.positionCS, inputData.shadowMask, _OOT_TorchStrength, _OOT_TorchMax, _OOT_TorchBands, _OOT_TorchBandSoftness);

                // Sun, in flat bands with a tinted shadow. Inside a torch's circle the shadow gives way,
                // so the circle always reads as lit
                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
                half direct = saturate(dot(inputData.normalWS, mainLight.direction)) * mainLight.shadowAttenuation;
                half lit = max(OOTBand(direct), OOTTorchCoverage(glow, _OOT_TorchMax));
                half3 color = albedo * lerp(_OOT_ShadowTint.rgb, half3(1, 1, 1), lit) * mainLight.color;
                color += albedo * inputData.bakedGI * _OOT_AmbientStrength;
                color += albedo * glow;

                color += surfaceData.emission;
                color = MixFog(color, inputData.fogCoord);
                return half4(color, OutputAlpha(surfaceData.alpha, IsSurfaceTypeTransparent()));
            }

            void CutoutLitFragment(Varyings input, out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                OOTCutout(input.positionCS, input.positionWS, _OOT_Strength);
                if (_OOT_WorldUV > 0.5)
                {
                    // Planar mapping on the face's main axis: walls tile the texture evenly at any size
                    float3 n = abs(input.normalWS);
                    float2 world = n.y > max(n.x, n.z) ? input.positionWS.xz
                                 : (n.x > n.z ? input.positionWS.zy : input.positionWS.xy);
                    input.uv = world * _BaseMap_ST.xy + _BaseMap_ST.zw;
                }
                if (_OOT_Toon > 0.5)
                {
                    outColor = OOTToonShade(input);
                #ifdef _WRITE_RENDERING_LAYERS
                    outRenderingLayers = EncodeMeshRenderingLayer();
                #endif
                    return;
                }
                LitPassFragment(input, outColor
            #ifdef _WRITE_RENDERING_LAYERS
                    , outRenderingLayers
            #endif
                );
            }

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Universal Pipeline keywords

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "GBuffer"
            Tags
            {
                "LightMode" = "UniversalGBuffer"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite[_ZWrite]
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5

            // Deferred Rendering Path does not support the OpenGL-based graphics API:
            // Desktop OpenGL, OpenGL ES 3.0, WebGL 2.0.
            #pragma exclude_renderers gles3 glcore

            // -------------------------------------
            // Shader Stages
            #pragma vertex LitGBufferPassVertex
            #pragma fragment LitGBufferPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            //#pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_local_fragment _ _EMISSION // always built: emissive props (crystal, fire, machine) keep their glow while cut
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED

            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SCREENSPACEREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            //#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            //#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_REFLECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitGBufferPass.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutputFormat.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthOnlyVertex
            #pragma fragment CutoutDepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            #include "OccluderCutout.hlsl"
            float _OOT_Strength; // per renderer, 0..1, set by OccluderFader through a MaterialPropertyBlock
            float _OOT_WorldUV;  // 1 = map the base texture in world space (see Properties)
            float _OOT_Toon;
            float _OOT_ToonBands;
            float _OOT_ToonSoftness;
            float4 _OOT_ShadowTint;
            float _OOT_AmbientStrength;
            float _OOT_TorchStrength;
            float _OOT_TorchMax;
            float _OOT_TorchBands;
            float _OOT_TorchBandSoftness;
            TEXTURE2D(_OOT_TopAtlas);
            SAMPLER(sampler_OOT_TopAtlas);
            float _OOT_TopCount;
            float4 _OOT_TopRect0, _OOT_TopRect1, _OOT_TopRect2, _OOT_TopRect3, _OOT_TopRect4, _OOT_TopRect5;
            float _OOT_TopBrightness;
            float _OOT_PaletteSnap;
            float _OOT_PixelGrain;
            half CutoutDepthOnlyFragment(Varyings input) : SV_TARGET
            {
                // Not cut: depth/normals keep the whole wall, so its edges are outlined right across the see-through hole
                return DepthOnlyFragment(input);
            }

            ENDHLSL
        }

        // This pass is used when drawing to a _CameraNormalsTexture texture
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthNormalsVertex
            #pragma fragment CutoutDepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SCREENSPACEREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SCREENSPACEREFLECTIONSCONTRIBUTETRANSPARENT_OFF

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // -------------------------------------
            // Universal Pipeline keywords
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile _ _WRITE_SMOOTHNESS
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            #include "OccluderCutout.hlsl"
            float _OOT_Strength; // per renderer, 0..1, set by OccluderFader through a MaterialPropertyBlock
            float _OOT_WorldUV;  // 1 = map the base texture in world space (see Properties)
            float _OOT_Toon;
            float _OOT_ToonBands;
            float _OOT_ToonSoftness;
            float4 _OOT_ShadowTint;
            float _OOT_AmbientStrength;
            float _OOT_TorchStrength;
            float _OOT_TorchMax;
            float _OOT_TorchBands;
            float _OOT_TorchBandSoftness;
            TEXTURE2D(_OOT_TopAtlas);
            SAMPLER(sampler_OOT_TopAtlas);
            float _OOT_TopCount;
            float4 _OOT_TopRect0, _OOT_TopRect1, _OOT_TopRect2, _OOT_TopRect3, _OOT_TopRect4, _OOT_TopRect5;
            float _OOT_TopBrightness;
            float _OOT_PaletteSnap;
            float _OOT_PixelGrain;
            void CutoutDepthNormalsFragment(Varyings input, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                // Not cut: depth/normals keep the whole wall, so its edges are outlined right across the see-through hole
                DepthNormalsFragment(input, outNormalWS
            #ifdef _WRITE_RENDERING_LAYERS
                    , outRenderingLayers
            #endif
                );
            }

            ENDHLSL
        }

        // This pass it not used during regular rendering, only for lightmap baking.
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }

            // -------------------------------------
            // Render State Commands
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma multi_compile_local_fragment _ _EMISSION // always built: emissive props (crystal, fire, machine) keep their glow while cut
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SPECGLOSSMAP
            #pragma shader_feature EDITOR_VISUALIZATION

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "Universal2D"
            Tags
            {
                "LightMode" = "Universal2D"
            }

            // -------------------------------------
            // Render State Commands
            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Universal2D.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG

            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "XRMotionVectors"
            Tags { "LightMode" = "XRMotionVectors" }
            ColorMask RGBA

            // Stencil write for obj motion pixels
            Stencil
            {
                WriteMask 1
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ APPLICATION_SPACE_WARP_MOTION_TRANSPARENT
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY
            #define APPLICATION_SPACE_WARP_MOTION 1

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
