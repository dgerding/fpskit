# Creator Kit FPS: NavMesh-Based AI & Fleeing Behavior Implementation Guide

**Document Purpose**: Complete reference for implementing NavMesh-based AI pathfinding and fleeing behavior in Unity's Creator Kit FPS template, designed for student learning following Harrison Ferrone's "Learning C# by Developing Games with Unity" (Chapter 9: Basic AI and Enemy Behavior).

**Date**: November 4, 2025  
**Target Unity Version**: Unity 6 (with Unity AI Navigation Package)  
**Primary Reference**: Ferrone, Chapter 9, pages 265-294

---

## Table of Contents

1. [Context & Background](#1-context--background)
2. [Current Creator Kit FPS Architecture](#2-current-creator-kit-fps-architecture)
3. [NavMesh Implementation Overview](#3-navmesh-implementation-overview)
4. [Complete Script Implementations](#4-complete-script-implementations)
5. [Fleeing Behavior Implementation](#5-fleeing-behavior-implementation)
6. [Integration & Testing Guide](#6-integration--testing-guide)
7. [Learning Outcomes & Book References](#7-learning-outcomes--book-references)
8. [Troubleshooting & Common Pitfalls](#8-troubleshooting--common-pitfalls)
9. [Recommended Project Knowledge Files](#9-recommended-project-knowledge-files)

---

## 1. Context & Background

### 1.1 Educational Objective

This implementation transforms the Creator Kit FPS from a **scripted path-following system** to an **intelligent NavMesh-based AI system**, teaching students:

- Unity's AI Navigation system (NavMeshSurface, NavMeshAgent, NavMeshLink)
- State machine architecture for AI behavior
- Procedural programming with waypoint systems
- Vector mathematics for direction calculation
- Event-driven programming with callbacks

### 1.2 Current vs. Target System

**Current System:**

- Targets follow predefined paths using `PathSystem` (linear interpolation)
- Movement is deterministic and scripted
- No awareness of environment or player
- Movement via `Rigidbody.MovePosition()`

**Target System:**

- Targets navigate autonomously using NavMesh
- Patrol routes with dynamic player detection
- Chase behavior when player enters range
- Flee behavior when wounded but not destroyed
- Movement via `NavMeshAgent` with obstacle avoidance

### 1.3 Ferrone Book Alignment

This implementation directly applies Chapter 9 concepts:

- **Pages 266-270**: NavMesh fundamentals and baking
- **Pages 271-273**: NavMeshAgent setup and registration
- **Pages 274-277**: Procedural programming for patrol routes
- **Pages 278-282**: Moving agents and Update() logic
- **Pages 283-284**: Player detection and destination changes

---

## 2. Current Creator Kit FPS Architecture

### 2.1 Key System Components

#### Target.cs - Damage & Destruction System

```csharp
// Location: Assets/Creator Kit - FPS/Scripts/System/Target.cs
// Purpose: Handles target health, damage, and destruction
// Key Method: Got(float damage) - Called by weapons when hit

public class Target : MonoBehaviour
{
    public float health = 5.0f;
    public int pointValue;
    private float m_CurrentHealth;
    private bool m_Destroyed = false;

    public void Got(float damage)
    {
        m_CurrentHealth -= damage;

        if (HitPlayer != null)
            HitPlayer.PlayRandom();

        // KEY INSIGHT: This check provides the fleeing hook
        if (m_CurrentHealth > 0)
            return; // Target survived - FLEEING BEHAVIOR GOES HERE

        // Target destroyed - cleanup and notify GameSystem
        m_Destroyed = true;
        gameObject.SetActive(false);
        GameSystem.Instance.TargetDestroyed(pointValue);
    }
}
```

**Critical Insight**: The `Got()` method is called **every time** a target is hit, providing the perfect callback for fleeing behavior when `m_CurrentHealth > 0`.

#### TargetSpawner.cs - Current Movement System

```csharp
// Location: Assets/Creator Kit - FPS/Scripts/System/TargetSpawner.cs
// Purpose: Spawns targets along predefined paths
// Movement: Uses PathSystem for linear interpolation

public class TargetSpawner : MonoBehaviour
{
    public PathSystem path = new PathSystem();
    public float speed = 1.0f;

    void Update()
    {
        float distanceToGo = speed * Time.deltaTime;
        for (int i = 0; i < m_ActiveElements.Count; ++i)
        {
            var currentElem = m_ActiveElements[i];

            if(currentElem.target.Destroyed)
                continue;

            var evt = path.Move(currentElem.pathData, distanceToGo);

            switch (evt)
            {
                case PathSystem.PathEvent.Finished:
                    m_ActiveElements.RemoveAt(i);
                    i--;
                    break;
                default:
                    currentElem.rb.MovePosition(currentElem.pathData.position);
                    break;
            }
        }
    }
}
```

**Key Observation**: This system will be **replaced** with NavMeshAgent-based movement.

#### Weapon.cs - Raycast Damage Application

```csharp
// Location: Assets/Creator Kit - FPS/Scripts/System/Weapon.cs
// Relevant Method: RaycastShot() - line ~1650

void RaycastShot(Vector3 direction)
{
    Ray r = new Ray(EndPoint.position, direction);
    RaycastHit hit;

    if (Physics.Raycast(r, out hit, 1000.0f, ~(1 << 9), QueryTriggerInteraction.Ignore))
    {
        // Play impact effects
        ImpactManager.Instance.PlayImpact(hit.point, hit.normal, renderer?.sharedMaterial);

        // Check if hit object is a Target (Layer 10)
        if (hit.collider.gameObject.layer == 10)
        {
            Target target = hit.collider.gameObject.GetComponent<Target>();
            target.Got(damage); // THIS CALLS Target.Got()
        }
    }
}
```

**Key Flow**: Weapon fires â†’ Raycast hits Target â†’ `target.Got(damage)` called â†’ Fleeing behavior triggered if health > 0.

#### Controller.cs - Player Reference

```csharp
// Location: Assets/Creator Kit - FPS/Scripts/System/Controller.cs
// Purpose: Player controller with Singleton access

public class Controller : MonoBehaviour
{
    // Singleton pattern - accessible globally
    public static Controller Instance { get; protected set; }

    void Awake()
    {
        Instance = this;
    }

    // CharacterController for movement
    CharacterController m_CharacterController;

    // Camera transforms
    public Transform CameraPosition;
}
```

**Usage in AI**: AI scripts access player via `Controller.Instance.transform.position`.

#### GameSystem.cs - Game State Manager

```csharp
// Location: Assets/Creator Kit - FPS/Scripts/System/GameSystem.cs
// Purpose: Singleton managing game progression and scoring

public class GameSystem : MonoBehaviour
{
    public static GameSystem Instance { get; private set; }

    public void TargetDestroyed(int score)
    {
        m_TargetDestroyed += 1;
        m_Score += score;
        GameSystemInfo.Instance.UpdateScore(m_Score);
    }
}
```

**Integration Point**: Called by `Target.Got()` when health reaches zero.

#### LevelLayout.cs - Modular Room System

```csharp
// Location: Assets/Creator Kit - FPS/Scripts/LevelLayout/LevelLayout.cs
// Purpose: Manages dynamically-assembled level rooms

public class LevelLayout : MonoBehaviour
{
    public LevelRoom[] rooms = new LevelRoom[0];
    public bool Destroyed { get; private set; }
}
```

**Challenge**: Levels are built from modular rooms, requiring special NavMesh handling.

### 2.2 System Data Flow

```
Player Fires Weapon
    â†“
Weapon.RaycastShot() performs raycast
    â†“
Raycast hits Target (Layer 10)
    â†“
Weapon calls: target.Got(damage)
    â†“
Target.Got() reduces health
    â†“
IF health > 0: Target survives (FLEE HERE)
IF health <= 0: Target.Destroyed() called
    â†“
GameSystem.TargetDestroyed() updates score
```

### 2.3 Key Dependencies

The Creator Kit FPS relies on these systems (not yet in project knowledge):

- **Helpers.cs**: Utility functions (e.g., `RecursiveLayerChange()`)
- **RandomPlayer.cs**: Random audio clip playback
- **PoolSystem.cs**: Object pooling for particles and effects
- **WorldAudioPool.cs**: 3D audio source pooling
- **ImpactManager.cs**: Bullet hit effect management
- **CameraShaker.cs**: Screen shake effects
- **WeaponInfoUI.cs**: HUD ammo/weapon display
- **GameSystemInfo.cs**: HUD timer/score display

---

## 3. NavMesh Implementation Overview

### 3.1 Implementation Phases

**Phase 1: NavMesh Foundation** (30-45 minutes)

- Install AI Navigation Package
- Create per-room NavMeshSurface components
- Implement collective baking system
- Verify NavMesh coverage

**Phase 2: TargetAI Component** (1-2 hours)

- Create new TargetAI script with NavMeshAgent
- Implement patrol state machine
- Add player detection system
- Test basic navigation

**Phase 3: Waypoint System** (45-60 minutes)

- Create NavDestination component
- Design waypoint prefab
- Place waypoints in rooms
- Test patrol routes

**Phase 4: Spawner Integration** (30-45 minutes)

- Refactor TargetSpawner for NavMesh
- Remove PathSystem dependencies
- Test spawning with patrol routes

**Phase 5: Room Connectivity** (1-2 hours)

- Create NavMeshRoomConnector
- Generate NavMeshLinks at exits
- Test multi-room traversal
- Debug pathfinding issues

**Phase 6: Fleeing Behavior** (30-45 minutes)

- Modify Target.Got() to trigger flee
- Implement flee state in TargetAI
- Add flee position calculation
- Test flee-then-resume-patrol

### 3.2 Architecture Diagram

```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚                        GAME SYSTEM                          â”‚
â”‚                    (Singleton Manager)                      â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                         â”‚
            â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”´â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
            â”‚                         â”‚
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â–¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”   â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â–¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚   CONTROLLER         â”‚   â”‚   LEVELLAY OUT     â”‚
â”‚   (Player)           â”‚   â”‚   (Rooms)          â”‚
â”‚  - Transform         â”‚   â”‚  - LevelRoom[]     â”‚
â”‚  - Singleton         â”‚   â”‚  - NavMeshLinks    â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜   â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
            â”‚                         â”‚
            â”‚ Player                  â”‚ Waypoints
            â”‚ Position                â”‚
            â”‚                         â”‚
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â–¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â–¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚              TARGETAI                           â”‚
â”‚         (NavMesh-Based Enemy AI)                â”‚
â”‚  - NavMeshAgent                                 â”‚
â”‚  - State Machine (Patrol/Chase/Flee)            â”‚
â”‚  - Patrol Waypoints                             â”‚
â”‚  - Player Detection                             â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
             â”‚
             â”‚ Attached to
             â”‚
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â–¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚              TARGET                              â”‚
â”‚         (Health & Destruction)                   â”‚
â”‚  - Got(damage) method                            â”‚
â”‚  - Health tracking                               â”‚
â”‚  - Calls TargetAI.StartFleeing()                 â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
             â”‚
             â”‚ When destroyed
             â”‚
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â–¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚         WEAPON â†’ RAYCAST â†’ TARGET.GOT()         â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

---

## 4. Complete Script Implementations

### 4.1 NavMesh Foundation Scripts

#### 4.1.1 LevelRoomNavMesh.cs

```csharp
// Location: Create new file at Assets/Creator Kit - FPS/Scripts/AI/LevelRoomNavMesh.cs
// Purpose: Manages NavMesh generation for individual modular rooms
// References: Ferrone pg. 268-270 (NavMeshSurface setup)

using UnityEngine;
using Unity.AI.Navigation;

/// <summary>
/// Handles per-room NavMesh baking for modular level architecture
/// Attached to each room prefab to enable runtime NavMesh generation
///
/// ARCHITECTURE NOTES:
/// The Creator Kit uses modular rooms assembled at runtime, so each room
/// needs its own NavMeshSurface rather than one global NavMesh
///
/// STUDENT LEARNING:
/// - Unity.AI.Navigation namespace (new package system)
/// - Component-based architecture
/// - Runtime NavMesh generation
/// </summary>
[RequireComponent(typeof(NavMeshSurface))]
public class LevelRoomNavMesh : MonoBehaviour
{
    // Reference to this room's NavMeshSurface component
    private NavMeshSurface navMeshSurface;

    // Track if this room's NavMesh has been baked
    private bool hasBeenBaked = false;

    /// <summary>
    /// Initialize on awakening
    /// NOTE: NavMeshSurface must be added to prefab in Unity Editor
    /// </summary>
    void Awake()
    {
        // Get the NavMeshSurface component (required by RequireComponent attribute)
        navMeshSurface = GetComponent<NavMeshSurface>();

        if (navMeshSurface == null)
        {
            Debug.LogError($"Room {gameObject.name} is missing NavMeshSurface! Add it to the prefab.");
            return;
        }

        // Configure NavMeshSurface for this room
        navMeshSurface.collectObjects = CollectObjects.Children;
        navMeshSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;

        Debug.Log($"LevelRoomNavMesh initialized for {gameObject.name}");
    }

    /// <summary>
    /// Bake the NavMesh for this specific room
    /// Called by LevelLayout after all rooms are positioned
    ///
    /// FERRONE REFERENCE: pg. 269 - "Baking the NavMesh"
    /// </summary>
    public void BakeRoomNavMesh()
    {
        if (hasBeenBaked)
        {
            Debug.LogWarning($"Room {gameObject.name} NavMesh already baked, skipping");
            return;
        }

        if (navMeshSurface == null)
        {
            Debug.LogError($"Cannot bake NavMesh for {gameObject.name} - no NavMeshSurface!");
            return;
        }

        // Build the NavMesh for this room
        navMeshSurface.BuildNavMesh();
        hasBeenBaked = true;

        Debug.Log($"Successfully baked NavMesh for room: {gameObject.name}");
    }

    /// <summary>
    /// Get bounds of this room's NavMesh (for debugging/visualization)
    /// </summary>
    public Bounds GetNavMeshBounds()
    {
        if (navMeshSurface != null)
        {
            return navMeshSurface.navMeshData.sourceBounds;
        }
        return new Bounds(transform.position, Vector3.one);
    }

    /// <summary>
    /// Editor visualization of NavMesh bounds
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (navMeshSurface != null && hasBeenBaked)
        {
            Gizmos.color = Color.cyan;
            Bounds bounds = GetNavMeshBounds();
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
```

#### 4.1.2 Modified LevelLayout.cs

```csharp
// Location: Modify existing file at Assets/Creator Kit - FPS/Scripts/LevelLayout/LevelLayout.cs
// Purpose: Add NavMesh baking coordination for all rooms
// Changes: Add BakeCompleteNavMesh() method and NavMesh management

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class LevelLayout : MonoBehaviour
{
    // EXISTING CODE...
    public LevelRoom[] rooms = new LevelRoom[0];
    public bool Destroyed { get; private set; }

    // ====================================================================
    // NEW: NAVMESH MANAGEMENT SECTION
    // ====================================================================

    /// <summary>
    /// Bake NavMesh for all rooms in the level
    /// Call this after level layout is complete
    ///
    /// STUDENT NOTE:
    /// This demonstrates coordination between multiple AI components
    /// Each room manages its own NavMesh, but we need central coordination
    ///
    /// FERRONE REFERENCE: pg. 269-270 (Runtime NavMesh generation)
    /// </summary>
    [ContextMenu("Bake Complete NavMesh")]
    public void BakeCompleteNavMesh()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("NavMesh baking only works in Play Mode!");
            return;
        }

        Debug.Log("Starting complete NavMesh bake for all rooms...");

        int successCount = 0;
        int errorCount = 0;

        // Iterate through all rooms and bake their NavMeshes
        foreach (LevelRoom room in rooms)
        {
            if (room == null) continue;

            // Check if room has LevelRoomNavMesh component
            // FIXED: No longer auto-adds component to avoid duplication
            LevelRoomNavMesh navMeshComponent = room.GetComponent<LevelRoomNavMesh>();

            if (navMeshComponent == null)
            {
                Debug.LogError($"Room {room.name} is missing LevelRoomNavMesh component! " +
                             "Add NavMeshSurface and LevelRoomNavMesh to the room prefab.");
                errorCount++;
                continue;
            }

            // Bake the NavMesh for this room
            navMeshComponent.BakeRoomNavMesh();
            successCount++;
        }

        Debug.Log($"NavMesh baking complete! Success: {successCount}, Errors: {errorCount}");

        // After baking all room NavMeshes, create connections
        if (successCount > 0)
        {
            StartCoroutine(CreateNavMeshLinksAfterDelay());
        }
    }

    /// <summary>
    /// Create NavMeshLinks between rooms after a short delay
    /// Ensures NavMesh data is fully initialized
    /// </summary>
    IEnumerator CreateNavMeshLinksAfterDelay()
    {
        yield return new WaitForSeconds(0.5f); // Let NavMesh data settle

        // Find or create NavMeshRoomConnector
        NavMeshRoomConnector connector = GetComponent<NavMeshRoomConnector>();
        if (connector == null)
        {
            connector = gameObject.AddComponent<NavMeshRoomConnector>();
        }

        // Connect all rooms
        connector.ConnectAllRooms();
    }

    /// <summary>
    /// Automatically bake NavMesh when entering Play Mode
    /// Disabled by default - enable for testing
    /// </summary>
    void Start()
    {
        // Uncomment to auto-bake on play
        // if (Application.isPlaying)
        // {
        //     StartCoroutine(AutoBakeAfterDelay());
        // }
    }

    /// <summary>
    /// Delayed auto-bake to ensure rooms are fully initialized
    /// </summary>
    IEnumerator AutoBakeAfterDelay()
    {
        yield return new WaitForSeconds(1.0f);
        BakeCompleteNavMesh();
    }

    // EXISTING METHODS...
    void OnDestroy()
    {
        Destroyed = true;
    }

    // ====================================================================
    // EDITOR HELPERS
    // ====================================================================

#if UNITY_EDITOR
    /// <summary>
    /// Custom inspector button to trigger NavMesh baking
    /// </summary>
    [CustomEditor(typeof(LevelLayout))]
    public class LevelLayoutEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            LevelLayout layout = (LevelLayout)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("NavMesh Tools", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                if (GUILayout.Button("Bake Complete NavMesh", GUILayout.Height(30)))
                {
                    layout.BakeCompleteNavMesh();
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "NavMesh baking only works in Play Mode.\n" +
                    "Enter Play Mode to bake NavMeshes.",
                    MessageType.Info
                );
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Setup Instructions:\n" +
                "1. Ensure each room prefab has NavMeshSurface component\n" +
                "2. Ensure each room prefab has LevelRoomNavMesh component\n" +
                "3. Enter Play Mode\n" +
                "4. Click 'Bake Complete NavMesh'\n" +
                "5. NavMeshLinks will be created automatically",
                MessageType.Info
            );
        }
    }
#endif
}
```

### Implementation Notes

**Key Fix Applied**:

- **REMOVED** automatic `AddComponent<LevelRoomNavMesh>()` to prevent double-adding components
- **REQUIRES** designers to add both `NavMeshSurface` and `LevelRoomNavMesh` to room prefabs
- **ADDED** clear error messages when components are missing
- **USES** `RequireComponent` attribute to ensure NavMeshSurface exists when LevelRoomNavMesh is added

This prevents the double component issue while maintaining clear setup requirements for students.

### 4.2 Phase 2: TargetAI Component

#### 4.2.1 Complete TargetAI.cs

```csharp
// Location: Create new file at Assets/Creator Kit - FPS/Scripts/System/TargetAI.cs
// Purpose: NavMesh-based AI controller for Target enemies
// Reference: Ferrone Chapter 9, pages 273-284

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMesh-based AI controller for enemy targets.
/// Implements patrol, chase, and flee behaviors using state machine architecture.
///
/// LEARNING OBJECTIVES (Ferrone Chapter 9):
/// - NavMeshAgent setup and configuration (pg. 271-273)
/// - Procedural programming for patrol routes (pg. 274-277)
/// - State-based AI behavior (pg. 278-284)
/// - Player detection and destination changes (pg. 283-284)
///
/// STATE MACHINE:
/// Patrol â†’ (Player detected) â†’ Chase â†’ (Player far) â†’ Patrol
///    â†“                            â†“
///    â†“                            â†“
///    â””â”€â”€â”€â”€â”€â”€â†’ (Hit) â†’ Flee â† â”€â”€â”€â”€â”€â”˜
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class TargetAI : MonoBehaviour
{
    // ====================================================================
    // AI STATE ENUMERATION
    // ====================================================================

    /// <summary>
    /// Defines the current behavior state of the AI
    /// Reference: Ferrone pg. 278-284 (state-based enemy behavior)
    /// </summary>
    private enum AIState
    {
        /// <summary>
        /// Patrolling between waypoints on predefined route
        /// Default state - enemy walks patrol route looking for player
        /// </summary>
        Patrolling,

        /// <summary>
        /// Chasing the player after detection
        /// Enemy actively pursues player, updating destination continuously
        /// </summary>
        ChasingPlayer,

        /// <summary>
        /// Fleeing after taking damage but not destroyed
        /// Enemy runs away from player, then returns to patrol
        /// </summary>
        Fleeing,

        /// <summary>
        /// Searching for player at last known location
        /// Optional enhancement - enemy investigates where player was seen
        /// </summary>
        Searching
    }

    // ====================================================================
    // NAVIGATION COMPONENTS
    // ====================================================================

    [Header("Navigation")]
    [Tooltip("Reference to the NavMeshAgent component (auto-assigned)")]
    private NavMeshAgent _agent;

    [Tooltip("Movement speed in units per second")]
    public float moveSpeed = 3.5f;

    [Tooltip("How close to destination before selecting next waypoint")]
    [Range(0.1f, 2.0f)]
    public float destinationThreshold = 0.5f;

    // ====================================================================
    // PATROL SYSTEM
    // ====================================================================
    // Reference: Ferrone pg. 274-277 (procedural patrol route initialization)

    [Header("Patrol Settings")]
    [Tooltip("Parent GameObject containing patrol waypoint children")]
    public Transform patrolRoute;

    [Tooltip("List of patrol waypoints (auto-populated from patrolRoute children)")]
    private List<Transform> patrolLocations = new List<Transform>();

    [Tooltip("Current index in patrol route")]
    private int _currentLocationIndex = 0;

    [Tooltip("Should patrol route loop or ping-pong?")]
    public bool loopPatrol = true;

    [Tooltip("Direction through patrol route (1 = forward, -1 = backward)")]
    private int _patrolDirection = 1;

    // ====================================================================
    // PLAYER DETECTION SYSTEM
    // ====================================================================
    // Reference: Ferrone pg. 283-284 (seek and destroy mechanics)

    [Header("Player Interaction")]
    [Tooltip("Detection range for spotting player")]
    [Range(5f, 30f)]
    public float detectionRange = 15f;

    [Tooltip("Range at which enemy loses player")]
    [Range(15f, 50f)]
    public float losePlayerRange = 25f;

    [Tooltip("How often to check for player (seconds) - optimization")]
    [Range(0.1f, 1.0f)]
    public float playerCheckInterval = 0.5f;

    [Tooltip("Reference to player transform (auto-assigned via Controller.Instance)")]
    private Transform _playerTransform;

    private float _lastPlayerCheck = 0f;

    // ====================================================================
    // FLEE SYSTEM
    // ====================================================================

    [Header("Flee Behavior")]
    [Tooltip("Distance to flee from player when wounded")]
    [Range(10f, 30f)]
    public float fleeDistance = 20f;

    [Tooltip("How long to flee before returning to patrol")]
    [Range(2f, 10f)]
    public float fleeDuration = 5f;

    private float _fleeTimer = 0f;

    // ====================================================================
    // STATE MANAGEMENT
    // ====================================================================

    [Header("Debug")]
    [Tooltip("Current AI state (readonly - for debugging)")]
    [SerializeField] private AIState _currentState = AIState.Patrolling;

    [Tooltip("Show debug visualizations in Scene view")]
    public bool showDebugGizmos = true;

    // ====================================================================
    // INITIALIZATION
    // ====================================================================
    // Reference: Ferrone pg. 276-279 (Start method initialization)

    /// <summary>
    /// Unity lifecycle method - called on first frame before Update
    /// Initializes NavMeshAgent, finds player, sets up patrol route
    ///
    /// Reference: Ferrone pg. 278 "After that, it uses GetComponent() to find
    /// and return the attached NavMeshAgent component to the agent"
    /// </summary>
    void Start()
    {
        // ================================================================
        // GET NAVMESHAGENT COMPONENT
        // ================================================================

        // Get NavMeshAgent component (required by RequireComponent attribute)
        _agent = GetComponent<NavMeshAgent>();

        if (_agent == null)
        {
            Debug.LogError($"TargetAI on {gameObject.name} requires NavMeshAgent component!");
            enabled = false;
            return;
        }

        // ================================================================
        // CONFIGURE AGENT PROPERTIES
        // ================================================================
        // Reference: Ferrone pg. 271-273 (NavMeshAgent component properties)

        // Speed: How fast agent moves (units per second)
        _agent.speed = moveSpeed;

        // Angular Speed: How fast agent rotates (degrees per second)
        _agent.angularSpeed = 120f;

        // Acceleration: How quickly agent reaches max speed
        _agent.acceleration = 8f;

        // Stopping Distance: How close to get to destination
        _agent.stoppingDistance = destinationThreshold;

        // Auto Braking: Slow down when approaching destination
        _agent.autoBraking = true;

        // ================================================================
        // FIND PLAYER REFERENCE
        // ================================================================
        // Reference: Ferrone pg. 284 "Then, we use GameObject.Find("Player")
        // to return a reference to the Player object in the scene"

        // Get player transform via Controller Singleton
        // Controller.Instance is set in Controller.Awake()
        if (Controller.Instance != null)
        {
            _playerTransform = Controller.Instance.transform;
        }
        else
        {
            Debug.LogWarning($"TargetAI on {gameObject.name}: Controller.Instance is null! Player detection disabled.");
        }

        // ================================================================
        // INITIALIZE PATROL ROUTE
        // ================================================================
        // Reference: Ferrone pg. 276-277 (procedural patrol route initialization)

        InitializePatrolRoute();

        // Start patrolling if we have waypoints
        if (patrolLocations.Count > 0)
        {
            MoveToNextPatrolLocation();
        }
        else
        {
            Debug.LogWarning($"TargetAI on {gameObject.name}: No patrol locations found! Assign patrolRoute in Inspector.");
        }
    }

    // ====================================================================
    // PATROL INITIALIZATION
    // ====================================================================
    // Reference: Ferrone pg. 276-277 (procedural programming)

    /// <summary>
    /// Procedurally populates patrol locations from patrolRoute children
    ///
    /// PROCEDURAL PROGRAMMING:
    /// Ferrone pg. 274: "Any task that executes the same logic on one or more
    /// sequential objects is the perfect candidate for procedural programming"
    ///
    /// This method iterates through child transforms and adds them to a list,
    /// demonstrating the procedural approach Ferrone teaches.
    /// </summary>
    void InitializePatrolRoute()
    {
        // Clear existing locations (safety check)
        patrolLocations.Clear();

        // Validate patrol route is assigned
        if (patrolRoute == null)
        {
            Debug.LogWarning($"TargetAI on {gameObject.name}: patrolRoute not assigned!");
            return;
        }

        // Iterate through all children of patrol route parent
        // Reference: Ferrone pg. 277 "Then, we use a foreach statement to loop
        // through each child GameObject in PatrolRoute"
        foreach (Transform child in patrolRoute)
        {
            // Add each child transform to patrol locations list
            // Reference: Ferrone pg. 277 "Finally, we add each sequential child
            // Transform component to the list of locations using the Add() method"
            patrolLocations.Add(child);
        }

        Debug.Log($"TargetAI on {gameObject.name}: Initialized patrol route with {patrolLocations.Count} locations");
    }

    // ====================================================================
    // UPDATE LOOP - STATE MACHINE
    // ====================================================================
    // Reference: Ferrone pg. 280-282 (Update method and state management)

    /// <summary>
    /// Unity lifecycle method - called every frame
    /// Implements state machine pattern for AI behavior
    /// </summary>
    void Update()
    {
        // State machine: execute behavior based on current state
        switch (_currentState)
        {
            case AIState.Patrolling:
                UpdatePatrolling();
                break;

            case AIState.ChasingPlayer:
                UpdateChasing();
                break;

            case AIState.Fleeing:
                UpdateFleeing();
                break;

            case AIState.Searching:
                UpdateSearching();
                break;
        }

        // Periodic player detection check (optimization)
        // Only check every playerCheckInterval seconds instead of every frame
        if (Time.time - _lastPlayerCheck > playerCheckInterval)
        {
            CheckForPlayer();
            _lastPlayerCheck = Time.time;
        }
    }

    // ====================================================================
    // PATROL STATE
    // ====================================================================
    // Reference: Ferrone pg. 280-281 (patrol movement logic)

    /// <summary>
    /// Updates patrol behavior - moves between waypoints
    ///
    /// Reference: Ferrone pg. 280 "First, it declares the Update() method and
    /// adds an if statement to check whether two different conditions are true:
    /// remainingDistance and pathPending"
    /// </summary>
    void UpdatePatrolling()
    {
        // Check if agent has reached current destination
        // Reference: Ferrone pg. 280 "remainingDistance returns how far the
        // NavMeshAgent component currently is from its set destination"
        if (!_agent.pathPending && _agent.remainingDistance < destinationThreshold)
        {
            // Reached waypoint - move to next one
            MoveToNextPatrolLocation();
        }
    }

    /// <summary>
    /// Moves agent to next patrol location in sequence
    ///
    /// Reference: Ferrone pg. 279-281 (MoveToNextPatrolLocation implementation)
    /// </summary>
    void MoveToNextPatrolLocation()
    {
        // Defensive programming: ensure locations list isn't empty
        // Reference: Ferrone pg. 281 "Here, we added an if statement to make
        // sure that Locations isn't empty before the rest of the code"
        if (patrolLocations.Count == 0)
            return;

        // Set NavMeshAgent destination to current patrol location
        // Reference: Ferrone pg. 279 "Finally, it declares MoveToNextPatrolLocation()
        // as a private method and sets _agent.destination"
        _agent.SetDestination(patrolLocations[_currentLocationIndex].position);

        // Increment index with wraparound using modulo operator
        // Reference: Ferrone pg. 281 "Then, we set _locationIndex to its current
        // value, +1, followed by the modulo (%) of Locations.Count"

        if (loopPatrol)
        {
            // Loop mode: 0 â†’ 1 â†’ 2 â†’ 3 â†’ 0 â†’ 1 â†’ ...
            _currentLocationIndex = (_currentLocationIndex + 1) % patrolLocations.Count;
        }
        else
        {
            // Ping-pong mode: 0 â†’ 1 â†’ 2 â†’ 3 â†’ 2 â†’ 1 â†’ 0 â†’ 1 â†’ ...
            _currentLocationIndex += _patrolDirection;

            // Reverse direction at endpoints
            if (_currentLocationIndex >= patrolLocations.Count)
            {
                _currentLocationIndex = patrolLocations.Count - 2;
                _patrolDirection = -1;
            }
            else if (_currentLocationIndex < 0)
            {
                _currentLocationIndex = 1;
                _patrolDirection = 1;
            }
        }
    }

    // ====================================================================
    // PLAYER DETECTION
    // ====================================================================
    // Reference: Ferrone pg. 283-284 (seek and destroy mechanics)

    /// <summary>
    /// Checks for player proximity and transitions states accordingly
    ///
    /// Reference: Ferrone pg. 284 "Finally, we set _agent.destination to the
    /// player's Vector3 position in OnTriggerEnter() whenever the player enters
    /// the enemies' attack zone"
    ///
    /// NOTE: We use distance check instead of trigger collider for simplicity
    /// </summary>
    void CheckForPlayer()
    {
        if (_playerTransform == null)
            return;

        // Calculate distance to player
        // Reference: Unity-3d-Math-Explained.md "Vector3.Distance() calculates
        // straight-line distance between two points for game logic"
        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);

        // STATE TRANSITION: Patrol â†’ Chase
        if (distanceToPlayer <= detectionRange && _currentState == AIState.Patrolling)
        {
            _currentState = AIState.ChasingPlayer;
            Debug.Log($"{gameObject.name} detected player - switching to chase mode");
        }
        // STATE TRANSITION: Chase â†’ Patrol (player escaped)
        else if (distanceToPlayer > losePlayerRange && _currentState == AIState.ChasingPlayer)
        {
            _currentState = AIState.Patrolling;
            MoveToNextPatrolLocation();
            Debug.Log($"{gameObject.name} lost player - resuming patrol");
        }
    }

    // ====================================================================
    // CHASE STATE
    // ====================================================================
    // Reference: Ferrone pg. 283-284 (changing agent destination)

    /// <summary>
    /// Updates chase behavior - pursues player continuously
    ///
    /// Reference: Ferrone pg. 284 "If you play the game now and get too close
    /// to the patrolling enemy, you'll see that it breaks from its path and
    /// comes straight for you"
    /// </summary>
    void UpdateChasing()
    {
        if (_playerTransform == null)
        {
            // Player reference lost - return to patrol
            _currentState = AIState.Patrolling;
            MoveToNextPatrolLocation();
            return;
        }

        // Continuously update destination to player's current position
        // This creates smooth pursuit even as player moves
        _agent.SetDestination(_playerTransform.position);
    }

    // ====================================================================
    // FLEE STATE
    // ====================================================================

    /// <summary>
    /// Called by Target.Got() when enemy is wounded but not destroyed
    /// Triggers flee behavior - enemy runs away from player
    ///
    /// INTEGRATION POINT: This method is called from Target.cs:
    /// if (m_CurrentHealth > 0)
    /// {
    ///     GetComponent<TargetAI>()?.StartFleeing();
    ///     return;
    /// }
    /// </summary>
    public void StartFleeing()
    {
        // Transition to flee state
        _currentState = AIState.Fleeing;
        _fleeTimer = fleeDuration;

        Debug.Log($"{gameObject.name} is fleeing after taking damage!");

        // Calculate flee position (away from player)
        if (_playerTransform != null)
        {
            // Calculate direction away from player
            // Vector3 subtraction: (A - B) gives direction from B to A
            Vector3 directionAwayFromPlayer = (transform.position - _playerTransform.position).normalized;

            // Calculate target flee position
            Vector3 fleePosition = transform.position + directionAwayFromPlayer * fleeDistance;

            // Find valid NavMesh position near calculated point
            // SamplePosition ensures we flee to a walkable location
            NavMeshHit hit;
            if (NavMesh.SamplePosition(fleePosition, out hit, fleeDistance, NavMesh.AllAreas))
            {
                // Valid position found - flee there
                _agent.SetDestination(hit.position);
            }
            else
            {
                // No valid position - flee to nearest patrol waypoint
                Debug.LogWarning($"{gameObject.name}: Could not find valid flee position, moving to nearest waypoint");
                if (patrolLocations.Count > 0)
                {
                    // Find closest patrol waypoint
                    Transform closestWaypoint = patrolLocations[0];
                    float closestDistance = Vector3.Distance(transform.position, closestWaypoint.position);

                    foreach (Transform waypoint in patrolLocations)
                    {
                        float distance = Vector3.Distance(transform.position, waypoint.position);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestWaypoint = waypoint;
                        }
                    }

                    _agent.SetDestination(closestWaypoint.position);
                }
            }
        }
    }

    /// <summary>
    /// Updates flee behavior - runs away for duration then returns to patrol
    /// </summary>
    void UpdateFleeing()
    {
        // Decrement flee timer
        _fleeTimer -= Time.deltaTime;

        // STATE TRANSITION: Flee â†’ Patrol
        if (_fleeTimer <= 0 || (!_agent.pathPending && _agent.remainingDistance < destinationThreshold))
        {
            // Flee complete - return to patrol
            _currentState = AIState.Patrolling;
            MoveToNextPatrolLocation();
            Debug.Log($"{gameObject.name} finished fleeing - resuming patrol");
        }
    }

    // ====================================================================
    // SEARCHING STATE (OPTIONAL ENHANCEMENT)
    // ====================================================================

    /// <summary>
    /// Updates searching behavior - investigates last known player location
    /// Currently just returns to patrol - can be enhanced by students
    /// </summary>
    void UpdateSearching()
    {
        // Future enhancement: search last known player location
        // For now, just return to patrol
        _currentState = AIState.Patrolling;
        MoveToNextPatrolLocation();
    }

    // ====================================================================
    // DEBUG VISUALIZATION
    // ====================================================================

    /// <summary>
    /// Draws debug gizmos in Scene view for AI visualization
    /// Shows detection range, patrol route, and current state
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
            return;

        // Draw detection range sphere
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw lose player range sphere
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, losePlayerRange);

        // Draw patrol route
        if (patrolLocations != null && patrolLocations.Count > 1)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < patrolLocations.Count; i++)
            {
                if (patrolLocations[i] == null)
                    continue;

                // Draw sphere at waypoint
                Gizmos.DrawWireSphere(patrolLocations[i].position, 0.5f);

                // Draw line to next waypoint
                int nextIndex = (i + 1) % patrolLocations.Count;
                if (patrolLocations[nextIndex] != null)
                {
                    Gizmos.DrawLine(patrolLocations[i].position, patrolLocations[nextIndex].position);
                }
            }
        }

        // Draw current destination
        if (_agent != null && _agent.hasPath)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _agent.destination);
            Gizmos.DrawWireSphere(_agent.destination, 0.3f);
        }
    }
}
```

---

### 4.3 Phase 3: Waypoint System

#### 4.3.1 NavDestination.cs

```csharp
// Location: Create new file at Assets/Creator Kit - FPS/Scripts/System/NavDestination.cs
// Purpose: Waypoint component for AI navigation
// Reference: Ferrone pg. 272 (creating patrol locations)

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a navigation destination point for AI agents.
/// Placed as child objects under a patrol route parent GameObject.
///
/// LEARNING POINTS:
/// - GameObject organization for procedural systems
/// - Enum-based behavior variants
/// - Gizmo visualization for level design
///
/// Reference: Ferrone pg. 272 "Select Patrol_Route, click + | Create Empty to
/// add a child GameObject, and name it Location_1"
/// </summary>
public class NavDestination : MonoBehaviour
{
    // ====================================================================
    // DESTINATION TYPE ENUMERATION
    // ====================================================================

