# Editor Setup & Testing Checklist

Here is a step-by-step, ordered checklist for wiring up the new architecture in the Unity Editor, grouped into isolated testing phases to ensure each system works independently before turning on the entire Dragon Brain.

### Phase 1: Test Player Debuffs & Hazards
*Goal: Ensure the player can take damage, be blinded, and the Roguelite death flow triggers correctly.*
1. **Player Setup:**
   - Ensure the Player GameObject has the `PlayerHealth` and `VRHeadsetStickyBlindness` scripts attached.
   - Tag the Player GameObject with `"Player"`.
2. **Hazard Setup:**
   - Place a test `GroundHazard` prefab in the scene.
   - Give it a trigger collider (e.g., SphereCollider) and ensure `isTrigger` is checked.
3. **Death Flow Hookup:**
   - Ensure a `LevelProgressionManager` is in the scene.
   - Verify `PlayerHealth` invokes the Game Over logic through the `LevelProgressionManager`.
4. **Test:** Play the scene, walk into the `GroundHazard`, and verify you take damage and are blinded. Let health drop to 0 and ensure the game over flow triggers properly.

### Phase 2: Test Minion Cover & Coordination
*Goal: Ensure minions can navigate to cover, hide, and swarm the player when triggered.*
1. **Cover Point Setup:**
   - Create empty GameObjects around the arena and attach `CoverPoint`.
   - Set the `capacity` appropriately (e.g., 3).
2. **Minion Setup:**
   - Ensure Minion prefabs have `StandardCreature` attached.
   - Verify that `MinionManager` is in the scene and wired to the `WaveSpawner`.
3. **Test:** In a test script or via a UI Button, call `CoverPoint.TriggerCharge()` while minions are hiding there. Verify they stop hiding and charge directly at the Player.

### Phase 3: Test Spatial Math (Chess-Master)
*Goal: Ensure the `SpatialStrategyMiniGame` correctly calculates player intercept points and identifies blocking pillars.*
1. **Arena Setup:**
   - Create an empty GameObject at the center of your arena and name it `ArenaCenter`.
   - Add `SpatialStrategyMiniGame` to a manager object (or the Dragon itself).
   - Drag `ArenaCenter` into the `arenaCenter` field of `SpatialStrategyMiniGame`.
2. **Pillar Setup:**
   - Ensure destructible pillars have the `ToppleItem` script attached with a Rigidbody.
3. **Initialization:**
   - Make sure `SpatialStrategyMiniGame.Initialize(playerTransform)` is called at start-up.
4. **Test:** Use Gizmos or debug logs to periodically call `GetOptimalHazardCoordinate()` and `GetOptimalToppleTarget()` as the player moves around. Verify the points calculated correctly attempt to box the player in.

### Phase 4: Test Movement Execution
*Goal: Ensure the BossNavigator can fluidly transition between freestyle flight and spline follow.*
1. **BossNavigator Setup:**
   - Attach `BossNavigator` to the Dragon Root.
   - Create a `DragonFlightBoundsGizmo` volume and assign it to the `flightBounds` field.
   - Assign the `BossPathManager` to the `pathManager` field.
2. **Test:** Send the "MoveToSpline" message to the navigator, and verify the dragon successfully intercepts the spline and rides it. Then send "Swoop" and verify it breaks off the spline and attacks the player position.

### Phase 5: The Final Brain
*Goal: Enable the Event-Driven Tactician and verify the holistic flow.*
1. **DragonBrain Setup:**
   - Ensure `DragonBrain` is on the same GameObject as `BossNavigator`, `DragonSnakeMovementStyle`, and `BossStatsAndHealth`.
   - Assign all subsystem references in the inspector.
2. **Message System:**
   - Ensure `BossStatsAndHealth` is firing events (like `"PlayerShootsBoss"`) through PixelCrushers `MessageSystem`.
   - Ensure the Dragon GameObject (or whichever has `DragonBrain`) is registered to receive these messages.
3. **Test:** Play the full scene. Watch the dragon react dynamically to damage and player blindness.

---

# Adversarial AI / Senior Systems Engineer Review

I reviewed the architecture overview you provided and attempted to break it. I found three significant missing pieces of "glue" that would have caused the system to fail in Unity. **I have proactively fixed these issues in the codebase.**

### Risk 1: The Missing Damage Pipeline (Fixed)
**The Problem:** `BossStatsAndHealth` triggers `MessageSystem.SendMessage("PlayerShootsBoss", ...)`, but `DragonBrain` was not listening to it. Furthermore, individual `DragonSegment` body parts took damage, relayed it to `vitals.TakeDamage()`, but `DragonBrain.AbsorbHit()` was completely bypassed. Because of this, `damageAccumulator` would never increase, `OnHealthSegmentLost` would never be called by the brain, and the dragon would never organically retreat or shed segments.
**The Fix:** I updated `DragonBrain` to implement `IMessageHandler` so it intercepts the `"SegmentDestroyed"` message broadcast by `DragonSegment.cs`. I decoupled this by having `DragonSegment.cs` broadcast a `SegmentDestroyed` message via `PixelCrushers.MessageSystem` when its health reaches 0. The `DragonBrain` listens to this message and correctly triggers `OnHealthSegmentLost()`, completely separating the visual locomotion layer from the tactical decision layer.

