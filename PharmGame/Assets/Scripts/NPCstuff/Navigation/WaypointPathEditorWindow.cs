// --- START OF FILE WaypointPathEditorWindow.cs ---

using UnityEngine;
using UnityEditor; // Needed for EditorWindow and Handle utility
using System.Collections.Generic;
using System.Linq; // Needed for LINQ operations
using Game.Navigation; // Needed for Waypoint and PathSO

namespace Game.Navigation.Editor
{
    /// <summary>
    /// Custom Editor Window for creating and visualizing Waypoint paths.
    /// Allows selecting Waypoint GameObjects in the scene to define a path
    /// and creating a PathSO asset from the definition.
    /// </summary>
    public class WaypointPathEditorWindow : EditorWindow
    {
        private string newPathID = "NewPath";
        private List<GameObject> currentPathWaypoints = new List<GameObject>(); // Temporarily holds GameObjects

        // Variables for adding waypoints by selecting in scene
        private GameObject selectedWaypointObject;

        // Drawing settings
        private Color pathLineColor = Color.yellow;
        private float pathLineWidth = 2f;
        private float arrowSize = 1f;

        [MenuItem("Tools/Navigation/Waypoint Path Editor")]
        public static void ShowWindow()
        {
            GetWindow<WaypointPathEditorWindow>("Waypoint Path Editor");
        }