    /// <summary>
    /// Defines the purpose/behavior of this destination point
    /// Allows designers to create varied AI patterns
    /// </summary>
    public enum DestinationType
    {
        /// <summary>
        /// Regular patrol point - enemy walks here on patrol route
        /// Most common type - forms basic patrol circuit
        /// </summary>
        Patrol,

        /// <summary>
        /// Ambush point - strategic attack position
        /// Enemy may wait here or use as attack location
        /// </summary>
        Ambush,

        /// <summary>
        /// Cover point - defensive position
        /// Enemy retreats here when wounded (future enhancement)
        /// </summary>
        CoverPoint,

        /// <summary>
        /// Connection point - room-to-room transition
        /// Marks doorways/exits for multi-room navigation
        /// </summary>
        ConnectionPoint
    }

    // ====================================================================
    // PUBLIC FIELDS
    // ====================================================================

    [Header("Destination Settings")]
    [Tooltip("Purpose of this destination point")]
    public DestinationType destinationType = DestinationType.Patrol;

    [Tooltip("Time to wait at this destination (0 = no wait)")]
    [Range(0f, 10f)]
    public float waitTime = 0f;

    [Tooltip("Connected destinations for multi-path routing")]
    public List<NavDestination> connections = new List<NavDestination>();

    [Tooltip("Show destination gizmo in Scene view")]
    public bool showGizmo = true;

