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
        link.autoUpdate = false; // Static link
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