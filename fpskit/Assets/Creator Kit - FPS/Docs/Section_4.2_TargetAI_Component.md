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