    // ====================================================================
    // INITIALIZATION
    // ====================================================================

    /// <summary>
    /// Called when waypoint is instantiated
    /// Hides visual representation (waypoints should be invisible in game)
    /// </summary>
    void Awake()
    {
        // Make waypoint invisible in game
        // Designers see waypoints in Scene view via Gizmos
        // Players never see them

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = false;
        }

        // Optional: disable collider if present
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }
    }

    // ====================================================================
    // GIZMO VISUALIZATION
    // ====================================================================

    /// <summary>
    /// Draws waypoint visualization in Scene view
    /// Color-coded by destination type for easy identification
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showGizmo)
            return;

        // Color-code by destination type
        switch (destinationType)
        {
            case DestinationType.Patrol:
                Gizmos.color = new Color(0f, 1f, 0f, 0.5f); // Green
                break;
            case DestinationType.Ambush:
                Gizmos.color = new Color(1f, 0f, 0f, 0.5f); // Red
                break;
            case DestinationType.CoverPoint:
                Gizmos.color = new Color(0f, 0f, 1f, 0.5f); // Blue
                break;
            case DestinationType.ConnectionPoint:
                Gizmos.color = new Color(1f, 1f, 0f, 0.5f); // Yellow
                break;
        }

        // Draw sphere at waypoint position
        Gizmos.DrawSphere(transform.position, 0.3f);

        // Draw wire sphere for selection area
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        // Draw connections to other waypoints
        Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
        foreach (var connection in connections)
        {
            if (connection != null)
            {
                Gizmos.DrawLine(transform.position, connection.transform.position);
            }
        }
    }

    /// <summary>
    /// Draws label in Scene view showing waypoint type
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (!showGizmo)
            return;

        #if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.5f,
            $"{destinationType}\n{(waitTime > 0 ? $"Wait: {waitTime}s" : "")}",
            new GUIStyle()
            {
                normal = new GUIStyleState() { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            }
        );
        #endif
    }
}
```

#### 4.3.2 Modified LevelRoom.cs

```csharp
// Location: Modify existing file at Assets/Creator Kit - FPS/Scripts/LevelLayout/LevelRoom.cs
// Purpose: Add waypoint system to room prefabs
// Changes: Add patrol waypoint management

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class LevelRoom : MonoBehaviour
{
    // EXISTING CODE...
    public Transform[] Exits;

    [HideInInspector]
    public LevelRoom[] ExitDestination;

    [HideInInspector]
    public LevelLayout Owner;

    // ====================================================================
    // NEW: AI NAVIGATION SYSTEM
    // ====================================================================

    [Header("AI Navigation")]
    [Tooltip("Parent GameObject containing patrol waypoints for this room")]
    public Transform patrolWaypointsParent;

    [Tooltip("Connection points to adjacent rooms (for multi-room AI)")]
    public List<NavDestination> roomConnectionPoints = new List<NavDestination>();

    /// <summary>
    /// Gets all patrol waypoints in this room
    /// Used by TargetSpawner to assign patrol routes to spawned enemies
    ///
    /// USAGE:
    /// LevelRoom room = GetComponent<LevelRoom>();
    /// List<Transform> waypoints = room.GetPatrolWaypoints();
    /// targetAI.patrolRoute = waypoints[0].parent; // Assign parent transform
    /// </summary>
    public List<Transform> GetPatrolWaypoints()
    {
        List<Transform> waypoints = new List<Transform>();

        if (patrolWaypointsParent == null)
        {
            Debug.LogWarning($"Room {name} has no patrol waypoints assigned!");
            return waypoints;
        }

        // Collect all child transforms
        foreach (Transform child in patrolWaypointsParent)
        {
            waypoints.Add(child);
        }

        return waypoints;
    }

    /// <summary>
    /// Gets waypoints of a specific type
    /// Useful for assigning different behaviors to different enemies
    /// </summary>
    public List<NavDestination> GetWaypointsByType(NavDestination.DestinationType type)
    {
        List<NavDestination> result = new List<NavDestination>();

        if (patrolWaypointsParent == null)
            return result;

        foreach (Transform child in patrolWaypointsParent)
        {
            NavDestination dest = child.GetComponent<NavDestination>();
            if (dest != null && dest.destinationType == type)
            {
                result.Add(dest);
            }
        }

        return result;
    }

    // EXISTING CODE...
    public void Placed(LevelLayout layoutOwner)
    {
        Owner = layoutOwner;
        ExitDestination = new LevelRoom[Exits.Length];
    }

#if UNITY_EDITOR
    public void Removed()
    {
        // EXISTING REMOVAL CODE...
        if (ExitDestination != null)
        {
            for (int i = 0; i < ExitDestination.Length; ++i)
            {
                if (ExitDestination[i] != null)
                {
                    SerializedObject otherObj = new SerializedObject(ExitDestination[i]);
                    var connectorProp = otherObj.FindProperty(nameof(ExitDestination));

                    for (int k = 0; k < connectorProp.arraySize; ++k)
                    {
                        var prop = connectorProp.GetArrayElementAtIndex(k);

                        if (prop.objectReferenceValue == this)
                        {
                            prop.objectReferenceValue = null;
                            prop.serializedObject.ApplyModifiedProperties();
                        }
                    }
                }
            }
        }

        if (Owner != null && !Owner.Destroyed)
        {
            SerializedObject ownerObject = new SerializedObject(Owner);
            var piecesProp = ownerObject.FindProperty(nameof(Owner.rooms));

            for (int i = 0; i < piecesProp.arraySize; ++i)
            {
                var prop = piecesProp.GetArrayElementAtIndex(i);

                if (prop.objectReferenceValue == this)
                {
                    piecesProp.DeleteArrayElementAtIndex(i);
                    piecesProp.DeleteArrayElementAtIndex(i);
                    break;
                }
            }

            ownerObject.ApplyModifiedProperties();
        }
    }
#endif
}
```

---

### 4.4 Phase 4: Spawner Integration

#### 4.4.1 Modified TargetSpawner.cs

```csharp
// Location: Modify existing file at Assets/Creator Kit - FPS/Scripts/System/TargetSpawner.cs
// Purpose: Refactor to spawn NavMesh-based AI targets instead of path-following targets
// Changes: Replace PathSystem with patrol route assignment

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Spawns NavMesh-based AI targets with assigned patrol routes
/// Replaces path-following system with NavMesh navigation
///
/// REFACTORING NOTES:
/// - Removed PathSystem dependency
/// - Removed Rigidbody movement
/// - Added patrol route assignment
/// - Maintained spawn timing system
/// </summary>
public class TargetSpawner : MonoBehaviour
{
    // ====================================================================
    // SPAWN EVENT DEFINITION
    // ====================================================================

