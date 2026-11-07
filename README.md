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
- Travel through four eras: **Caveman → Medieval → Steampunk → Future**, each with its own look and soundtrack.

## Getting started

### Requirements

- **Unity 6000.6.8f1**
- **Git LFS**

### Setup

1. Install Git LFS (`brew install git-lfs && git lfs install`), then clone.
2. Open the project in Unity **6000.6.8f1**.
3. Import the **Undead Survivor** pack from the Asset Store (Package Manager → My Assets). It's git-ignored because its licence doesn't allow public redistribution. Without it, characters and tiles are missing.
4. Open `Assets/Scenes/SampleScene.unity` and press Play.

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

### Levels and eras

Each era is its own scene. Every scene contains the **`LevelEssentials`** prefab, which brings the camera, light, player, HUD, clock, music and level border.

| Script | Lives on | Job |
|---|---|---|
| `Levels/LevelInfo.cs` | LevelEssentials (root) | Which era this scene is. Set **Era** on the LevelEssentials instance in each scene |
| `Levels/LevelManager.cs` | Persistent | Tracks `CurrentEra` and survives scene loads. `RestartLevel()` reloads, `CompleteLevel()` loads the next scene in Build Settings |
| `Levels/LevelBorder.cs` | LevelBorder | Builds a raised greyscale wall around the painted floor, plus dimmed floor beyond it |

- **Adding an era scene:** duplicate a scene, set **Era** on `LevelEssentials`, and add it to **Build Settings** in play order (Caveman first).
- `CompleteLevel()` isn't hooked up yet. Call it from whatever ends a level.
- The border rebuilds itself whenever the floor is painted, and isn't saved into the scene. Tweak it on `prefabs/LevelBorder.prefab` (wall tiles, outside tiles, thickness, height, seed).

### Music

Each era has two stems of identical length in `Assets/Audio/Music/`: `EraN_Name_Music.wav` and `EraN_Name_Drums.wav`. Both are mixed to about −16 LUFS.

`Audio/MusicPlayer.cs` (on Persistent) plays the current era's stems in sync and crossfades when the era changes. It reacts to world time: when the player stands still, the drums fade out and the music gets muffled and reverby. Music keeps playing through level restarts. Tweak volume, fades and filter amounts on `prefabs/Persistent.prefab`.

Changing **Era** on `LevelEssentials` during Play mode switches the music immediately, for testing.

### Floor tiles

Paint the floor on the scene's `Tilemap` using a **Weighted Random Tile** (`Tiles/WeightedRandomTile.cs`):

1. Right-click in `Assets/Tiles` → **Create → Out Of Time → Weighted Random Tile**.
2. Add sprites and give each a weight (higher = more common).
3. Drag it into the Tile Palette, select **only that cell**, and paint with the Default Brush.

Each cell's pick depends on its position, so repainting never reshuffles the floor.

### Prefabs and teamwork

Unity scene files merge badly, so **build things as prefabs** and keep scenes to layout only.

```
LevelEssentials.prefab        + LevelInfo (era is a per-scene override)
 ├─ Main Camera, Light
 ├─ GameManager.prefab        Clock
 ├─ HUD.prefab                Clock display
 ├─ Player.prefab
 ├─ Persistent.prefab         LevelManager, MusicPlayer, Music + Drums audio sources
 └─ LevelBorder.prefab
```

- **Pull before you start.** Don't edit the same scene as someone else at the same time.
- **Commit `.meta` files with their assets.** Switch to Unity before committing new files so it creates them. A missing `.meta` means everyone's Unity invents a different ID, and references break.
- Set **Unity → Settings → General → Script Changes While Playing** to **Recompile After Finished Playing**, so code changes don't break a running test.

### Project layout

```
Assets/
├── Scenes/SampleScene.unity     Main scene
├── Scripts/
│   ├── PlayerMovement.cs        Top-down movement (Input System)
│   ├── Billboard.cs             Sprites face the camera
│   ├── CameraFollow.cs          Angled, smoothed follow camera
│   ├── EnemyChase.cs            Test enemy that chases in world time
│   ├── PickupItem.cs            Pickup that adds time
│   ├── interactionZone.cs       Shows a panel when the player is near
│   ├── Time/                    WorldTime, TimeController, Clock, ClockUI
│   ├── Levels/                  LevelInfo, LevelManager, LevelBorder
│   ├── Audio/                   MusicPlayer
│   └── Tiles/                   WeightedRandomTile
├── prefabs/                     LevelEssentials and everything nested in it, pickups, box
├── Audio/Music/                 Era soundtracks (music + drums stems)
├── Animations/Player/           Player animator and clips
├── Tiles/                       Floor tiles and random tiles
├── Shaders/                     Greyscale sprite shader (level border)
├── Materials/                   Ground, prop and border materials
├── Settings/                    URP pipeline and renderer assets
└── Undead Survivor/             Asset Store pack (git-ignored, import locally)
```

## Credits

- **Team:** _Borrowed Time_. Ethan Gorman, Stefan
- **Music:** original soundtrack by Stefan (Ableton Live)
- **Assets:** Undead Survivor asset pack (Unity Asset Store)
- Built with Unity 6
