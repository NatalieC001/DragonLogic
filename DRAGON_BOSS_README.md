# VR Dragon Boss Fight: The Complete Guide

Welcome to the Dragon Boss Fight documentation!

This guide explains how all the pieces of the boss fight fit together. It is written for level designers, gameplay programmers, and environment artists.

Everything here is explained fully. There are no missing steps and no half-finished ideas. We will use real in-game examples to show you exactly how things work.

---

## 1. How The Dragon Creature Works

The Dragon is not just one simple object. It is made of several different scripts. Each script has exactly one job.

They work together like a team to make the Dragon feel smart and dangerous.

### The Brain (`DragonBrain.cs`)
This is the commander. It decides what the Dragon wants to do.
- It does **not** move the Dragon.
- It does **not** manage the Dragon's body parts.
- It simply looks at the game, decides "I need to heal" or "I need to attack the player", and tells the other scripts to do the work.
- **Example:** If the Dragon loses a health segment, the `DragonBrain` receives a message. It stops attacking the player immediately. It then tells the `BossNavigator` to fly to a healing crystal.

### The Steering Wheel (`BossNavigator.cs`)
This script actually moves the Dragon's head through the 3D world.
- It receives orders from the `DragonBrain`.
- If the order is "fly freely to the player," it calculates a path through the air.
- If the order is "fly along a specific path," it locks onto a pre-drawn line (a spline) and moves along it smoothly.

### The Path Tracker (`BossPathManager.cs`)
This script holds all the invisible flight paths in the sky.
- It is a giant map.
- When the `BossNavigator` needs to fly in a specific pattern (like circling the arena to heal), it asks the `BossPathManager` where that path is.

### The Body (`DragonSnakeMovementStyle.cs`)
This script makes the Dragon's long body look like a snake following its head.
- It records exactly where the head was a few seconds ago.
- It places the neck, body, and tail pieces exactly in those old footsteps.
- It has zero logic about fighting. It only cares about looking cool and following the head.

### The Armor (`DragonSegment.cs` and `BossStatsAndHealth.cs`)
These scripts handle damage.
- The player must shoot the Dragon with an arrow.
- The arrow hits a `DragonSegment`. The segment records the hit and tells `BossStatsAndHealth` that damage was taken.
- The Dragon has a huge pool of health, broken into chunks. If a full chunk of health is gone, the Dragon sheds a body part, and the `DragonBrain` tells the Dragon to retreat and heal.

---

## 2. The Game Board & The Play Area

The player fights the Dragon on a flat piece of ground. We call this the **Game Board**.

The Game Board is an invisible grid. It tracks exactly where the player can walk, and where the traps are.

### `GameBoard.cs`
This script sits on an empty object in the very middle of your level.
- It breaks the floor into square tiles (for example, a 10 by 10 grid).
- It tracks what is on every single tile.
- **Example:** If the Dragon shoots fire at the floor, `GameBoard.cs` remembers that Tile [3, 4] is now on fire.

### The Elemental Recipes (Combos)
The `GameBoard.cs` is very smart. It knows how elements mix together.
- We call these combinations "Recipes."
- **Example:** If Tile [3, 4] is already covered in a Fire trap, and a barrel of Water spills onto it, the `GameBoard` sees the Recipe: Fire + Water. It destroys both traps, leaving the tile safe to walk on again.
- **Example:** If Tile [3, 4] is covered in Fire, and a barrel of Oil spills onto it, the `GameBoard` creates an Inferno! The fire becomes bigger and deadlier.

---

## 3. Ground Hazards & Player Traps

The Dragon wants to trap the player by covering the floor in dangerous elements.

### `GroundHazard.cs`
This script is placed on the trap prefabs (like a puddle of acid or a wall of fire).
- When a trap spawns on a tile, this script starts hurting the player if they stand in it.
- **The Dispel Mechanic:** The player can shoot the trap on the ground with a fully-drawn arrow to destroy it. This gives the player a safe spot to stand.
- **Elemental Debuffs:** Different traps do different things. A Fire trap burns the player's health. An Ice trap slows the player down.

### `VRHeadsetStickyBlindness.cs`
This script sits on the Player's VR Camera. It makes standing in Dark Fire very scary.
- If the player steps into Dark Fire, their screen is covered in dark, sticky particles. They cannot see far away.
- It also makes their arrows weaker. A fully drawn arrow will only do half damage.
- When the player steps out of the Dark Fire, it takes exactly 10 seconds for their eyes to clear up and for their weapon to become strong again.

---

## 4. Environmental Puzzles & Hiding Spots

The player is not alone. The Dragon commands an army of minions.