    /// <summary>
    /// Defines what to spawn, how many, and timing
    /// Modified to support NavMesh-based targets with patrol routes
    /// </summary>
    [System.Serializable]
    public class SpawnEvent
    {
        [Tooltip("Target prefab to spawn (must have TargetAI component)")]
        public GameObject targetToSpawn;

        [Tooltip("Number of targets to spawn")]
        public int count = 1;

        [Tooltip("Time delay between spawning each target")]
        public float timeBetweenSpawn = 2.0f;

        [Tooltip("Patrol route for spawned targets (Transform with waypoint children)")]
        public Transform patrolRoute;
    }

    // ====================================================================
    // PUBLIC FIELDS
    // ====================================================================

    [Tooltip("Array of spawn events defining what and when to spawn")]
    public SpawnEvent[] spawnEvents;

    // ====================================================================
    // SPAWN QUEUE SYSTEM
    // ====================================================================

    /// <summary>
    /// Internal class tracking individual target spawn data
    /// Simplified from original - removed path/rigidbody references
    /// </summary>
    class SpawnQueueElement
    {
        /// <summary>GameObject instance of the spawned target</summary>
        public GameObject obj;

        /// <summary>Target component reference</summary>
        public Target target;

        /// <summary>TargetAI component reference (NEW)</summary>
        public TargetAI targetAI;

