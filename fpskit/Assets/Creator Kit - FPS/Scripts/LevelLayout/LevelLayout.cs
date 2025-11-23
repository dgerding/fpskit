// ============================================================================
// LEVELLAYOUT.CS - Level Assembly & NavMesh Coordination System
// ============================================================================
// Location: Assets/Creator Kit - FPS/Scripts/LevelLayout/LevelLayout.cs
// Purpose: Manages modular room assembly and coordinates NavMesh baking
// 
// ORIGINAL FUNCTIONALITY:
// - Tracks all LevelRoom instances in the level
// - Provides custom editor for visual room placement
// - Handles room snapping and exit connections
// 
// NAVMESH ADDITIONS (Phase 4.1.2):
// - BakeCompleteNavMesh() method for coordinated NavMesh generation
// - CreateNavMeshLinksAfterDelay() for room connectivity
// - Editor button for triggering NavMesh bake in Play Mode
//
// FERRONE REFERENCE: pg. 269-270 (Runtime NavMesh generation)
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class LevelLayout : MonoBehaviour
{
    // ========================================================================
    // PUBLIC FIELDS - Room Management
    // ========================================================================

    /// <summary>
    /// Array of all LevelRoom instances in this level layout
    /// Populated by the custom editor when rooms are placed
    /// </summary>
    public LevelRoom[] rooms = new LevelRoom[0];

    // ========================================================================
    // PROPERTIES
    // ========================================================================

    /// <summary>
    /// Indicates if this LevelLayout is being destroyed
    /// Each piece when they are destroyed try to update the pieces array, 
    /// problem is if the system is destroyed, that spawned an assert. 
    /// So we use that bool to know if the system is getting destroyed.
    /// </summary>
    public bool Destroyed { get; private set; }

    // ========================================================================
    // UNITY LIFECYCLE
    // ========================================================================

    void OnDestroy()
    {
        Destroyed = true;
    }

    // ========================================================================
    // EDITOR-ONLY: Hide rooms in hierarchy
    // ========================================================================

    // This is a small hack to go around the fact that hideFlag change on 
    // prefab instance (like the room) does not get saved in the scene. 
    // So we make sure (only in editor) to hide all the room manually every frame
#if UNITY_EDITOR
    void Update()
    {
        if (Application.isPlaying)
            return;

        foreach (var room in rooms)
        {
            if (room != null)
                room.gameObject.hideFlags = HideFlags.HideInHierarchy;
        }
    }
#endif

    // ========================================================================
    // NAVMESH MANAGEMENT SECTION (NEW - Phase 4.1.2)
    // ========================================================================

    /// <summary>
    /// Bake NavMesh for all rooms in the level
    /// Call this after level layout is complete (in Play Mode)
    ///
    /// STUDENT NOTE:
    /// This demonstrates coordination between multiple AI components.
    /// Each room manages its own NavMesh via LevelRoomNavMesh, but we need
    /// central coordination to bake them all and then connect them.
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
            // NOTE: We do NOT auto-add the component to avoid duplication issues
            // Designers must add NavMeshSurface and LevelRoomNavMesh to room prefabs
            LevelRoomNavMesh navMeshComponent = room.GetComponent<LevelRoomNavMesh>();

            if (navMeshComponent == null)
            {
                Debug.LogWarning($"Room {room.name} is missing LevelRoomNavMesh component. " +
                             "Add NavMeshSurface and LevelRoomNavMesh to the room prefab for NavMesh support.");
                errorCount++;
                continue;
            }

            // Bake the NavMesh for this room
            navMeshComponent.BakeRoomNavMesh();
            successCount++;
        }

        Debug.Log($"NavMesh baking complete! Success: {successCount}, Skipped: {errorCount}");

        // After baking all room NavMeshes, create connections between rooms
        if (successCount > 0)
        {
            StartCoroutine(CreateNavMeshLinksAfterDelay());
        }
    }

    /// <summary>
    /// Create NavMeshLinks between rooms after a short delay
    /// Ensures NavMesh data is fully initialized before creating links
    /// 
    /// NOTE: This method requires NavMeshRoomConnector.cs from Phase 5.
    /// Uncomment the implementation when you reach Phase 5 of the NavMesh implementation.
    /// </summary>
    IEnumerator CreateNavMeshLinksAfterDelay()
    {
        // Let NavMesh data settle before creating links
        yield return new WaitForSeconds(0.5f);

        NavMeshRoomConnector connector = GetComponent<NavMeshRoomConnector>();
        if (connector != null)
        {
            // Connect all rooms via NavMeshLinks
            connector.ConnectRoomNavMeshes();
        }
        else
        {
            Debug.Log("NavMeshRoomConnector not found. Add it to enable multi-room navigation.");
        }

        // Placeholder until Phase 5
        Debug.Log("NavMesh baking complete. Room connectivity (Phase 5) not yet implemented.");
    }
}

