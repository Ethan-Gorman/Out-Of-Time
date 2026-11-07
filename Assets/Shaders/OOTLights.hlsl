#ifndef OOT_LIGHTS_INCLUDED
#define OOT_LIGHTS_INCLUDED

// Torch (point/spot light) glow shared by the floor and the toon walls/props, so a torch lights the same
// sphere around it on every surface, whichever way the surface faces, in the same flat toon rings.
// Needs URP's Lighting.hlsl and the _ADDITIONAL_LIGHTS / _CLUSTER_LIGHT_LOOP variants.
// Returns how much to brighten a surface: final colour += surface colour * glow
//   strength: overall torch brightness      maxGlow: brightest the glow can get (stops blow-out)
//   bands:    number of flat rings (0 = smooth)   softness: how soft the ring edges are (0..0.5)
half3 OOTTorchGlow(float3 positionWS, float4 positionCS, half4 shadowMask, half strength, half maxGlow, half bands, half softness)
{
    half3 glow = 0;
#if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
    // URP's light loop macro expects a variable called inputData
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = half3(0, 1, 0);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);

    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
        glow += light.color * (light.distanceAttenuation * light.shadowAttenuation);
    LIGHT_LOOP_END
#endif
    glow = min(glow * strength, maxGlow);

    // Toon rings: snap the glow's brightness (as a fraction of the max) to flat steps, keeping its colour.
    // Done on brightness, not distance, so a stronger torch gives bigger rings
    if (bands >= 1.0 && maxGlow > 0.0)
    {
        half brightness = max(Luminance(glow), 1e-4);
        half f = saturate(brightness / maxGlow) * bands;
        half i = floor(f);
        half step01 = (i + smoothstep(0.5 - softness, 0.5 + softness, f - i)) / bands;
        glow *= (step01 * maxGlow) / brightness;
    }
    return glow;
}

// 0..1: how strongly this spot is lit by torches (glow from OOTTorchGlow, as a fraction of its max).
// Used to cancel shadows inside torch light, so a torch's circle always reads as lit
half OOTTorchCoverage(half3 glow, half maxGlow)
{
    return maxGlow > 0.0 ? saturate(Luminance(glow) / maxGlow) : 0.0;
}

#endif