        /// <summary>Time remaining until this target should spawn</summary>
        public float remainingTime;
    }

    /// <summary>Queue of targets waiting to be spawned</summary>
    Queue<SpawnQueueElement> m_SpawnQueue;

    /// <summary>List of currently active (spawned) targets</summary>
    List<SpawnQueueElement> m_ActiveElements;

    // ====================================================================
    // INITIALIZATION
    // ====================================================================

    /// <summary>
    /// Called when spawner is created
    /// Instantiates all targets and queues them for delayed spawning
    /// </summary>
    void Awake()
    {
        m_SpawnQueue = new Queue<SpawnQueueElement>();

        // Create all targets from spawn events
        foreach (var spawnEvent in spawnEvents)
        {
            // Validate spawn event
            if (spawnEvent.targetToSpawn == null)
            {
                Debug.LogError($"TargetSpawner on {gameObject.name}: targetToSpawn is null in spawn event!");
                continue;
            }

            // Validate target prefab has required components
            TargetAI prefabAI = spawnEvent.targetToSpawn.GetComponent<TargetAI>();
            if (prefabAI == null)
            {
                Debug.LogError($"TargetSpawner: {spawnEvent.targetToSpawn.name} missing TargetAI component!");
                continue;
            }

            // Instantiate targets for this spawn event
            for (int i = 0; i < spawnEvent.count; ++i)
            {
                // Create target instance
                GameObject targetObj = Instantiate(
                    spawnEvent.targetToSpawn,
                    transform.position,
                    transform.rotation
                );

                // Get component references
                Target targetComponent = targetObj.GetComponentInChildren<Target>();
                TargetAI aiComponent = targetObj.GetComponent<TargetAI>();

                if (targetComponent == null)
                {
                    Debug.LogError($"TargetSpawner: Spawned target missing Target component!");
                    Destroy(targetObj);
                    continue;
                }

                if (aiComponent == null)
                {
                    Debug.LogError($"TargetSpawner: Spawned target missing TargetAI component!");
                    Destroy(targetObj);
                    continue;
                }

                // Assign patrol route
                if (spawnEvent.patrolRoute != null)
                {
                    aiComponent.patrolRoute = spawnEvent.patrolRoute;
                }
                else
                {
                    Debug.LogWarning($"TargetSpawner: No patrol route assigned for {targetObj.name}");
                }

                // Disable target initially (will activate after delay)
                targetObj.SetActive(false);

                // Create spawn queue element
                SpawnQueueElement element = new SpawnQueueElement()
                {
                    obj = targetObj,
                    target = targetComponent,
                    targetAI = aiComponent,
                    remainingTime = i * spawnEvent.timeBetweenSpawn
                };

                // Add to spawn queue
                m_SpawnQueue.Enqueue(element);
            }
        }

        // Check if we have anything to spawn
        if (m_SpawnQueue.Count == 0)
        {
            Debug.LogWarning($"TargetSpawner on {gameObject.name}: No targets queued for spawning!");
            Destroy(gameObject);
        }
        else
        {
            m_ActiveElements = new List<SpawnQueueElement>();
            Debug.Log($"TargetSpawner: Queued {m_SpawnQueue.Count} targets for spawning");
        }
    }

    // ====================================================================
    // UPDATE - SPAWN TIMING
    // ====================================================================

    /// <summary>
    /// Called every frame
    /// Manages spawn timing - activates targets when their delay expires
    /// </summary>
    void Update()
    {
        // Check if there are targets waiting to spawn
        if (m_SpawnQueue.Count > 0)
        {
            // Get next target in queue (without removing it yet)
            var element = m_SpawnQueue.Peek();

            // Decrement spawn timer
            element.remainingTime -= Time.deltaTime;

            // Time to spawn this target?
            if (element.remainingTime <= 0)
            {
                // Remove from queue and activate
                m_SpawnQueue.Dequeue();
                element.obj.SetActive(true);
                m_ActiveElements.Add(element);

                Debug.Log($"TargetSpawner: Spawned {element.obj.name}");
            }
        }

        // Optional: Remove destroyed targets from active list
        for (int i = m_ActiveElements.Count - 1; i >= 0; i--)
        {
            if (m_ActiveElements[i].target.Destroyed)
            {
                m_ActiveElements.RemoveAt(i);
            }
        }

        // Optional: Destroy spawner when all targets are destroyed
        if (m_SpawnQueue.Count == 0 && m_ActiveElements.Count == 0)
        {
            Debug.Log($"TargetSpawner: All targets destroyed - removing spawner");
            Destroy(gameObject);
        }
    }
}

// ====================================================================
// CUSTOM INSPECTOR (OPTIONAL)
// ====================================================================

#if UNITY_EDITOR
[CustomEditor(typeof(TargetSpawner))]
public class TargetSpawnerEditor : Editor
{
    TargetSpawner m_TargetSpawner;

    void OnEnable()
    {
        m_TargetSpawner = target as TargetSpawner;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("NavMesh Spawner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This spawner creates NavMesh-based AI targets.\n" +
            "Ensure each target prefab has:\n" +
            "- Target component\n" +
            "- TargetAI component\n" +
            "- NavMeshAgent component",
            MessageType.Info
        );

        // Validation button
        if (GUILayout.Button("Validate Spawn Events"))
        {
            ValidateSpawnEvents();
        }
    }

    void ValidateSpawnEvents()
    {
        bool allValid = true;

        foreach (var spawnEvent in m_TargetSpawner.spawnEvents)
        {
            if (spawnEvent.targetToSpawn == null)
            {
                Debug.LogError("Spawn event has null targetToSpawn!");
                allValid = false;
                continue;
            }

            Target target = spawnEvent.targetToSpawn.GetComponentInChildren<Target>();
            TargetAI ai = spawnEvent.targetToSpawn.GetComponent<TargetAI>();
            UnityEngine.AI.NavMeshAgent agent = spawnEvent.targetToSpawn.GetComponent<UnityEngine.AI.NavMeshAgent>();

            if (target == null)
            {
                Debug.LogError($"{spawnEvent.targetToSpawn.name} missing Target component!");
                allValid = false;
            }

            if (ai == null)
            {
                Debug.LogError($"{spawnEvent.targetToSpawn.name} missing TargetAI component!");
                allValid = false;
            }

            if (agent == null)
            {
                Debug.LogError($"{spawnEvent.targetToSpawn.name} missing NavMeshAgent component!");
                allValid = false;
            }

            if (spawnEvent.patrolRoute == null)
            {
                Debug.LogWarning($"Spawn event for {spawnEvent.targetToSpawn.name} has no patrol route assigned!");
            }
        }

        if (allValid)
        {
            Debug.Log("All spawn events are valid!");
        }
    }
}
#endif
```

---

### 4.5 Phase 5: Room Connectivity

#### 4.5.1 NavMeshRoomConnector.cs

```csharp
// Location: Create new file at Assets/Creator Kit - FPS/Scripts/System/NavMeshRoomConnector.cs
// Purpose: Creates NavMeshLinks between modular rooms for multi-room navigation
// Handles the challenge of Creator Kit's room-based level construction

