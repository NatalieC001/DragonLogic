# VR Dragon Boss Fight: Scene Setup & Implementation Guide

This guide is intended for level designers and environment artists to correctly integrate and configure the new structural combat systems into the Unity scene. Follow these steps sequentially to ensure the decoupled architecture works correctly.

## Phase 1: ScriptableObject Configuration (The Data Layer)
Before setting up the scene, you must create the data assets that drive the gameplay tunables.
1. Right-click in your Project window (e.g., in a `Assets/Data/Configs` folder).
2. Go to **Create -> ScriptableObjects** and create one of each of the following:
   - `HazardConfig` (Assign damage per second, duration, and dispel health thresholds).
   - `LeverConfig` (Assign the required damage to trigger, and movement distance/speed for covers).
   - `DragonAIConfig` (Assign attack cooldowns, health thresholds, and minion scaling buffs).
   - `PlayerDebuffConfig` (Assign the recovery time, currently `10f`, and the debuff multipliers).
3. Keep these files readily available; you will drag-and-drop them into inspector slots in the following steps.

## Phase 2: The GameBoard (The Spatial Matrix)
The `GameBoard` is the absolute authority on spatial awareness, tracking hazards, and combining elements (recipes). It contains zero AI logic.
1. Create a new Empty GameObject at the absolute center of your playable arena. Name it `ArenaCenter`.
2. Create another Empty GameObject at the root level of your hierarchy and name it `GameBoard_Manager`.
3. Attach the `GameBoard.cs` script to `GameBoard_Manager`.
4. **Configuration in Inspector:**
   - Drag the `ArenaCenter` transform into the `Arena Center` slot.
   - Set the `Tile Size` (e.g., `4` means 4x4 meter tiles).
   - Set the `Grid Width` and `Grid Height` (e.g., 10x10 means a 40x40 meter arena).
   - Drag your hazard particle prefabs into the respective slots (Dark Mist, Fire, Water, Oil, etc.). *Ensure these prefabs have their visual particles designed to fit inside the `Tile Size` parameter, as the GameBoard will scale their root transforms exactly to the tile bounds.*

## Phase 3: The Player Setup & VR Blindness
The player must be able to visually and mechanically react to the `GameBoard` hazards.
1. Locate your VR Player Rig (e.g., XROrigin or Camera Offset).
2. Attach the `VRHeadsetStickyBlindness.cs` script to the main Camera object (or the highest level script that handles player health/input).
3. **Configuration in Inspector:**
   - Assign the `PlayerDebuffConfigSO` you created in Phase 1.
   - **Viewport Obfuscation Particles:** Create a particle system as a child of the VR Camera. Position it very close to the near-clipping plane so it covers the lens. Disable `Play On Awake` and disable the GameObject itself. Drag it into this slot.
   - **Ground Stream Particles:** Create a particle system at the base of the player's feet. Disable the GameObject. Drag it into this slot.
4. *Weapons Engineers:* Ensure your Bow/Arrow script listens to `VRHeadsetStickyBlindness.GetOutgoingDamageMultiplier()` to halve damage when blinded.

## Phase 4: Environmental Hazards & Puzzles
1. **Topple Items (Pillars/Barrels):**
   - Attach `ToppleItem.cs` to the physical pillar/barrel.
   - Ensure it has a `Rigidbody` and a `Collider`.
   - If it is a barrel, check `Spills Contents`, select the `Spill Type` (e.g., Oil, Water), and assign a basic hazard prefab (the `GameBoard` handles the heavy lifting).
2. **Puzzle Levers:**
   - Attach `PuzzleLever.cs` to your interactive lever.
   - Assign the `LeverConfigSO`.
   - Assign the `Target Cover` GameObject (the physical wall hiding the minions).
   - Use the `UnityEvents` in the inspector (`OnLeverActivated`, `OnCoverMoved`) to hook up your lever-pulling animation and sound effects without needing to write code.

## Phase 5: The Dragon Brain (The AI Layer)
The Dragon's tactical brain has been completely decoupled from arbitrary distances and now queries the `GameBoard`.
1. Locate the Dragon's Root GameObject.
2. Attach `DragonBrain.cs` (this should sit alongside `BossNavigator` and `BossStatsAndHealth`).
3. **Configuration in Inspector:**
   - Assign the `DragonAIConfigSO`.
   - Assign the required subsystems (`Navigator`, `Fireball`, `Dragon`, `StrategyMiniGame`, `StatsAndHealth`).
   - Use the `Animation Events` in the inspector to hook up your procedural face/leg animations (e.g., map `OnPlayRoar` to the Animator trigger that opens the jaw).

## Incremental Testing Workflow
To verify this setup incrementally before playing a full level:
1. **Test GameBoard Math:** Create a simple script that clicks on the floor to call `GameBoard.ApplyElementToTile(pos, HazardType.Fire)`. Verify the prefab spawns and fits the grid square. Then click the exact same spot with `HazardType.Water`. Verify the console logs `Recipe: Water + Fire = Steam (Nullified)` and the tile clears.
2. **Test Blindness:** Walk the VR player rig into a spawned `DarkMist` hazard. Verify the screen obfuscation turns on. Walk out, and verify the 10-second recovery timer starts and successfully clears the screen.
3. **Test Levers:** Shoot a puzzle lever with a full-power shot (ensure your arrow script passes the correct `damage` float to `OnArrowHit`). Verify the cover moves and the minions behind it charge.
