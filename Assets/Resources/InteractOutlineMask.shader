// Silhouette mask for InteractOutline: each outlined renderer is drawn flat in its outline colour into an offscreen
// mask the size of the low-res render. Sprites are cut out by their texture's alpha (the SpriteRenderer binds
// its frame as _MainTex); meshes use the default white, so their whole shape counts.
// Lives in Resources so it is included in builds.
Shader "Hidden/Out Of Time/Interact Outline Mask"
{
    Properties
    {
        _MainTex ("Sprite (bound by the renderer)", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Set per renderer by InteractOutline's command buffer
            float4 _OOT_OutlineMaskColor;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a - 0.5);
                return _OOT_OutlineMaskColor;
            }
            ENDHLSL
        }
    }
}
