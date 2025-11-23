// ============================================================================
// LEVELROOM.CS - Modular Room Component with AI Navigation Support
// ============================================================================
// Location: Assets/Creator Kit - FPS/Scripts/LevelLayout/LevelRoom.cs
// Purpose: Represents a single modular room piece in the level layout system
// 
// ORIGINAL FUNCTIONALITY:
// - Tracks room exit points for modular level assembly
// - Manages connections to adjacent rooms via exits
// - Integrates with LevelLayout for dynamic room placement
// - Supports custom editor for visual room snapping
// 
// NAVMESH ADDITIONS (Phase 4.3.2):
// - Patrol waypoint management for AI navigation
// - Room connection points for multi-room AI pathfinding
// - Helper methods for waypoint access by TargetSpawner
//
// FERRONE REFERENCE: pg. 274-277 (Procedural patrol setup)
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Modular room component for Creator Kit FPS level construction
/// Each room prefab has this component to enable dynamic level assembly
/// 
/// EDUCATIONAL CONTEXT:
/// The Creator Kit uses a modular room system where levels are built by
/// snapping together prefabricated room pieces. This script manages:
/// 1. Exit points (doorways, hallways) for connecting to other rooms
/// 2. Patrol waypoints for AI navigation within the room
/// 3. Connection tracking for the level layout system
/// 
/// DESIGN PATTERN: Component Pattern
/// This script is a data container + behavior manager attached to room prefabs
/// Works in conjunction with LevelLayout (coordinator) and custom editor
/// </summary>
[ExecuteInEditMode]
public class LevelRoom : MonoBehaviour
{
   // ========================================================================
   // ROOM STRUCTURE - Original Functionality
   // ========================================================================

   /// <summary>
   /// Array of exit transforms marking connection points to other rooms
   /// Each exit represents a doorway, hallway, or portal
   /// 
   /// SETUP:
   /// Manually assigned in prefab - each exit is an empty GameObject
   /// positioned at the doorway location with forward vector pointing OUT
   /// 
   /// USAGE:
   /// LevelLayout editor uses these to snap rooms together
   /// When placing a room, its exit aligns with another room's exit
   /// 
   /// EXAMPLE:
   /// Room A has Exit[0] at north doorway
   /// Room B has Exit[1] at south doorway
   /// Connecting them aligns Exit[0] to Exit[1] (facing opposite directions)
   /// </summary>
   public Transform[] Exits;

   /// <summary>
   /// Tracks which rooms are connected to each exit
   /// Array parallel to Exits[] - ExitDestination[i] = room connected to Exits[i]
   /// 
   /// INITIALIZATION:
   /// Created by Placed() method when room is added to level
   /// Length always matches Exits.Length
   /// 
   /// NULL VALUES:
   /// null = exit is unconnected (open doorway, level boundary)
   /// non-null = another LevelRoom is connected at this exit
   /// 
   /// SERIALIZATION:
   /// [HideInInspector] prevents cluttering the Inspector
   /// Managed programmatically by LevelLayout editor scripts
   /// Data persists in scene file for proper level reconstruction
   /// </summary>
   [HideInInspector]
   public LevelRoom[] ExitDestination;

   /// <summary>
   /// Reference to the LevelLayout that owns this room
   /// Set by LevelLayout when room is placed
   /// 
   /// PURPOSE:
   /// Allows room to communicate back to the level coordinator
   /// Used during removal to update LevelLayout's room array
   /// Checked to see if owner is being destroyed (prevents errors)
   /// 
   /// PATTERN: Parent/Child relationship
   /// LevelLayout = parent coordinator
   /// LevelRoom = child component
   /// Owner reference enables bidirectional communication
   /// </summary>
   [HideInInspector]
   public LevelLayout Owner;

   // ========================================================================
   // AI NAVIGATION - New NavMesh Support (Phase 4.3.2)
   // ========================================================================

