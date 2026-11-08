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

### Caveman Era

The Caveman Era is the opening survival-and-crafting level. The time machine's temporal compass is damaged, leaving the player stranded in the Stone Age. Repair it with three magnetic stones from a dark cave, then board the machine to travel to the Medieval Era.

The intended objective sequence is:

1. Take wood from the tribe's firewood stacks and break rocks for stone.
2. Craft a Stone Axe at the flat knapping stone using **2 wood and 2 stone**.
3. Defeat the wild boar with the axe. Its death restores **30 seconds** to the clock.
4. Clear the cave brambles and collect all **3 magnetic stones**.
5. Return to the time machine, repair its compass, and board it to trigger the Medieval transition.

`Assets/Scripts/Caveman/CavemanLevel.cs` owns the story state, resource counts, crafting cost, boar reward, and repair requirement. `Breakable.cs`, `CraftingStation.cs`, `WildBoar.cs`, and `MagneticStone.cs` implement the Caveman interactions; the inventory readout and animated effects live in `CavemanInventoryUI.cs` and `CavemanSpriteAnimation.cs`. Caveman scene content is grouped into reusable prefabs under `Assets/prefabs/Caveman/` and composed in `Assets/Scenes/Caveman.unity`.

Keep the project-wide **2.5D** presentation: gameplay and collisions use the 3D world/time systems, characters such as the player and boar use 2D billboard pixel sprites, and larger scenery such as rocks, mountains and pillars uses stylized 3D forms with pixel-art materials (world-space textures at the sprites' pixel density, flat faces, hard shadows). Camp props and the cave villagers (`CaveNpc.cs`) are sprites. The level is built by Tools > Out Of Time > Build Caveman Era (`CavemanSceneBuilder`, `CavemanNature`, `CavemanSettlement`). The Caveman time machine uses the same design as the other eras; only its era-specific story text and travel setting differ. Reuse shared movement, clock, UI, and level-transition systems rather than creating era-specific replacements. Keep changes scoped to Caveman-owned files where possible, and ask before adding any animals; do not introduce dinosaurs.

### Steampunk Era

The third era: one enclosed Victorian factory with two floors. The time machine crash-lands on the first floor and the player falls through the broken boards to the ground floor. To get back up, they repair the gear tower that powers the mechanical lift.

Objective sequence:

1. Arrival dialogue (SYSTEM / PLAYER lines), then the objective "Repair the gear tower".
2. **Gear 1:** find the spare iron chain in the storage area, then fit it at the foot of the tower. Gear 1 starts turning, driven by chain from the engine.
3. **Gear 2:** take the oil can by the mechanic's workbench and oil the seized gear. Gear 2 turns once Gear 1 drives it.
4. **Gear 3** starts by itself: the brake lifts and the connecting rod on Gear 2 drags it round. The lift gets power.
5. Cross the steam-vent wall and the conveyor (jump the cargo, dodge the stamping presses) to reach the lift at the right end, and ride it up.
6. First floor: vents, swinging crane hooks, a narrow catwalk, then the **Clockwork Guard** (patrols, chases, winds up a spinning blade attack; jump on its head to beat it).
7. Repair the time machine's Chrono Regulator, then board it. It loads the next scene only if a scene named `Future` comes next in Build Settings; otherwise it shows a "Future Era, coming soon" banner.

Optional: a **pocket watch** in the furnace corner (+15 s via `PickupItem`). Six factory workers (`CaveNpc`) give hints.

How it's built:

- **Builder:** the whole level comes from **Tools > Out Of Time > Build Steampunk Era** (`Assets/Editor/SteampunkSceneBuilder.cs` for art, materials and prefabs; `Assets/Editor/SteampunkFactory.cs` for the layout and coordinates). It rewrites `Assets/Scenes/Steampunk.unity`, everything in `Assets/prefabs/Steampunk/`, and the Steampunk materials, textures and sprites, then puts the scene after Medieval in Build Settings. **To change the level, edit the builder and rebuild** rather than hand-editing the scene, or a rebuild will wipe your changes.
- **Scripts** are in `Assets/Scripts/Steampunk/`:

| Script | Job |
|---|---|
| `SteampunkLevel` | Story, objectives and dialogue; implements `ITimeMachineStory` |
| `SteampunkItem` | Chain and oil pickups |
| `GearRepair` | Gears 1 and 2 (`Interactable`) |
| `GearLink` | Gear 3: brake and connecting rod |
| `GearSpinner` | World-time rotation for gears, shafts, rollers and clock hands |
| `ChainLoop` | Moving drive chain |
| `MechanicalLift` | The lift |
| `ConveyorBelt` | Belt, cargo spawning and pushing the player |
| `SteamVent` | Timed steam hazard |
| `SteamPress` | Stamping press hazard |
| `ClockworkGuard` | The guard enemy |
| `SteamEmitter` | Pooled pixel steam and spark sprites; the project has Unity's Particle System module off, so don't use `ParticleSystem` |

- **Reused systems:**
  - Medieval: `Interactable`, `MedievalInteractor`, `MedievalUI`, `Hazard`, `SwingingAxe` (crane hooks), `TimeMachine`, `TorchFlicker`, plus the Medieval skeleton's body for the guard.
  - Caveman: `CaveNpc`.
  - Shared: `PickupItem`, `PlayerDamage`, `Clock`, `WorldTime`.
- **World time:** every hazard and machine runs on `WorldTime`. Two exceptions run on real time on purpose, because the player stands still while they happen: the lift ride and Gear 3's release moment.
- **Look:** a dark bronze, brass and iron factory, with warm amber bulbs against cool daylight from arched windows and roof glass. Pixel art throughout: 32 px point-filtered textures generated by the builder, chunky 3D shapes and billboard sprites. The roof and the upper front wall are shadow-only, so they shape the light without blocking the camera.
- **Placeholder art:** the NPCs use the Undead Survivor *Farmer* sprites, and the Clockwork Guard is the skeleton sprite tinted brass. Swap them for final art on their prefabs.

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

## Presenter cheats

For demos: `Scripts/Presenter/PresenterCheats.cs` creates itself on startup, so it needs no setup. The cheats are on in the Editor and in Development builds. In a normal build, type `hacker` to unlock them.

| Key | Cheat |
|---|---|
| **F1–F4** | Jump to Caveman / Medieval / Steampunk / Future |
| **N** | Finish this level and travel to the next era |
| **O** | Complete the next objective (the story, dialogue and objectives still play) |
| **P** | Teleport to the next point of interest |
| **T** | +30 seconds |
| **H** | **Hacker Mode**: the clock stops draining and nothing hurts you |
| **`** | Show or hide the cheat list |

Hacker Mode is checked in `Clock` and `PlayerDamage`. Add new eras' objective steps to `PresenterCheats.CompleteObjective`.

## Working with AI coding agents

The Caveman and Steampunk eras were built with Claude Code driving the open Unity Editor. If you use an agent (Claude Code, Copilot, ChatGPT...), point it at this README first, then at these tools.

### Tools that worked well

| Tool / skill | What it's for |
|---|---|
| **Unity CLI** (`unity`) + **`unity-cli` skill** | Drive the live Editor from a terminal instead of hand-editing `.unity`/`.prefab` YAML. Install: `irm https://unity.com/install.ps1 \| iex` (Windows) or `curl -fsSL https://unity.com/install.sh \| bash` |
| **Pipeline package** (`com.unity.pipeline`, already in `Packages/manifest.json`) | What `unity command` talks to inside the Editor |
| **`unity-pipeline` skill** | Deeper guide to the Pipeline commands. Mirror it with `unity skill install <client> --local` |
| Claude Code skills: `craft-game-floors`, `2d-pixel-perfect`, `physics-3d-collision`, `ui-ugui` | Look and feel, pixel rendering, trigger/collider debugging, the uGUI HUD |

### Commands used most

```bash
unity status                                    # is the Editor connected and ready?
unity recompile                                 # compile and list C# errors
unity command script eval 'SteampunkSceneBuilder.Build(); return "ok";'   # run any C# in the Editor (e.g. a level builder)
unity command editor set_auto_tick              # keep Play mode ticking while the Editor is in the background
unity command editor.playmode start             # enter Play mode (stop: editor.playmode stop)
unity command capture game_view --output-path Screenshots/view.png   # screenshot the Game view
unity command console get --level warning --tail 20                 # read errors and warnings
```

### Rules for agents in this repo

- Follow the **world time** rules in [World time](#world-time). Never use `Time.timeScale`.
- Keep era work inside that era's folders (`Scripts/<Era>`, `prefabs/<Era>`, `Materials/<Era>`...) and reuse the shared systems instead of copying them.
- Eras built by an Editor script (Caveman, Steampunk) are changed **in the builder**, then rebuilt.
- The era travel device is always a **time machine**, never a portal.
- Prefabs live in lowercase `Assets/prefabs/`. Never create `Assets/Prefabs/`, because on Windows it's the same folder.
- Commit `.meta` files, and push straight to `main` (team rule: no PRs or branches unless asked).

## Credits

- **Team:** _Borrowed Time_. Ethan Gorman, Stefan
- **Music:** original soundtrack by Stefan (Ableton Live)
- **Assets:** Undead Survivor asset pack (Unity Asset Store)
- Built with Unity 6
