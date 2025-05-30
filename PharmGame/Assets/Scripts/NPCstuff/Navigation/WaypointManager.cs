// --- START OF FILE WaypointManager.cs ---

using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Needed for LINQ methods
using System; // Needed for System.Enum (though not directly used here, good practice)
using Game.Navigation; // Needed for Waypoint and PathSO

namespace Game.Navigation // Use the same namespace
{
    /// <summary>
    /// Singleton manager responsible for finding and registering all Waypoints in the scene
    /// by tag, and managing access to PathSO assets.
    /// Provides lookup functionality for Waypoints and Paths by their unique IDs.
    /// </summary>
    public class WaypointManager : MonoBehaviour
    {
        // --- Singleton Instance ---
        public static WaypointManager Instance { get; private set; }

        [Header("Path Assets")]
        [Tooltip("Drag all your PathSO assets into this list.")]
        [SerializeField] private List<PathSO> pathAssets;

        [Header("Waypoint Settings")]
        [Tooltip("The tag used to identify Waypoint GameObjects in the scene.")]
        [SerializeField] private string waypointTag = "Waypoint"; // --- NEW: Tag field ---

        // Internal dictionaries for quick lookup
        private Dictionary<string, PathSO> pathToSODictionary;
        private Dictionary<string, Waypoint> waypointDictionary;


