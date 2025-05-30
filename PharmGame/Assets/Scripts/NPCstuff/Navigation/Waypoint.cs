// --- START OF FILE Waypoint.cs ---

using UnityEngine;

namespace Game.Navigation // Or a suitable namespace for navigation/waypoints
{
    /// <summary>
    /// Represents a single point in the world used as part of a navigation path.
    /// Attach this script to an empty GameObject to define a waypoint.
    /// </summary>
    [AddComponentMenu("Navigation/Waypoint")] // Adds to the 'Add Component' menu
    public class Waypoint : MonoBehaviour
    {
        [Tooltip("Optional unique identifier for this specific waypoint.")]
        [SerializeField] private string id;

        /// <summary>
        /// Gets the unique identifier for this waypoint (optional).
        /// </summary>
        public string ID => id;

        /// <summary>
        /// Gets the Transform component of this waypoint GameObject.
        /// </summary>
        public Transform Transform => transform;

        // Optional: Add gizmo drawing for visualization in the editor
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan; // Or any color you prefer
            Gizmos.DrawSphere(transform.position, 0.5f); // Draw a sphere at the waypoint position

            // Optional: Draw the ID as text
            #if UNITY_EDITOR
            if (!string.IsNullOrEmpty(id))
            {
                 UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f, id);
            }
            #endif
        }

        // Optional: Draw lines to connected waypoints if this waypoint stored connections
        // (We are storing connections in PathSO, so this might not be needed here)
    }
}

// --- END OF FILE Waypoint.cs ---