#ifndef OOT_OCCLUDER_CUTOUT_INCLUDED
#define OOT_OCCLUDER_CUTOUT_INCLUDED

// Dithers away an oval of a surface around the player.
// WHICH objects get the hole is decided once, in OccluderFader.CanObscure() (C#): it sets _OOT_Strength
// (per renderer, via a MaterialPropertyBlock) only on objects that can actually hide the player.
// This file only draws the hole, and keeps it off surfaces that are behind the player or at floor level.
// Driven by global properties that OccluderFader sets every frame.

// xy: player position on screen (0..1), z: how many times wider than tall the hole is
float4 _OOT_CutoutCenter;
// x: hole radius as a fraction of screen height, y: width of the soft edge,
// z: world height a surface must rise above to be cut (player's feet + a margin),
// w: extra height over which the cut ramps up to full
float4 _OOT_CutoutShape;
// xyz: player's feet in world space, w: how much the middle of the hole is cut (1 - faded alpha)
float4 _OOT_CutoutPlayer;

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

    // Distance from the player on screen, corrected for aspect ratio, then widened into an oval
    float2 uv = GetNormalizedScreenSpaceUV(positionCS);
    float2 offset = uv - _OOT_CutoutCenter.xy;
    offset.x *= _ScaledScreenParams.x / _ScaledScreenParams.y;
    offset.x /= max(_OOT_CutoutCenter.z, 0.01);
    float inside = 1.0 - smoothstep(_OOT_CutoutShape.x - _OOT_CutoutShape.y, _OOT_CutoutShape.x, length(offset));

    return inside * strength;
}

// Discards pixels inside the hole with a 4x4 ordered dither: a pixel pattern instead of blending,
// so there are no sorting problems and it suits the pixel-art look
void OOTCutout(float4 positionCS, float3 positionWS, float strength)
{
    float amount = OOTCutoutAmount(positionCS, positionWS, strength) * _OOT_CutoutPlayer.w;
    if (amount <= 0.0)
        return;

    static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    uint2 p = uint2(positionCS.xy) % 4;
    float threshold = (bayer[p.y * 4 + p.x] + 0.5) / 16.0;
    clip(threshold - amount);
}

// For passes without a world position (depth only / depth normals): rebuilds it from the depth.
// These passes only feed the camera depth texture (depth priming is off), so an approximation is fine
void OOTCutoutScreen(float4 positionCS, float strength)
{
    float2 uv = GetNormalizedScreenSpaceUV(positionCS);
    OOTCutout(positionCS, ComputeWorldSpacePosition(uv, positionCS.z, UNITY_MATRIX_I_VP), strength);
}

#endif