using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Creates NavMeshLinks between connected rooms for seamless navigation
/// Solves the challenge of Creator Kit's modular room system
///
/// PROBLEM:
/// Creator Kit levels are built from separate room prefabs.
/// Each room has its own NavMeshSurface.
/// NavMeshAgents cannot cross between disconnected NavMeshes.
///
/// SOLUTION:
/// NavMeshLink components bridge gaps between room NavMeshes.
/// This script automatically creates links at room exit points.
///
/// LEARNING POINT:
/// NavMeshLink allows agents to traverse disconnected NavMesh surfaces,
/// enabling navigation across doorways, gaps, and separate room meshes.
/// </summary>
public class NavMeshRoomConnector : MonoBehaviour
{
    // ====================================================================
    // REFERENCES
    // ====================================================================

    [Header("References")]
    [Tooltip("Reference to LevelLayout managing room assembly")]
    private LevelLayout levelLayout;

    [Header("Link Settings")]
    [Tooltip("Width of doorway links (should match doorway size)")]
    [Range(1f, 5f)]
    public float linkWidth = 2f;

    [Tooltip("Cost modifier for links (higher = agents avoid)")]
    [Range(-10f, 10f)]
    public float costModifier = 1f;

    [Tooltip("Are links bidirectional (both ways)?")]
    public bool bidirectional = true;

    [Tooltip("Should links be created automatically on Start?")]
    public bool autoConnect = true;

    [Header("Debug")]
    [Tooltip("Log link creation details")]
    public bool debugMode = false;

    /// <summary>List of created links for cleanup</summary>
    private List<NavMeshLink> createdLinks = new List<NavMeshLink>();

    // ====================================================================
    // INITIALIZATION
    // ====================================================================

    /// <summary>
    /// Called when component is created
    /// Gets reference to LevelLayout and optionally auto-connects rooms
    /// </summary>
    void Start()
    {
        // Get LevelLayout component
        levelLayout = GetComponent<LevelLayout>();

        if (levelLayout == null)
        {
            Debug.LogError("NavMeshRoomConnector requires LevelLayout component on same GameObject!");
            enabled = false;
            return;
        }

        // Auto-connect if enabled
        if (autoConnect)
        {
            // Delay connection to ensure NavMeshes are baked
            StartCoroutine(DelayedConnection());
        }
    }

    /// <summary>
    /// Delays connection to ensure NavMeshes are fully baked
    /// </summary>
    System.Collections.IEnumerator DelayedConnection()
    {
        // Wait one frame for NavMeshSurfaces to complete baking
        yield return null;

        ConnectRoomNavMeshes();
    }

    // ====================================================================
    // CONNECTION SYSTEM
    // ====================================================================

    /// <summary>
    /// Creates NavMeshLinks between all connected rooms
    /// Call this after level assembly and NavMesh baking are complete
    /// </summary>
    public void ConnectRoomNavMeshes()
    {
        if (levelLayout == null || levelLayout.rooms == null)
        {
            Debug.LogError("NavMeshRoomConnector: LevelLayout or rooms is null!");
            return;
        }

        Debug.Log("NavMeshRoomConnector: Creating links between rooms...");

        int linksCreated = 0;

        // Iterate through all rooms
        foreach (var room in levelLayout.rooms)
        {
            if (room == null)
                continue;

            // Check each exit of this room
            for (int i = 0; i < room.Exits.Length; i++)
            {
                // Check if this exit is connected to another room
                if (room.ExitDestination[i] != null)
                {
                    Transform exitA = room.Exits[i];
                    LevelRoom connectedRoom = room.ExitDestination[i];

                    // Find the corresponding exit in the connected room
                    for (int j = 0; j < connectedRoom.Exits.Length; j++)
                    {
                        if (connectedRoom.ExitDestination[j] == room)
                        {
                            Transform exitB = connectedRoom.Exits[j];

                            // Create link between these exits
                            if (CreateRoomLink(exitA, exitB, room.name, connectedRoom.name))
                            {
                                linksCreated++;
                            }

                            break; // Found matching exit
                        }
                    }
                }
            }
        }

        Debug.Log($"NavMeshRoomConnector: Created {linksCreated} NavMeshLinks");
    }

    /// <summary>
    /// Creates a NavMeshLink between two room exits
    /// </summary>
    /// <param name="exitA">First room's exit transform</param>
    /// <param name="exitB">Second room's exit transform</param>
    /// <param name="roomAName">First room's name (for debug)</param>
    /// <param name="roomBName">Second room's name (for debug)</param>
    /// <returns>True if link was created successfully</returns>
    bool CreateRoomLink(Transform exitA, Transform exitB, string roomAName, string roomBName)
    {
        // Check if exits are close enough (should be touching)
        float distance = Vector3.Distance(exitA.position, exitB.position);

        if (distance > 5f)
        {
            if (debugMode)
                Debug.LogWarning($"Exits too far apart ({distance}m): {roomAName} â†’ {roomBName}");
            return false;
        }

        // Create GameObject for NavMeshLink
        GameObject linkObj = new GameObject($"NavLink_{roomAName}_to_{roomBName}");
        linkObj.transform.SetParent(transform);
        linkObj.transform.position = exitA.position;

        // Add NavMeshLink component
        NavMeshLink link = linkObj.AddComponent<NavMeshLink>();

        // Configure link
        link.startPoint = Vector3.zero; // Local position (at exitA)
        link.endPoint = exitB.position - exitA.position; // Local offset to exitB
        link.width = linkWidth;
        link.costModifier = costModifier;
        link.bidirectional = bidirectional;
        link.autoUpdatePositions = false; // Static link
        link.area = 0; // Walkable area

        // Store reference for cleanup
        createdLinks.Add(link);

        if (debugMode)
        {
            Debug.Log($"Created NavMeshLink: {roomAName} â†’ {roomBName} " +
                     $"(distance: {distance:F2}m)");
        }

        return true;
    }

    // ====================================================================
    // CLEANUP
    // ====================================================================

    /// <summary>
    /// Removes all created NavMeshLinks
    /// Useful for dynamic level changes
    /// </summary>
    public void DisconnectAllLinks()
    {
        foreach (var link in createdLinks)
        {
            if (link != null)
            {
                Destroy(link.gameObject);
            }
        }

        createdLinks.Clear();
        Debug.Log("NavMeshRoomConnector: Removed all links");
    }

    /// <summary>
    /// Clean up on destroy
    /// </summary>
    void OnDestroy()
    {
        DisconnectAllLinks();
    }

    // ====================================================================
    // DEBUG VISUALIZATION
    // ====================================================================

    /// <summary>
    /// Draws link visualization in Scene view
    /// </summary>
    void OnDrawGizmos()
    {
        if (!debugMode || createdLinks == null)
            return;

        Gizmos.color = Color.cyan;

        foreach (var link in createdLinks)
        {
            if (link == null)
                continue;

            Vector3 start = link.transform.position + link.startPoint;
            Vector3 end = link.transform.position + link.endPoint;

            // Draw line
            Gizmos.DrawLine(start, end);

            // Draw spheres at endpoints
            Gizmos.DrawWireSphere(start, 0.3f);
            Gizmos.DrawWireSphere(end, 0.3f);
        }
    }
}
```

---

## 5. Fleeing Behavior Implementation

### 5.1 Modified Target.cs with Fleeing Hook

```csharp
// Location: Modify existing file at Assets/Creator Kit - FPS/Scripts/System/Target.cs
// Purpose: Add fleeing behavior trigger when wounded but not destroyed
// Changes: Modify Got() method to call TargetAI.StartFleeing()

using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public class Target : MonoBehaviour
{
    // EXISTING CODE...
    public float health = 5.0f;
    public int pointValue;
    public ParticleSystem DestroyedEffect;

    [Header("Audio")]
    public RandomPlayer HitPlayer;
    public AudioSource IdleSource;

    public bool Destroyed => m_Destroyed;

    bool m_Destroyed = false;
    float m_CurrentHealth;

    // ====================================================================
    // NEW: FLEEING BEHAVIOR TOGGLE
    // ====================================================================

    [Header("Flee Behavior")]
    [Tooltip("Should this target flee when wounded but not destroyed?")]
    public bool canFlee = true;

    // ====================================================================
    // EXISTING AWAKE & START
    // ====================================================================

    void Awake()
    {
        Helpers.RecursiveLayerChange(transform, LayerMask.NameToLayer("Target"));
    }

    void Start()
    {
        if (DestroyedEffect)
            PoolSystem.Instance.InitPool(DestroyedEffect, 16);

        m_CurrentHealth = health;

        if (IdleSource != null)
            IdleSource.time = Random.Range(0.0f, IdleSource.clip.length);
    }

    // ====================================================================
    // MODIFIED: GOT() METHOD - ADDS FLEEING TRIGGER
    // ====================================================================

    /// <summary>
    /// Called when target takes damage from weapon
    /// MODIFIED: Triggers fleeing behavior when wounded but alive
    ///
    /// CRITICAL CALLBACK:
    /// This method is called by Weapon.cs line ~1665:
    /// target.Got(damage);
    ///
    /// FLEEING INTEGRATION:
    /// When health > 0 after damage, triggers TargetAI.StartFleeing()
    /// This creates reactive behavior: enemy flees when wounded
    ///
    /// Reference: Ferrone pg. 285-289 (detecting bullet collisions)
    /// </summary>
    public void Got(float damage)
    {
        // Subtract damage from current health
        m_CurrentHealth -= damage;

        // Play hit audio feedback
        if (HitPlayer != null)
            HitPlayer.PlayRandom();

        // ================================================================
        // FLEEING BEHAVIOR HOOK
        // ================================================================
        // THIS IS THE KEY MODIFICATION FOR FLEEING BEHAVIOR

        // If target still has health remaining (wounded but alive)
        if (m_CurrentHealth > 0)
        {
            // Trigger fleeing behavior if enabled
            if (canFlee)
            {
                // Get TargetAI component
                TargetAI targetAI = GetComponent<TargetAI>();

                if (targetAI != null)
                {
                    // Call fleeing method
                    targetAI.StartFleeing();

                    if (Debug.isDebugBuild)
                    {
                        Debug.Log($"{gameObject.name} wounded! Health: {m_CurrentHealth}/{health} - Fleeing!");
                    }
                }
                else if (Debug.isDebugBuild)
                {
                    Debug.LogWarning($"{gameObject.name} set to flee but has no TargetAI component!");
                }
            }

            return; // Exit method - target survived
        }

        // ================================================================
        // TARGET DESTROYED (health <= 0)
        // ================================================================
        // EXISTING DESTRUCTION CODE UNCHANGED

        Vector3 position = transform.position;

        // Play destruction audio
        if (HitPlayer != null)
        {
            var source = WorldAudioPool.GetWorldSFXSource();
            source.transform.position = position;
            source.pitch = HitPlayer.source.pitch;
            source.PlayOneShot(HitPlayer.GetRandomClip());
        }

        // Play destruction particle effect
        if (DestroyedEffect != null)
        {
            var effect = PoolSystem.Instance.GetInstance<ParticleSystem>(DestroyedEffect);
            effect.time = 0.0f;
            effect.Play();
            effect.transform.position = position;
        }

        // Mark destroyed and deactivate
        m_Destroyed = true;
        gameObject.SetActive(false);

        // Notify game system
        GameSystem.Instance.TargetDestroyed(pointValue);
    }
}
```

### 5.2 Simple Flee Implementation (No NavMesh)

```csharp
// Location: Create new file at Assets/Creator Kit - FPS/Scripts/System/SimpleTargetFlee.cs
// Purpose: Basic fleeing behavior without NavMesh (for students not using NavMesh)
// Learning Point: Vector math and direction calculation