### Risk 2: The Topple Pillar Soft-Lock (Fixed)
**The Problem:** The `DragonBrain` could transition into `DragonState.TopplePillar` and command the `BossNavigator` to fly to the pillar. However, there was no code in the `Update()` loop checking if the dragon ever *arrived* at the pillar. The dragon would fly to the pillar and just hover there forever, soft-locking the AI state machine. `ToppleItem.Topple()` was never being invoked.
**The Fix:** I added logic to `DragonBrain.Update()` to actively measure the distance between the dragon and the target pillar while in the `TopplePillar` state. Once the dragon gets close enough (within `toppleReachDistance`), the `DragonBrain` broadcasts a `"DragonReachedPillar"` message via `PixelCrushers.MessageSystem`, signaling to the environment that the dragon has arrived and is executing the topple action. It then immediately evaluates a new tactical state, breaking the soft-lock.

The `ToppleItem.cs` script has been updated to listen for this broadcast. When it hears `"DragonReachedPillar"`, it confirms it is the intended target, independently calculates the physics trajectory based on the dragon's incoming vector, and triggers its own rigidbody to fall over. This preserves the strict separation of concerns: the Dragon only worries about moving and signaling its arrival, while the Pillar solely handles the physics of falling.

### Risk 3: Minion Defense Always Returned False (Fixed)
**The Problem:** The `DragonBrain.MinionsNeedDefense()` method was hardcoded to `return false;`. The dragon would never enter the `DefendMinions` state.
**The Fix:** I decoupled this system entirely using `PixelCrushers.MessageSystem`. Now, when a `StandardCreature` minion takes damage, it broadcasts `MinionUnderFire`. The `DragonBrain` listens for this message and evaluates its state to see if it should transition into `DefendMinions`, keeping the architecture purely event-driven and separated.

### Risk-Reward Mechanic: Dispelling Dark Fire (Game Designer Note)
The `GroundHazard.cs` (Dark Fire) script has been updated to implement the `IArrowTarget` interface. Players can now shoot the dark fire tiles to "dispel" or "unenchant" them, returning the ground to normal.

**The Strategy Stub:**
This mechanic revolves around a **Full Power Shot** (which takes ~1 second to fully draw back on the VR bow).
- **The Risk:** In the second it takes to draw the bow, the player sacrifices an attack opportunity. The Dragon can use this time to advance, command minions, topple a pillar, or launch a fireball.
- **The Code Implementation:** The hazard tile now has a `dispelHealth` pool (e.g., 100). The mechanic is cumulative:
  - If the player is standing **outside** the hazard, their full-power shot does 100 damage, clearing the tile in **1 hit**.
  - If the player is standing **inside** the hazard, their shots are weakened by the `StickyBlindness` debuff (dealing 50 damage), meaning they must shoot the tile **2 times** to clear it.
- **Next Steps for Weapons Engineer:** When updating the Bow/Arrow scripts, ensure a fully drawn bow deals exactly enough base damage to clear a tile in one shot, and that the `VRHeadsetStickyBlindness` script halves arrow damage when active. This same fully-charged requirement should be mapped to the puzzle levers.

### Future Territory Control Expansion (Game Designer Note)
The Dragon's spatial control arsenal is being expanded to utilize different elemental hazards that synergize with the environment. `GroundHazard.cs` has been updated with a `HazardType` enum to support these new variations.

**Planned Hazard Variations:**
- **Dark Mist (Current):** Blinds the player and weakens arrows.
- **Sticky Substance:** Coats the player and the bow, increasing the bow draw speed 3x.
- **Fire:** Ignites the ground, preventing safe passage and applying a burning DoT.
- **Ice:** Creates slippery or slowing terrain.
- **Electricity:** Electrifies the ground. This will synergize with environmental properties; for example, if an electric hazard is placed on a "Conductive Metal Sheet," its effective area and damage may drastically increase (or synergize with water puddles).

**Player Counter-Play:**
In addition to the "Dispel" mechanic (shooting the hazard directly to clear it), a future strategy involves allowing the player to cast temporary "Enchantments" on specific small areas of the plane. This would immunize that patch of ground from being captured or corrupted by the Dragon's hazards for a short time, giving the player a safe foothold to maneuver and fight back.


### Level Design Mechanic: Puzzle Levers
A new interactive script, `PuzzleLever.cs`, has been introduced. Players can shoot these levers to manipulate the environment (e.g., exposing hidden minion clusters before they are fully formed).

**Mechanic Details:**
- Levers require a **Full Power Shot** (configured via `requiredDamage`, defaulting to 50f) to activate.
- Upon activation, they smoothly translate a target `GameObject` (the cover) over a specified distance and direction.
- Once the cover moves, any attached `CoverPoint` is immediately instructed to trigger its hiding minions to charge the player.
- **Editor Visuals:** The script includes `OnDrawGizmos` to render a yellow line from the lever to its target cover, and a magenta trajectory line showing exactly where the cover will move and rest.
