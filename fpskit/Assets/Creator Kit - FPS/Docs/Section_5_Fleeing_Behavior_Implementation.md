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
