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