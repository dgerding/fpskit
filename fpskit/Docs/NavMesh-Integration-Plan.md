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
    ↓
Weapon.RaycastShot() performs raycast
    ↓
Raycast hits Target (Layer 10)
    ↓
Weapon calls: target.Got(damage)
    ↓
Target.Got() reduces health
    ↓
IF health > 0: Target survives (FLEE HERE)
IF health <= 0: Target.Destroyed() called
    ↓
GameSystem.TargetDestroyed() updates score
```



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
┌─────────────────────────────────────────────────────────────┐
│                        GAME SYSTEM                          │
│                    (Singleton Manager)                      │
└───────────────────────┬─────────────────────────────────────┘
                        │
            ┌───────────┴────────────┐
            │                        │
┌───────────▼───────────┐   ┌─────────▼──────────┐
│   CONTROLLER          │   │   LEVELLAYOUT      │
│   (Player)            │   │   (Rooms)          │
│  - Transform          │   │  - LevelRoom[]     │
│  - Singleton          │   │  - NavMeshLinks    │
└───────────┬───────────┘   └─────────┬──────────┘
            │                         │
            │ Player                  │ Waypoints
            │ Position                │
            │                         │
┌───────────▼─────────────────────────▼───────────┐
│              TARGETAI                           │
│         (NavMesh-Based Enemy AI)                │
│  - NavMeshAgent                                 │
│  - State Machine (Patrol/Chase/Flee)            │
│  - Patrol Waypoints                             │
│  - Player Detection                             │
└──────────────┬──────────────────────────────────┘
              │
              │ Attached to
              │
┌──────────────▼───────────────────────────────────┐
│              TARGET                              │
│         (Health & Destruction)                   │
│  - Got(damage) method                            │
│  - Health tracking                               │
│  - Calls TargetAI.StartFleeing()                 │
└──────────────────────────────────────────────────┘
              │
              │ When destroyed
              │
┌──────────────▼───────────────────────────────────┐
│         WEAPON → RAYCAST → TARGET.GOT()          │
└──────────────────────────────────────────────────┘
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
using UnityEngine.AI;

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


#### 4.1.3 Complete NavMeshRoomConnector.cs

```csharp
// ============================================================================
// NAVMESHROOMCONNECTOR.CS - Multi-Room NavMesh Connectivity System
// ============================================================================
// Location: Assets/Creator Kit - FPS/Scripts/AI/NavMeshRoomConnector.cs
// Purpose: Creates NavMeshLinks between modular rooms for multi-room navigation
// Phase: 4.1.3 (NavMesh Foundation - Room Connectivity)
//
// Handles the challenge of Creator Kit's room-based level construction
// where each room has its own NavMeshSurface that needs to be connected
//
// FERRONE REFERENCE: pg. 270 (NavMeshLink for connecting surfaces)
// ============================================================================

using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
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
/// 
/// STUDENT NOTE:
/// This component should be attached to the same GameObject as LevelLayout.
/// It works in conjunction with LevelRoomNavMesh to create a complete
/// multi-room navigation system.
/// </summary>
public class NavMeshRoomConnector : MonoBehaviour
{
    // ====================================================================
    // REFERENCES
    // ====================================================================

    [Header("References")]
    [Tooltip("Reference to LevelLayout managing room assembly")]
    private LevelLayout levelLayout;

    // ====================================================================
    // CONFIGURATION
    // ====================================================================

    [Header("Link Settings")]
    [Tooltip("Width of doorway links (should match doorway size)")]
    [Range(1f, 5f)]
    public float linkWidth = 2f;

    [Tooltip("Cost modifier for links (higher = agents avoid)")]
    [Range(-10f, 10f)]
    public float costModifier = 1f;

    [Tooltip("Are links bidirectional (both ways)?")]
    public bool bidirectional = true;

    [Tooltip("Should links be created automatically on Start? (Set to false - LevelLayout handles this)")]
    public bool autoConnect = false;

    [Header("Debug")]
    [Tooltip("Log link creation details")]
    public bool debugMode = false;

    // ====================================================================
    // PRIVATE STATE
    // ====================================================================

    /// <summary>List of created links for cleanup</summary>
    private List<NavMeshLink> createdLinks = new List<NavMeshLink>();

    // ====================================================================
    // INITIALIZATION
    // ====================================================================

    /// <summary>
    /// Called when component is created
    /// Gets reference to LevelLayout and optionally auto-connects rooms
    /// 
    /// NOTE: autoConnect should typically be false since LevelLayout.BakeCompleteNavMesh()
    /// handles connection timing after NavMesh baking is complete
    /// </summary>
    void Start()
    {
        // Get LevelLayout component (must be on same GameObject)
        levelLayout = GetComponent<LevelLayout>();

        if (levelLayout == null)
        {
            Debug.LogError("NavMeshRoomConnector requires LevelLayout component on same GameObject!");
            enabled = false;
            return;
        }

        // Auto-connect if enabled (typically disabled - see autoConnect tooltip)
        if (autoConnect)
        {
            // Delay connection to ensure NavMeshes are baked
            StartCoroutine(DelayedConnection());
        }
    }

    /// <summary>
    /// Delays connection to ensure NavMeshes are fully baked
    /// Used when autoConnect is enabled
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
    /// 
    /// TYPICAL USAGE:
    /// Called by LevelLayout.BakeCompleteNavMesh() after all room NavMeshes
    /// have been baked successfully
    /// 
    /// ALGORITHM:
    /// 1. Iterate through all rooms in LevelLayout
    /// 2. For each room, check all exits
    /// 3. If exit connects to another room, find the matching exit
    /// 4. Create NavMeshLink between the two exit points
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
                Debug.LogWarning($"Exits too far apart ({distance}m): {roomAName} -> {roomBName}");
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
            Debug.Log($"Created NavMeshLink: {roomAName} -> {roomBName} " +
                     $"(distance: {distance:F2}m)");
        }

        return true;
    }

    // ====================================================================
    // CLEANUP
    // ====================================================================

    /// <summary>
    /// Removes all created NavMeshLinks
    /// Useful for dynamic level changes or when reloading scenes
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
    /// Ensures no orphaned NavMeshLink GameObjects remain
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
    /// Only active when debugMode is enabled
    /// 
    /// VISUAL REPRESENTATION:
    /// - Cyan lines connecting exit points
    /// - Wire spheres at each endpoint
    /// - Visible in Scene view during Play Mode
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

            // Draw line connecting exits
            Gizmos.DrawLine(start, end);

            // Draw spheres at endpoints
            Gizmos.DrawWireSphere(start, 0.3f);
            Gizmos.DrawWireSphere(end, 0.3f);
        }
    }
}

