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