### Minion Hiding Spots (`CoverPoint.cs`)
Minions do not spawn right next to the player. They spawn far away and run to hiding spots (like walls or rocks).
- While hiding, the player cannot see them clearly.
- When the Dragon roars, all hiding minions charge at the player at the exact same time.

### The Puzzle Levers (`PuzzleLever.cs`)
The player can ruin the Dragon's plan by destroying the hiding spots early.
- You can place a puzzle lever near a hiding spot.
- If the player shoots the lever with a fully-drawn arrow, the lever activates.
- **Example:** The lever is shot. The lever moves a giant stone wall out of the way. The minions hiding behind it are now exposed! The player can easily shoot them before they have a chance to charge.

### Falling Pillars and Barrels (`ToppleItem.cs`)
The Dragon can fight back against the environment, too.
- You can tag tall pillars or barrels of oil as "Topple Items."
- The Dragon can fly into these items to knock them over.
- **Removing Walkable Area:** When a pillar falls over, it lands on the `GameBoard`. The `GameBoard` marks those tiles as "Blocked." The player can never walk there again.
- **Spilling Elements:** If the Dragon knocks over a barrel of Oil, the barrel breaks. It spills Oil onto the `GameBoard` two tiles forward. This can trigger a Recipe if it spills into an existing Fire trap!

---

## 5. The Mini-Game Strategy (The Chess Match)

The Dragon does not just attack randomly. It plays a game of chess against the player using the `SpatialStrategyMiniGame.cs` script.

### How The AI Chooses Targets
The mini-game script looks at the `GameBoard` every second. It wants to box the player into a corner.
- **Example:** The mini-game sees the player is trying to run to the center of the room. It tells the Dragon to spit Fire directly into the player's path.
- **Recipe Intelligence:** Before the Dragon knocks over a barrel, the mini-game checks the `GameBoard`. It asks: "Is there a barrel of Oil near a puddle of Fire?" If the answer is yes, the Dragon will fly out of its way to knock the Oil into the Fire to create a massive explosion. It is always looking for the smartest move.

---

## 6. How To Set Up The Scene (Step-By-Step)

If you are building the level in Unity, here is exactly what you must do to make all these scripts work.

### Step 1: Create The Configuration Files
We do not hardcode numbers (like damage or speed) in the scripts. We use ScriptableObjects.
1. Right-click in your Unity Project window.
2. Go to **Create -> ScriptableObjects**.
3. Create a `HazardConfig`, a `LeverConfig`, a `DragonAIConfig`, and a `PlayerDebuffConfig`.
4. Click on each one and type in the numbers you want (for example, set the lever's required damage to 50).

### Step 2: Build The Game Board
1. Create an Empty GameObject. Place it exactly in the center of your floor. Name it `ArenaCenter`.
2. Create another Empty GameObject. Name it `GameBoard_Manager`.
3. Drag the `GameBoard.cs` script onto `GameBoard_Manager`.
4. Look at the Inspector for `GameBoard_Manager`. Drag `ArenaCenter` into the slot. Tell it how big your tiles are (for example, 4 meters) and how many tiles fit in your room (for example, 10 wide and 10 long).
5. Drag your Fire, Water, and Oil particle prefabs into their slots on the `GameBoard_Manager`.

### Step 3: Setup The Player Blindness
1. Find your VR Player object in the scene.
2. Drag the `VRHeadsetStickyBlindness.cs` script onto the Main Camera.
3. In the Inspector, assign the `PlayerDebuffConfig` you made in Step 1.
4. Drag your dark particle effects into the slots. These are the particles that will cover the player's face when they step in a trap. Make sure the particles are turned off by default!

### Step 4: Place Your Traps and Levers
1. Drag a barrel model into your scene.
2. Add a `Rigidbody` and a `Collider` to the barrel.
3. Drag the `ToppleItem.cs` script onto the barrel. Check the box that says "Spills Contents" and select "Oil" from the drop-down menu.
4. Drag a lever model into your scene.
5. Drag the `PuzzleLever.cs` script onto the lever. In the inspector, tell it which wall it is supposed to move, and how far it should move it.

### Step 5: Turn On The Dragon
1. Click on the root object of your Dragon.
2. Make sure it has `BossNavigator`, `BossStatsAndHealth`, and `DragonBrain.cs` attached.
3. Look at `DragonBrain.cs` in the inspector. Drag all the required scripts into their empty slots.
4. Drag your `DragonAIConfig` (from Step 1) into the config slot.

You are finished! The scene is fully connected. The Game Board is tracking the floor. The Dragon Brain is commanding the body. The traps are ready to trigger.

---
End of Document.