   /// <summary>
   /// Parent transform containing all patrol waypoints for this room
   /// Typically an empty GameObject named "PatrolRoute" with waypoint children
   /// 
   /// STRUCTURE:
   /// PatrolRoute (this transform)
   ///   ├─ Waypoint_1 (NavDestination component)
   ///   ├─ Waypoint_2 (NavDestination component)
   ///   ├─ Waypoint_3 (NavDestination component)
   ///   └─ Waypoint_4 (NavDestination component)
   /// 
   /// ASSIGNMENT:
   /// Drag the PatrolRoute GameObject from the room prefab hierarchy
   /// If null, AI targets in this room won't have patrol routes
   /// 
   /// USAGE BY AI:
   /// TargetSpawner calls GetPatrolWaypoints() to assign routes
   /// TargetAI receives the parent transform and iterates children
   /// 
   /// FERRONE REFERENCE: pg. 274 - "Create a parent object for waypoints"
   /// </summary>
   [Header("AI Navigation")]
   [Tooltip("Parent GameObject containing patrol waypoints for this room")]
   public Transform patrolWaypointsParent;

   /// <summary>
   /// Special waypoints marking transitions to adjacent rooms
   /// Used for multi-room patrol routes that span multiple rooms
   /// 
   /// OPTIONAL FEATURE:
   /// Most rooms only need internal patrol waypoints
   /// Connection points enable advanced multi-room AI behavior
   /// 
   /// EXAMPLE USE CASE:
   /// Guard patrols from Armory → Hallway → Security Room → back
   /// Connection points mark doorway transitions between rooms
   /// AI uses these to maintain patrol state across room boundaries
   /// 
   /// IMPLEMENTATION NOTE:
   /// Currently optional - basic AI implementation doesn't require this
   /// Included for future enhancement and multi-room patrol systems
   /// </summary>
   [Tooltip("Connection points to adjacent rooms (for multi-room AI)")]
   public List<NavDestination> roomConnectionPoints = new List<NavDestination>();

   // ========================================================================
   // PUBLIC INTERFACE - Room Placement
   // ========================================================================

   /// <summary>
   /// Called by LevelLayout editor when this room is placed in the level
   /// Initializes the room's connection tracking arrays
   /// 
   /// CALL CONTEXT:
   /// LevelLayoutEditor (custom editor script) calls this
   /// Happens when designer clicks to place a room prefab instance
   /// 
   /// WHAT IT DOES:
   /// 1. Stores reference to owning LevelLayout
   /// 2. Creates ExitDestination array sized to match Exits array
   /// 3. All exit destinations start as null (unconnected)
   /// 
   /// PARAMETER:
   /// layoutOwner = the LevelLayout GameObject managing this level
   /// 
   /// DESIGN NOTE:
   /// Separate initialization method (vs constructor) needed because:
   /// - MonoBehaviours can't have constructors
   /// - Allows editor script to control initialization timing
   /// - Can be called multiple times safely (array recreated)
   /// </summary>
   public void Placed(LevelLayout layoutOwner)
   {
      // Store reference to the LevelLayout that owns this room
      Owner = layoutOwner;

      // Create array to track connected rooms
      // Size matches Exits.Length so we have one slot per exit
      // All elements start as null (no connections yet)
      ExitDestination = new LevelRoom[Exits.Length];
   }

   // ========================================================================
   // PUBLIC INTERFACE - Waypoint Access (Phase 4.3.2)
   // ========================================================================

