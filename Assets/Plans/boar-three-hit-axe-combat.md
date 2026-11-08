# Project Overview
- Game Title: Out Of Time (project folder name).
- High-Level Concept: Existing time-themed game with a Caveman era featuring resource gathering, Stone Axe crafting, a wild boar, and cave progression. This task repairs the existing close-range boar interaction, not a new combat system.
- Players: Existing single-player character.
- Inspiration / Reference Games: Not specified; preserve existing design.
- Tone / Art Direction: Existing billboard sprite characters in a 3D world; unchanged.
- Target Platform: Standalone macOS.
- Screen Orientation / Resolution: Existing settings unchanged; irrelevant to this interaction fix.
- Render Pipeline: URP, PC_RPAsset.

# Game Mechanics
## Core Gameplay Loop
Gather wood and stone, craft the Stone Axe, approach the boar and press E three separate times while in its interaction trigger. Each accepted press removes one health: 3 → 2 → 1 → dead. Preserve hit flash, knockback, sounds, death animation, clock reward and cave objective progression. Without the axe, show the existing denial dialogue and do not damage the boar. Idle contact must never count as an axe attack.

## Controls and Input Methods
Use the existing New Input System Player/Interact action, bound to keyboard E and gamepad north. Keep MedievalInteractor's one-hit-per-press behavior; holding E must not repeatedly damage the boar. No new bindings or input polling systems. Preserve nearest-eligible-target selection and current proximity range (boar capsule radius 0.78, height 1.25, center Y 0.55, unit scale).

# UI
Reuse the existing prompt and dialogue; no new HUD or assets.

```text
Existing interaction prompt area:
[E] Attack wild boar   (Stone Axe owned)
[E] Wild boar          (no Stone Axe)

Existing dialogue area:
It's too dangerous to fight bare-handed. I need a Stone Axe.
```
Prompt disappears when the boar dies or the player exits its trigger.

# Key Asset & Context
## Modify
- `Assets/scripts/Caveman/WildBoar.cs`: its current `void OnTriggerEnter(Collider other) => TouchPlayer(other)` hides `Interactable.OnTriggerEnter`, preventing the inherited registration with MedievalInteractor. Replace it with `protected override void OnTriggerEnter(Collider other)` that calls `base.OnTriggerEnter(other)` and then `TouchPlayer(other)`.

## Existing assets to preserve and verify
- `Assets/scripts/Medieval/Interactable.cs`: inherited trigger enter registers a target; inherited trigger exit unregisters it. No modification required.
- `Assets/scripts/Medieval/MedievalInteractor.cs`: selects nearest registered eligible target and calls Interact once per press. No modification required.
- `Assets/scripts/Caveman/CavemanLevel.cs`: HasAxe, CraftAxe and OnBoarKilled already support the required inventory gate and one-time reward.
- `Assets/scripts/Caveman/CraftingStation.cs`: crafts the Stone Axe through existing resource costs.
- `Assets/prefabs/Caveman/WildBoar.prefab`: live instance verified active, health=3, axeDamage=1, enabled trigger capsule and kinematic Rigidbody.
- `Assets/prefabs/Caveman/CavemanLevel.prefab`: depends on the boar prefab/script; receives the script fix without prefab changes.
- `Assets/Scenes/Caveman.unity`: live scene has an enabled MedievalInteractor on the tagged player with Rigidbody, and active CavemanDirector.
- `Assets/InputSystem_Actions.inputactions`: existing Player/Interact bindings verified.

Dependency inspection used AssetDatabase.GetDependencies because a dedicated dependency tool is unavailable. No existing Assets/Plans directory or approved plan file was found. No test assemblies were found; avoid introducing a testing framework for this minimal repair.

# Implementation Steps
1. **Repair boar trigger registration** in `Assets/scripts/Caveman/WildBoar.cs`.
   - Reinspect the file before editing to avoid overwriting intervening changes.
   - Change only OnTriggerEnter to an explicit protected override.
   - Call the base implementation to register this boar with the entering player's MedievalInteractor.
   - Retain TouchPlayer(other) to preserve contact damage behavior.
   - Leave OnTriggerStay and inherited OnTriggerExit intact.
   - Leave health=3, axeDamage=1, CanInteract's death guard and all feedback/reward code intact.
   - Dependencies: verified Interactable base registration and existing scene wiring.
2. **Compile and inspect the resulting configuration**.
   - Confirm no new compile errors, and the live boar still has health 3 and damage 1.
   - Ensure no scene or prefab edits are necessary; only change serialized configuration if later inspection reveals a discrepancy, and perform impact analysis first.
   - Dependencies: Step 1.
3. **Exercise the interaction path in Play Mode** in the Caveman scene.
   - Approach without axe and press E; confirm denial with no health loss.
   - Craft the axe normally, enter the boar trigger and confirm the attack prompt.
   - Press/release E three times, reapproaching if knockback takes the boar out of reach; inspect health after each accepted hit.
   - Confirm death effects, collision shutdown, one-time reward and story update.
   - Test exit/re-entry and holding E; reset runtime state afterward without saving temporary test changes.
   - Dependencies: Steps 1–2.

# Verification & Testing
- Entering boar range registers the target and shows the existing prompt.
- Leaving range removes the prompt and prevents hits; re-entry restores interaction.
- No axe: repeated E presses do not change boar health.
- Axe owned: first hit leaves health 2; second leaves health 1; third kills the boar.
- Holding E results in one hit per press, not automatic attacks.
- Knockback and contact damage still behave as before; contact alone cannot kill the boar.
- Dead boar cannot be interacted with; colliders disable immediately, reward is delivered once and cave progression unlocks as before.
- Nearest-target selection remains unchanged for trees, crafting stations and other interactables.
- No new Console errors or accidental prefab/scene changes.