```



### 4.2 TargetAI Component

#### 4.2.1 TargetAI.cs

```csharp
// Location: Create new file at Assets/Creator Kit - FPS/Scripts/AI/TargetAI.cs
// Purpose: Main AI behavior controller for NavMesh-based enemies
// References: Ferrone pg. 278-284 (AI state management and player detection)

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// AI controller for targets using NavMesh pathfinding
/// Implements patrol, chase, flee, and search behaviors
/// 
/// ARCHITECTURE:
/// - State machine pattern for behavior management
/// - NavMeshAgent for pathfinding
/// - Integrates with existing Target damage system
/// 
/// FERRONE ALIGNMENT:
/// - pg. 278: "Moving Enemy Agents"
/// - pg. 283: "Seek and Destroy" player detection
/// - pg. 291: "Refactoring and keeping it DRY"
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class TargetAI : MonoBehaviour
{
    // ====================================================================
    // CONFIGURATION
    // ====================================================================
    
    [Header("AI Configuration")]
    [Tooltip("Reference to patrol waypoint parent (Ferrone pg. 274)")]
    public Transform patrolRoute;
    
    [Tooltip("Detection radius for spotting player (Ferrone pg. 283)")]
    [Range(5f, 30f)]
    public float detectionRadius = 15f;
    
    [Tooltip("Distance to flee when damaged")]
    [Range(10f, 30f)]
    public float fleeDistance = 20f;
    
    [Tooltip("How close to waypoint before moving to next")]
    [Range(0.5f, 3f)]
    public float waypointTolerance = 1f;
    
    [Tooltip("Speed when patrolling")]
    public float patrolSpeed = 3.5f;
    
    [Tooltip("Speed when chasing player")]
    public float chaseSpeed = 6f;
    
    [Tooltip("Speed when fleeing")]
    public float fleeSpeed = 7f;
    
    [Header("Behavior Timers")]
    [Tooltip("How often to check for player (performance)")]
    public float playerCheckInterval = 0.5f;
    
    [Tooltip("How long to search after losing player")]
    public float searchDuration = 5f;
    
    [Tooltip("How long to flee after being hit")]
    public float fleeDuration = 3f;
    
    // ====================================================================
    // STATE MANAGEMENT
    // ====================================================================
    
    public enum AIState
    {
        Patrol,     // Following waypoint route
        Chase,      // Pursuing player
        Flee,       // Running from damage source  
        Searching   // Lost player, searching area
    }
    
    [Header("Debug")]
    public AIState currentState = AIState.Patrol;
    
    // ====================================================================
    // PRIVATE MEMBERS
    // ====================================================================
    
    private NavMeshAgent _agent;
    private List<Transform> _waypoints = new List<Transform>();
    private int _currentWaypointIndex = 0;
    private Transform _player;
    private Vector3 _lastKnownPlayerPosition;
    private float _stateTimer = 0f;
    private float _playerCheckTimer = 0f;
    private Vector3 _fleeDestination;
    
    // ====================================================================
    // INITIALIZATION
    // ====================================================================
    
    void Start()
    {
        // Get NavMeshAgent component
        _agent = GetComponent<NavMeshAgent>();
        if (_agent == null)
        {
            Debug.LogError($"TargetAI on {gameObject.name} requires NavMeshAgent!");
            enabled = false;
            return;
        }
        
        // Cache player reference (Ferrone pg. 283 - finding the player)
        if (Controller.Instance != null)
        {
            _player = Controller.Instance.transform;
        }
        else
        {
            Debug.LogWarning("TargetAI: Controller.Instance not found!");
        }
        
        // Initialize patrol route
        InitializePatrolRoute();
        
        // Start patrolling
        SetState(AIState.Patrol);
        
        Debug.Log($"TargetAI initialized on {gameObject.name} with {_waypoints.Count} waypoints");
    }
    
    /// <summary>
    /// Initialize waypoint list from patrol route
    /// Reference: Ferrone pg. 274-277 (procedural patrol setup)
    /// </summary>
    void InitializePatrolRoute()
    {
        _waypoints.Clear();
        
        if (patrolRoute == null)
        {
            Debug.LogWarning($"No patrol route assigned to {gameObject.name}");
            return;
        }
        
        // Collect all child transforms as waypoints
        foreach (Transform waypoint in patrolRoute)
        {
            _waypoints.Add(waypoint);
        }
        
        if (_waypoints.Count == 0)
        {
            Debug.LogWarning($"Patrol route for {gameObject.name} has no waypoints!");
        }
    }
    
    // ====================================================================
    // UPDATE LOOP - State Machine Core
    // ====================================================================
    
    void Update()
    {
        // Periodic player detection check (performance optimization)
        _playerCheckTimer += Time.deltaTime;
        if (_playerCheckTimer >= playerCheckInterval)
        {
            _playerCheckTimer = 0f;
            CheckForPlayer();
        }
        
        // State machine update
        switch (currentState)
        {
            case AIState.Patrol:
                UpdatePatrol();
                break;
                
            case AIState.Chase:
                UpdateChase();
                break;
                
            case AIState.Flee:
                UpdateFlee();
                break;
                
            case AIState.Searching:
                UpdateSearching();
                break;
        }
    }
    
    // ====================================================================
    // STATE BEHAVIORS
    // ====================================================================
    
    /// <summary>
    /// Patrol behavior - move between waypoints
    /// Reference: Ferrone pg. 278-279 (moving agents)
    /// </summary>
    void UpdatePatrol()
    {
        if (_waypoints.Count == 0) return;
        
        // Check if reached current waypoint
        if (!_agent.pathPending && _agent.remainingDistance < waypointTolerance)
        {
            MoveToNextPatrolLocation();
        }
    }
    
    /// <summary>
    /// Chase behavior - pursue the player
    /// Reference: Ferrone pg. 283-284 (seek and destroy)
    /// </summary>
    void UpdateChase()
    {
        if (_player == null) return;
        
        // Update destination to player position
        _agent.SetDestination(_player.position);
        _lastKnownPlayerPosition = _player.position;
        
        // If player escapes detection range, switch to searching
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        if (distanceToPlayer > detectionRadius * 1.5f) // Give some buffer
        {
            SetState(AIState.Searching);
        }
    }
    
    /// <summary>
    /// Flee behavior - run away from danger
    /// Custom implementation for Creator Kit
    /// </summary>
    void UpdateFlee()
    {
        _stateTimer += Time.deltaTime;
        
        // Return to patrol after flee duration
        if (_stateTimer >= fleeDuration)
        {
            Debug.Log($"{gameObject.name} finished fleeing, returning to patrol");
            SetState(AIState.Patrol);
        }
    }
    
    /// <summary>
    /// Search behavior - look for lost player
    /// </summary>
    void UpdateSearching()
    {
        _stateTimer += Time.deltaTime;
        
        // Search timeout - return to patrol
        if (_stateTimer >= searchDuration)
        {
            Debug.Log($"{gameObject.name} gave up searching, returning to patrol");
            SetState(AIState.Patrol);
        }
        
        // Move to last known position if not there yet
        if (!_agent.pathPending && _agent.remainingDistance < waypointTolerance)
        {
            // Could add random search pattern here
            SetState(AIState.Patrol);
        }
    }
    
    // ====================================================================
    // STATE TRANSITIONS
    // ====================================================================
    
    /// <summary>
    /// Change AI state and configure agent accordingly
    /// </summary>
    void SetState(AIState newState)
    {
        // Exit current state
        OnStateExit(currentState);
        
        // Enter new state
        currentState = newState;
        OnStateEnter(newState);
        
        Debug.Log($"{gameObject.name} changed state to: {newState}");
    }
    
    /// <summary>
    /// Configure agent when entering a state
    /// </summary>
    void OnStateEnter(AIState state)
    {
        _stateTimer = 0f;
        
        switch (state)
        {
            case AIState.Patrol:
                _agent.speed = patrolSpeed;
                _agent.isStopped = false;
                if (_waypoints.Count > 0)
                {
                    MoveToNextPatrolLocation();
                }
                break;
                
            case AIState.Chase:
                _agent.speed = chaseSpeed;
                _agent.isStopped = false;
                if (_player != null)
                {
                    _agent.SetDestination(_player.position);
                }
                break;
                
            case AIState.Flee:
                _agent.speed = fleeSpeed;
                _agent.isStopped = false;
                _agent.SetDestination(_fleeDestination);
                break;
                
            case AIState.Searching:
                _agent.speed = patrolSpeed;
                _agent.isStopped = false;
                _agent.SetDestination(_lastKnownPlayerPosition);
                break;
        }
    }
    
    /// <summary>
    /// Cleanup when exiting a state
    /// </summary>
    void OnStateExit(AIState state)
    {
        // Could add state-specific cleanup here
    }
    
    // ====================================================================
    // PATROL HELPERS
    // ====================================================================
    
    /// <summary>
    /// Move to next waypoint in patrol route
    /// Reference: Ferrone pg. 278 (waypoint navigation)
    /// </summary>
    void MoveToNextPatrolLocation()
    {
        if (_waypoints.Count == 0) return;
        
        // Set destination to current waypoint
        _agent.SetDestination(_waypoints[_currentWaypointIndex].position);
        
        // Move to next waypoint (with wrapping)
        _currentWaypointIndex = (_currentWaypointIndex + 1) % _waypoints.Count;
    }
    
    // ====================================================================
    // PLAYER DETECTION
    // ====================================================================
    
    /// <summary>
    /// Check if player is within detection range
    /// Reference: Ferrone pg. 283 (player detection)
    /// </summary>
    void CheckForPlayer()
    {
        // Skip if already chasing or fleeing
        if (currentState == AIState.Chase || currentState == AIState.Flee)
            return;
            
        if (_player == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        
        if (distanceToPlayer <= detectionRadius)
        {
            // Optional: Add line-of-sight check here
            Debug.Log($"{gameObject.name} detected player at {distanceToPlayer:F1}m");
            SetState(AIState.Chase);
        }
    }
    
    // ====================================================================
    // PUBLIC INTERFACE
    // ====================================================================
    
    /// <summary>
    /// Called by Target.cs when damaged but not destroyed
    /// Triggers flee behavior
    /// </summary>
    public void StartFleeing()
    {
        if (currentState == AIState.Flee) return; // Already fleeing

        // null check
        if (_player == null)
        {
            Debug.LogWarning($"{gameObject.name} cannot flee - player reference is null");
            return;
        }
        
        // Calculate flee direction (away from player or damage source)
        Vector3 fleeDirection = transform.position - _player.position;
        fleeDirection.y = 0; // Keep on horizontal plane
        fleeDirection.Normalize();
        
        // Calculate flee destination
        _fleeDestination = transform.position + (fleeDirection * fleeDistance);
        
        // Sample to nearest point on NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(_fleeDestination, out hit, fleeDistance, NavMesh.AllAreas))
        {
            _fleeDestination = hit.position;
        }
        
        Debug.Log($"{gameObject.name} fleeing to {_fleeDestination}");
        SetState(AIState.Flee);
    }
    
    /// <summary>
    /// Force the AI to stop all movement
    /// </summary>
    public void StopMovement()
    {
        if (_agent != null)
        {
            _agent.isStopped = true;
        }
    }
    
    // ====================================================================
    // DEBUG VISUALIZATION
    // ====================================================================
    
    void OnDrawGizmosSelected()
    {
        // Draw detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        // Draw flee radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, fleeDistance);
        
        // Draw waypoint connections
        if (_waypoints != null && _waypoints.Count > 1)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < _waypoints.Count; i++)
            {
                if (_waypoints[i] == null) continue;
                
                // Draw sphere at waypoint
                Gizmos.DrawSphere(_waypoints[i].position, 0.5f);
                
                // Draw line to next waypoint
                int nextIndex = (i + 1) % _waypoints.Count;
                if (_waypoints[nextIndex] != null)
                {
                    Gizmos.DrawLine(_waypoints[i].position, _waypoints[nextIndex].position);
                }
            }
        }
        
        // Draw current destination
        if (Application.isPlaying && _agent != null && _agent.hasPath)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _agent.destination);
            Gizmos.DrawWireCube(_agent.destination, Vector3.one * 0.5f);
        }
    }
}
```

#### 4.2.2 Modified Target.cs

```csharp
// Location: Modify existing file at Assets/Creator Kit - FPS/Scripts/System/Target.cs
// Purpose: Add AI fleeing trigger when damaged but not destroyed
// Changes: Add TargetAI integration in Got() method

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// FIXED: Removed System.Numerics import - not needed in Unity runtime scripts
// FIXED: Removed Vector3 alias - use UnityEngine.Vector3 directly

/// <summary>
/// Target component for shootable objects in FPS Kit
/// Modified to trigger AI fleeing behavior when damaged
/// 
/// INTEGRATION POINT:
/// When health > 0 after damage, triggers TargetAI.StartFleeing()
/// This creates the "wounded enemy runs away" behavior
/// </summary>
public class Target : MonoBehaviour
{
    // EXISTING TARGET FIELDS...
    public float health = 1;
    public int pointValue = 10;
    public ParticleSystem DestroyedEffect;
    
