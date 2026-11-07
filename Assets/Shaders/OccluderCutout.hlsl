#ifndef OOT_OCCLUDER_CUTOUT_INCLUDED
#define OOT_OCCLUDER_CUTOUT_INCLUDED

// Dithers away a circle of a surface around the player, only where the surface stands up in front of them.
// Driven by global properties that OccluderFader sets every frame.

// xy: player position on screen (0..1), w: cut strength for shaders without their own fade (tile sprites)
float4 _OOT_CutoutCenter;
// x: circle radius as a fraction of screen height, y: width of the soft edge,
// z: world height a surface must rise above to be cut (player's feet + a margin),
// w: extra height over which the cut ramps up to full, so low boxes are only lightly cut
float4 _OOT_CutoutShape;
// xyz: player's feet in world space, w: how much a fully blocking surface is cut (1 - faded alpha)
float4 _OOT_CutoutPlayer;

// strength: 0 = solid, 1 = cut as much as allowed
void OOTCutout(float4 positionCS, float3 positionWS, float strength)
{
    if (strength <= 0.0)
        return;

    // Only cut surfaces on the camera's side of the player, measured along the ground.
    // (A view-depth test leans backwards with the tilted camera and would cut tall walls behind the player)
    float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
    float2 towardsScene = normalize(cameraForward.xz);
    if (dot(positionWS.xz - _OOT_CutoutPlayer.xz, towardsScene) >= 0.0)
        return;

    // Only cut surfaces that stand up above the player's feet. With the camera tilted down,
    // flat ground in front of the player is also on the camera's side, but it never hides them
    if (positionWS.y <= _OOT_CutoutShape.z)
        return;

    // Ramp in with height: crates and low steps get a light cut, tall walls the full one
    strength *= saturate((positionWS.y - _OOT_CutoutShape.z) / max(_OOT_CutoutShape.w, 0.001));

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
void OOTCutoutScreen(float4 positionCS, float strength)
{
    float2 uv = GetNormalizedScreenSpaceUV(positionCS);
    OOTCutout(positionCS, ComputeWorldSpacePosition(uv, positionCS.z, UNITY_MATRIX_I_VP), strength);
}

#endif