        private void OnEnable()
        {
            // Subscribe to selection changes in the editor
            Selection.selectionChanged += Repaint;
            // Subscribe to Scene view updates to draw gizmos
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            Selection.selectionChanged -= Repaint;
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        void OnGUI()
        {
            GUILayout.Label("Path Definition", EditorStyles.boldLabel);

            // Path ID input
            newPathID = EditorGUILayout.TextField("Path ID", newPathID);

            GUILayout.Space(10);

            GUILayout.Label("Waypoints in Path (Ordered)", EditorStyles.boldLabel);

            // --- List Display ---
            if (currentPathWaypoints.Count == 0)
            {
                EditorGUILayout.HelpBox("Select Waypoint GameObjects in the scene and use 'Add Selected' below.", MessageType.Info);
            }
            else
            {
                // Display the list of waypoints with indices
                for (int i = 0; i < currentPathWaypoints.Count; i++)
                {
                    GameObject wpGo = currentPathWaypoints[i];
                    EditorGUILayout.BeginHorizontal();
                    // Display index and the object reference
                    EditorGUILayout.ObjectField($"[{i}]", wpGo, typeof(GameObject), true);

                    // Add Up/Down buttons
                    if (GUILayout.Button("▲", GUILayout.Width(20)))
                    {
                        if (i > 0) SwapWaypoints(i, i - 1);
                    }
                    if (GUILayout.Button("▼", GUILayout.Width(20)))
                    {
                        if (i < currentPathWaypoints.Count - 1) SwapWaypoints(i, i + 1);
                    }

                    // Add Remove button
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        RemoveWaypointAtIndex(i);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
            // --- End List Display ---

            GUILayout.Space(10);

            GUILayout.Label("Add Waypoints from Scene Selection", EditorStyles.boldLabel);

            // Get the currently selected GameObject in the scene hierarchy
            selectedWaypointObject = Selection.activeGameObject;

            // Check if the selected object is a Waypoint and display it
            bool isSelectedWaypoint = selectedWaypointObject != null && selectedWaypointObject.GetComponent<Waypoint>() != null;
            EditorGUILayout.ObjectField("Selected", isSelectedWaypoint ? selectedWaypointObject : null, typeof(GameObject), true);

            GUI.enabled = isSelectedWaypoint; // Only enable buttons if a Waypoint is selected
            if (GUILayout.Button("Add Selected Waypoint to Path"))
            {
                AddSelectedWaypoint();
            }
             if (GUILayout.Button("Add Selected Waypoints to Path (Order by Hierarchy)"))
            {
                AddSelectedWaypointsOrdered();
            }
            GUI.enabled = true; // Restore GUI enabled state

            GUILayout.Space(10);

            if (GUILayout.Button("Clear Path Definition"))
            {
                ClearPathDefinition();
            }

            GUILayout.Space(20);

            GUILayout.Label("Create Asset", EditorStyles.boldLabel);

            GUI.enabled = !string.IsNullOrWhiteSpace(newPathID) && currentPathWaypoints.Count >= 2; // Enable only if valid
            if (GUILayout.Button("Create PathSO Asset"))
            {
                CreatePathSOAsset();
            }
            GUI.enabled = true; // Restore GUI enabled state

             GUILayout.Space(20);
             GUILayout.Label("Visualization Settings (Editor Only)", EditorStyles.boldLabel);
             pathLineColor = EditorGUILayout.ColorField("Path Line Color", pathLineColor);
             pathLineWidth = EditorGUILayout.FloatField("Path Line Width", pathLineWidth);
             arrowSize = EditorGUILayout.FloatField("Arrow Size", arrowSize);


            // Request a repaint of the Scene view to update gizmos
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Draws the current path definition in the Scene view.
        /// </summary>
        void OnSceneGUI(SceneView sceneView)
        {
            if (currentPathWaypoints == null || currentPathWaypoints.Count < 2) return;

            // Use Handles for drawing in the Scene view
            Handles.color = pathLineColor;
            Handles.lighting = true; // Ensure handles are lit

            List<Vector3> points = currentPathWaypoints
                .Where(go => go != null) // Filter out any null entries just in case
                .Select(go => go.transform.position) // Get positions
                .ToList();

            if (points.Count < 2) return;

            // Draw lines between consecutive waypoints
            Handles.DrawAAPolyLine(pathLineWidth, points.ToArray());

            // Draw arrows to show direction
            for (int i = 0; i < points.Count - 1; i++)
            {
                 Vector3 p1 = points[i];
                 Vector3 p2 = points[i+1];
                 Vector3 direction = (p2 - p1).normalized;
                 Vector3 arrowPosition = Vector3.Lerp(p1, p2, 0.5f); // Draw arrow mid-segment

                 // Draw an arrow using the built-in cap
                 Handles.ArrowHandleCap(
                     0,                          // Control ID (0 or unique per handle)
                     arrowPosition,              // Position
                     Quaternion.LookRotation(direction), // Orientation
                     arrowSize,                  // Size
                     EventType.Repaint           // Event type for drawing
                 );
            }

            // Optional: Draw numbers at waypoints
             Handles.BeginGUI();
             GUIStyle labelStyle = new GUIStyle();
             labelStyle.normal.textColor = Color.white; // Or another color
             labelStyle.fontSize = 12;
             labelStyle.alignment = TextAnchor.MiddleCenter;

             for (int i = 0; i < points.Count; i++)
             {
                 Vector3 screenPos = sceneView.camera.WorldToScreenPoint(points[i] + Vector3.up * 1f); // Offset slightly above waypoint
                 if (screenPos.z > 0) // Only draw if in front of camera
                 {
                     Rect labelRect = new Rect(screenPos.x - 20, Screen.height - screenPos.y - 10, 40, 20); // Adjust size/position as needed
                     GUI.Label(labelRect, i.ToString(), labelStyle);
                 }
             }
             Handles.EndGUI();

             // Request repaint for continuous drawing when window is open
             sceneView.Repaint();
        }


        private void AddSelectedWaypoint()
        {
            if (selectedWaypointObject != null)
            {
                Waypoint wp = selectedWaypointObject.GetComponent<Waypoint>();
                if (wp != null)
                {
                     // Basic validation: ensure the waypoint has an ID
                     if (string.IsNullOrWhiteSpace(wp.ID))
                     {
                          Debug.LogError($"Waypoint '{selectedWaypointObject.name}' does not have an ID assigned! Cannot add to path definition. Please assign an ID.", selectedWaypointObject);
                          return;
                     }

                     // Optional: Prevent adding the same waypoint twice consecutively
                     if (currentPathWaypoints.LastOrDefault() != selectedWaypointObject)
                     {
                          currentPathWaypoints.Add(selectedWaypointObject);
                          Repaint(); // Repaint the window to update list display
                     }
                     else
                     {
                          Debug.LogWarning($"Waypoint '{selectedWaypointObject.name}' is already the last waypoint in the definition. Skipping.", selectedWaypointObject);
                     }
                }
            }
        }

         private void AddSelectedWaypointsOrdered()
         {
             // Get all selected GameObjects that have a Waypoint component
             GameObject[] selectedObjects = Selection.gameObjects
                 .Where(go => go != null && go.GetComponent<Waypoint>() != null)
                 .ToArray();

             if (selectedObjects.Length == 0)
             {
                  Debug.LogWarning("No Waypoint GameObjects selected.", this);
                  return;
             }

             // Sort the selected objects by their name or sibling index to attempt ordering
             // Sorting by hierarchy sibling index is more reliable if placed correctly
             System.Array.Sort(selectedObjects, (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
             // Alternative sort by name: System.Array.Sort(selectedObjects, (a, b) => a.name.CompareTo(b.name));


             foreach (var go in selectedObjects)
             {
                 Waypoint wp = go.GetComponent<Waypoint>();
                  if (string.IsNullOrWhiteSpace(wp.ID))
                  {
                       Debug.LogError($"Waypoint '{go.name}' does not have an ID assigned! Cannot add to path definition. Please assign an ID.", go);
                       // Decide whether to continue or stop here. Let's continue and just skip this one.
                       continue;
                  }

                 // Prevent adding the same waypoint twice consecutively in this batch operation
                 if (currentPathWaypoints.LastOrDefault() != go)
                 {
                     currentPathWaypoints.Add(go);
                 }
                  else
                  {
                       Debug.LogWarning($"Waypoint '{go.name}' is already the last waypoint in the definition. Skipping duplicate add.", go);
                  }
             }

             Repaint(); // Repaint the window to update list display
         }

        private void RemoveWaypointAtIndex(int index)
        {
            if (index >= 0 && index < currentPathWaypoints.Count)
            {
                currentPathWaypoints.RemoveAt(index);
                Repaint(); // Repaint the window to update list display
            }
        }

        private void SwapWaypoints(int index1, int index2)
        {
            if (index1 >= 0 && index1 < currentPathWaypoints.Count &&
                index2 >= 0 && index2 < currentPathWaypoints.Count)
            {
                GameObject temp = currentPathWaypoints[index1];
                currentPathWaypoints[index1] = currentPathWaypoints[index2];
                currentPathWaypoints[index2] = temp;
                Repaint(); // Repaint the window to update list display
            }
        }

        private void ClearPathDefinition()
        {
            currentPathWaypoints.Clear();
            newPathID = "NewPath"; // Reset default ID
            Repaint(); // Repaint the window
        }

        private void CreatePathSOAsset()
        {
            if (string.IsNullOrWhiteSpace(newPathID))
            {
                Debug.LogError("Cannot create PathSO: Path ID is empty.");
                return;
            }
            if (currentPathWaypoints.Count < 2)
            {
                Debug.LogError("Cannot create PathSO: Path requires at least 2 waypoints.");
                return;
            }
             if (currentPathWaypoints.Any(go => go == null))
             {
                  Debug.LogError("Cannot create PathSO: Path definition contains null entries. Please clear or fix.");
                  return;
             }
             if (currentPathWaypoints.Any(go => go.GetComponent<Waypoint>() == null))
             {
                 Debug.LogError("Cannot create PathSO: Path definition contains GameObjects without a Waypoint component. Please clear or fix.");
                 return;
             }
              if (currentPathWaypoints.Any(go => string.IsNullOrWhiteSpace(go.GetComponent<Waypoint>().ID)))
              {
                   Debug.LogError("Cannot create PathSO: Path definition contains Waypoints without an ID assigned. Please assign IDs.");
                   return;
              }


            // --- Create the PathSO asset ---
            PathSO newPath = CreateInstance<PathSO>();
            newPath.name = newPathID; // Asset name
            // Use the PathID property setter if it had one, otherwise set directly
            typeof(PathSO).GetProperty("PathID").SetValue(newPath, newPathID); // Set private field via reflection, or make PathID settable internal
            // Or, if pathID is private [SerializeField], you can't set it directly here without reflection.
            // Make pathID internal set in PathSO for easier editor access, or use reflection.
            // Let's assume we make PathID settable internal for cleaner code here.
            // Modify PathSO: public string PathID { get; internal set; }

            // Populate waypoint IDs from the list of GameObjects
            List<string> waypointIDs = currentPathWaypoints
                .Select(go => go.GetComponent<Waypoint>().ID)
                .ToList();

            // Assuming WaypointIDs list in PathSO is accessible (e.g., public or internal set)
            typeof(PathSO).GetProperty("WaypointIDs").SetValue(newPath, waypointIDs); // Set private field via reflection, or make WaypointIDs settable internal
             // Modify PathSO: public List<string> WaypointIDs { get; internal set; }


            // --- Save the asset ---
            string path = EditorUtility.SaveFilePanelInProject("Save PathSO", newPathID, "asset", "Please enter a file name for the PathSO asset.");
            if (string.IsNullOrEmpty(path))
            {
                DestroyImmediate(newPath); // Clean up created instance
                return;
            }

            AssetDatabase.CreateAsset(newPath, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = newPath; // Select the new asset in the Project window

            Debug.Log($"Successfully created PathSO asset: {path}", newPath);

            // Optional: Automatically add the new PathSO to the WaypointManager in the scene
            // WaypointManager manager = FindObjectOfType<WaypointManager>();
            // if (manager != null)
            // {
            //      SerializedObject serializedManager = new SerializedObject(manager);
            //      SerializedProperty pathAssetsProp = serializedManager.FindProperty("pathAssets"); // Assumes field name is pathAssets
            //      if (pathAssetsProp != null)
            //      {
            //           pathAssetsProp.InsertArrayElementAtIndex(pathAssetsProp.arraySize);
            //           pathAssetsProp.GetArrayElementAtIndex(pathAssetsProp.arraySize - 1).objectReferenceValue = newPath;
            //           serializedManager.ApplyModifiedProperties();
            //           Debug.Log($"Added '{newPathID}' to WaypointManager asset list.", manager);
            //      }
            // }


            // Clear the current definition after creating the asset
            ClearPathDefinition();
        }
    }
}

// --- END OF FILE WaypointPathEditorWindow.cs ---