    [Header("Audio")]
    public RandomPlayer HitPlayer;
    public AudioSource IdleSource;
    
    public bool Destroyed => m_Destroyed;
    
    bool m_Destroyed = false;
    float m_CurrentHealth;
    
    // ====================================================================
    // NEW: AI INTEGRATION
    // ====================================================================
    
    /// <summary>
    /// Cached reference to AI component (if this target has AI)
    /// </summary>
    private TargetAI targetAI;
    
    // ====================================================================
    // EXISTING METHODS WITH MODIFICATIONS
    // ====================================================================
    
    void Awake()
    {
        Helpers.RecursiveLayerChange(transform, LayerMask.NameToLayer("Target"));
        
        // NEW: Cache TargetAI reference if it exists
        // Note: Using GetComponentInParent to handle nested prefab structures
        targetAI = GetComponentInParent<TargetAI>();
        if (targetAI != null)
        {
            Debug.Log($"Target {gameObject.name} has AI component");
        }
    }
    
    void Start()
    {
        PoolSystem.Create();
        
        if (HitPlayer != null)
        {
            HitPlayer.source.pitch = 1.0f + Random.Range(-0.2f, 0.2f);
        }
        
        if (IdleSource != null)
        {
            IdleSource.pitch = 1.0f + Random.Range(-0.2f, 0.2f);
        }
        
        m_CurrentHealth = health;
        
        if (HitPlayer != null)
        {
            m_CurrentHealth += HitPlayer.Clips.Length - 1;
        }
    }
    
    /// <summary>
    /// Called when target takes damage
    /// Modified to trigger fleeing if health > 0
    /// 
    /// STUDENT NOTE:
    /// This is where damage leads to behavior change
    /// If destroyed: remove from game
    /// If wounded: trigger flee behavior
    /// </summary>
    public void Got(float damage = 1.0f)
    {
        m_CurrentHealth -= damage;
        
        if (HitPlayer != null)
        {
            HitPlayer.PlayRandom();
        }
        
        // ================================================================
        // NEW: FLEEING BEHAVIOR TRIGGER
        // ================================================================
        // If target survives the hit, make it flee
        if (m_CurrentHealth > 0)
        {
            // Target is wounded but not destroyed
            Debug.Log($"{gameObject.name} wounded! Health: {m_CurrentHealth}/{health}");
            
            // Trigger flee behavior if this target has AI
            if (targetAI != null)
            {
                targetAI.StartFleeing();
                Debug.Log($"{gameObject.name} is fleeing!");
            }
            
            // Could add visual feedback here (damage particles, color change, etc.)
            
            return; // Don't destroy the target
        }
        
        // ================================================================
        // EXISTING DESTRUCTION CODE (health <= 0)
        // ================================================================
        
        if (IdleSource != null && IdleSource.isPlaying)
        {
            IdleSource.Stop();
        }
        
        var position = transform.position;
        
        if (HitPlayer != null)
        {
            var source = WorldAudioPool.GetWorldSFXSource();
            source.transform.position = position;
            source.pitch = HitPlayer.source.pitch;
            source.PlayOneShot(HitPlayer.GetRandomClip());
        }
        
        if (DestroyedEffect != null)
        {
            var effect = PoolSystem.Instance.GetInstance<ParticleSystem>(DestroyedEffect);
            effect.time = 0.0f;
            effect.Play();
            effect.transform.position = position;
        }
        
        m_Destroyed = true;
        
        // Stop AI movement before destroying
        if (targetAI != null)
        {
            targetAI.StopMovement();
        }
        
        gameObject.SetActive(false);
        GameSystem.Instance.TargetDestroyed(pointValue);
    }
}

// ============================================================================
// END OF MODIFIED TARGET.CS
// ============================================================================
// 
// SUMMARY OF CHANGES:
// 1. FIXED: Removed System.Numerics using statement (not needed)
// 2. FIXED: Removed Vector3 alias (use UnityEngine.Vector3 directly)
// 3. Added TargetAI caching in Awake() (uses GetComponentInParent for flexibility)
// 4. Added fleeing trigger in Got() when health > 0
// 5. Added AI movement stop before destruction
// 
// INTEGRATION NOTES:
// - Works with both AI and non-AI targets (null-safe)
// - Uses GetComponentInParent to handle various prefab structures
// - Maintains all existing Target functionality
// ============================================================================
```

### 4.3 Phase 3: Waypoint System

#### 4.3.1 NavDestination.cs

```csharp
// ============================================================================
// NAVDESTINATION.CS - AI Navigation Waypoint Component
// ============================================================================
// Location: Assets/Creator Kit - FPS/Scripts/System/NavDestination.cs
// Purpose: Represents a navigation destination point for AI agents
// Reference: Ferrone pg. 272 (creating patrol locations)
//
// USAGE:
// This component marks waypoints in patrol routes. AI agents (using TargetAI.cs)
// walk between these waypoints to create patrol behavior.
//
// TYPICAL HIERARCHY:
// PatrolRoute (empty GameObject - assigned to TargetAI.patrolRoute)
//   ├─ Waypoint_1 (this component)
//   ├─ Waypoint_2 (this component)
//   ├─ Waypoint_3 (this component)
//   └─ Waypoint_4 (this component)
//
// FERRONE ALIGNMENT:
// - pg. 272: "Create Empty to add a child GameObject, and name it Location_1"
// - Demonstrates GameObject organization for procedural AI systems
// - Shows enum-based behavior variants and Gizmo visualization
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Navigation waypoint component for AI patrol routes
/// Provides visual feedback in Scene view via color-coded Gizmos
/// </summary>
public class NavDestination : MonoBehaviour
{
    // ========================================================================
    // DESTINATION TYPE ENUMERATION
    // ========================================================================

    /// <summary>
    /// Defines the purpose and behavior type of this waypoint
    /// Enables designers to create varied and strategic AI patterns
    /// </summary>
    public enum DestinationType
    {
        /// <summary>
        /// Regular patrol point - standard waypoint for patrol routes
        /// VISUAL: Green sphere | USAGE: Most common type for basic patrol circuits
        /// </summary>
        Patrol,

        /// <summary>
        /// Ambush point - strategic attack position with good visibility
        /// VISUAL: Red sphere | USAGE: AI may wait longer, watching for player
        /// </summary>
        Ambush,

        /// <summary>
        /// Cover point - defensive position behind obstacles
        /// VISUAL: Blue sphere | USAGE: AI retreats here when wounded (future enhancement)
        /// </summary>
        CoverPoint,

        /// <summary>
        /// Connection point - marks transition between rooms
        /// VISUAL: Yellow sphere | USAGE: Multi-room patrol routes spanning multiple rooms
        /// </summary>
        ConnectionPoint
    }

    // ========================================================================
    // PUBLIC CONFIGURATION FIELDS
    // ========================================================================

    [Header("Destination Settings")]
    [Tooltip("Purpose of this destination point")]
    public DestinationType destinationType = DestinationType.Patrol;

    [Tooltip("Time to wait at this destination (0 = no wait)")]
    [Range(0f, 10f)]
    public float waitTime = 0f;

    [Tooltip("Connected destinations for multi-path routing (optional)")]
    public List<NavDestination> connections = new List<NavDestination>();

    [Tooltip("Show destination gizmo in Scene view")]
    public bool showGizmo = true;

    // ========================================================================
    // INITIALIZATION
    // ========================================================================

    /// <summary>
    /// Hides visual representation so waypoints are invisible during gameplay
    /// Waypoints are level design tools - players should never see them
    /// Designers see them in Scene view via Gizmos (color-coded spheres)
    /// </summary>
    void Awake()
    {
        // Disable mesh renderer if present (makes invisible in game)
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = false;
        }

        // Disable collider if present (no physics interaction)
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        // Transform remains enabled - AI needs position data
    }

    // ========================================================================
    // GIZMO VISUALIZATION - Scene View Only
    // ========================================================================

    /// <summary>
    /// Draws waypoint visualization in Unity Scene view
    /// Color-coded by destination type for easy identification
    /// Shows connections between waypoints for multi-path routing
    /// 
    /// PERFORMANCE NOTE: Only runs in Editor, not in builds (zero runtime cost)
    /// FERRONE REFERENCE: pg. 272 - Unity calls OnDrawGizmos() automatically
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showGizmo)
            return;

        // Set color based on destination type (semi-transparent)
        switch (destinationType)
        {
            case DestinationType.Patrol:
                Gizmos.color = new Color(0f, 1f, 0f, 0.5f); // Green - standard patrol
                break;

            case DestinationType.Ambush:
                Gizmos.color = new Color(1f, 0f, 0f, 0.5f); // Red - danger/attack
                break;

            case DestinationType.CoverPoint:
                Gizmos.color = new Color(0f, 0f, 1f, 0.5f); // Blue - defensive
                break;

            case DestinationType.ConnectionPoint:
                Gizmos.color = new Color(1f, 1f, 0f, 0.5f); // Yellow - transition
                break;
        }

        // Draw solid sphere at waypoint position (main visual indicator)
        Gizmos.DrawSphere(transform.position, 0.3f);

        // Draw white wire sphere as selection outline
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        // Draw connections to other waypoints (visualizes multi-path routing)
        Gizmos.color = new Color(1f, 1f, 1f, 0.3f); // White with low alpha
        foreach (var connection in connections)
        {
            if (connection != null)
            {
                Gizmos.DrawLine(transform.position, connection.transform.position);
            }
        }
    }

    /// <summary>
    /// Draws text label when waypoint is selected in hierarchy
    /// Shows destination type and wait time (if any)
    /// Only compiles in Unity Editor (not included in builds)
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (!showGizmo)
            return;

#if UNITY_EDITOR
        // Draw text label 0.5 units above waypoint
        // Shows type and wait time in a compact format
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

    // ========================================================================
    // END OF NAVDESTINATION.CS
    // ========================================================================
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

### 4.4 Spawner Integration

#### 4.4.1 Modified TargetSpawner.cs

