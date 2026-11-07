#ifndef OOT_OCCLUDER_CUTOUT_INCLUDED
#define OOT_OCCLUDER_CUTOUT_INCLUDED

// Dithers away a circle of a surface around the player, only where the surface stands up in front of them.
// Driven by global properties that OccluderFader sets every frame.

// xy: player position on screen (0..1), z: player's distance from the camera, w: how much to cut (0..1)
float4 _OOT_CutoutCenter;
// x: circle radius as a fraction of screen height, y: width of the soft edge,
// z: world height a surface must rise above to be cut (player's feet + a margin)
float4 _OOT_CutoutShape;

void OOTCutout(float4 positionCS, float3 positionWS)
{
    float strength = _OOT_CutoutCenter.w;
    if (strength <= 0.0)
        return;

    // Only cut surfaces between the camera and the player
    float eyeDepth = LinearEyeDepth(positionCS.z, _ZBufferParams);
    if (eyeDepth >= _OOT_CutoutCenter.z)
        return;

    // Only cut surfaces that stand up above the player's feet. With the camera tilted down,
    // flat ground in front of the player is also "closer than the player", but it never hides them
    if (positionWS.y <= _OOT_CutoutShape.z)
        return;

    // Distance from the player on screen, corrected for aspect ratio so the hole is round
    float2 uv = GetNormalizedScreenSpaceUV(positionCS);
    float2 offset = uv - _OOT_CutoutCenter.xy;
    offset.x *= _ScaledScreenParams.x / _ScaledScreenParams.y;
    float inside = 1.0 - smoothstep(_OOT_CutoutShape.x - _OOT_CutoutShape.y, _OOT_CutoutShape.x, length(offset));

    // 4x4 ordered dither: removes a pixel pattern instead of blending, so there are no sorting problems
    // and it suits the pixel-art look
    static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    uint2 p = uint2(positionCS.xy) % 4;
    float threshold = (bayer[p.y * 4 + p.x] + 0.5) / 16.0;
    clip(threshold - inside * strength);
}

// For passes without a world position (depth only / depth normals): rebuilds it from the depth.
// These passes only feed the camera depth texture (depth priming is off), so an approximation is fine
void OOTCutoutScreen(float4 positionCS)
{
    float2 uv = GetNormalizedScreenSpaceUV(positionCS);
    OOTCutout(positionCS, ComputeWorldSpacePosition(uv, positionCS.z, UNITY_MATRIX_I_VP));
}

#endif
