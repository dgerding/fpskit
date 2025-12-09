# NavMesh Patrol Route Implementation Plan
## Full Prefab-Based Setup for ExampleScene

**Date**: December 8, 2024  
**Estimated Time**: 45-60 minutes  
**Prerequisites**: All NavMesh scripts deployed, chase/flee behaviors validated  

---

## Overview

This plan completes the NavMesh AI implementation by adding patrol routes to room **prefabs** rather than scene instances. This approach:

- Avoids the HideFlags issue (room instances hidden in Edit Mode)
- Ensures every room instance inherits patrol waypoints automatically
- Creates maintainable, reusable patrol patterns per room type
- Enables spawners to reference built-in routes

### What You'll Accomplish

| Task | Result |
|------|--------|
| Add NavMeshRoomConnector to LevelLayout | Rooms connected via NavMeshLinks |
| Add PatrolRoute_Default to room prefabs | Every room has built-in waypoints |
| Configure TargetSpawner objects | Spawners reference patrol routes |
| Test complete behavior loop | Patrol → Chase → Flee → Return to Patrol |

---

## Phase 1: Add NavMeshRoomConnector to LevelLayout

**Time**: 5 minutes  
**Location**: ExampleScene

### Steps

1. **Open ExampleScene**
   - `Assets/Creator Kit - FPS/Scenes/ExampleScene.unity`

2. **Select LevelLayout in Hierarchy**
   - May need to scroll; room children are hidden but LevelLayout is visible

3. **Add NavMeshRoomConnector Component**
   - Click "Add Component" in Inspector
   - Search: "NavMeshRoomConnector"
   - Select it

4. **Configure Settings**
   | Setting | Value | Purpose |
   |---------|-------|---------|
   | Link Width | 2.0 | Matches doorway width |
   | Cost Modifier | 1.0 | Default traversal cost |
   | Bidirectional | ✓ | Agents can cross both ways |
   | Auto Connect | ✓ | Creates links on Start |
   | Debug Mode | ✓ | Console logs link creation |

5. **Save Scene**
   - Ctrl+S

### Verification
- Component visible on LevelLayout in Inspector
- Settings match table above

---

## Phase 2: Add Patrol Routes to Room Prefabs

**Time**: 25-35 minutes  
**Location**: Room prefabs in Project window

### Strategy

You'll add a `PatrolRoute_Default` child GameObject to each room prefab that will contain enemies. Each patrol route has 3-4 waypoint children positioned on the floor.

### Room Prefabs to Modify

Based on ExampleScene's 11 rooms, prioritize rooms where spawners exist:

| Room Prefab | Priority | Waypoint Pattern |
|-------------|----------|------------------|
| HeartRoom | HIGH | Square patrol (4 waypoints) |
| StomachRoom | HIGH | Triangle patrol (3 waypoints) |
| BrainRoom | HIGH | Square patrol (4 waypoints) |
| CorridorLong | MEDIUM | Linear (2 waypoints at ends) |
| CorridorMedium | MEDIUM | Linear (2 waypoints) |
| CorridorCorner | LOW | L-shape (3 waypoints) |
| CorridorCube | LOW | Square (4 waypoints) |
| CorridorV | LOW | V-shape (3 waypoints) |

**Start with HIGH priority rooms** - these likely have spawners.

---

### Step-by-Step: HeartRoom Prefab

#### 2.1 Open Prefab in Prefab Mode

1. In Project window, navigate to:
   ```
   Assets/Creator Kit - FPS/Prefabs/Rooms/
   ```

2. **Double-click `HeartRoom.prefab`**
   - This opens Prefab Mode (isolated editing)
   - Hierarchy shows prefab contents only
   - Scene view shows the room geometry

#### 2.2 Create PatrolRoute_Default Parent

1. In Hierarchy (showing HeartRoom contents):
   - Right-click the **root** (HeartRoom)
   - Select **Create Empty**

2. Rename to `PatrolRoute_Default`
   - Select the new GameObject
   - Press F2 or click name in Inspector
   - Type: `PatrolRoute_Default`

