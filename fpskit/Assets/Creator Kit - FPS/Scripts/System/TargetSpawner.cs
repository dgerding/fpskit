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