```csharp
// Location: Modify existing file at Assets/Creator Kit - FPS/Scripts/System/TargetSpawner.cs
// Purpose: Refactor spawner to work with NavMesh-based AI targets
// Major Changes: Remove PathSystem dependency, add NavMesh spawning, fix instantiation timing

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns targets with NavMesh AI instead of path-following movement
/// 
/// ARCHITECTURE CHANGES:
/// - Removed PathSystem dependency (was rigid path following)
/// - Added patrol route assignment for AI waypoints
/// - FIXED: Targets now instantiated at spawn time (not pre-instantiated)
/// 
/// STUDENT LEARNING:
/// - Object pooling vs just-in-time instantiation
/// - Component communication (Spawner → TargetAI)
/// - Performance considerations
/// </summary>
public class TargetSpawner : MonoBehaviour
{
    // ====================================================================
    // SPAWN CONFIGURATION
    // ====================================================================

    [System.Serializable]
    public class SpawnEvent
    {
        [Tooltip("Target prefab with TargetAI component")]
        public GameObject targetToSpawn;

        [Tooltip("Number of this target type to spawn")]
        public int count = 1;

        [Tooltip("Delay between spawning each target")]
        public float timeBetweenSpawn = 1.0f;

        [Tooltip("Patrol route for spawned AI (optional)")]
        public Transform patrolRoute;

        // Could add more per-spawn settings:
        // public float aiDetectionRange = 15f;
        // public float aiMoveSpeed = 3.5f;
    }

    [Header("Spawn Configuration")]
    [Tooltip("List of spawn waves/events")]
    public SpawnEvent[] spawnEvents = new SpawnEvent[0];

    [Header("Spawn Position")]
    [Tooltip("Randomize spawn position within radius")]
    public float spawnRadius = 2f;

    [Tooltip("Check for clear spawn location")]
    public bool ensureClearSpawn = true;

    // ====================================================================
    // SPAWN QUEUE SYSTEM - FIXED VERSION
    // ====================================================================

    /// <summary>
    /// Element in spawn queue - stores prefab reference, not instance
    /// FIXED: No longer stores GameObject instance until actual spawn
    /// </summary>
    [System.Serializable]
    public class SpawnQueueElement
    {
        // Prefab to spawn (not an instance!)
        public GameObject targetPrefab;

        // Time until spawn
        public float remainingTime;

        // Patrol route to assign
        public Transform patrolRoute;

        // After spawning, we track the instance
        public GameObject spawnedInstance;
        public Target targetComponent;
        public TargetAI aiComponent;
    }

    // Queue of targets waiting to spawn
    Queue<SpawnQueueElement> m_SpawnQueue;

    // List of active (spawned) targets
    List<SpawnQueueElement> m_ActiveElements;

    [Header("Debug")]
    public bool showSpawnGizmos = true;

    // ====================================================================
    // INITIALIZATION - FIXED VERSION
    // ====================================================================

    /// <summary>
    /// Initialize spawn queue WITHOUT instantiating targets
    /// FIXED: Only queues spawn data, doesn't create GameObjects yet
    /// </summary>
    void Awake()
    {
        m_SpawnQueue = new Queue<SpawnQueueElement>();
        m_ActiveElements = new List<SpawnQueueElement>();

        // Build spawn queue from configuration
        foreach (var spawnEvent in spawnEvents)
        {
            if (spawnEvent.targetToSpawn == null)
            {
                Debug.LogWarning($"TargetSpawner: Null target in spawn event!");
                continue;
            }

            // Validate that prefab has required components
            var prefabAI = spawnEvent.targetToSpawn.GetComponentInChildren<TargetAI>();
            if (prefabAI == null && spawnEvent.patrolRoute != null)
            {
                Debug.LogWarning($"Prefab {spawnEvent.targetToSpawn.name} has patrol route but no TargetAI!");
            }


            // Queue spawn data (NOT instances!)
            for (int i = 0; i < spawnEvent.count; ++i)
            {
                SpawnQueueElement element = new SpawnQueueElement()
                {
                    targetPrefab = spawnEvent.targetToSpawn,  // Store prefab reference
                    patrolRoute = spawnEvent.patrolRoute,     // Store route reference
                    remainingTime = i * spawnEvent.timeBetweenSpawn  // Calculate spawn delay
                };

                m_SpawnQueue.Enqueue(element);
            }

            Debug.Log($"Queued {spawnEvent.count} spawns of {spawnEvent.targetToSpawn.name}");
        }

        // Log warning if no targets queued
        if (m_SpawnQueue.Count == 0)
        {
            Debug.LogWarning($"TargetSpawner on {gameObject.name}: No targets queued for spawning!");
            // NOTE: We do NOT destroy the spawner - it might be configured later
        }
        else
        {
            Debug.Log($"TargetSpawner ready with {m_SpawnQueue.Count} targets queued");
        }
    }

    // ====================================================================
    // SPAWN TIMING & INSTANTIATION - FIXED VERSION
    // ====================================================================

    /// <summary>
    /// Update spawn timers and instantiate targets when ready
    /// FIXED: Now instantiates targets just-in-time instead of pre-creating
    /// </summary>
    void Update()
    {
        // Process spawn queue
        if (m_SpawnQueue.Count > 0)
        {
            var element = m_SpawnQueue.Peek();

            // Countdown timer
            element.remainingTime -= Time.deltaTime;

            // Time to spawn!
            if (element.remainingTime <= 0)
            {
                m_SpawnQueue.Dequeue();
                SpawnTarget(element);
            }
        }

        // Update active targets (check if destroyed)
        for (int i = m_ActiveElements.Count - 1; i >= 0; i--)
        {
            var element = m_ActiveElements[i];

            // Remove destroyed targets from active list
            if (element.spawnedInstance == null ||
                (element.targetComponent != null && element.targetComponent.Destroyed))
            {
                m_ActiveElements.RemoveAt(i);
                Debug.Log($"Removed destroyed target from active list");
            }
        }
    }

    /// <summary>
    /// Spawn a target at the spawner location
    /// FIXED: This is where instantiation happens (not in Awake)
    /// </summary>
    void SpawnTarget(SpawnQueueElement element)
    {
        // Calculate spawn position (with optional randomization)
        Vector3 spawnPosition = CalculateSpawnPosition();

        // FIXED: Instantiate the target NOW (when it's time to spawn)
        GameObject targetObj = Instantiate(
            element.targetPrefab,
            spawnPosition,
            transform.rotation
        );

        // Store the spawned instance
        element.spawnedInstance = targetObj;

        // Get components
        element.targetComponent = targetObj.GetComponentInChildren<Target>();
        element.aiComponent = targetObj.GetComponentInChildren<TargetAI>();

        // Configure AI if present
        if (element.aiComponent != null)
        {
            // Assign patrol route
            if (element.patrolRoute != null)
            {
                element.aiComponent.patrolRoute = element.patrolRoute;
                Debug.Log($"Assigned patrol route to {targetObj.name}");
            }
            else
            {
                Debug.LogWarning($"No patrol route for AI target {targetObj.name}");
            }

            // NavMeshAgent is now properly on the NavMesh at spawn position
            // The AI's Start() method will initialize it correctly
        }

        // Add to active list
        m_ActiveElements.Add(element);

        // Log spawn
        Debug.Log($"Spawned {targetObj.name} at {spawnPosition}");

        // Optional: Spawn effects
        PlaySpawnEffects(spawnPosition);
    }

    /// <summary>
    /// Calculate spawn position with optional randomization
    /// </summary>
    Vector3 CalculateSpawnPosition()
    {
        Vector3 basePosition = transform.position;

        if (spawnRadius > 0)
        {
            // Add random offset within radius
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            basePosition += new Vector3(randomCircle.x, 0, randomCircle.y);

            // Ensure spawn position is on NavMesh
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(basePosition, out hit, spawnRadius, UnityEngine.AI.NavMesh.AllAreas))
            {
                basePosition = hit.position;
            }
        }

        return basePosition;
    }

    /// <summary>
    /// Optional spawn effects (particles, sound, etc.)
    /// </summary>
    void PlaySpawnEffects(Vector3 position)
    {
        // Could add:
        // - Spawn particle effect
        // - Spawn sound
        // - Camera shake
        // - UI notification
    }

    // ====================================================================
    // PUBLIC INTERFACE
    // ====================================================================

    /// <summary>
    /// Get count of targets waiting to spawn
    /// </summary>
    public int GetQueuedCount()
    {
        return m_SpawnQueue.Count;
    }

    /// <summary>
    /// Get count of active (spawned) targets
    /// </summary>
    public int GetActiveCount()
    {
        return m_ActiveElements.Count;
    }

    /// <summary>
    /// Force spawn all queued targets immediately
    /// </summary>
    [ContextMenu("Force Spawn All")]
    public void ForceSpawnAll()
    {
        while (m_SpawnQueue.Count > 0)
        {
            var element = m_SpawnQueue.Dequeue();
            SpawnTarget(element);
        }
    }

    // ====================================================================
    // DEBUG VISUALIZATION
    // ====================================================================

    void OnDrawGizmos()
    {
        if (!showSpawnGizmos) return;

        // Draw spawn point
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        // Draw spawn radius
        if (spawnRadius > 0)
        {
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }

        // Draw spawn direction
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }

    void OnDrawGizmosSelected()
    {
        // Draw patrol routes when selected
        foreach (var spawnEvent in spawnEvents)
        {
            if (spawnEvent.patrolRoute != null)
            {
                Gizmos.color = Color.cyan;

                // Draw waypoints
                foreach (Transform waypoint in spawnEvent.patrolRoute)
                {
                    Gizmos.DrawWireSphere(waypoint.position, 0.3f);
                }

                // Connect to spawner
                if (spawnEvent.patrolRoute.childCount > 0)
                {
                    Gizmos.DrawLine(transform.position, spawnEvent.patrolRoute.GetChild(0).position);
                }
            }
        }
    }
}

// ============================================================================
// END OF MODIFIED TARGETSPAWNER.CS
// ============================================================================
// 
// KEY FIXES APPLIED:
// 1. No longer pre-instantiates targets in Awake()
// 2. Only stores prefab references in queue, not instances
// 3. Instantiates targets just-in-time when spawn timer expires
// 4. NavMeshAgents are created at proper spawn positions on NavMesh
// 5. Proper cleanup of destroyed targets from active list
// 
// STUDENT LEARNING POINTS:
// - Object lifecycle management
// - Just-in-time vs pre-allocation strategies
// - NavMeshAgent initialization requirements
// - Component communication patterns
// ============================================================================
```

