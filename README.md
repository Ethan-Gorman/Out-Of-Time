# Out Of Time

A 2.5D top-down action game made for the **Bristol Game Jam**. Theme: **time**.

> **Time only moves when you move.** Your clock is your health. Stand still and the world freezes so you can plan, but the clock keeps ticking slowly. Get hit and you lose seconds. Run out and it's over.

## How to play

| Input | Action |
|---|---|
| WASD / Arrow keys / Left stick | Move |

- The clock in the top-left is your time left
- While you move, the world runs at full speed. When you stop, enemies, animations and the clock slow *almost* to a halt.
- The clock still drains at 10% speed while you stand still, so you can't plan forever.
- When the clock hits zero, the level restarts.

## Getting started

### Requirements

- **Unity 6000.6.8f1**
- **Git LFS**

## How it's built

### 2.5D look

The world is 3D, but it's made to read as a 2D pixel-art game:

- **Billboard sprites.** Characters are 2D sprites that always face the camera (`Billboard.cs`).
- **Flattened camera.** A perspective camera with a low field of view (20°) sits far back at a 35° tilt (`CameraFollow.cs`). This flattens the perspective while keeping real depth and shadows.
- **Pixelated rendering.** URP renders at 25% resolution and upscales with nearest-neighbour filtering. Change this with **Render Scale** on `Assets/Settings/PC_RPAsset` and `Mobile_RPAsset`: `1` turns pixelation off.

### World time

The core mechanic is a global time scale that everything except the player runs on.

| Script | Lives on | Job |
|---|---|---|
| `Time/WorldTime.cs` | static class | `WorldTime.Scale` (0–1), plus `Delta` and `FixedDelta` |
| `Time/TimeController.cs` | Player | Sets `Scale` from how hard the player is moving |
| `Time/Clock.cs` | GameManager | Time left: drains, `AddTime()`, `RemoveTime()`, restarts at zero |
| `Time/ClockUI.cs` | HUD → Clock | Top-left timer; turns red and pulses under 10s |

**Rules for new code:**

- Anything that should slow down with the world uses **`WorldTime.Delta`** (in `Update`) or **`WorldTime.FixedDelta`** (in `FixedUpdate`), not `Time.deltaTime`.
- **Don't touch `Time.timeScale`.** It would slow the player too.
- World-time objects that move should use a **kinematic Rigidbody** moved with `MovePosition`, as `EnemyChase.cs` does. Normal physics ignores `WorldTime`.
- Animators: set `animator.speed = WorldTime.Scale`.
- Timers and coroutines: count down with `WorldTime.Delta`, not `WaitForSeconds`.
- Damage and rewards: call `Clock.Instance.RemoveTime(seconds)` and `Clock.Instance.AddTime(seconds)`.

### Project layout

```
Assets/
├── Scenes/SampleScene.unity     Main scene
├── Scripts/
│   ├── PlayerMovement.cs        Top-down movement (Input System)
│   ├── Billboard.cs             Sprites face the camera
│   ├── CameraFollow.cs          Angled, smoothed follow camera
│   ├── EnemyChase.cs            Test enemy that chases in world time
│   └── Time/                    WorldTime, TimeController, Clock, ClockUI
├── Materials/                   Ground and prop materials
├── Settings/                    URP pipeline and renderer assets
└── Undead Survivor/             Asset Store pack (git-ignored, import locally)
```

## Credits

- **Team:** _Borrowed Time_. Ethan Gorman, Stefan
- **Assets:** Undead Survivor asset pack (Unity Asset Store)
- Built with Unity 6
