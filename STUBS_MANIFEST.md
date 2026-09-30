# VR Dragon Boss Refactor: Stubs Manifest
**Context for the AI Agent (Senior Engineer):**
You are a Senior Systems Engineer returning to this codebase in a new session. The junior developer has laid out an event-driven, decoupled architecture based strictly on single-responsibility principles. However, several mechanics were implemented as "stubs" (placeholders).

Your mandate in the next session is to **complete these stubs to production-level standards**. You must obey strict separation of concerns, ensuring classes only handle their core responsibilities in line with the established design goals of the document. Do not merge concerns (e.g., visual logic stays separated from physics logic).

### 1. GroundHazard.cs: Dispel Accumulation Logic
**Location:** `GroundHazard.OnArrowHit`
**What it is:** The dark fire tile can be shot and dispelled. Currently, it uses a raw damage integer pool (`dispelHealth = 100f`) where taking exactly 100 damage destroys the object.
**What needs to be finalized:**
- **Weapons Integration:** The Bow/Arrow scripts must be tuned so that a "Full Power Shot" deals exactly enough damage to one-shot the tile (e.g., 100 damage), or the script should be updated to look for a specific `ElementTypeOB7.Dispel` flag instead of raw damage.
- **Debuff Integration:** The `VRHeadsetStickyBlindness` script must definitively cut player outgoing arrow damage in half while active so that shooting from inside the dark fire requires two shots (50 + 50 = 100) as per the design spec.
- **Visuals:** The `Dispel()` method calls `Destroy(gameObject)` instantly. A VFX hook (e.g., a burst of purifying light) should be added before destruction.

### 2. GroundHazard.cs: Elemental Area Effects (Future Territory Expansion)
**Location:** `GroundHazard.DamageTick` and `OnTriggerExit`
**What it is:** The script now contains a `HazardType` enum (DarkMist, Electricity, Fire, Sticky, Ice). Inside the damage application loop, there are commented stubs outlining where the specific debuff logic should fire (e.g., slowing bow draw speed if Sticky).
**What needs to be finalized:**
- **Sticky Debuff:** Hook up `ApplyStickyDebuff` and `RemoveStickyDebuff` to actually manipulate the Bow's draw speed multiplier (targeting 3x slower).
- **Electricity Synergies:** Add a physics check (`Physics.OverlapBox` or `OnTriggerStay`) to see if the hazard is resting on a GameObject tagged "ConductiveMetal" or "WaterPuddle", and multiply the hazard's damage and radius accordingly.
- **Fire & Ice Debuffs:** Implement the actual DoT burn or movement-slowing logic via the player's status manager.

### 3. PuzzleLever.cs: Full Power Shot Activation
**Location:** `PuzzleLever.OnArrowHit`
**What it is:** A lever that moves environmental cover and exposes minions. It currently activates by checking if the incoming arrow damage is `>= requiredDamage` (default 50f).
**What needs to be finalized:**
- **Weapons Integration:** Similar to the GroundHazard, verify that a "Full Power Shot" actually maps to this 50f threshold, or switch this check to use a specific boolean flag passed from the Arrow script.
- **Visuals/Audio:** The lever simply translates the target cover object via a Coroutine. Sound effects and animations (like the physical lever arm rotating down) need to be added.

### 4. DragonBrain.cs: Minion Coordination Broadcast
**Location:** `DragonBrain.CommandMinionsToCharge`
**What it is:** The brain sends a global `MessageSystem.SendMessage("DragonNeedsSupport")`. `CoverPoint.cs` listens and releases its hidden minions.
**What needs to be finalized:**
- This broadcast works perfectly, but the designers may want to add a visible/audible "Roar" animation or sound effect to the Dragon when this state triggers, giving the player a clear telegraph that a minion wave is about to flank them.

### 5. DragonBrain.cs: Topple Flight Trajectory
**Location:** `DragonBrain.TransitionToState` (TopplePillar case)
**What it is:** The Dragon calculates a point 15 meters behind the pillar along the optimal hit vector and commands `BossNavigator` to fly there.
**What needs to be finalized:**
- **Animation:** The dragon is just moving through the air. An aggressive "Ramming" or "Swooping" animation state should be triggered during this flight path.
- **Pathing Smoothing:** The 15-meter overshoot is a hardcoded float (`15f`). This might need to be exposed as a variable or tuned based on the size of the arena to ensure the dragon doesn't clip through arena walls after the ram.


### 6. ToppleItem.cs: Elemental Spills & Recipe Creation
**Location:** `ToppleItem.SpillContents`
**What it is:** Topple items are no longer just stone pillars; they can be barrels of Oil, Water, or Sticky substances. When toppled, they generate an area-of-effect hazard.
**What needs to be finalized:**
- **Spatial Coverage:** The physical barrel itself becomes an obstacle on the floor tiles. The spill must be instantiated to extend ~2 tiles *in the direction of the fall*.
- **Recipe/Nullification Logic:** Implement physics overlap checks when the spill is generated.
  - If a Water spill hits a Fire hazard, it should nullify the fire.
  - If an Oil spill hits a Fire hazard (or is later hit by a fireball), it should ignite.
- **AI Intelligence:** The Dragon's SpatialStrategy will be updated to intentionally target these synergies (e.g. knocking an oil barrel specifically toward a burning tile).


### 7. SpatialStrategyMiniGame.cs: Recipe Opportunity Logic
**Location:** `SpatialStrategyMiniGame.GetStrategicToppleTarget`
**What it is:** The MiniGame calculates the best barrel/pillar to knock over. Currently, it just picks the one that optimally blocks the player.
**What needs to be finalized:**
- **Recipe Intelligence:** The method contains a stub block. It should be updated to first check if any `availableToppleItems` containing `spillType == Oil` can be toppled into an existing Fire hazard from `activeHazardZones`. If so, it should override the standard logic, returning that barrel and the trajectory needed to knock the oil into the fire, setting `isRecipeOpportunity = true`.
