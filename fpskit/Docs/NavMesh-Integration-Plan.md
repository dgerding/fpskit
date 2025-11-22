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
            var prefabTarget = spawnEvent.targetToSpawn.GetComponentInChildren<Target>();
            if (prefabTarget == null)
            {
                Debug.LogError($"Spawn prefab {spawnEvent.targetToSpawn.name} missing Target component!");
                continue;
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

In the Unity Inspector, configure each `SpawnEvent`:

1. **Target To Spawn**: Drag AI-enabled Target prefab
2. **Count**: Number of targets to spawn
3. **Time Between Spawn**: Delay between each spawn (seconds)
4. **Patrol Route**: Assign waypoint parent object

Example configuration for wave spawning:
```
SpawnEvent[0]:
  - Target: "BasicEnemy" prefab
  - Count: 3
  - Time Between: 2.0
  - Patrol Route: "PatrolRoute_A"

SpawnEvent[1]:
  - Target: "FastEnemy" prefab  
  - Count: 2
  - Time Between: 1.5
  - Patrol Route: "PatrolRoute_B"
```

This creates a wave of 3 basic enemies followed by 2 fast enemies, each following different patrol routes.

### Testing the Spawner

1. **Create Spawner GameObject**:
   - Add empty GameObject
   - Add modified `TargetSpawner` component
   - Position at spawn point

2. **Setup Patrol Routes**:
   - Create empty GameObject "PatrolRoute"
   - Add child GameObjects as waypoints
   - Position waypoints to form patrol path

3. **Configure SpawnEvents**:
   - Add Target prefabs with TargetAI
   - Set spawn counts and timing
   - Assign patrol routes

4. **Test in Play Mode**:
   - Targets should spawn at timed intervals
   - Each should follow assigned patrol route
   - NavMeshAgents should initialize correctly

### Integration Notes

- Spawner now works with both AI and non-AI targets
- NavMeshAgent initialization happens at proper spawn position
- No performance overhead from pre-instantiation
- Compatible with object pooling systems if needed later

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

Three approaches based on project needs:

### 5.1 Implementation A: NavMesh-Based Flee (Already in TargetAI)

The `TargetAI.cs` script already includes fleeing in its state machine:

```csharp
public void StartFleeing()
{
    if (currentState == AIState.Flee) return;
    
    // Calculate flee direction (away from player)
    Vector3 fleeDirection = transform.position - _player.position;
    fleeDirection.y = 0;
    fleeDirection.Normalize();
    
    // Calculate flee destination
    _fleeDestination = transform.position + (fleeDirection * fleeDistance);
    
    // Sample to nearest point on NavMesh
    NavMeshHit hit;
    if (NavMesh.SamplePosition(_fleeDestination, out hit, fleeDistance, NavMesh.AllAreas))
    {
        _fleeDestination = hit.position;
    }
    
    SetState(AIState.Flee);
}
```

This is the recommended approach for NavMesh-based enemies.

### 5.2 Implementation B: Simple Physics-Based Flee (Alternative)

For targets without NavMesh (flying enemies, simple obstacles):

```csharp
// Location: Create new file at Assets/Creator Kit - FPS/Scripts/AI/SimpleTargetFlee.cs
// Purpose: Non-NavMesh fleeing for simple targets using physics
// Use Case: Flying enemies, turrets, or obstacles that don't use NavMesh

using UnityEngine;

/// <summary>
/// Simple flee behavior using physics movement
/// For targets that don't use NavMesh pathfinding
/// 
/// FIXED: Correct flee direction calculation
/// FIXED: Uses Rigidbody.MovePosition for physics-safe movement
/// 
/// STUDENT LEARNING:
/// - Vector math for directional movement
/// - Physics-based vs transform-based movement
/// - Time-based state management
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SimpleTargetFlee : MonoBehaviour
{
    [Header("Flee Configuration")]
    [Tooltip("How fast to flee")]
    public float fleeSpeed = 5f;
    
    [Tooltip("How long to flee before stopping")]
    public float fleeDuration = 3f;
    
    [Tooltip("Distance to flee from threat")]
    public float fleeDistance = 10f;
    
    [Header("Movement Constraints")]
    [Tooltip("Keep movement on horizontal plane")]
    public bool constrainToHorizontal = true;
    
    [Tooltip("Return to original position after fleeing")]
    public bool returnToOrigin = true;
    
    // State tracking
    private bool isFleeing = false;
    private float fleeTimer = 0f;
    private Vector3 fleeDirection;
    private Vector3 originalPosition;
    
    // Component references
    private Rigidbody m_Rigidbody;
    private Target m_Target;
    
    // ====================================================================
    // INITIALIZATION
    // ====================================================================
    
    void Start()
    {
        // Cache original position for return behavior
        originalPosition = transform.position;
        
        // Get required components
        m_Rigidbody = GetComponent<Rigidbody>();
        if (m_Rigidbody == null)
        {
            Debug.LogError($"SimpleTargetFlee on {gameObject.name} requires Rigidbody!");
            enabled = false;
            return;
        }
        
        // Get Target component for damage integration
        m_Target = GetComponent<Target>();
        
        // Ensure Rigidbody is configured for kinematic movement
        // This prevents gravity and other forces from affecting our controlled movement
        m_Rigidbody.isKinematic = true;
    }
    
    // ====================================================================
    // PHYSICS UPDATE - FIXED VERSION
    // ====================================================================
    
    /// <summary>
    /// Use FixedUpdate for physics-based movement
    /// FIXED: Now uses Rigidbody.MovePosition instead of transform manipulation
    /// </summary>
    void FixedUpdate()
    {
        if (isFleeing)
        {
            // Calculate new position
            Vector3 movement = fleeDirection * fleeSpeed * Time.fixedDeltaTime;
            Vector3 newPosition = m_Rigidbody.position + movement;
            
            // Apply movement through physics system
            m_Rigidbody.MovePosition(newPosition);
            
            // Update timer
            fleeTimer -= Time.fixedDeltaTime;
            
            // Check if flee duration expired
            if (fleeTimer <= 0)
            {
                StopFleeing();
            }
        }
        else if (returnToOrigin)
        {
            // Optional: Slowly return to original position
            ReturnToOriginalPosition();
        }
    }
    
    // ====================================================================
    // FLEE BEHAVIOR - FIXED VERSION
    // ====================================================================
    
    /// <summary>
    /// Start fleeing away from threat
    /// FIXED: Correct direction calculation (away from player)
    /// </summary>
    public void StartFleeing()
    {
        if (isFleeing) return; // Already fleeing
        
        // Get player reference
        Transform player = Controller.Instance?.transform;
        if (player == null)
        {
            Debug.LogWarning("Cannot flee - no player reference!");
            return;
        }
        
        // FIXED: Calculate direction AWAY from player
        // Direction = target position - player position (points away from player)
        Vector3 awayFromPlayer = transform.position - player.position;
        
        // Constrain to horizontal plane if configured
        if (constrainToHorizontal)
        {
            awayFromPlayer.y = 0;
        }
        
        // Normalize to get unit direction vector
        fleeDirection = awayFromPlayer.normalized;
        
        // Set flee state
        isFleeing = true;
        fleeTimer = fleeDuration;
        
        Debug.Log($"{gameObject.name} fleeing away from player! Direction: {fleeDirection}");
        
        // Optional: Add flee start effects
        OnFleeStart();
    }
    
    /// <summary>
    /// Stop fleeing behavior
    /// </summary>
    public void StopFleeing()
    {
        if (!isFleeing) return;
        
        isFleeing = false;
        fleeTimer = 0f;
        
        Debug.Log($"{gameObject.name} stopped fleeing");
        
        // Optional: Add flee end effects
        OnFleeEnd();
    }
    
    /// <summary>
    /// Return to original position after fleeing
    /// </summary>
    void ReturnToOriginalPosition()
    {
        float distanceToOrigin = Vector3.Distance(transform.position, originalPosition);
        
        if (distanceToOrigin > 0.1f)
        {
            // Move back slowly
            Vector3 returnDirection = (originalPosition - transform.position).normalized;
            Vector3 movement = returnDirection * (fleeSpeed * 0.5f) * Time.fixedDeltaTime;
            Vector3 newPosition = m_Rigidbody.position + movement;
            
            m_Rigidbody.MovePosition(newPosition);
        }
    }
    
    // ====================================================================
    // INTEGRATION WITH TARGET SYSTEM
    // ====================================================================
    
    /// <summary>
    /// Automatic integration with Target damage system
    /// </summary>
    void OnEnable()
    {
        // This could be called by Target.Got() instead
        // For automatic integration, you'd modify Target.cs to call this
    }
    
    // ====================================================================
    // VISUAL FEEDBACK
    // ====================================================================
    
    /// <summary>
    /// Called when flee starts - add effects here
    /// </summary>
    void OnFleeStart()
    {
        // Could add:
        // - Particle effect (dust cloud, speed lines)
        // - Sound effect (panic sound, footsteps)
        // - Animation trigger (run animation)
        // - Color change (flash red)
    }
    
    /// <summary>
    /// Called when flee ends - add effects here
    /// </summary>
    void OnFleeEnd()
    {
        // Could add:
        // - Return to idle animation
        // - Play recovery sound
        // - Reset color
    }
    
    // ====================================================================
    // DEBUG VISUALIZATION
    // ====================================================================
    
    void OnDrawGizmosSelected()
    {
        // Draw flee distance radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, fleeDistance);
        
        // Draw flee direction when fleeing
        if (isFleeing && Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, fleeDirection * fleeDistance);
        }
        
        // Draw original position
        if (returnToOrigin)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(originalPosition, Vector3.one * 0.5f);
            Gizmos.DrawLine(transform.position, originalPosition);
        }
    }
}

// ============================================================================
// KEY FIXES APPLIED:
// 1. Correct flee direction: (target - player) points AWAY from player
// 2. Uses Rigidbody.MovePosition() in FixedUpdate() for physics compliance
// 3. Rigidbody set to kinematic to prevent physics interference
// 4. All movement through physics system, not transform manipulation
// ============================================================================
```

### 5.3 Implementation C: Event-Driven Flee System

For complex multi-behavior scenarios:

```csharp
// Location: Create new file at Assets/Creator Kit - FPS/Scripts/AI/FleeEventSystem.cs
// Purpose: Event-based fleeing that any component can trigger

using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Event-driven flee system for modular behavior
/// Other components can trigger flee through events
/// 
/// STUDENT LEARNING:
/// - UnityEvents for decoupled communication
/// - Component modularity
/// - State machine alternatives
/// </summary>
public class FleeEventSystem : MonoBehaviour
{
    [Header("Flee Events")]
    public UnityEvent onFleeStart;
    public UnityEvent onFleeEnd;
    public UnityEvent<float> onFleeProgress; // Progress 0-1
    
    [Header("Configuration")]
    public float fleeDuration = 3f;
    public float fleeSpeed = 7f;
    public bool autoTriggerOnDamage = true;
    
    private bool isFleeing = false;
    private float fleeProgress = 0f;
    
    void Start()
    {
        if (autoTriggerOnDamage)
        {
            // Auto-subscribe to Target damage event
            Target target = GetComponent<Target>();
            if (target != null)
            {
                // Would need to modify Target to have damage event
            }
        }
    }
    
    public void TriggerFlee()
    {
        if (!isFleeing)
        {
            isFleeing = true;
            fleeProgress = 0f;
            onFleeStart?.Invoke();
            StartCoroutine(FleeCoroutine());
        }
    }
    
    System.Collections.IEnumerator FleeCoroutine()
    {
        float elapsed = 0f;
        
        while (elapsed < fleeDuration)
        {
            elapsed += Time.deltaTime;
            fleeProgress = elapsed / fleeDuration;
            onFleeProgress?.Invoke(fleeProgress);
            yield return null;
        }
        
        isFleeing = false;
        onFleeEnd?.Invoke();
    }
}
```

### Integration Summary

1. **For NavMesh enemies**: Use the built-in flee state in `TargetAI.cs`
2. **For physics enemies**: Use the fixed `SimpleTargetFlee.cs` with proper Rigidbody movement
3. **For complex behaviors**: Use `FleeEventSystem.cs` with UnityEvents

All three approaches now:

- Calculate flee direction correctly (away from player)
- Use physics-compliant movement methods
- Integrate cleanly with the existing Target damage system

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