#### 4.4.2 SpawnEvent Configuration

#### 4.4.2 SpawnEvent Configuration (Unity Inspector Setup)

##### Understanding the New Architecture

With the old PathSystem, the TargetSpawner owned both spawning AND movement—targets followed a path defined directly on the spawner. With the NavMesh refactor, responsibilities are now split:

| Component | Responsibility |
|-----------|----------------|
| **TargetSpawner** | When and where targets appear |
| **TargetAI** | How targets move (patrol, chase, flee) |
| **Patrol Route** | Where targets patrol (waypoint positions) |

This means the TargetSpawner's job is now simpler: spawn a target, hand it a patrol route reference, and let TargetAI handle everything else. The Inspector configuration reflects this delegation.

##### What is a SpawnEvent?

A `SpawnEvent` defines a "wave" of identical targets. Each SpawnEvent in the array spawns sequentially—all targets from SpawnEvent[0] spawn before SpawnEvent[1] begins. This creates natural difficulty progression.

```
SpawnEvent[0]: 3 slow targets, 2 seconds apart  →  6 seconds total
SpawnEvent[1]: 2 fast targets, 1.5 seconds apart →  3 seconds total
                                                    ─────────────────
                                                    9 seconds for full spawn sequence
```

##### SpawnEvent Fields Explained

| Field | Type | Purpose |
|-------|------|---------|
| **Target To Spawn** | GameObject (Prefab) | The target prefab. Must have a `Target` component. Should have `TargetAI` + `NavMeshAgent` if you want AI behavior. |
| **Count** | int | How many of this target type to spawn in this wave. |
| **Time Between Spawn** | float | Seconds between each spawn within this wave. First target spawns immediately (t=0), second at t=timeBetweenSpawn, etc. |
| **Patrol Route** | Transform | Parent GameObject containing waypoint children. TargetSpawner passes this to TargetAI.patrolRoute at spawn time. |

##### What is a Patrol Route?

A patrol route is simply a **parent GameObject with child GameObjects as waypoints**. TargetAI iterates through these children in order.

```
Hierarchy Example:
├── PatrolRoute_Hallway        ← Drag THIS into "Patrol Route" field
│   ├── Waypoint_0             ← First destination (child index 0)
│   ├── Waypoint_1             ← Second destination (child index 1)
│   ├── Waypoint_2             ← Third destination (child index 2)
│   └── Waypoint_3             ← Fourth destination, then loops to 0
```

**Why Transform children instead of a custom PathSystem?**

- Simpler to set up (just empty GameObjects)
- Visible in Scene view without custom editor
- Works naturally with Unity's hierarchy
- TargetAI can iterate with `foreach (Transform child in patrolRoute)`

**Optional Enhancement**: Add `NavDestination` components to waypoints for typed behavior (Patrol, Ambush, CoverPoint) and wait times. But plain empty GameObjects work fine for basic patrol.

##### Step-by-Step Configuration

**Step 1: Create Patrol Routes (do this first)**

You need patrol routes to exist before you can assign them to SpawnEvents.

