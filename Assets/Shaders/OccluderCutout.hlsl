#ifndef OOT_OCCLUDER_CUTOUT_INCLUDED
#define OOT_OCCLUDER_CUTOUT_INCLUDED

// Dithers away an oval of a surface around the player.
// WHICH objects get the hole is decided once, in OccluderFader.CanObscure() (C#): it sets _OOT_Strength
// (per renderer, via a MaterialPropertyBlock) only on objects that can actually hide the player.
// This file only draws the hole, and keeps it off surfaces that are behind the player or at floor level.
// Driven by global properties that OccluderFader sets every frame.

// xy: player position on screen (0..1), z: how many times wider than tall the hole is,
// w: how open the most-open hole is (0..1), used by the outline pass
float4 _OOT_CutoutCenter;
// x: hole radius as a fraction of screen height, y: width of the soft edge,
// z: world height a surface must rise above to be cut (player's feet + a margin),
// w: extra height over which the cut ramps up to full
float4 _OOT_CutoutShape;
// xyz: player's feet in world space, w: how much the middle of the hole is cut (1 - faded alpha)
float4 _OOT_CutoutPlayer;

// The hole's shape on screen: 1 in the middle of the oval, fading to 0 across its soft edge.
// uv is the normalised screen position (0..1). Also used by the outline pass
float OOTOvalInside(float2 uv)
{
    // Distance from the player on screen, corrected for aspect ratio, then widened into an oval
    float2 offset = uv - _OOT_CutoutCenter.xy;
    offset.x *= _ScaledScreenParams.x / _ScaledScreenParams.y;
    offset.x /= max(_OOT_CutoutCenter.z, 0.01);
    return 1.0 - smoothstep(_OOT_CutoutShape.x - _OOT_CutoutShape.y, _OOT_CutoutShape.x, length(offset));
}

// How much of this pixel is inside the hole (0 = untouched, 1 = fully cut), before dithering.
// strength: this renderer's fade, 0..1
float OOTCutoutAmount(float4 positionCS, float3 positionWS, float strength)
{
    if (strength <= 0.0)
        return 0.0;

    // Only surfaces on the camera's side of the player, measured along the ground
    float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
    float2 towardsScene = normalize(cameraForward.xz);
    if (dot(positionWS.xz - _OOT_CutoutPlayer.xz, towardsScene) >= 0.0)
        return 0.0;

    // Only surfaces above the player's feet, so the foot of a wall and any floor stay solid
    if (positionWS.y <= _OOT_CutoutShape.z)
        return 0.0;
    strength *= saturate((positionWS.y - _OOT_CutoutShape.z) / max(_OOT_CutoutShape.w, 0.001));

    return OOTOvalInside(GetNormalizedScreenSpaceUV(positionCS)) * strength;
}

// Discards pixels inside the hole with an ordered dither: a pixel pattern instead of blending,
// so there are no sorting problems and it suits the pixel-art look
void OOTCutout(float4 positionCS, float3 positionWS, float strength)
{
    float amount = OOTCutoutAmount(positionCS, positionWS, strength) * _OOT_CutoutPlayer.w;
    if (amount <= 0.0)
        return;

    // 8x8 ordered dither: 64 steps, so the edge fades smoothly instead of in a few coarse bands
    static const float bayer[64] =
    {
         0, 32,  8, 40,  2, 34, 10, 42,
        48, 16, 56, 24, 50, 18, 58, 26,
        12, 44,  4, 36, 14, 46,  6, 38,
        60, 28, 52, 20, 62, 30, 54, 22,
         3, 35, 11, 43,  1, 33,  9, 41,
        51, 19, 59, 27, 49, 17, 57, 25,
        15, 47,  7, 39, 13, 45,  5, 37,
        63, 31, 55, 23, 61, 29, 53, 21
    };
    uint2 p = uint2(positionCS.xy) % 8;
    float threshold = (bayer[p.y * 8 + p.x] + 0.5) / 64.0;
    clip(threshold - amount);
}

#endif
