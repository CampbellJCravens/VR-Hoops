# Multiplayer Setup Guide

This guide explains how to set up multiplayer synchronization for the VR Basketball game using Normcore.

## Overview

The multiplayer system syncs:
- **Basketball positions/rotations** (via RealtimeTransform)
- **Basketball material type** (via RealtimeBasketballModel - Orange vs RedWhiteBlue money ball)
- **Score and lives** (via RealtimeScoreModel)
- **Game state** (via RealtimePlayAreaModel)
- **Hoop position/coordinate** (via RealtimePlayAreaModel - server authoritative)
- **On Fire VFX state** (synced through ScoreManager)
- **Player avatars** (handled automatically by Normcore's RealtimeAvatarManager)

## Setup Steps

### 1. Basketball Prefab Setup

The basketball prefab must have Normcore components for synchronization:

1. Open `Assets/Resources/Prefabs/Basketball.prefab`
2. Add a **RealtimeView** component to the root GameObject
3. Add a **RealtimeTransform** component to the root GameObject
   - Set **Ownership** to "Take Ownership When Requested"
   - Enable **Sync Position** and **Sync Rotation**
   - Enable **Use Interpolation** for smooth movement
4. **IMPORTANT:** Add a **BasketballMaterialModelBridge** component to the root GameObject
   - This component MUST be on the prefab (not added at runtime)
   - Normcore only creates models for `RealtimeComponent` components that exist on the prefab at instantiation time
   - Without this component on the prefab, material sync will NOT work
   - The RealtimeView will automatically get a `RealtimeBasketballModel` model component (auto-created by Normcore)
5. Ensure the **BasketballVisualController** component is present (should already be on the prefab)
6. Ensure the prefab is in `Resources/Prefabs/` folder (required for `Realtime.Instantiate`)

### 2. PlayArea Prefab Setup

Each PlayArea needs multiplayer components:

1. Open `Assets/Resources/Prefabs/PlayArea.prefab`
2. On the root GameObject (with `PlayAreaManager` component):
   - Add a **RealtimeView** component
   - Add the **PlayAreaManagerMultiplayer** component
   - The RealtimeView should have a **RealtimePlayAreaModel** model component (auto-created)

### 3. ScoreManager Setup

Each ScoreManager is now a `RealtimeComponent<RealtimeScoreModel>` (no wrapper needed):

1. In the PlayArea prefab, find the GameObject with `ScoreManager` component
2. Add a **RealtimeView** component
3. The RealtimeView should have a **RealtimeScoreModel** model component (auto-created)
4. **Note:** `ScoreManager` now directly inherits from `RealtimeComponent` - no separate multiplayer wrapper component is needed

### 4. HoopPositionsManager Setup

Each HoopPositionsManager needs multiplayer components:

1. In the PlayArea prefab, find the GameObject with `HoopPositionsManager` component
2. Add the **HoopPositionsManagerMultiplayer** component
3. **Note:** This component does NOT need its own RealtimeView - it uses the PlayArea's RealtimeView model
4. The hoop position (coordinate) is synced via the `RealtimePlayAreaModel` on the PlayArea

### 5. Code Integration

The code has been updated to support multiplayer using Normcore best practices:

- `ScoreManager` now directly inherits from `RealtimeComponent<RealtimeScoreModel>` (no wrapper needed)
- `PlayAreaManager` now uses `Realtime.Instantiate()` for basketballs when Normcore is connected
- `PlayAreaManager.SetGameState()` syncs state changes to the model
- `PlayAreaManager.IncrementShotCount()` syncs shot count changes
- Score and lives sync automatically - owner writes to model, non-owners read from model events

### 7. Play Area Ownership

The system tracks which client is using each play area:
- When a player starts a game at a play area, `PlayAreaManagerMultiplayer.SetOwner()` is called
- Other clients can see which play areas are occupied
- Only the owner can spawn balls and control that play area's game

### 8. Testing

1. Build and run two instances of the game
2. Both should connect to the same Normcore room ("Test Room" by default)
3. Start a game at a play area in one instance
4. The other instance should see:
   - The game state change
   - Balls spawning and moving
   - Score and lives updating
   - On Fire VFX when activated

## Notes

- **Server Authority**: 
  - Ball physics are handled by the client that owns the ball (via RealtimeTransform)
  - Hoop position is server authoritative - only the owner of the PlayArea can move the hoop
- **Ownership**: The client that spawns a ball owns it initially, but ownership can transfer
- **Play Area Ownership**: Only one client can actively play at each play area at a time
- **Spectating**: Other clients can watch games at play areas they don't own
- **Hoop Position Sync**: The hoop coordinate is synced via `RealtimePlayAreaModel` and only the PlayArea owner can move it

## Troubleshooting

- **Balls not syncing**: Ensure Basketball prefab has RealtimeView and RealtimeTransform
- **Ball material not syncing**: Ensure Basketball prefab has RealtimeView (BasketballVisualController handles sync internally)
- **Score not syncing**: Ensure ScoreManager has RealtimeView component (ScoreManager is now a RealtimeComponent directly)
- **Game state not syncing**: Ensure PlayAreaManager has RealtimeView and PlayAreaManagerMultiplayer component
- **Hoop position not syncing**: Ensure HoopPositionsManager has HoopPositionsManagerMultiplayer component
- **Balls not spawning**: Check that basketball prefab is in `Resources/Prefabs/` folder and path is "Prefabs/Basketball"