1. In Hierarchy, create empty GameObject: `GameObject > Create Empty`
2. Rename it descriptively: "PatrolRoute_RoomA" or "PatrolRoute_Perimeter"
3. Position the parent anywhere (its position doesn't matter—only children matter)
4. Add child empty GameObjects as waypoints:
   - Right-click parent → Create Empty
   - Rename: "Waypoint_0", "Waypoint_1", etc.
   - Position each waypoint where you want the AI to walk
5. Repeat for additional patrol routes

**Tip**: In Scene view, waypoints are invisible by default. Add a small sphere mesh (disabled at runtime) or use the NavDestination component's Gizmo for visibility.

**Step 2: Prepare Target Prefabs**

Your target prefab needs these components for full AI behavior:

| Component | Required? | Purpose |
|-----------|-----------|---------|
| Target | ✅ Yes | Health, damage, destruction, scoring |
| TargetAI | ✅ For AI | State machine (Patrol/Chase/Flee) |
| NavMeshAgent | ✅ For AI | Pathfinding and movement |
| Rigidbody | Optional | Only if using physics interactions |
| Collider | ✅ Yes | For raycasts (shooting) to detect hits |

If a prefab has no TargetAI, it will spawn but won't move—useful for stationary targets.

**Step 3: Configure TargetSpawner Component**

1. Select or create a GameObject where targets should spawn
2. Add Component → TargetSpawner
3. Set **Spawn Radius**: How far from the spawner position targets can appear (randomized within circle)
4. Expand **Spawn Events** array
5. Set array size (e.g., 2 for two waves)
6. For each SpawnEvent:
   - **Target To Spawn**: Drag prefab from Project window
   - **Count**: Number to spawn in this wave
   - **Time Between Spawn**: Delay between each
   - **Patrol Route**: Drag patrol route GameObject from Hierarchy

##### Example Configuration

**Scenario**: Training room with warm-up targets, then challenging fast movers.

```
TargetSpawner (Component on "SpawnPoint_TrainingRoom")
├── Spawn Radius: 1.5
├── Spawn Events: 2 elements
│
├── [0] SpawnEvent
│   ├── Target To Spawn: "Target_Basic" (prefab)
│   ├── Count: 3
│   ├── Time Between Spawn: 2.0
│   └── Patrol Route: "PatrolRoute_SlowLoop" (scene object)
│
└── [1] SpawnEvent
    ├── Target To Spawn: "Target_Fast" (prefab)
    ├── Count: 2
    ├── Time Between Spawn: 1.0
    └── Patrol Route: "PatrolRoute_Zigzag" (scene object)
```

**What happens at runtime**:

1. t=0.0s: Basic target #1 spawns, TargetAI receives PatrolRoute_SlowLoop
2. t=2.0s: Basic target #2 spawns
3. t=4.0s: Basic target #3 spawns
4. t=4.0s: Fast target #1 spawns (SpawnEvent[1] begins), TargetAI receives PatrolRoute_Zigzag
5. t=5.0s: Fast target #2 spawns

Each target's TargetAI.Start() reads its assigned patrolRoute and begins navigating to Waypoint_0.

##### Testing Checklist

| Test | Expected Result | If Failing |
|------|-----------------|------------|
| Enter Play Mode | Console shows "Queued X spawns of [prefab]" | Check SpawnEvents aren't empty |
| Wait for spawn timer | Console shows "Spawned [name] at [position]" | Check prefab reference is valid |
| Target appears on NavMesh | Target visible, not falling through floor | Verify NavMesh is baked, spawn point is on NavMesh |
| Target moves to waypoint | Target walks toward first waypoint | Check TargetAI + NavMeshAgent on prefab, patrol route assigned |
| Target patrols loop | Target visits waypoints in order, loops | Check patrol route has multiple children |

##### Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Patrol route has no children | Target spawns but stands still | Add waypoint child GameObjects |
| Prefab missing NavMeshAgent | Console error, no movement | Add NavMeshAgent component to prefab |
| Spawn point not on NavMesh | Target falls or warps | Move spawner to NavMesh area, or increase spawnRadius |
| Forgot to bake NavMesh | Agent can't find path | Window > AI > Navigation, bake surfaces |
| Assigned prefab instead of scene object for patrol route | Null reference at runtime | Patrol Route must be a scene hierarchy object, not a prefab |

##### Integration Notes

- **Non-AI targets**: If prefab lacks TargetAI, spawner still works—target appears but doesn't move. Useful for stationary turrets or popup targets.
- **Multiple spawners**: Each spawner operates independently. Use multiple spawners for different spawn points with different timing.
- **Shared patrol routes**: Multiple SpawnEvents (or multiple spawners) can reference the same patrol route. All targets using it will follow the same waypoints.
- **Runtime patrol changes**: TargetAI.patrolRoute is public—you could reassign it mid-game for dynamic behavior.

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

### 5.1 Architecture Overview

Fleeing behavior is implemented as a **state transition** within the existing TargetAI state machine, not as a separate system. When a target is damaged but not destroyed, the Target component notifies TargetAI to enter the Flee state.

```
┌─────────────────────────────────────────────────────────────┐
│                    DAMAGE → FLEE FLOW                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Player fires weapon                                        │
│         ↓                                                   │
│  Weapon.RaycastShot() hits Target                          │
│         ↓                                                   │
│  Target.Got(damage) called                                  │
│         ↓                                                   │
│  Health reduced: m_CurrentHealth -= damage                  │
│         ↓                                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  IF m_CurrentHealth > 0:                            │   │
│  │     → Target survives                               │   │
│  │     → Call targetAI.StartFleeing()  ← NEW HOOK      │   │
│  │     → TargetAI enters Flee state                    │   │
│  │     → NavMeshAgent pathfinds away from player       │   │
│  │     → After fleeDuration, returns to Patrol         │   │
│  ├─────────────────────────────────────────────────────┤   │
│  │  IF m_CurrentHealth <= 0:                           │   │
│  │     → Target destroyed                              │   │
│  │     → Call targetAI.StopMovement()  ← CLEANUP       │   │
│  │     → GameSystem.TargetDestroyed() updates score    │   │
│  │     → GameObject deactivated                        │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Key Insight**: The flee behavior logic already exists in `TargetAI.StartFleeing()` (deployed in Phase 2). This phase only adds the **trigger**—the connection between taking damage and entering the flee state.

### 5.2 Understanding TargetAI.StartFleeing()

The `TargetAI.cs` script (already deployed) contains this method:

```csharp
/// <summary>
/// Called by Target.cs when damaged but not destroyed
/// Triggers flee behavior using NavMesh pathfinding
/// </summary>
public void StartFleeing()
{
    if (currentState == AIState.Flee) return; // Already fleeing

    // Null check for player reference
    if (_player == null)
    {
        Debug.LogWarning($"{gameObject.name} cannot flee - player reference is null");
        return;
    }
    
    // Calculate flee direction (AWAY from player)
    // Vector math: (target position - player position) points away from player
    Vector3 fleeDirection = transform.position - _player.position;
    fleeDirection.y = 0; // Keep on horizontal plane
    fleeDirection.Normalize();
    
    // Calculate flee destination
    _fleeDestination = transform.position + (fleeDirection * fleeDistance);
    
    // Sample to nearest valid point on NavMesh
    NavMeshHit hit;
    if (NavMesh.SamplePosition(_fleeDestination, out hit, fleeDistance, NavMesh.AllAreas))
    {
        _fleeDestination = hit.position;
    }
    
    // Transition to Flee state
    SetState(AIState.Flee);
}
```

**What happens in the Flee state:**

1. NavMeshAgent speed increases to `fleeSpeed` (default: 7)
2. Agent pathfinds to `_fleeDestination` (away from player)
3. Timer counts down for `fleeDuration` seconds (default: 3)
4. When timer expires, state transitions back to Patrol
5. Agent resumes waypoint navigation

This is all handled by the TargetAI state machine—no additional code needed for the flee behavior itself.

### 5.3 Modified Target.cs

The only code change required is in `Target.cs`. This modification:

1. Caches a reference to the TargetAI component (if present)
2. Calls `StartFleeing()` when damaged but not destroyed
3. Calls `StopMovement()` before destruction for clean NavMeshAgent shutdown

#### 5.3.1 Complete Modified Target.cs

```csharp
// Location: Assets/Creator Kit - FPS/Scripts/System/Target.cs
// Modification: Add TargetAI integration for flee behavior
// Changes marked with // NAVMESH ADDITION comments

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shootable target with health, damage handling, and AI integration
/// 
/// NAVMESH MODIFICATIONS:
/// - Added TargetAI reference caching
/// - Added flee trigger when damaged but not destroyed
/// - Added movement cleanup before destruction
/// </summary>
public class Target : MonoBehaviour
{
    // ========================================================================
    // PUBLIC FIELDS - Inspector Configuration
    // ========================================================================
    
    [Header("Health Settings")]
    [Tooltip("Maximum health points")]
    public float health = 5.0f;
    
    [Header("Scoring")]
    [Tooltip("Points awarded when destroyed")]
    public int pointValue;
    
    [Header("Audio")]
    [Tooltip("Sound player for hit effects")]
    public RandomPlayer HitPlayer;
    
    [Header("Destruction Effects")]
    [Tooltip("Objects to enable when destroyed (e.g., broken version)")]
    public GameObject[] EnableOnDeath;
    
    // ========================================================================
    // PRIVATE STATE
    // ========================================================================
    
    /// <summary>Current health (decremented by damage)</summary>
    private float m_CurrentHealth;
    
    /// <summary>Has this target been destroyed?</summary>
    private bool m_Destroyed = false;
    
    /// <summary>
    /// Reference to AI controller (if present)
    /// NAVMESH ADDITION: Cached for flee behavior trigger
    /// </summary>
    private TargetAI targetAI;
    
    // ========================================================================
    // PROPERTIES
    // ========================================================================
    
    /// <summary>
    /// Public accessor for destroyed state
    /// Used by TargetSpawner to track active targets
    /// </summary>
    public bool Destroyed => m_Destroyed;
    
    // ========================================================================
    // INITIALIZATION
    // ========================================================================
    
    /// <summary>
    /// Called when object is first created
    /// Sets layer and caches component references
    /// </summary>
    void Awake()
    {
        // Set layer recursively for raycast detection
        // Layer 10 is "Target" layer used by Weapon.RaycastShot()
        Helpers.RecursiveLayerChange(transform, LayerMask.NameToLayer("Target"));
        
        // NAVMESH ADDITION: Cache TargetAI reference
        // Uses GetComponentInParent because TargetAI may be on parent GameObject
        // (Target component is often on a child mesh object)
        targetAI = GetComponentInParent<TargetAI>();
        
        if (targetAI != null)
        {
            Debug.Log($"Target {gameObject.name}: AI component found, flee behavior enabled");
        }
    }
    
    /// <summary>
    /// Called when object becomes active
    /// Resets health for respawning/pooling scenarios
    /// </summary>
    void OnEnable()
    {
        m_CurrentHealth = health;
        m_Destroyed = false;
    }
    
    // ========================================================================
    // DAMAGE HANDLING
    // ========================================================================
    
    /// <summary>
    /// Called by Weapon.RaycastShot() when this target is hit
    /// Reduces health and triggers appropriate response
    /// 
    /// NAVMESH MODIFICATION: Added flee trigger for wounded targets
    /// </summary>
    /// <param name="damage">Amount of damage to apply</param>
    public void Got(float damage)
    {
        // Apply damage
        m_CurrentHealth -= damage;
        
        // Play hit sound effect
        if (HitPlayer != null)
            HitPlayer.PlayRandom();
        
        // ================================================================
        // SURVIVAL CHECK - Target wounded but not destroyed
        // ================================================================
        if (m_CurrentHealth > 0)
        {
            // NAVMESH ADDITION: Trigger flee behavior
            if (targetAI != null)
            {
                Debug.Log($"{gameObject.name} wounded ({m_CurrentHealth}/{health} HP) - fleeing!");
                targetAI.StartFleeing();
            }
            
            return; // Don't destroy - target survives
        }
        
        // ================================================================
        // DESTRUCTION - Target health depleted
        // ================================================================
        
        // Mark as destroyed
        m_Destroyed = true;
        
        // NAVMESH ADDITION: Stop AI movement before destruction
        // Prevents NavMeshAgent errors from operating on inactive GameObject
        if (targetAI != null)
        {
            targetAI.StopMovement();
        }
        
        // Enable death effects (broken version, particles, etc.)
        foreach (var go in EnableOnDeath)
        {
            go.SetActive(true);
            go.transform.SetParent(null); // Detach so it persists
        }
        
        // Notify game system for scoring
        GameSystem.Instance.TargetDestroyed(pointValue);
        
        // Deactivate this target
        gameObject.SetActive(false);
    }
}
```

#### 5.3.2 Changes Summary

| Line/Section | Change | Purpose |
|--------------|--------|---------|
| Field declaration | Added `private TargetAI targetAI;` | Store AI reference |
| Awake() | Added `GetComponentInParent<TargetAI>()` | Cache reference once |
| Got() - survival branch | Added `targetAI.StartFleeing()` call | Trigger flee on wound |
| Got() - destruction branch | Added `targetAI.StopMovement()` call | Clean shutdown |

#### 5.3.3 Why GetComponentInParent?

The Target component and TargetAI component may not be on the same GameObject:

```
Target_Prefab (root)          ← TargetAI + NavMeshAgent here
├── Model                     
│   └── TargetMesh            ← Target component + Collider here
└── Effects
```

Raycasts hit the collider (on TargetMesh), so `Target.Got()` runs on that child object. `GetComponentInParent<TargetAI>()` searches upward to find TargetAI on the root.

### 5.4 Flee Behavior Configuration

These fields in TargetAI control flee behavior (configured in Inspector on prefab):

| Field | Default | Description |
|-------|---------|-------------|
| `fleeDistance` | 20f | How far (in units) to flee from player |
| `fleeSpeed` | 7f | Movement speed while fleeing |
| `fleeDuration` | 3f | Seconds to remain in flee state |

**Tuning suggestions:**

- **Fast, short flee**: `fleeDistance=10, fleeSpeed=8, fleeDuration=2` — Quick dodge, aggressive enemies
- **Long, cautious flee**: `fleeDistance=30, fleeSpeed=5, fleeDuration=5` — Cowardly enemies, gives player time to chase
- **Room-scale flee**: Match `fleeDistance` to typical room size so enemies don't flee into walls

---

## 6. Integration & Testing Guide

### 6.1 Implementation Checklist

This checklist assumes you are completing all phases before testing. Check items as you complete them.

#### Phase 1: NavMesh Foundation

- [ ] AI Navigation Package installed (`com.unity.ai.navigation`)
- [ ] `LevelRoomNavMesh.cs` deployed to `/Scripts/AI/`
- [ ] `NavMeshRoomConnector.cs` deployed to `/Scripts/AI/`
- [ ] `LevelLayout.cs` modified with `BakeCompleteNavMesh()`
- [ ] NavMesh baked successfully (blue overlay visible in Scene view)

#### Phase 2: TargetAI Component

- [ ] `TargetAI.cs` deployed to `/Scripts/AI/`
- [ ] Target prefab has NavMeshAgent component added
- [ ] Target prefab has TargetAI component added
- [ ] NavMeshAgent settings configured:
  - [ ] Speed: 3.5
  - [ ] Angular Speed: 120
  - [ ] Acceleration: 8
  - [ ] Stopping Distance: 0.5
  - [ ] Auto Braking: enabled

#### Phase 3: Waypoint System

- [ ] `NavDestination.cs` deployed to `/Scripts/System/`
- [ ] `LevelRoom.cs` modified with waypoint support
- [ ] At least one patrol route GameObject created with waypoint children

#### Phase 4: Spawner Integration

- [ ] `TargetSpawner.cs` replaced with NavMesh version
- [ ] Old PathSystem configurations acknowledged as orphaned
- [ ] At least one SpawnEvent configured with:
  - [ ] Target prefab (with TargetAI)
  - [ ] Patrol route assigned

#### Phase 5: Room Connectivity

- [ ] NavMeshRoomConnector component added to LevelLayout GameObject
- [ ] Settings configured:
  - [ ] Link Width: 2.0
  - [ ] Auto Connect On Start: true
  - [ ] Debug Mode: true (for initial testing)
- [ ] Play mode tested—console shows link creation messages

#### Phase 6: Fleeing Behavior

- [ ] `Target.cs` modified with TargetAI integration
- [ ] Target prefab uses modified Target.cs
- [ ] Flee behavior tested (shoot target without killing)

### 6.2 Testing Procedures

Complete these tests in order. Each builds on the previous.

#### Test 1: NavMesh Verification

**Goal**: Confirm NavMesh is baked and visible.

1. Open scene with LevelLayout
2. Select a room GameObject
3. Open Navigation window: `Window > AI > Navigation`
4. Verify blue NavMesh overlay on walkable surfaces
5. If no NavMesh visible:
   - Select LevelLayout GameObject
   - Click "Bake All Room NavMeshes" button (custom inspector)
   - Or manually add NavMeshSurface to each room and bake

**Pass criteria**: Blue NavMesh overlay visible on all floors

#### Test 2: Basic Patrol Navigation

**Goal**: Confirm TargetAI can navigate waypoints.

1. Place a Target prefab (with TargetAI + NavMeshAgent) directly in scene
2. Create patrol route:
   - Create empty GameObject "TestPatrolRoute"
   - Add 4 child empty GameObjects as waypoints
   - Position waypoints in a square pattern within one room
3. Select the Target and assign patrol route to TargetAI.patrolRoute
4. Enter Play Mode
5. Observe target movement

**Pass criteria**: Target walks to each waypoint in sequence, then loops

**Common failures**:
- Target doesn't move → Check NavMeshAgent is enabled, patrol route is assigned
- Target slides/vibrates → NavMesh not baked under target's position
- Target walks through walls → NavMesh incorrectly baked on walls

#### Test 3: Player Detection and Chase

**Goal**: Confirm TargetAI detects and chases player.

1. Setup from Test 2 (patrolling target)
2. Enter Play Mode
3. Walk player toward target (within 15 units default detection radius)
4. Observe target behavior

**Pass criteria**: Target stops patrolling and moves toward player

5. Walk player far away (>25 units)

**Pass criteria**: Target returns to patrol after losing player

#### Test 4: Fleeing Behavior

**Goal**: Confirm damaged targets flee.

1. Setup from Test 3 (target that can detect/chase)
2. Configure target to survive multiple hits:
   - Select Target prefab
   - Set `health` to 10 or higher
   - Set weapon damage lower than health (or use weak weapon)
3. Enter Play Mode
4. Shoot target once (wound but don't kill)
5. Observe target behavior

**Pass criteria**: 
- Console shows "[name] wounded... fleeing!"
- Target moves away from player
- After ~3 seconds, target returns to patrol

**Common failures**:
- Target dies instead of fleeing → Increase target health or reduce weapon damage
- Target doesn't flee → Check Target.cs has TargetAI integration, check TargetAI reference isn't null
- Target flees toward player → Vector math error—verify `transform.position - _player.position` (not reversed)

#### Test 5: Multi-Room Navigation

**Goal**: Confirm NavMeshLinks enable room-to-room traversal.

1. Create patrol route spanning two connected rooms:
   - Waypoints 0-1 in Room A
   - Waypoints 2-3 in Room B
2. Position target in Room A
3. Assign multi-room patrol route
4. Enter Play Mode
5. Observe target crossing between rooms

**Pass criteria**: Target navigates through doorway to reach waypoints in other room

**Common failures**:
- Target stops at doorway → NavMeshLink not created; check NavMeshRoomConnector console output
- Target teleports between rooms → NavMeshLink startPoint/endPoint misconfigured
- "No path found" warning → Rooms not connected; verify room exits are properly aligned

#### Test 6: Spawner Integration

**Goal**: Confirm TargetSpawner creates functional AI targets.

1. Create or select TargetSpawner GameObject
2. Configure SpawnEvent:
   - Target To Spawn: AI-enabled prefab
   - Count: 3
   - Time Between Spawn: 2.0
   - Patrol Route: valid patrol route object
3. Enter Play Mode
4. Wait for spawns

**Pass criteria**:
- Console shows spawn messages
- Targets appear at spawner location
- Each target begins patrolling assigned route

#### Test 7: Complete Gameplay Loop

**Goal**: Validate full system integration.

1. Configure scene with:
   - Multiple spawners
   - Multiple patrol routes (some single-room, some multi-room)
   - Multiple target types (different health values)
2. Enter Play Mode
3. Play through scenario:
   - Let targets spawn and patrol
   - Approach to trigger chase
   - Shoot to wound (trigger flee)
   - Kill targets (verify destruction and scoring)
   - Move between rooms (verify multi-room behavior)

**Pass criteria**: All behaviors work together without errors or unexpected interactions

---

## 7. ExampleScene Migration Guide

This section provides step-by-step instructions for converting the existing ExampleScene to use the NavMesh-based AI system. 

**Prerequisites**: All code from Phases 1-6 must be deployed before starting this migration.

### 7.1 Understanding ExampleScene's Current State

Before modifying, understand what exists:

#### What ExampleScene Contains

| Element | Count | Current State |
|---------|-------|---------------|
| LevelLayout | 1 | Manages 11 room instances |
| LevelRoom instances | 11 | Including "HeartRoom", corridors, etc. |
| TargetSpawner objects | Multiple | Configured with OLD PathSystem data |
| Target prefabs | 2+ types | Referenced by GUID in spawners |
| PathSystem data | Per spawner | `path.localNodes` with waypoint coordinates |

#### What Will Happen After Migration

| Element | Change |
|---------|--------|
| LevelLayout | Gets NavMeshRoomConnector component |
| LevelRoom instances | Get baked NavMesh surfaces |
| TargetSpawner objects | Reconfigured with patrol routes (old path data ignored) |
| Target prefabs | Modified with TargetAI + NavMeshAgent |
| PathSystem data | Orphaned (Unity ignores non-existent fields) |

#### The Orphaned Data Problem

When Unity loads ExampleScene with the new TargetSpawner.cs:

```yaml
# OLD serialized data in scene file:
speed: 1
path:
  pathType: 1
  localNodes:
  - {x: 0, y: 0, z: 0}
  - {x: -5.95, y: -0.14, z: 1.21}
```

These fields no longer exist in the script, so Unity silently ignores them. The spawner loads with:
- `spawnEvents`: Empty array (needs configuration)
- `patrolRoute`: Null (needs assignment)

**This is not an error**—it's expected. You must reconfigure each spawner.

### 7.2 Pre-Migration Checklist

Verify all code is deployed before starting:

- [ ] `LevelRoomNavMesh.cs` exists in `/Scripts/AI/`
- [ ] `NavMeshRoomConnector.cs` exists in `/Scripts/AI/`
- [ ] `TargetAI.cs` exists in `/Scripts/AI/`
- [ ] `NavDestination.cs` exists in `/Scripts/System/`
- [ ] `TargetSpawner.cs` is the NEW version (check for `patrolRoute` field)
- [ ] `Target.cs` has TargetAI integration (check for `GetComponentInParent<TargetAI>()`)
- [ ] `LevelLayout.cs` has `BakeCompleteNavMesh()` method
- [ ] `LevelRoom.cs` has waypoint support fields

### 7.3 Step-by-Step Migration

#### Step 1: Backup ExampleScene

**Why**: Preserve the original in case you need to reference old configurations or revert.

1. In Project window, navigate to `Assets/Creator Kit - FPS/Scenes/`
2. Select `ExampleScene.unity`
3. Press Ctrl+D (duplicate)
4. Rename duplicate to `ExampleScene_Original_Backup.unity`
5. Open the original `ExampleScene.unity` for modification

#### Step 2: Bake NavMesh for All Rooms

**Why**: AI navigation requires NavMesh surfaces. Without baking, NavMeshAgents cannot pathfind.

1. Open ExampleScene
2. In Hierarchy, find and select the `LevelLayout` GameObject
3. In Inspector, look for the custom LevelLayout editor
4. Click **"Bake All Room NavMeshes"** button
   - If button doesn't appear, ensure LevelLayout.cs has the editor modification
5. Wait for baking to complete (may take 10-30 seconds for 11 rooms)
6. Verify baking succeeded:
   - Select any room GameObject
   - In Scene view, you should see blue NavMesh overlay on floors
   - Check Console for any baking errors

**Troubleshooting**:
- No blue overlay → Room may lack floor geometry or geometry isn't marked Navigation Static
- Baking errors → Check room prefabs have LevelRoomNavMesh component

#### Step 3: Add NavMeshRoomConnector

**Why**: Enables AI to navigate between rooms through doorways.

1. Select `LevelLayout` GameObject in Hierarchy
2. In Inspector, click **Add Component**
3. Search for and add `NavMeshRoomConnector`
4. Configure settings:
   - **Link Width**: 2.0 (matches doorway width)
   - **Auto Connect On Start**: ✓ enabled
   - **Debug Mode**: ✓ enabled (for initial testing)
5. Save the scene (Ctrl+S)

**What this does**: At runtime, NavMeshRoomConnector scans all rooms, finds aligned exits, and creates NavMeshLink components connecting them.

#### Step 4: Modify Target Prefabs

**Why**: Targets need NavMeshAgent and TargetAI to use NavMesh navigation.

1. In Project window, navigate to target prefabs:
   - `Assets/Creator Kit - FPS/Prefabs/Targets/` (or similar location)
2. For **each target prefab** used in ExampleScene:

   a. Double-click prefab to open in Prefab Mode
   
   b. Select the **root GameObject** of the prefab
   
   c. Add **NavMeshAgent** component:
      - Click Add Component → Navigation → NavMeshAgent
      - Configure settings:
        - Speed: 3.5
        - Angular Speed: 120
        - Acceleration: 8
        - Stopping Distance: 0.5
        - Auto Braking: ✓
   
   d. Add **TargetAI** component:
      - Click Add Component → search "TargetAI"
      - Configure settings:
        - Detection Radius: 15
        - Patrol Speed: 3.5
        - Chase Speed: 6
        - Flee Speed: 7
        - Flee Distance: 20
        - Flee Duration: 3
        - (Leave Patrol Route empty—assigned by spawner)
   
   e. Verify Target component exists on child:
      - Find the child with Target component
      - Confirm it will find TargetAI via GetComponentInParent
   
   f. Save prefab (Ctrl+S) and exit Prefab Mode

3. Repeat for all target prefab variants

**Prefab hierarchy should look like**:

```
TargetPrefab (root)
├── NavMeshAgent ← NEW
├── TargetAI ← NEW  
├── Model
│   └── Mesh (with Target component + Collider)
└── Effects
```

#### Step 5: Create Patrol Routes

**Why**: TargetAI needs waypoints to patrol. You'll create patrol route GameObjects that spawners reference.

**Strategy**: Create one patrol route per spawner, positioned appropriately for that spawner's location.

1. In Hierarchy, create organizational parent:
   - Right-click → Create Empty
   - Name it `--- PATROL ROUTES ---` (dashes help visual separation)

2. For each TargetSpawner in the scene, create a patrol route:

   a. Find a TargetSpawner (search Hierarchy for "Spawner")
   
   b. Note its position and which room it's in
   
   c. Create patrol route:
      - Right-click `--- PATROL ROUTES ---` → Create Empty
      - Name it descriptively: `PatrolRoute_[RoomName]_[SpawnerNumber]`
      - Example: `PatrolRoute_HeartRoom_01`
   
   d. Add waypoint children:
      - Right-click the patrol route → Create Empty
      - Name: `Waypoint_0`
      - Position it at first patrol point (in Scene view, drag to location)
      - Repeat for 3-5 more waypoints
      - Position waypoints to create logical patrol path within the room
   
   e. **Important**: Keep waypoints within NavMesh areas (on floors, not in walls)

3. For multi-room patrols (optional, for advanced testing):
   - Create waypoints that span connected rooms
   - NavMeshLinks will handle the doorway transitions

**Tips for waypoint placement**:
- Place waypoints where you want the AI to walk
- Keep them slightly away from walls (0.5-1 unit margin)
- Create natural patrol patterns (rectangles, figure-8s, room perimeters)
- For interesting behavior, place waypoints near cover or corners

#### Step 6: Reconfigure TargetSpawners

**Why**: Spawners have orphaned PathSystem data and need new patrol route assignments.

1. In Hierarchy, find each TargetSpawner GameObject
   - Search for "Spawner" to find them all
   - ExampleScene has multiple spawners

2. For **each spawner**, configure in Inspector:

   a. **Spawn Events** array:
      - Set array size (e.g., 1 for single wave, 2+ for multiple waves)
      - For each element:
        - **Target To Spawn**: Drag the modified target prefab
        - **Count**: Number to spawn (start with 1-2 for testing)
        - **Time Between Spawn**: Delay in seconds (2.0 is reasonable)
        - **Patrol Route**: Drag the corresponding patrol route GameObject
   
   b. **Spawn Radius**: 1.5 (reasonable default)
   
   c. **Show Spawn Gizmos**: ✓ (helps verify positioning)

3. Verify configuration:
   - With spawner selected, Scene view should show:
     - Yellow sphere at spawn point
     - Cyan lines to patrol waypoints (when selected)

**Example configuration for one spawner**:

```
TargetSpawner (on "SpawnPoint_HeartRoom")
├── Spawn Events: 1 element
│   └── [0]
│       ├── Target To Spawn: Target_Basic (prefab)
│       ├── Count: 2
│       ├── Time Between Spawn: 3.0
│       └── Patrol Route: PatrolRoute_HeartRoom_01 (scene object)
├── Spawn Radius: 1.5
└── Show Spawn Gizmos: ✓
```

#### Step 7: Configure Target Health for Flee Testing

**Why**: To test fleeing, targets need to survive at least one hit.

1. Open each target prefab
2. Find the child with Target component
3. Set **Health** to a value higher than weapon damage:
   - Default weapon damage is ~2-5
   - Set health to 10-15 for flee testing
   - Or set even higher to see multiple flee triggers

4. Save prefabs

**Note**: You can adjust this after testing. Higher health = more flee opportunities = more visible AI behavior during testing.

#### Step 8: Save and Test

1. Save the scene (Ctrl+S)
2. Enter Play Mode
3. Wait for spawns (based on your timeBetweenSpawn settings)
4. Observe:
   - Console should show spawn messages
   - Console should show NavMeshLink creation (from NavMeshRoomConnector)
   - Targets should appear and begin patrolling

5. Test behaviors:
   - Walk toward a target → Should trigger chase
   - Walk away → Should return to patrol
   - Shoot target (wound, don't kill) → Should flee
   - Kill target → Should be destroyed, score updated

### 7.4 Troubleshooting Migration Issues

#### Issue: Targets Don't Move

**Symptoms**: Targets spawn but stand still

**Checks**:
1. Is patrol route assigned in SpawnEvent? (Check TargetSpawner Inspector)
2. Does patrol route have child waypoints? (Check patrol route in Hierarchy)
3. Is NavMeshAgent component on prefab root?
4. Is NavMesh baked? (Check for blue overlay)
5. Is spawner position on NavMesh? (Target may spawn off-mesh)

**Console clues**:
- "No patrol route for AI target" → Patrol route not assigned
- "SetDestination failed" → NavMesh issue

#### Issue: Targets Fall Through Floor

**Symptoms**: Targets spawn then fall or disappear

**Cause**: Spawn position not on baked NavMesh

**Fix**:
1. Move spawner to a position with NavMesh coverage
2. Increase spawner's Spawn Radius (tries to find valid NavMesh position)
3. Re-bake NavMesh if floor is missing coverage

#### Issue: Targets Can't Cross Rooms

**Symptoms**: Targets patrol within room but stop at doorways

**Checks**:
1. Is NavMeshRoomConnector on LevelLayout?
2. In Play Mode, check console for "Created NavMeshLink" messages
3. Are rooms properly connected? (Exits aligned)

**Debug**:
1. Enable Debug Mode on NavMeshRoomConnector
2. In Scene view (Play Mode), look for cyan link visualizations at doorways
3. If no links visible, room exits may not be aligned properly

#### Issue: Fleeing Doesn't Trigger

**Symptoms**: Shooting target doesn't cause flee behavior

**Checks**:
1. Is target health high enough to survive the shot?
2. Does Target.cs have the TargetAI integration? (Check for `targetAI` field)
3. Is TargetAI component on the prefab?
4. Check console for "wounded... fleeing!" message

**Debug**:
Add temporary debug line in Target.Got():
```csharp
Debug.Log($"Got() called. Health: {m_CurrentHealth}/{health}, TargetAI: {targetAI != null}");
```

#### Issue: Flee Goes Wrong Direction

**Symptoms**: Target flees toward player instead of away

**Cause**: Vector math error in TargetAI.StartFleeing()

**Verify**: The flee direction calculation should be:
```csharp
Vector3 fleeDirection = transform.position - _player.position; // Correct
// NOT: _player.position - transform.position (would go toward player)
```

#### Issue: Console Spam During Play

**Symptoms**: Hundreds of debug messages

**Fix**: After confirming system works:
1. In NavMeshRoomConnector, uncheck Debug Mode
2. In TargetSpawner, uncheck Show Spawn Gizmos
3. Optionally, comment out verbose Debug.Log lines in TargetAI

### 7.5 Post-Migration Cleanup

Once everything works:

1. **Disable debug modes**:
   - NavMeshRoomConnector: Debug Mode = false
   - TargetSpawner: Show Spawn Gizmos = false (or leave on if helpful)

2. **Tune gameplay values**:
   - Adjust target health for desired difficulty
   - Adjust detection radius for desired awareness
   - Adjust flee parameters for desired behavior

3. **Add more patrol routes** for variety:
   - Different patterns in different rooms
   - Some simple (rectangle), some complex (multi-room)

4. **Delete backup scene** (optional):
   - Once satisfied, remove `ExampleScene_Original_Backup.unity`
   - Or keep for reference

5. **Document your changes**:
   - Note which spawners use which patrol routes
   - Record target prefab configurations
   - Helps future modifications

### 7.6 Migration Complete

After completing this migration:

- ExampleScene uses NavMesh-based AI navigation
- Targets patrol dynamically instead of following rigid paths
- Targets detect and chase the player
- Wounded targets flee before resuming patrol
- Targets can navigate between connected rooms
- The old PathSystem is no longer used (data orphaned, system bypassed)

**Next steps**:
- Create additional scenes using the same patterns
- Experiment with different patrol configurations
- Adjust AI parameters for gameplay balance
- Consider adding NavDestination components to waypoints for typed behavior
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


---

## Document Summary

This comprehensive guide provides:

✅ **Complete understanding** of Creator Kit FPS architecture  
✅ **Fully implemented** NavMesh AI system with all code  
✅ **Three fleeing behavior** approaches (NavMesh, Simple, Event-based)  
✅ **Step-by-step integration** with testing procedures  
✅ **Direct mapping** to Ferrone Chapter 9 learning objectives  
✅ **Troubleshooting guide** for common issues  
✅ **Recommended files** for complete project knowledge

**Next Steps for Opus Conversation:**

1. Add recommended files to project knowledge (Section 9)
2. Begin implementation with Phase 1 (NavMesh Foundation)
3. Use this document as complete reference throughout development
4. Refer to specific script sections for debugging

**Total Implementation Time**: 5-7 hours for complete system  
**Student Learning Time**: 10-15 hours with exercises and extensions

---

**End of Document**