        private void Awake()
        {
            // Implement singleton pattern
            if (Instance == null)
            {
                Instance = this;
                // Optional: DontDestroyOnLoad(gameObject); // Consider if this manager should persist across scenes
            }
            else
            {
                Debug.LogWarning("WaypointManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            // Initialize dictionaries
            pathToSODictionary = new Dictionary<string, PathSO>();
            waypointDictionary = new Dictionary<string, Waypoint>();

            // Load and register paths and waypoints
            LoadPaths();
            FindAndRegisterWaypoints(); // --- This method will be modified ---

            Debug.Log("WaypointManager: Awake completed.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                // Clear dictionaries and singleton reference
                pathToSODictionary.Clear();
                waypointDictionary.Clear();
                Instance = null;
                Debug.Log("WaypointManager: OnDestroy completed. Dictionaries cleared.");
            }
        }

        /// <summary>
        /// Loads PathSO assets from the inspector list into the lookup dictionary.
        /// Validates for null entries and duplicate Path IDs.
        /// </summary>
        private void LoadPaths()
        {
            if (pathAssets == null || pathAssets.Count == 0)
            {
                Debug.LogWarning("WaypointManager: No PathSO assets assigned in the inspector.", this);
                return;
            }

            foreach (var pathSO in pathAssets)
            {
                if (pathSO == null)
                {
                    Debug.LogWarning("WaypointManager: Found null entry in Path Assets list. Skipping.", this);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(pathSO.PathID))
                {
                    Debug.LogError($"WaypointManager: PathSO asset '{pathSO.name}' has an empty or whitespace Path ID! Skipping.", pathSO);
                    continue;
                }

                if (pathToSODictionary.ContainsKey(pathSO.PathID))
                {
                    Debug.LogError($"WaypointManager: Duplicate Path ID '{pathSO.PathID}' found for asset '{pathSO.name}'. Previous asset was '{pathToSODictionary[pathSO.PathID].name}'. Skipping duplicate.", pathSO);
                    continue;
                }

                pathToSODictionary.Add(pathSO.PathID, pathSO);
            }

            Debug.Log($"WaypointManager: Loaded {pathToSODictionary.Count} unique PathSO assets.");
        }

        /// <summary>
        /// Finds all Waypoint GameObjects in the scene by tag and registers their Waypoint components
        /// in the lookup dictionary. Validates for duplicate Waypoint IDs.
        /// </summary>
        private void FindAndRegisterWaypoints()
        {
            if (string.IsNullOrWhiteSpace(waypointTag))
            {
                 Debug.LogError("WaypointManager: Waypoint Tag is not set! Cannot find waypoints by tag.", this);
                 return;
            }

            // --- MODIFIED: Find GameObjects by tag instead of components directly ---
            GameObject[] waypointObjects = GameObject.FindGameObjectsWithTag(waypointTag);
            // --- END MODIFIED ---


            if (waypointObjects == null || waypointObjects.Length == 0)
            {
                Debug.LogWarning($"WaypointManager: No GameObjects found with tag '{waypointTag}'.", this);
                return;
            }

            foreach (var go in waypointObjects) // Iterate through GameObjects found by tag
            {
                if (go == null)
                {
                    Debug.LogWarning("WaypointManager: Found null GameObject with waypoint tag in scene. Skipping.", this);
                    continue;
                }

                // --- MODIFIED: Get the Waypoint component from the GameObject ---
                Waypoint waypoint = go.GetComponent<Waypoint>();
                if (waypoint == null)
                {
                     Debug.LogWarning($"WaypointManager: GameObject '{go.name}' has tag '{waypointTag}' but is missing the Waypoint component! Skipping registration.", go);
                     continue;
                }
                // --- END MODIFIED ---


                if (string.IsNullOrWhiteSpace(waypoint.ID))
                {
                    Debug.LogError($"WaypointManager: Waypoint GameObject '{waypoint.gameObject.name}' has tag '{waypointTag}' but its Waypoint component has an empty or whitespace ID! It cannot be registered.", waypoint.gameObject);
                    continue;
                }

                if (waypointDictionary.ContainsKey(waypoint.ID))
                {
                    Debug.LogError($"WaypointManager: Duplicate Waypoint ID '{waypoint.ID}' found for GameObject '{waypoint.gameObject.name}'. Previous GameObject was '{waypointDictionary[waypoint.ID].gameObject.name}'. Skipping duplicate.", waypoint.gameObject);
                    continue;
                }

                waypointDictionary.Add(waypoint.ID, waypoint);
            }

            Debug.Log($"WaypointManager: Registered {waypointDictionary.Count} unique Waypoint components from GameObjects with tag '{waypointTag}'.");
        }


        /// <summary>
        /// Gets a PathSO asset by its unique Path ID.
        /// </summary>
        /// <param name="pathID">The unique identifier of the path.</param>
        /// <returns>The PathSO asset, or null if not found.</returns>
        public PathSO GetPath(string pathID)
        {
            if (string.IsNullOrWhiteSpace(pathID))
            {
                Debug.LogWarning("WaypointManager: Attempted to get path with null or empty ID.", this);
                return null;
            }

            if (pathToSODictionary.TryGetValue(pathID, out PathSO pathSO))
            {
                return pathSO;
            }

            Debug.LogWarning($"WaypointManager: Path with ID '{pathID}' not found in loaded assets.", this);
            return null;
        }

        /// <summary>
        /// Gets a Waypoint component by its unique Waypoint ID.
        /// </summary>
        /// <param name="waypointID">The unique identifier of the waypoint.</param>
        /// <returns>The Waypoint component, or null if not found.</returns>
        public Waypoint GetWaypoint(string waypointID)
        {
             if (string.IsNullOrWhiteSpace(waypointID))
            {
                Debug.LogWarning("WaypointManager: Attempted to get waypoint with null or empty ID.", this);
                return null;
            }

            if (waypointDictionary.TryGetValue(waypointID, out Waypoint waypoint))
            {
                return waypoint;
            }

            Debug.LogWarning($"WaypointManager: Waypoint with ID '{waypointID}' not found in scene dictionary. Ensure it exists, has the correct tag, and a non-empty ID.", this); // Updated warning
            return null;
        }

         /// <summary>
         /// Gets the Transform of a Waypoint component by its unique Waypoint ID.
         /// Convenience method.
         /// </summary>
         /// <param name="waypointID">The unique identifier of the waypoint.</param>
         /// <returns>The Waypoint's Transform, or null if the waypoint is not found.</returns>
         public Transform GetWaypointTransform(string waypointID)
         {
             Waypoint waypoint = GetWaypoint(waypointID);
             return waypoint?.Transform; // Use null-conditional operator
         }
    }
}

// --- END OF FILE WaypointManager.cs ---