3. **Reset Transform** (important!)
   - With PatrolRoute_Default selected
   - In Inspector, right-click Transform component
   - Select "Reset"
   - Position should be (0, 0, 0)

#### 2.3 Create Waypoint Children

Create 4 waypoint children for a square patrol pattern:

**Waypoint_0:**
1. Right-click `PatrolRoute_Default` → Create Empty
2. Rename to `Waypoint_0`
3. In Scene view:
   - Press F to frame the room
   - Use Move tool (W) to position on floor
   - Place near one corner of the room's walkable area
4. In Inspector, note the position (for reference)

**Waypoint_1:**
1. Right-click `PatrolRoute_Default` → Create Empty
2. Rename to `Waypoint_1`
3. Position diagonally opposite from Waypoint_0
   - Creates first leg of patrol

**Waypoint_2:**
1. Right-click `PatrolRoute_Default` → Create Empty
2. Rename to `Waypoint_2`
3. Position to form third corner of square

**Waypoint_3:**
1. Right-click `PatrolRoute_Default` → Create Empty
2. Rename to `Waypoint_3`
3. Position to complete square pattern
   - AI will loop: 0 → 1 → 2 → 3 → 0 → ...

#### 2.4 Waypoint Positioning Guidelines

| Guideline | Reason |
|-----------|--------|
| Keep 0.5-1 unit from walls | NavMeshAgent radius clearance |
| Place on floor level (Y ≈ 0) | Must be on NavMesh surface |
| Spread across room | Creates visible patrol movement |
| Avoid obstacles | Prevents pathfinding detours |

**Visual Check**: In Scene view, imagine the AI walking the path:
- 0 → 1 → 2 → 3 → 0
- Does it cover key areas?
- Are there awkward corners?

#### 2.5 Save Prefab

1. Press **Ctrl+S** to save prefab changes
2. Exit Prefab Mode:
   - Click the **left arrow** (←) in Hierarchy header, OR
   - Click "Scenes" in the breadcrumb at top of Hierarchy

#### 2.6 Verify Prefab Structure

After saving, the prefab hierarchy should look like:

```
HeartRoom (Prefab Root)
├── Heart                        ← Existing geometry
├── HeartRoom001                 ← Existing child
├── HeartRoom_doorSnap1          ← Existing exit
├── HeartRoom_doorSnap2          ← Existing exit
├── LightParticles               ← Existing
├── Point Light                  ← Existing
├── ReverbZone                   ← Existing
├── Sound                        ← Existing
└── PatrolRoute_Default          ← NEW
    ├── Waypoint_0               ← NEW
    ├── Waypoint_1               ← NEW
    ├── Waypoint_2               ← NEW
    └── Waypoint_3               ← NEW
```

---

### Step-by-Step: StomachRoom Prefab

Repeat the process with 3 waypoints (triangle pattern):

1. Double-click `StomachRoom.prefab`
2. Create `PatrolRoute_Default` child
3. Create 3 waypoints:
   - `Waypoint_0` - position A
   - `Waypoint_1` - position B
   - `Waypoint_2` - position C
4. Arrange in triangle pattern covering room
5. Save prefab (Ctrl+S)
6. Exit Prefab Mode

---

### Step-by-Step: BrainRoom Prefab

Repeat with 4 waypoints (square pattern):

1. Double-click `BrainRoom.prefab`
2. Create `PatrolRoute_Default` child
3. Create 4 waypoints arranged in square
4. Save prefab
5. Exit Prefab Mode

---

### Step-by-Step: Corridor Prefabs

For corridors, use 2 waypoints (linear patrol - back and forth):

**CorridorLong:**
1. Double-click `CorridorLong.prefab`
2. Create `PatrolRoute_Default`
3. Create 2 waypoints:
   - `Waypoint_0` - near one end
   - `Waypoint_1` - near other end
4. Save and exit

**CorridorMedium:**
- Same as CorridorLong (2 waypoints)

**CorridorCorner:**
- Use 3 waypoints forming L-shape