using UnityEngine;

/// <summary>
/// Simple fleeing behavior using Transform movement (no NavMesh required)
/// Demonstrates vector mathematics for direction calculation
///
/// LEARNING OBJECTIVES:
/// - Vector subtraction for direction
/// - Vector normalization
/// - Time-based movement
/// - State management with timers
///
/// Reference: Unity-3d-Math-Explained.md
/// "Vector3 direction calculation, Time.deltaTime for frame-rate independence"
/// </summary>
public class SimpleTargetFlee : MonoBehaviour
{
    // ====================================================================
    // PUBLIC SETTINGS
    // ====================================================================

    [Header("Flee Settings")]
    [Tooltip("Speed when fleeing (units per second)")]
    [Range(3f, 10f)]
    public float fleeSpeed = 6f;

    [Tooltip("How long to flee (seconds)")]
    [Range(1f, 5f)]
    public float fleeDuration = 3f;

    [Tooltip("Should target return to patrol after fleeing?")]
    public bool returnToPatrol = true;

    // ====================================================================
    // PRIVATE STATE
    // ====================================================================

    /// <summary>Is target currently fleeing?</summary>
    private bool isFleeing = false;

    /// <summary>Time remaining in flee state</summary>
    private float fleeTimer = 0f;

    /// <summary>Direction to flee (away from player)</summary>
    private Vector3 fleeDirection;

    /// <summary>Original position (for returning)</summary>
    private Vector3 originalPosition;

    // ====================================================================
    // INITIALIZATION
    // ====================================================================

    void Start()
    {
        // Store original position
        originalPosition = transform.position;
    }

    // ====================================================================
    // UPDATE - FLEE MOVEMENT
    // ====================================================================

    /// <summary>
    /// Called every frame
    /// Moves target in flee direction while timer is active
    /// </summary>
    void Update()
    {
        if (isFleeing)
        {
            // Move in flee direction
            // Movement formula: position += direction * speed * deltaTime
            // deltaTime makes movement frame-rate independent
            transform.position += fleeDirection * fleeSpeed * Time.deltaTime;

            // Decrement timer
            fleeTimer -= Time.deltaTime;

            // Check if flee duration expired
            if (fleeTimer <= 0)
            {
                isFleeing = false;

                // Optional: Return to original position
                if (returnToPatrol)
                {
                    // This is a simple implementation
                    // Real implementation would resume patrol route
                    Debug.Log($"{gameObject.name} finished fleeing");
                }
            }
        }
    }

    // ====================================================================
    // PUBLIC METHODS
    // ====================================================================

    /// <summary>
    /// Starts fleeing behavior
    /// Called by Target.Got() when wounded
    ///
    /// VECTOR MATHEMATICS:
    /// 1. Calculate vector from player to target (toPlayer)
    /// 2. Negate it to get opposite direction (fleeDirection)
    /// 3. Normalize to unit vector (length = 1)
    /// 4. Scale by speed for movement
    ///
    /// Reference: Unity-3d-Math-Explained.md
    /// "Vector subtraction gives direction from B to A"
    /// </summary>
    public void StartFleeing()
    {
        // Get player reference
        if (Controller.Instance == null)
        {
            Debug.LogWarning($"{gameObject.name}: Cannot flee - no player reference");
            return;
        }

        // Calculate direction from player to target
        // Vector subtraction: (target - player) = direction from player to target
        Vector3 toPlayer = transform.position - Controller.Instance.transform.position;

        // Negate to get direction away from player
        fleeDirection = -toPlayer.normalized;

        // Keep movement on horizontal plane (no flying away)
        fleeDirection.y = 0;

        // Re-normalize after Y removal
        fleeDirection.Normalize();

        // Start fleeing
        isFleeing = true;
        fleeTimer = fleeDuration;

        Debug.Log($"{gameObject.name} fleeing away from player!");
    }

    /// <summary>
    /// Stops fleeing behavior immediately
    /// </summary>
    public void StopFleeing()
    {
        isFleeing = false;
        fleeTimer = 0f;
    }

    // ====================================================================
    // DEBUG VISUALIZATION
    // ====================================================================