   /// <summary>
   /// Gets all patrol waypoints in this room as a list of transforms
   /// Primary method used by TargetSpawner to assign patrol routes to AI
   /// 
   /// RETURN VALUE:
   /// List of Transform references - each is a waypoint position
   /// Returns empty list if no patrol waypoints configured
   /// 
   /// USAGE EXAMPLE:
   /// LevelRoom room = GetComponent<LevelRoom>();
   /// List<Transform> waypoints = room.GetPatrolWaypoints();
   /// if (waypoints.Count > 0)
   /// {
   ///     targetAI.patrolRoute = waypoints[0].parent; // Pass parent transform
   /// }
   /// 
   /// AI INTEGRATION:
   /// TargetAI expects a parent Transform with children as waypoints
   /// This method returns the children for inspection/validation
   /// But TargetAI.patrolRoute should be assigned the parent (patrolWaypointsParent)
   /// 
   /// FERRONE REFERENCE: pg. 277 - "Access patrol locations procedurally"
   /// </summary>
   public List<Transform> GetPatrolWaypoints()
   {
      // Create empty list to hold waypoint transforms
      List<Transform> waypoints = new List<Transform>();

      // Check if room has patrol waypoints configured
      if (patrolWaypointsParent == null)
      {
         // No patrol route assigned - this is OK for rooms without AI
         // Log warning for debugging (helps designers identify missing waypoints)
         Debug.LogWarning($"Room {name} has no patrol waypoints assigned!");
         return waypoints; // Return empty list
      }

      // Iterate through all children of the patrol route parent
      // foreach with Transform iterates over transform.childCount children
      // This is Unity-specific syntax: foreach (Transform child in parent)
      foreach (Transform child in patrolWaypointsParent)
      {
         // Add each child transform to the waypoints list
         // Children should be empty GameObjects positioned as patrol points
         // May have NavDestination components for additional configuration
         waypoints.Add(child);
      }

      // Return the collected waypoints
      // Could be empty if patrolWaypointsParent has no children
      return waypoints;
   }

   /// <summary>
   /// Gets waypoints filtered by specific destination type
   /// Enables advanced AI behavior with specialized waypoint types
   /// 
   /// USAGE:
   /// Get only ambush points: GetWaypointsByType(NavDestination.DestinationType.Ambush)
   /// Get only cover points: GetWaypointsByType(NavDestination.DestinationType.CoverPoint)
   /// 
   /// RETURN VALUE:
   /// List of NavDestination components (not raw Transforms)
   /// Only waypoints with matching destinationType are included
   /// 
   /// EXAMPLE SCENARIO:
   /// Room has 8 waypoints total:
   /// - 6 standard Patrol points
   /// - 2 CoverPoint points (behind crates)
   /// 
   /// GetWaypointsByType(CoverPoint) returns only the 2 cover points
   /// AI can use these for wounded/fleeing behavior
   /// 
   /// PARAMETER:
   /// type = the DestinationType to filter by (from NavDestination.cs enum)
   /// 
   /// ADVANCED FEATURE:
   /// Basic implementation doesn't need this - included for expansion
   /// Demonstrates enum-based filtering and component querying
   /// 
   /// FERRONE REFERENCE: pg. 291 - "Refactoring and keeping it DRY"
   /// </summary>
   public List<NavDestination> GetWaypointsByType(NavDestination.DestinationType type)
   {
      // Create empty result list
      List<NavDestination> result = new List<NavDestination>();

      // Early return if no patrol waypoints exist
      if (patrolWaypointsParent == null)
         return result;

      // Iterate through all child transforms of the patrol route
      foreach (Transform child in patrolWaypointsParent)
      {
         // Try to get NavDestination component from this waypoint
         // May be null if child is just an empty GameObject (basic waypoint)
         NavDestination dest = child.GetComponent<NavDestination>();

         // Check if component exists AND matches requested type
         if (dest != null && dest.destinationType == type)
         {
            // This waypoint matches the filter - add it to results
            result.Add(dest);
         }

         // If component is null or wrong type, skip this waypoint
      }

      // Return filtered list
      // May be empty if no waypoints match the requested type
      return result;
   }