**CorridorCube:**
- Use 4 waypoints in square

**CorridorV:**
- Use 3 waypoints in V-shape

---

### Phase 2 Checklist

| Prefab | PatrolRoute Added | Waypoint Count | Saved |
|--------|-------------------|----------------|-------|
| HeartRoom | ☐ | 4 | ☐ |
| StomachRoom | ☐ | 3 | ☐ |
| BrainRoom | ☐ | 4 | ☐ |
| CorridorLong | ☐ | 2 | ☐ |
| CorridorMedium | ☐ | 2 | ☐ |
| CorridorCorner | ☐ | 3 | ☐ |
| CorridorCube | ☐ | 4 | ☐ |
| CorridorV | ☐ | 3 | ☐ |

---

## Phase 3: Configure TargetSpawner Objects

**Time**: 10-15 minutes  
**Location**: ExampleScene (Play Mode for some steps)

### Challenge: Room Instances Hidden in Edit Mode

Room instances under LevelLayout are hidden via `HideFlags.HideInHierarchy` during Edit Mode. This means you can't directly drag patrol routes from room instances to spawner fields in Edit Mode.

### Solution: Runtime Configuration Approach

1. Enter Play Mode (rooms become visible)
2. Bake NavMesh
3. Pause game
4. Configure spawners with patrol routes
5. Note configurations
6. Exit Play Mode
7. Apply configurations in Edit Mode using a helper script

**OR** use the simpler approach below:

### Simpler Solution: Auto-Find Patrol Route

Since every room prefab now has `PatrolRoute_Default`, we can modify TargetSpawner to auto-find the patrol route if none is assigned.

#### 3.1 Verify Current TargetSpawner.cs Behavior

