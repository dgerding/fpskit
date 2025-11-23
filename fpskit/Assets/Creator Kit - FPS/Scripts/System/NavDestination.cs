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