    void OnDrawGizmosSelected()
    {
        if (isFleeing)
        {
            // Draw flee direction
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, fleeDirection * 5f);
        }
    }
}
```

---

## 6. Integration & Testing Guide

### 6.1 Setup Checklist

#### Phase 1: NavMesh Foundation

- [ ] Install AI Navigation Package (com.unity.ai.navigation)
- [ ] Add LevelRoomNavMesh.cs to project
- [ ] Add LevelRoomNavMesh component to each room prefab
- [ ] Modify LevelLayout.cs with BakeCompleteNavMesh()
- [ ] Select LevelLayout GameObject in scene
- [ ] Click "Bake All Room NavMeshes" button
- [ ] Verify blue NavMesh overlay appears on floors

#### Phase 2: TargetAI Component

- [ ] Create TargetAI.cs script
- [ ] Add to Target prefab:
  - [ ] NavMeshAgent component
  - [ ] TargetAI component
- [ ] Configure NavMeshAgent in prefab:
  - [ ] Speed: 3.5
  - [ ] Angular Speed: 120
  - [ ] Acceleration: 8
  - [ ] Stopping Distance: 0.5
- [ ] Create patrol route GameObject with waypoint children
- [ ] Assign patrol route to TargetAI in Inspector

#### Phase 3: Waypoint System

- [ ] Create NavDestination.cs script
- [ ] Create NavDestination prefab (empty GameObject with component)
- [ ] Place 4-6 waypoints in each room type
- [ ] Parent waypoints under "PatrolRoute" GameObject
- [ ] Modify LevelRoom.cs to reference waypoints

#### Phase 4: Spawner Integration

- [ ] Backup original TargetSpawner.cs
- [ ] Replace with new NavMesh version
- [ ] Update TargetSpawner references in scene:
  - [ ] Assign target prefabs (with TargetAI)
  - [ ] Assign patrol routes
  - [ ] Set spawn counts and timings
- [ ] Test spawning

#### Phase 5: Room Connectivity

- [ ] Create NavMeshRoomConnector.cs
- [ ] Add to LevelLayout GameObject
- [ ] Configure settings:
  - [ ] Link Width: 2.0
  - [ ] Auto Connect: true
  - [ ] Debug Mode: true
- [ ] Play scene and verify links created in console

#### Phase 6: Fleeing Behavior

- [ ] Modify Target.cs with fleeing hook
- [ ] Verify TargetAI has StartFleeing() method
- [ ] Set canFlee = true in Target Inspector
- [ ] Test: Shoot target without killing it
- [ ] Verify target flees away from player

### 6.2 Testing Procedures

#### Test 1: Basic Navigation

1. Place Target with TargetAI in scene
2. Assign patrol route with 4 waypoints
3. Press Play
4. **Expected**: Target walks between waypoints in loop

#### Test 2: Player Detection

1. Setup from Test 1
2. Move player close to target (within 15 units)
3. **Expected**: Target stops patrol and chases player
4. Move player far away (>25 units)
5. **Expected**: Target returns to patrol

#### Test 3: Fleeing Behavior

1. Setup from Test 2
2. Shoot target once (don't kill)
3. **Expected**: Target flees away from player for 5 seconds
4. After 5 seconds: Target returns to patrol

#### Test 4: Multi-Room Navigation

1. Create level with 2 connected rooms
2. Place patrol routes spanning both rooms
3. Assign target with multi-room patrol
4. **Expected**: Target walks through doorway between rooms

#### Test 5: Complete Gameplay Loop

1. Full level with multiple targets
2. Targets patrol, detect, chase, and flee
3. **Expected**: All behaviors work together seamlessly

### 6.3 Common Issues & Solutions

#### Issue: NavMesh Not Visible

**Symptom**: No blue overlay on floors  
**Solution**:

- Ensure NavMeshSurface component has "collectObjects = Children"
- Check that floor GameObjects have MeshRenderer
- Verify "Bake All Room NavMeshes" was clicked
- Check Console for baking errors

#### Issue: Target Doesn't Move

**Symptom**: Target stands still, doesn't patrol  
**Solution**:

- Verify NavMeshAgent component exists
- Check agent is enabled (not disabled in Inspector)
- Ensure patrol route is assigned in TargetAI
- Verify waypoints are children of patrol route parent
- Check Console for "No patrol locations" warning

#### Issue: Target Stuck at Doorway

**Symptom**: Target reaches doorway, stops, doesn't enter next room  
**Solution**:

- Verify NavMeshLinks exist at doorways (check hierarchy)
- Increase NavMeshAgent radius (try 0.3 instead of 0.5)
- Widen doorways in level geometry
- Check doorways have clear NavMesh (no obstacles)

#### Issue: Target Doesn't Detect Player

**Symptom**: Player walks past target, no chase  
**Solution**:

- Verify Controller.Instance is not null (check Console)
- Increase detectionRange in TargetAI Inspector
- Ensure \_playerTransform is assigned (check Start() code)
- Check playerCheckInterval isn't too long

#### Issue: Fleeing Doesn't Work

**Symptom**: Target takes damage, doesn't flee  
**Solution**:

- Verify canFlee = true in Target Inspector
- Check TargetAI component exists on same GameObject
- Ensure health > 0 after damage (not one-shot kill)
- Look for "wounded!" debug message in Console
- Verify StartFleeing() method exists in TargetAI

#### Issue: Performance Problems

**Symptom**: Low framerate with many targets  
**Solution**:

- Reduce playerCheckInterval (0.5 â†’ 1.0 seconds)
- Lower number of spawned targets
- Use object pooling for targets (advanced)
- Reduce NavMesh precision (increase voxel size)

---

## 7. Learning Outcomes & Book References

### 7.1 Chapter 9 Mapping

| Ferrone Section              | Pages   | Implementation                                  | Learning Outcome                      |
| ---------------------------- | ------- | ----------------------------------------------- | ------------------------------------- |
| Navigating 3D Space          | 266     | NavMesh system                                  | Understanding navigation fundamentals |
| Navigation Components        | 266-267 | NavMeshSurface, Agent, Obstacle                 | Component roles and relationships     |
| AI Navigation Package        | 267-268 | Package installation                            | Unity package system                  |
| Setting Up NavMeshSurface    | 268-270 | LevelRoomNavMesh.cs                             | Baking navigation data                |
| Setting Up Enemy Agents      | 271-273 | TargetAI component setup                        | NavMeshAgent configuration            |
| Procedural Programming       | 274     | Patrol initialization                           | Iterating collections                 |
| Referencing Patrol Locations | 274-277 | InitializePatrolRoute()                         | Procedural waypoint collection        |
| Moving Enemy Agents          | 278-279 | MoveToNextPatrolLocation()                      | Setting agent destinations            |
| Update Loop Logic            | 280-282 | Update() state machine                          | Frame-by-frame AI updates             |
| Seek and Destroy             | 283-284 | CheckForPlayer()                                | Dynamic destination changes           |
| Lowering Player Health       | 285-286 | (Not implemented - FPS Kit handles differently) | Collision detection concepts          |
| Detecting Bullet Collisions  | 286-289 | Target.Got() callback                           | Event-driven programming              |
| Refactoring & DRY            | 291-293 | Method extraction                               | Code quality principles               |

### 7.2 Additional Learning Resources

#### Vector Mathematics (Unity-3d-Math-Explained.md)

- **Vector3.Distance()**: Player detection ranges
- **Vector3.MoveTowards()**: Alternative movement approach
- **Vector3 subtraction**: Flee direction calculation
- **Vector3.normalized**: Unit direction vectors

#### Unity Navigation Documentation

- **NavMeshSurface**: Baking walkable surfaces
- **NavMeshAgent**: Autonomous navigation
- **NavMeshLink**: Connecting disconnected surfaces
- **NavMeshObstacle**: Dynamic obstacle avoidance

### 7.3 Extension Exercises for Students

#### Beginner Extensions

1. **Variable Speed**: Make targets run faster when chasing
2. **Health Display**: Add health bar above targets
3. **Multiple Patrol Routes**: Targets switch routes randomly
4. **Sound Effects**: Add footstep sounds to moving targets

#### Intermediate Extensions

1. **Line of Sight**: Target only detects player if visible
2. **Alert System**: Detected target alerts nearby targets
3. **Cover System**: Targets seek cover when wounded
4. **Attack Patterns**: Different target types with varied behaviors

#### Advanced Extensions

1. **Dynamic Waypoints**: Procedurally generated patrol routes
2. **Squad Behavior**: Groups of targets coordinate
3. **Learning AI**: Targets remember player patterns
4. **Navigation Optimization**: Performance profiling and improvement

---

## 8. Troubleshooting & Common Pitfalls

### 8.1 Compilation Errors

#### Missing Namespace Error

```
Error CS0246: The type or namespace name 'NavMeshAgent' could not be found
```

**Solution**: Add `using UnityEngine.AI;` to top of script

#### Package Not Installed

```
Error: Unity.AI.Navigation package not found
```

**Solution**: Install package via Package Manager (com.unity.ai.navigation)

#### Missing Component

```
NullReferenceException: Object reference not set to an instance of an object
TargetAI.Start() (at Assets/Scripts/TargetAI.cs:XX)
```

**Solution**: Ensure NavMeshAgent component exists on GameObject

### 8.2 Runtime Warnings

#### No Patrol Locations

```
Warning: TargetAI on Target(Clone): No patrol locations found!
```

**Solution**: Assign patrolRoute in Inspector, ensure it has child GameObjects

#### Player Reference Null

```
Warning: TargetAI on Target(Clone): Controller.Instance is null!
```

**Solution**: Ensure Controller exists in scene with Instance set in Awake()

#### NavMesh Not Baked

```
Warning: Agent is not on a NavMesh
```

**Solution**: Click "Bake All Room NavMeshes" button in LevelLayout Inspector

### 8.3 Behavior Issues

#### Target Walks Through Walls

**Symptom**: Target ignores walls, walks through geometry  
**Root Cause**: NavMesh baked incorrectly or agent radius too small  
**Solution**:

1. Rebake NavMesh
2. Increase NavMeshAgent radius to 0.5
3. Ensure walls have colliders and are included in NavMesh baking

#### Target Spins in Place

**Symptom**: Target rotates rapidly but doesn't move  
**Root Cause**: Destination too close or NavMesh discontinuous  
**Solution**:

1. Increase destinationThreshold to 1.0
2. Check NavMesh has continuous path
3. Verify NavMeshAgent stoppingDistance setting

#### Flee Goes Wrong Direction

**Symptom**: Target flees toward player instead of away  
**Root Cause**: Vector math error in flee direction calculation  
**Solution**:

1. Verify direction formula: `(transform.position - player.position).normalized`
2. Check sign: should be positive (target - player), not negative
3. Add debug gizmo to visualize flee direction

### 8.4 Performance Issues

#### Frame Drops with Many Targets

**Symptom**: FPS drops below 30 with 10+ targets  
**Solutions**:

1. Increase playerCheckInterval (0.5 â†’ 1.0 seconds)
2. Reduce NavMesh precision in baking settings
3. Use NavMeshAgent avoidance priority system
4. Implement target culling (disable distant targets)

#### NavMesh Baking Too Slow

**Symptom**: Baking takes >10 seconds per room  
**Solutions**:

1. Increase voxel size (lower precision, faster baking)
2. Reduce room geometry complexity
3. Use fewer rooms or bake only modified rooms

---

## 9. Recommended Project Knowledge Files

These files are referenced throughout the implementation but not yet in project knowledge. Adding them would provide complete context for future conversations:

### 9.1 Core System Files (High Priority)

#### Helpers.cs

- **Path**: `Assets/Creator Kit - FPS/Scripts/Helpers.cs`
- **Why Needed**: Contains utility functions like `RecursiveLayerChange()` used in Target.cs
- **Functions**: Layer manipulation, common transforms, utility methods

#### PoolSystem.cs

- **Path**: `Assets/Creator Kit - FPS/Scripts/System/PoolSystem.cs`
- **Why Needed**: Object pooling system used by Target, Weapon, and effects
- **Functions**: `InitPool()`, `GetInstance<T>()`, object reuse management

#### RandomPlayer.cs

- **Path**: `Assets/Creator Kit - FPS/Scripts/RandomPlayer.cs`
- **Why Needed**: Plays random audio clips, used extensively in Target
- **Functions**: `PlayRandom()`, `GetRandomClip()`, audio management

#### WorldAudioPool.cs

- **Path**: `Assets/Creator Kit - FPS/Scripts/WorldAudioPool.cs`
- **Why Needed**: Global audio source pooling for 3D sound effects
- **Functions**: `GetWorldSFXSource()`, audio source management

### 9.2 Game State Files (Medium Priority)

#### GameDatabase.cs

- **Path**: `Assets/Creator Kit - FPS/Scripts/GameDatabase.cs`
- **Why Needed**: ScriptableObject storing episode/level data referenced by GameSystem
- **Functions**: Level progression, scene management

#### PauseMenu.cs

- **Path**: `Assets/Creator Kit - FPS/Scripts/UI/PauseMenu.cs`
- **Why Needed**: Singleton referenced by Controller, manages pause state
- **Functions**: `Display()`, pause/resume logic

#### GameSystemInfo.cs

- **Path**: `Assets/Creator Kit - FPS/Scripts/UI/GameSystemInfo.cs`
- **Why Needed**: HUD display for timer/score, called by GameSystem
- **Functions**: `UpdateScore()`, `UpdateTimer()`, UI updates

### 9.3 Visual Effects Files (Medium Priority)

#### ImpactManager.cs

- **Path**: `Assets/Creator Kit - FPS/Scripts/ImpactManager.cs`
- **Why Needed**: Manages bullet hit effects called by Weapon
- **Functions**: `PlayImpact()`, material-based effect selection

#### CameraShaker.cs

- **Path**: `Assets/Creator Kit - FPS/Scripts/CameraShaker.cs`
- **Why Needed**: Screen shake effects on weapon fire
- **Functions**: `Shake()`, camera shake management

### 9.4 UI System Files (Lower Priority)

#### WeaponInfoUI.cs

- **Path**: `Assets/Creator Kit - FPS/Scripts/UI/WeaponInfoUI.cs`
- **Why Needed**: Weapon HUD display, called by Controller
- **Functions**: `UpdateAmmoAmount()`, weapon info display

#### MinimapUI.cs & FullscreenMap.cs

- **Path**: `Assets/Creator Kit - FPS/Scripts/UI/MinimapUI.cs`
- **Path**: `Assets/Creator Kit - FPS/Scripts/UI/FullscreenMap.cs`
- **Why Needed**: Map rendering called by GameSystem
- **Functions**: `UpdateForPlayerTransform()`, map rendering

#### FinalScoreUI.cs

- **Path**: `Assets/Creator Kit - FPS/Scripts/UI/FinalScoreUI.cs`
- **Why Needed**: End-of-level results screen
- **Functions**: Score display, time calculation display

#### LevelSelectionUI.cs

- **Path**: `Assets/Creator Kit - FPS/Scripts/UI/LevelSelectionUI.cs`
- **Why Needed**: Level/episode selection menu
- **Functions**: Episode navigation, level loading

### 9.5 Additional System Files (Lower Priority)

#### Projectile.cs

- **Path**: `Assets/Creator Kit - FPS/Scripts/Projectile.cs`
- **Why Needed**: Physical projectile implementation for ProjectileShot weapons
- **Functions**: Projectile movement, collision, pooling

#### StartCheckpoint.cs & EndCheckpoint.cs

- **Path**: `Assets/Creator Kit - FPS/Scripts/StartCheckpoint.cs`
- **Path**: `Assets/Creator Kit - FPS/Scripts/EndCheckpoint.cs`
- **Why Needed**: Level start/end triggers that interact with GameSystem
- **Functions**: Timer start/stop, level completion

### 9.6 Recommended Addition Order

**First Conversation with Opus:**

1. Helpers.cs
2. PoolSystem.cs
3. RandomPlayer.cs
4. WorldAudioPool.cs

**Second Batch:** 5. ImpactManager.cs 6. CameraShaker.cs 7. GameDatabase.cs 8. PauseMenu.cs

**Third Batch:** 9. GameSystemInfo.cs 10. WeaponInfoUI.cs 11. FinalScoreUI.cs

**Fourth Batch (If Needed):** 12. MinimapUI.cs 13. FullscreenMap.cs 14. Projectile.cs 15. StartCheckpoint.cs 16. EndCheckpoint.cs

---

## Document Summary

This comprehensive guide provides:

âœ… **Complete understanding** of Creator Kit FPS architecture  
âœ… **Fully implemented** NavMesh AI system with all code  
âœ… **Three fleeing behavior** approaches (NavMesh, Simple, Event-based)  
âœ… **Step-by-step integration** with testing procedures  
âœ… **Direct mapping** to Ferrone Chapter 9 learning objectives  
âœ… **Troubleshooting guide** for common issues  
âœ… **Recommended files** for complete project knowledge

**Next Steps for Opus Conversation:**

1. Add recommended files to project knowledge (Section 9)
2. Begin implementation with Phase 1 (NavMesh Foundation)
3. Use this document as complete reference throughout development
4. Refer to specific script sections for debugging

**Total Implementation Time**: 5-7 hours for complete system  
**Student Learning Time**: 10-15 hours with exercises and extensions

---

**End of Document**