   // ========================================================================
   // EDITOR-ONLY FUNCTIONALITY
   // ========================================================================
   // Everything from here down only compiles when UNITY_EDITOR is defined
   // Not included in final game builds
   // Used by LevelLayoutEditor for room removal and cleanup
   // ========================================================================

#if UNITY_EDITOR
   /// <summary>
   /// Called by LevelLayout editor when this room is removed from the level
   /// Cleans up all connections and references to prevent broken links
   /// 
   /// CALL CONTEXT:
   /// LevelLayoutEditor custom inspector calls this during room deletion
   /// User deletes a room → Editor script calls Removed() → cleanup happens
   /// 
   /// CLEANUP RESPONSIBILITIES:
   /// 1. Disconnect this room from all connected rooms (clear their ExitDestination references)
   /// 2. Remove this room from the LevelLayout.rooms array
   /// 3. Prevent dangling references and null reference exceptions
   /// 
   /// SERIALIZATION APPROACH:
   /// Uses SerializedObject/SerializedProperty for proper undo/redo support
   /// Direct field assignment would bypass Unity's serialization system
   /// This ensures changes persist and can be undone
   /// 
   /// WHY IT MATTERS:
   /// Without proper cleanup, removed rooms leave broken references
   /// Other rooms would have ExitDestination pointing to destroyed objects
   /// LevelLayout.rooms would contain null entries
   /// 
   /// EDITOR-ONLY:
   /// This complexity only exists in editor for level design workflow
   /// Runtime gameplay doesn't dynamically add/remove rooms (layout is fixed)
   /// </summary>
   public void Removed()
   {
      // ====================================================================
      // STEP 1: Disconnect from all connected rooms
      // ====================================================================

      // Check if this room has any exit connections
      if (ExitDestination != null)
      {
         // Iterate through each exit in this room
         for (int i = 0; i < ExitDestination.Length; ++i)
         {
            // Check if this exit connects to another room
            if (ExitDestination[i] != null)
            {
               // Create SerializedObject wrapper for the connected room
               // This enables proper Unity serialization and undo support
               SerializedObject otherObj = new SerializedObject(ExitDestination[i]);

               // Get the ExitDestination array property from connected room
               // nameof() ensures correct property name (compiler-checked)
               var connectorProp = otherObj.FindProperty(nameof(ExitDestination));

               // Search through connected room's exits to find reference to this room
               for (int k = 0; k < connectorProp.arraySize; ++k)
               {
                  // Get the k-th element of the ExitDestination array
                  var prop = connectorProp.GetArrayElementAtIndex(k);

                  // Check if this exit references the room being removed
                  if (prop.objectReferenceValue == this)
                  {
                     // Found the connection - break it by setting to null
                     prop.objectReferenceValue = null;

                     // Apply changes to the SerializedObject
                     // This makes the change persistent and undoable
                     prop.serializedObject.ApplyModifiedProperties();
                  }
               }
            }
         }
      }

      // ====================================================================
      // STEP 2: Remove from LevelLayout.rooms array
      // ====================================================================

      // Check if this room has an owner and owner is not being destroyed
      // Owner.Destroyed flag prevents errors when entire level is destroyed
      if (Owner != null && !Owner.Destroyed)
      {
         // Create SerializedObject wrapper for the LevelLayout owner
         SerializedObject ownerObject = new SerializedObject(Owner);

         // Get the rooms array property from LevelLayout
         var piecesProp = ownerObject.FindProperty(nameof(Owner.rooms));

         // Search through the rooms array to find this room
         for (int i = 0; i < piecesProp.arraySize; ++i)
         {
            // Get the i-th element of the rooms array
            var prop = piecesProp.GetArrayElementAtIndex(i);

            // Check if this element references the room being removed
            if (prop.objectReferenceValue == this)
            {
               // Found it - remove from array
               // DeleteArrayElementAtIndex called TWICE is intentional:
               // 1st call: Sets element to null
               // 2nd call: Removes the null entry (shifts array down)
               // This is Unity's required pattern for removing from arrays
               piecesProp.DeleteArrayElementAtIndex(i);
               piecesProp.DeleteArrayElementAtIndex(i);

               // Stop searching - room can only appear once in array
               break;
            }
         }

         // Apply changes to persist the modified rooms array
         ownerObject.ApplyModifiedProperties();
      }
   }
#endif

   // ========================================================================
   // END OF LEVELROOM.CS
   // ========================================================================
}