// ============================================================================
// CUSTOM EDITOR - Level Layout Visual Editing Tools
// ============================================================================

#if UNITY_EDITOR
[CustomEditor(typeof(LevelLayout))]
public class LevelLayoutEditor : Editor
{
    // ========================================================================
    // EDITOR STATE
    // ========================================================================

    LevelLayout m_LevelLayout;

    bool m_EditingLayout = false;
    Material m_HighlightMaterial;
    int m_EditingMode = 0;

    List<LevelRoomGroup> m_AvailablesPalettes = new List<LevelRoomGroup>();

    LevelRoomGroup m_CurrentGroup = null;

    Vector2 m_PaletteSelectionScroll;
    Vector2 m_ObjectSelectScroll;

    LevelRoom m_SelectedRoom = null;
    LevelRoom m_CurrentInstance = null;
    GameObject m_SelectedPrefab;
    int m_CurrentUsedExit = 0;

    Vector3 m_CurrentScale = Vector3.one;

    SerializedProperty m_PieceProperty;

    // ========================================================================
    // EDITOR LIFECYCLE
    // ========================================================================

    void OnEnable()
    {
        m_LevelLayout = target as LevelLayout;

        var assets = AssetDatabase.FindAssets("t:LevelRoomGroup");
        foreach (var a in assets)
        {
            string path = AssetDatabase.GUIDToAssetPath(a);
            LevelRoomGroup palette = AssetDatabase.LoadAssetAtPath<LevelRoomGroup>(path);

            m_AvailablesPalettes.Add(palette);
        }

        m_PieceProperty = serializedObject.FindProperty("rooms");

        m_HighlightMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));

        m_HighlightMaterial.color = new Color32(255, 238, 0, 255);
        m_HighlightMaterial.SetInt("ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

        EditorApplication.playModeStateChanged += PlayModeChange;
    }

    void OnDisable()
    {
        Clean();
        EditorApplication.playModeStateChanged -= PlayModeChange;
    }

    void Clean()
    {
        if (m_CurrentInstance != null)
        {
            DestroyImmediate(m_CurrentInstance.gameObject);
            m_CurrentInstance = null;
        }
    }

    // ========================================================================
    // INSPECTOR GUI
    // ========================================================================

    public override void OnInspectorGUI()
    {
        bool editing = GUILayout.Toggle(m_EditingLayout, "Editing Layout", "Button");

        if (editing != m_EditingLayout)
        {
            if (!editing)
            {
                // Disabled editing, cleanup
                if (m_CurrentInstance != null)
                    DestroyImmediate(m_CurrentInstance.gameObject);
                m_CurrentGroup = null;
                m_CurrentInstance = null;
                m_SelectedRoom = null;
            }
            else
            {
                if (!SceneView.lastActiveSceneView.drawGizmos)
                {
                    if (EditorUtility.DisplayDialog("Warning",
                        "Gizmos are globally disabled, which prevents the layout editing tools from working. Do you want to re-enable Gizmos?",
                        "Yes", "No"))
                    {
                        SceneView.lastActiveSceneView.drawGizmos = true;
                        m_EditingLayout = true;
                    }
                }
                else
                {
                    m_EditingLayout = true;
                }

                if (m_EditingLayout && m_AvailablesPalettes.Count > 0)
                    m_CurrentGroup = m_AvailablesPalettes[0];
            }

            m_EditingLayout = editing;
        }

        if (m_EditingLayout)
        {
            EditorGUILayout.HelpBox("Press R to change which door the room use to connect to other room", MessageType.Info);

            EditorGUILayout.BeginHorizontal();

            int editingMode = GUILayout.Toolbar(m_EditingMode, new[] { "Add", "Remove" }, GUILayout.Width(120));
            if (editingMode != m_EditingMode)
            {
                if (editingMode == 1)
                {
                    if (m_CurrentInstance != null)
                        DestroyImmediate(m_CurrentInstance.gameObject);

                    m_SelectedRoom = null;
                }

                m_EditingMode = editingMode;
            }

            if (m_CurrentInstance != null)
            {
                EditorGUILayout.LabelField("Flip : ", GUILayout.Width(32));
                EditorGUI.BeginChangeCheck();

                bool flipX = GUILayout.Toggle(m_CurrentScale.x < 0, "X", "button", GUILayout.Width(32));
                bool flipY = GUILayout.Toggle(m_CurrentScale.y < 0, "Y", "button", GUILayout.Width(32));
                bool flipZ = GUILayout.Toggle(m_CurrentScale.z < 0, "Z", "button", GUILayout.Width(32));

                GUILayout.FlexibleSpace();

                if (EditorGUI.EndChangeCheck())
                {
                    m_CurrentScale = new Vector3(flipX ? -1 : 1, flipY ? -1 : 1, flipZ ? -1 : 1);
                    m_CurrentInstance.transform.localScale = m_CurrentScale;
                }
            }

            EditorGUILayout.EndHorizontal();

            // Repaint all scene views to be sure they get a notification so they can "steal" focus in edit mode
            SceneView.RepaintAll();
        }

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        GUILayout.Label("Group");
        m_PaletteSelectionScroll = GUILayout.BeginScrollView(m_PaletteSelectionScroll);

        foreach (var p in m_AvailablesPalettes)
        {
            GUI.enabled = m_CurrentGroup != p;
            if (GUILayout.Button(p.name))
            {
                if (!m_EditingLayout)
                {
                    if (!SceneView.lastActiveSceneView.drawGizmos)
                    {
                        if (EditorUtility.DisplayDialog("Warning",
                            "Gizmos are globally disabled, which prevent the layout editing tool to work. Do you want to re-enable Gizmos?",
                            "Yes", "No"))
                        {
                            SceneView.lastActiveSceneView.drawGizmos = true;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    m_EditingLayout = true;
                }

                if (m_EditingLayout)
                    m_CurrentGroup = p;
            }
        }

        GUI.enabled = true;

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        m_ObjectSelectScroll = GUILayout.BeginScrollView(m_ObjectSelectScroll, GUILayout.Width(72 * 3));
        GUILayout.BeginVertical();

        if (m_CurrentGroup != null)
        {
            bool horizontalOpen = false;

            for (int i = 0; i < m_CurrentGroup.levelPart.Length; ++i)
            {
                LevelRoom part = m_CurrentGroup.levelPart[i];

                if (i % 3 == 0 && i != 0)
                {
                    GUILayout.EndHorizontal();
                    horizontalOpen = false;
                }

                if (!horizontalOpen)
                {
                    GUILayout.BeginHorizontal();
                    horizontalOpen = true;
                }

                Texture2D preview = AssetPreview.GetAssetPreview(part.gameObject);

                GUI.enabled = part != m_SelectedRoom;
                if (GUILayout.Button(preview, GUILayout.Width(64), GUILayout.Height(64)))
                {
                    m_SelectedRoom = part;

                    if (m_CurrentInstance != null)
                        DestroyImmediate(m_CurrentInstance.gameObject);

                    m_CurrentInstance = Instantiate(m_SelectedRoom, m_LevelLayout.transform);
                    m_CurrentInstance.gameObject.isStatic = false;
                    m_CurrentInstance.gameObject.tag = "EditorOnly";
                    m_CurrentInstance.name = "TempInstance";
                    m_CurrentUsedExit = 0;

                    m_CurrentInstance.transform.localScale = m_CurrentScale;

                    m_EditingMode = 0;
                }
            }

            if (horizontalOpen)
                GUILayout.EndHorizontal();
        }

        GUI.enabled = true;

        GUILayout.EndVertical();
        GUILayout.EndScrollView();

        GUILayout.EndHorizontal();

        // ====================================================================
        // NAVMESH TOOLS SECTION (NEW - Phase 4.1.2)
        // ====================================================================

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("NavMesh Tools", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            if (GUILayout.Button("Bake Complete NavMesh", GUILayout.Height(30)))
            {
                m_LevelLayout.BakeCompleteNavMesh();
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
            "5. NavMeshLinks will be created automatically (if NavMeshRoomConnector is attached)",
            MessageType.Info
        );
    }

    // ========================================================================
    // SCENE GUI - Visual Room Placement
    // ========================================================================

    void OnSceneGUI()
    {
        if (m_EditingLayout)
        {
            if (m_EditingMode == 0)
            {
                AddPiece();
            }
            else
            {
                RemovePiece();
            }
        }
    }

    // ========================================================================
    // ADD PIECE - Place new rooms in the level
    // ========================================================================

    void AddPiece()
    {
        if (m_CurrentInstance == null)
            return;

        int controlID = GUIUtility.GetControlID(FocusType.Keyboard);

        if (GUIUtility.hotControl == 0)
            HandleUtility.AddDefaultControl(controlID);

        m_CurrentInstance.gameObject.SetActive(true);

        // Handle R key to cycle through exits
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.R)
        {
            if (m_CurrentInstance.Exits.Length > 0)
            {
                m_CurrentUsedExit = m_CurrentUsedExit + 1 >= m_CurrentInstance.Exits.Length ? 0 : m_CurrentUsedExit + 1;
            }
        }

        LevelRoom currentClosestPiece = null;
        int currentClosestExit = -1;

        if (m_LevelLayout.rooms.Length == 0)
        {
            // If we have no piece, we force the instance in 0,0,0, as it's the seed piece
            m_CurrentInstance.transform.position = m_LevelLayout.transform.TransformPoint(Vector3.zero);
        }
        else
        {
            var mousePos = Event.current.mousePosition;

            float closestSqrDist = float.MaxValue;
            for (int i = 0; i < m_LevelLayout.rooms.Length; ++i)
            {
                LevelRoom r = m_LevelLayout.rooms[i];

                if (r == null)
                    continue;

                for (int k = 0; k < r.Exits.Length; ++k)
                {
                    if (r.ExitDestination[k] != null)
                        continue;

                    var guiPts = HandleUtility.WorldToGUIPoint(r.Exits[k].transform.position);

                    float dist = (guiPts - mousePos).sqrMagnitude;

                    if (dist < closestSqrDist)
                    {
                        closestSqrDist = dist;
                        currentClosestPiece = r;
                        currentClosestExit = k;
                    }
                }
            }

            if (currentClosestPiece != null)
            {
                m_CurrentInstance.transform.rotation = Quaternion.identity;

                Transform closest = currentClosestPiece.Exits[currentClosestExit];
                Transform usedExit = m_CurrentInstance.Exits[m_CurrentUsedExit];

                Quaternion targetRotation = Quaternion.LookRotation(-closest.forward, closest.up);
                Quaternion difference = targetRotation * Quaternion.Inverse(usedExit.rotation);

                Quaternion rotation = m_CurrentInstance.transform.rotation * difference;
                m_CurrentInstance.transform.rotation = rotation;

                m_CurrentInstance.transform.position = closest.position +
                    m_CurrentInstance.transform.TransformVector(-usedExit.transform.localPosition);
            }
        }

        // If hot control is not 0, that means we clicked a gizmo and we don't want that
        if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && GUIUtility.hotControl == 0)
        {
            var c = PrefabUtility.InstantiatePrefab(m_SelectedRoom) as LevelRoom;

            int i = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Added new piece");

            Undo.RegisterCreatedObjectUndo(c.gameObject, "Added new piece");

            c.gameObject.hideFlags = HideFlags.HideInHierarchy;
            c.transform.SetParent(m_LevelLayout.transform, false);

            c.transform.position = m_CurrentInstance.transform.position;
            c.transform.rotation = m_CurrentInstance.transform.rotation;
            c.transform.localScale = m_CurrentInstance.transform.localScale;

            c.name = m_SelectedRoom.gameObject.name;
            c.gameObject.isStatic = true;

            c.Placed(m_LevelLayout);

            m_PieceProperty.serializedObject.Update();

            m_PieceProperty.InsertArrayElementAtIndex(m_PieceProperty.arraySize);
            m_PieceProperty.GetArrayElementAtIndex(m_PieceProperty.arraySize - 1).objectReferenceValue = c;

            if (currentClosestPiece != null)
            {
                Snap(currentClosestPiece, c, currentClosestExit, m_CurrentUsedExit);

                // Go through all remaining exits and find if any are close to another to link them
                for (int k = 0; k < c.Exits.Length; ++k)
                {
                    if (k == m_CurrentUsedExit)
                        continue;

                    bool exitLinked = false;
                    Transform testedExit = c.Exits[k];

                    for (int r = 0; r < m_LevelLayout.rooms.Length && !exitLinked; ++r)
                    {
                        for (int re = 0; re < m_LevelLayout.rooms[r].Exits.Length; ++re)
                        {
                            // This is an already used exit, no need to test here
                            if (m_LevelLayout.rooms[r].ExitDestination[re] != null)
                                continue;

                            // If we are close enough, let's consider those 2 exits linked
                            if (Vector3.SqrMagnitude(m_LevelLayout.rooms[r].Exits[re].position - testedExit.position) < 0.2f * 0.2f)
                            {
                                Snap(m_LevelLayout.rooms[r], c, re, k);
                                exitLinked = true;
                                break;
                            }
                        }
                    }
                }
            }

            Undo.CollapseUndoOperations(i);

            m_PieceProperty.serializedObject.ApplyModifiedProperties();
        }
    }

    // ========================================================================
    // REMOVE PIECE - Delete rooms from the level
    // ========================================================================

    void RemovePiece()
    {
        int controlID = GUIUtility.GetControlID(FocusType.Keyboard);

        if (GUIUtility.hotControl == 0)
            HandleUtility.AddDefaultControl(controlID);

        if (m_CurrentInstance != null)
        {
            m_CurrentInstance.gameObject.SetActive(false);
        }

        var mousePos = Event.current.mousePosition;
        LevelRoom closestPiece = null;
        Bounds closestBound = new Bounds();

        float closestSqrDist = float.MaxValue;
        for (int i = 0; i < m_LevelLayout.rooms.Length; ++i)
        {
            LevelRoom r = m_LevelLayout.rooms[i];

            if (r == null)
                continue;

            // This bit is inefficient, but should be enough for our purpose here in the kit. 
            // In very big scenes it could slow down the editing process. 
            // Bounds should probably be stored in local space or better should find a way to 
            // use the built-in picking but that requires more complexity than necessary for these small kits
            Bounds b = new Bounds();
            bool init = false;

            MeshRenderer[] renderers = r.GetComponentsInChildren<MeshRenderer>();

            if (renderers.Length > 0)
            {
                for (int k = 0; k < renderers.Length; ++k)
                {
                    if (!init)
                    {
                        b = renderers[k].bounds;
                        init = true;
                    }
                    else
                    {
                        b.Encapsulate(renderers[k].bounds);
                    }
                }
            }
            else
            {
                // If the piece got no renderer, it may be an "empty" piece used to introduce gaps,
                // so instead look for a collider to find its size
                Collider[] colliders = r.GetComponentsInChildren<Collider>();

                for (int k = 0; k < colliders.Length; ++k)
                {
                    if (!init)
                    {
                        b = colliders[k].bounds;
                        init = true;
                    }
                    else
                    {
                        b.Encapsulate(colliders[k].bounds);
                    }
                }
            }

            var guiPts = HandleUtility.WorldToGUIPoint(b.center);
            float dist = (guiPts - mousePos).sqrMagnitude;

            if (dist < closestSqrDist)
            {
                closestSqrDist = dist;
                closestPiece = r;
                closestBound = b;
            }
        }

        if (closestPiece != null)
        {
            // Draw highlight around the piece that would be removed
            Handles.DrawWireCube(closestBound.center, closestBound.size);
        }

        if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && GUIUtility.hotControl == 0)
        {
            if (closestPiece != null)
            {
                Undo.SetCurrentGroupName("Removed piece");

                closestPiece.Removed();

                Undo.DestroyObjectImmediate(closestPiece.gameObject);

                m_PieceProperty.serializedObject.Update();
            }
        }
    }

    // ========================================================================
    // SNAP - Connect two room exits together
    // ========================================================================

    static void Snap(LevelRoom A, LevelRoom B, int exitA, int exitB)
    {
        SerializedObject newObj = new SerializedObject(A);
        SerializedObject currentObj = new SerializedObject(B);

        var propNew = newObj.FindProperty("ExitDestination");
        var propCurrent = currentObj.FindProperty("ExitDestination");

        newObj.Update();
        currentObj.Update();

        propCurrent.GetArrayElementAtIndex(exitB).objectReferenceValue = A;
        propNew.GetArrayElementAtIndex(exitA).objectReferenceValue = B;

        var exitAObj = new SerializedObject(A.Exits[exitA].gameObject);
        var exitAActiveProp = exitAObj.FindProperty("m_IsActive");

        exitAActiveProp.boolValue = false;

        var exitBObj = new SerializedObject(B.Exits[exitB].gameObject);
        var exitBActiveProp = exitBObj.FindProperty("m_IsActive");

        exitBActiveProp.boolValue = false;

        exitAObj.ApplyModifiedProperties();
        exitBObj.ApplyModifiedProperties();
        newObj.ApplyModifiedProperties();
        currentObj.ApplyModifiedProperties();
    }

    // ========================================================================
    // PLAY MODE CHANGE HANDLER
    // ========================================================================

    void PlayModeChange(PlayModeStateChange mode)
    {
        Clean();
    }
}
#endif