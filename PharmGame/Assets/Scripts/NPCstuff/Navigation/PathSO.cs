// --- START OF FILE PathSO.cs (Modified) ---

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Game.Navigation
{
    /// <summary>
    /// Defines a sequence of waypoints that form a navigation path, referenced by ID.
    /// This is a Scriptable Object asset.
    /// </summary>
    [CreateAssetMenu(fileName = "Path_", menuName = "Navigation/Path", order = 1)]
    [HelpURL("https://your-documentation-link-here.com/PathSO")]
    public class PathSO : ScriptableObject
    {
        // --- MODIFIED: Make settable internally for editor ---
        [Tooltip("A unique identifier for this path.")]
        [SerializeField] private string pathID;
        public string PathID { get => pathID; internal set => pathID = value; } // <-- internal set

        [Tooltip("The ordered list of waypoint IDs that make up this path.")]
        [SerializeField] private List<string> waypointIDs;
        public List<string> WaypointIDs { get => waypointIDs; internal set => waypointIDs = value; } // <-- internal set
        // --- END MODIFIED ---


        public int WaypointCount => waypointIDs?.Count ?? 0;

        public string GetWaypointID(int index)
        {
            if (waypointIDs != null && index >= 0 && index < waypointIDs.Count)
            {
                string id = waypointIDs[index];
                if (string.IsNullOrWhiteSpace(id))
                {
                     Debug.LogWarning($"PathSO '{name}': Waypoint ID at index {index} is null or empty!", this);
                     return null;
                }
                return id;
            }
            Debug.LogWarning($"PathSO '{name}': Requested waypoint index {index} is out of bounds or list is null! WaypointCount: {WaypointCount}", this);
            return null;
        }

        private void OnEnable()
        {
            ValidatePath();
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            ValidatePath();
        }
        #endif

        private void ValidatePath()
        {
            if (string.IsNullOrWhiteSpace(pathID))
            {
                Debug.LogWarning($"PathSO '{name}': Path ID is empty or whitespace. Please assign a unique ID.", this);
            }
            if (waypointIDs == null || waypointIDs.Count < 2)
            {
                Debug.LogWarning($"PathSO '{name}': Path should contain at least 2 waypoint IDs. Current count: {WaypointCount}.", this);
            }
            else if (waypointIDs.Any(id => string.IsNullOrWhiteSpace(id)))
            {
                Debug.LogError($"PathSO '{name}': Contains null or empty waypoint ID entries! Please remove/fix entries.", this);
            }
        }
    }
}

// --- END OF FILE PathSO.cs (Modified) ---