Check if TargetSpawner already has auto-find logic. If not, the spawner will log a warning but targets will still spawn (just won't patrol until route assigned).

#### 3.2 Configure Spawners in Play Mode

1. **Open ExampleScene**

2. **Enter Play Mode**
   - Press Play button or Ctrl+P

3. **Bake NavMesh** (if not auto-baked)
   - Select LevelLayout
   - Click "Bake Complete NavMesh" in Inspector
   - Wait for "Successfully baked NavMesh" console message

4. **Pause Game**
   - Press Pause button or Ctrl+Shift+P

5. **Find TargetSpawner Objects**
   - In Hierarchy, search "Spawner"
   - Note: With rooms now visible, you can see which room each spawner is in

6. **For Each Spawner, Configure:**

   a. **Select the spawner** in Hierarchy
   
   b. **Expand `Spawn Events`** in Inspector
   
   c. **Set Size** to 1 (or more for multiple waves)
   
   d. **Configure SpawnEvent[0]:**
   
   | Field | Value |
   |-------|-------|
   | Target To Spawn | Drag `GermSlimeTarget` or `BloodCellTarget` prefab from Project window |
   | Count | 2 (start small for testing) |
   | Time Between Spawn | 2.0 |
   | Patrol Route | Drag `PatrolRoute_Default` from the parent room |

   e. **To find the patrol route:**
   - Look at the spawner's parent hierarchy
   - Find the room it's inside (e.g., HeartRoom)
   - Expand that room in Hierarchy
   - Find `PatrolRoute_Default` child
   - Drag it to the "Patrol Route" field

7. **Record Your Configurations**
   
   Write down each spawner's configuration because Play Mode changes are lost:
   
   ```
   Spawner: TargetKeySpawner
   Room: HeartRoom
   Target: GermSlimeTarget
   Count: 2
   Time: 2.0
   Route: HeartRoom/PatrolRoute_Default
   ```

8. **Exit Play Mode**
   - All Inspector changes are lost
   - This is expected

#### 3.3 Apply Configurations Permanently

Since we can't easily reference runtime patrol routes in Edit Mode, we have two options:

**Option A: Create Scene-Level Patrol Route References**

1. In Edit Mode, create empty GameObjects at scene root that mirror patrol routes
2. These are visible and assignable
3. Configure spawners to use these

**Option B: Modify TargetSpawner for Auto-Discovery**

Add code to TargetSpawner that finds the patrol route automatically.

---

### Recommended: Add Auto-Discovery to TargetSpawner

This small modification makes configuration much easier.

#### Code Addition for TargetSpawner.cs

Add this method to `TargetSpawner.cs`:

```csharp
/// <summary>
/// Finds patrol route in the room containing this spawner
/// Called when SpawnEvent.patrolRoute is null
/// </summary>
private Transform FindPatrolRouteInParentRoom()
{
    // Try to find LevelRoom in parent hierarchy
    LevelRoom room = GetComponentInParent<LevelRoom>();
    if (room != null)
    {
        // Look for PatrolRoute_Default child
        Transform route = room.transform.Find("PatrolRoute_Default");
        if (route != null && route.childCount > 0)
        {
            Debug.Log($"Auto-found patrol route in {room.name}");
            return route;
        }
    }
    
    Debug.LogWarning($"TargetSpawner {name}: No patrol route found in parent room");
    return null;
}
```

Then modify the spawn logic to use it:

```csharp
// In SpawnTarget() method, after instantiation:
if (element.aiComponent != null)
{
    // Use assigned route, or auto-find if null
    Transform routeToUse = element.patrolRoute;
    if (routeToUse == null)
    {
        routeToUse = FindPatrolRouteInParentRoom();
    }
    
    if (routeToUse != null)
    {
        element.aiComponent.patrolRoute = routeToUse;
        Debug.Log($"Assigned patrol route to {targetObj.name}");
    }
}
```

**With this modification:**
- Leave "Patrol Route" field empty on spawners
- Spawner automatically finds `PatrolRoute_Default` in its parent room
- Zero manual configuration needed for patrol routes

---

## Phase 4: Increase Target Health for Testing

**Time**: 5 minutes  
**Location**: Target prefabs

Flee behavior requires targets to survive hits. Default health may be too low.

### Steps

1. **Open GermSlimeTarget prefab**
   - `Assets/Creator Kit - FPS/Prefabs/Targets/GermSlimeTarget.prefab`
   - Double-click to enter Prefab Mode

2. **Find Target component**
   - May be on root or child GameObject
   - Look for "Target" component in Inspector

3. **Set Health**
   - Change `Health` field to **15** or **20**
   - Default weapon damage is typically 5-10
   - This ensures targets survive 1-2 hits

4. **Save prefab** (Ctrl+S)

5. **Repeat for BloodCellTarget**
   - Same process, set Health to 15-20

### Target Health Reference

| Target Prefab | Original Health | Test Health | Production Health |
|---------------|-----------------|-------------|-------------------|
| GermSlimeTarget | 1-5 | 15-20 | Tune to gameplay |
| BloodCellTarget | 1-5 | 15-20 | Tune to gameplay |

---

## Phase 5: Test Complete Behavior Loop

**Time**: 10 minutes  
**Location**: ExampleScene Play Mode

### Test Procedure

1. **Enter Play Mode**

2. **Bake NavMesh** (if needed)
   - Select LevelLayout
   - Click "Bake Complete NavMesh"

3. **Check Console for:**
   - ✓ "Successfully baked NavMesh for 11 rooms"
   - ✓ "NavMeshRoomConnector: Created X NavMeshLinks"
   - ✓ Spawn messages: "Spawned [target] at [position]"
   - ✓ "Assigned patrol route to [target]"

4. **Observe Target Behavior:**

   | Behavior | Expected | If Failing |
   |----------|----------|------------|
   | **Patrol** | Targets walk between waypoints | Check patrol route has children |
   | **Chase** | Targets pursue when player approaches (~15 units) | Check Detection Radius |
   | **Flee** | Targets flee when shot (if health > 0) | Increase target health |
   | **Return** | Targets resume patrol after flee duration | Check Flee Duration setting |
   | **Multi-room** | Targets navigate through doorways | Check NavMeshLinks created |

5. **Test Checklist**

   | Test | Pass/Fail |
   |------|-----------|
   | ☐ Targets spawn at spawner positions |  |
   | ☐ Targets begin patrolling waypoints |  |
   | ☐ Targets detect and chase player |  |
   | ☐ Targets flee when shot (not killed) |  |
   | ☐ Targets return to patrol after fleeing |  |
   | ☐ Targets can navigate between rooms |  |
   | ☐ Targets can be killed (scoring works) |  |

---

## Troubleshooting

### Targets Don't Move (Stand Still After Spawn)

| Cause | Solution |
|-------|----------|
| No patrol route assigned | Implement auto-discovery OR manually assign |
| Patrol route has no children | Add waypoint children to PatrolRoute_Default |
| NavMesh not baked | Click "Bake Complete NavMesh" |
| NavMeshAgent not on NavMesh | Move spawn point to valid NavMesh area |

### Targets Don't Patrol (Only Chase/Flee)

| Cause | Solution |
|-------|----------|
| Waypoints outside NavMesh | Reposition waypoints onto walkable area |
| Detection radius too large | Player always detected, never patrols |
| Waypoint tolerance too small | Increase waypointTolerance in TargetAI |

### Targets Don't Chase

| Cause | Solution |
|-------|----------|
| Detection radius too small | Increase detectionRadius (default 15) |
| Player reference null | Verify Controller.Instance exists |
| playerCheckInterval too high | Decrease for faster detection |

### Targets Don't Flee

| Cause | Solution |
|-------|----------|
| Target dies instantly | Increase target health (15-20) |
| Flee not triggered in Target.cs | Verify GetComponentInParent<TargetAI>() call |
| Flee distance too short | Increase fleeDistance in TargetAI |

### Multi-Room Navigation Fails

| Cause | Solution |
|-------|----------|
| No NavMeshLinks | Verify NavMeshRoomConnector on LevelLayout |
| Links not created | Check Debug Mode console output |
| Rooms not connected | Verify room exits are aligned in LevelLayout |

---

## Summary Checklist

### Phase 1: NavMeshRoomConnector
- ☐ Added to LevelLayout
- ☐ Link Width = 2.0
- ☐ Auto Connect = ✓
- ☐ Debug Mode = ✓
- ☐ Scene saved

### Phase 2: Room Prefab Patrol Routes
- ☐ HeartRoom has PatrolRoute_Default (4 waypoints)
- ☐ StomachRoom has PatrolRoute_Default (3 waypoints)
- ☐ BrainRoom has PatrolRoute_Default (4 waypoints)
- ☐ CorridorLong has PatrolRoute_Default (2 waypoints)
- ☐ CorridorMedium has PatrolRoute_Default (2 waypoints)
- ☐ At least 3 corridor prefabs modified
- ☐ All modified prefabs saved

### Phase 3: TargetSpawner Configuration
- ☐ Auto-discovery code added to TargetSpawner (recommended)
- ☐ OR spawners manually configured with patrol routes
- ☐ At least one SpawnEvent configured per spawner

### Phase 4: Target Health
- ☐ GermSlimeTarget health increased to 15-20
- ☐ BloodCellTarget health increased to 15-20
- ☐ Prefabs saved

### Phase 5: Testing
- ☐ NavMesh bakes successfully
- ☐ NavMeshLinks created (console confirms)
- ☐ Targets patrol waypoints
- ☐ Targets chase player
- ☐ Targets flee when wounded
- ☐ Targets return to patrol
- ☐ Multi-room navigation works

---

## Next Steps After Completion

1. **Tune AI Parameters**
   - Adjust detection radius for desired difficulty
   - Tune patrol/chase/flee speeds
   - Balance target health

2. **Disable Debug Mode**
   - NavMeshRoomConnector: Debug Mode = ✗
   - Reduce console spam

3. **Create Varied Patrol Routes**
   - Different patterns in different rooms
   - Some rooms with multiple route options

4. **Document Student Exercise**
   - Have students add patrol routes to remaining rooms
   - Challenge: Create multi-room patrol routes

---

*End of Implementation Plan*
