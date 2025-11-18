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
