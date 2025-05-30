// --- START OF FILE NpcPathFollowingHandler.cs ---

using UnityEngine;
using System.Collections.Generic; // Needed for List
using Game.Navigation; // Needed for PathSO and WaypointManager
using Game.NPC.Handlers; // Needed for NpcMovementHandler
using System.Linq; // Needed for Select, Where, ToList

namespace Game.NPC.Handlers // Place alongside other handlers
{
    /// <summary>
    /// Handles the movement and state of an active NPC while it is following a waypoint path.
    /// Disables the NavMeshAgent and uses Rigidbody.MovePosition.
    /// Now includes logic for following paths in reverse and restoring progress after activation.
    /// Includes debug visualization for the path being followed.
    /// </summary>
    [RequireComponent(typeof(NpcMovementHandler))]
    [RequireComponent(typeof(Rigidbody))] // Requires a Rigidbody for MovePosition
    public class NpcPathFollowingHandler : MonoBehaviour
    {
        // --- References ---
        private NpcMovementHandler movementHandler;
        private Rigidbody rb;
        private WaypointManager waypointManager; // Reference to the singleton manager

        [Header("Path Following Settings")]
        [Tooltip("The speed at which the NPC moves along the path.")]
        [SerializeField] private float pathFollowingSpeed = 3.5f; // Match BasicPatrol/NavMesh speed
        [Tooltip("The distance threshold to consider a waypoint reached.")]
        [SerializeField] private float waypointArrivalThreshold = 0.75f; // Match BasicPatrol/BasicExiting threshold
        [Tooltip("The rotation speed.")]
        [SerializeField] private float rotationSpeed = 0.5f;

        // --- Internal State ---
        private PathSO currentPathSO;
        private int currentWaypointIndex; // The index of the waypoint the NPC is currently moving *towards*
        private bool followReverse;
        private bool isFollowingPath = false;
        private Transform currentTargetWaypointTransform; // Cached transform of the target waypoint

        // --- Public State Flags for States to Check ---
        /// <summary>
        /// True if the handler is currently actively following a path.
        /// </summary>
        public bool IsFollowingPath => isFollowingPath;

        /// <summary>
        /// True if the handler has reached the end of the current path.
        /// Reset when a new path is started.
        /// </summary>
        public bool HasReachedEndOfPath { get; private set; } = false;


        private void Awake()
        {
            movementHandler = GetComponent<NpcMovementHandler>();
            rb = GetComponent<Rigidbody>();

            if (movementHandler == null || rb == null)
            {
                Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Missing required components (NpcMovementHandler or Rigidbody)! Self-disabling.", this);
                enabled = false;
                return;
            }

            // Get reference to the WaypointManager singleton
            waypointManager = WaypointManager.Instance;
            if (waypointManager == null)
            {
                Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: WaypointManager instance not found! Path following will not work. Ensure WaypointManager is in the scene.", this);
                // Do NOT disable handler entirely, but path following attempts will fail.
            }

            // Configure Rigidbody for kinematic movement when following path
            // We'll toggle isKinematic in Start/StopFollowingPath
            rb.isKinematic = true; // Start as kinematic, NavMeshAgent handles non-kinematic physics

            Debug.Log($"{gameObject.name}: NpcPathFollowingHandler Awake completed. References acquired.");
        }
        
        private void OnEnable()
        {
            // Ensure state is clean on enable
            StopFollowingPath(); // Resets internal state and enables NavMeshAgent
        }

        private void OnDisable()
        {
            // Ensure state is clean on disable
            StopFollowingPath(); // Resets internal state and enables NavMeshAgent
        }

        /// <summary>
        /// Handles movement and waypoint progression when actively following a path.
        /// Called by the Runner's Update loop, respecting throttling.
        /// </summary>
        /// <param name="deltaTime">The time elapsed since the last tick (can be throttled).</param>
        public void TickMovement(float deltaTime)
        {
            if (!isFollowingPath || currentPathSO == null || waypointManager == null)
            {
                // Not following a path, or dependencies missing
                return;
            }

            // --- Get Target Waypoint Transform ---
            // If currentTargetWaypointTransform is null, it means we just advanced to the next waypoint index
            // and need to look up the transform for that index.
            if (currentTargetWaypointTransform == null)
            {
                 if (currentWaypointIndex < 0 || currentWaypointIndex >= currentPathSO.WaypointCount)
                 {
                      Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Invalid currentWaypointIndex {currentWaypointIndex} for path '{currentPathSO.PathID}' (WaypointCount: {currentPathSO.WaypointCount})! Stopping path following.", this);
                      StopFollowingPath();
                      return;
                 }

                 string targetWaypointID = currentPathSO.GetWaypointID(currentWaypointIndex);
                 if (string.IsNullOrWhiteSpace(targetWaypointID))
                 {
                      Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Current path '{currentPathSO.PathID}' has null/empty waypoint ID at index {currentWaypointIndex}! Stopping path following.", this);
                      StopFollowingPath();
                      return;
                 }

                 currentTargetWaypointTransform = waypointManager.GetWaypointTransform(targetWaypointID);

                 if (currentTargetWaypointTransform == null)
                 {
                      Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Waypoint with ID '{targetWaypointID}' (index {currentWaypointIndex} in path '{currentPathSO.PathID}') not found in scene via WaypointManager! Stopping path following.", this);
                      StopFollowingPath();
                      return;
                 }
                 // Debug.Log($"NpcPathFollowingHandler on {gameObject.name}: Targeting waypoint '{targetWaypointID}' (index {currentWaypointIndex}).", this); // Too noisy
            }
            // --- End Get Target Waypoint Transform ---


            Vector3 targetPosition = currentTargetWaypointTransform.position;
            Vector3 currentPosition = transform.position;

            // --- Check for Arrival at Current Waypoint ---
            float distanceToTargetSq = (targetPosition - currentPosition).sqrMagnitude;

            if (distanceToTargetSq <= waypointArrivalThreshold * waypointArrivalThreshold)
            {
                // Reached the current target waypoint
                // Debug.Log($"NpcPathFollowingHandler on {gameObject.name}: Reached waypoint index {currentWaypointIndex}.", this); // Too noisy

                int nextIndex = followReverse ? currentWaypointIndex - 1 : currentWaypointIndex + 1;

                bool reachedEnd = followReverse ? (nextIndex < 0) : (nextIndex >= currentPathSO.WaypointCount);

                if (!reachedEnd)
                {
                    // There is a next waypoint, update state to target it
                    currentWaypointIndex = nextIndex;
                    currentTargetWaypointTransform = null; // Clear cached transform to force lookup in next tick
                    // Debug.Log($"NpcPathFollowingHandler on {gameObject.name}: Advancing to next waypoint index {currentWaypointIndex}.", this); // Too noisy
                }
                else
                {
                    // Reached the end of the path
                    Debug.Log($"NpcPathFollowingHandler on {gameObject.name}: Reached end of path '{currentPathSO.PathID}'. Stopping path following.", this);
                    StopFollowingPath();
                    HasReachedEndOfPath = true; // Signal completion
                    return; // Stop processing movement for this tick
                }
            }
            // --- End Check for Arrival ---


            // --- Move Towards Current Target Waypoint ---
            Vector3 direction = (targetPosition - currentPosition).normalized;
            float moveStep = pathFollowingSpeed * deltaTime;

            // Calculate the new position
            Vector3 newPosition = currentPosition + direction * moveStep;

            // Use Rigidbody.MovePosition for physics-safe movement
            rb.MovePosition(newPosition);

            // Simulate rotation towards the movement direction
            if (direction.sqrMagnitude > 0.001f) // Avoid LookRotation with zero vector
            {
                 Quaternion targetRotation = Quaternion.LookRotation(direction);
                 // Use Slerp for smooth rotation, scale speed by deltaTime
                 transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * deltaTime);
            }
            // --- End Move ---
        }


        /// <summary>
        /// Starts the NPC following a specific waypoint path.
        /// Disables NavMeshAgent and begins Rigidbody movement.
        /// This method is typically used when starting a path from a non-waypoint location (initial NavMesh leg).
        /// </summary>
        /// <param name="path">The PathSO asset to follow.</param>
        /// <param name="startIndex">The index of the waypoint the NPC is starting *from*.</param>
        /// <param name="reverse">If true, follow the path in reverse.</param>
        /// <returns>True if path following was successfully started, false otherwise.</returns>
        public bool StartFollowingPath(PathSO path, int startIndex = 0, bool reverse = false)
        {
            if (path == null || path.WaypointCount < 2 || waypointManager == null)
            {
                Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Cannot start path following. Path is null ({path == null}), has less than 2 waypoints ({path?.WaypointCount ?? 0}), or WaypointManager is null ({waypointManager == null}).", this);
                StopFollowingPath(); // Ensure state is clean
                return false;
            }

            // Determine the index of the *first target* waypoint based on direction
            int firstTargetIndex = reverse ? startIndex - 1 : startIndex + 1;

            // Check if the path is effectively completed immediately (e.g., starting at the very end)
            bool reachedEndImmediately = reverse ? (firstTargetIndex < 0) : (firstTargetIndex >= path.WaypointCount);

            if (reachedEndImmediately)
            {
                 Debug.LogWarning($"NpcPathFollowingHandler on {gameObject.name}: Path '{path.PathID}' is effectively completed immediately (start index {startIndex}, reverse {reverse}). Signalling completion.", this);
                 StopFollowingPath(); // Ensure state is clean
                 HasReachedEndOfPath = true; // Signal completion right away
                 return true; // Consider this a successful "start" that immediately finishes
            }


            // Get the transform for the first target waypoint to validate it exists
            string firstTargetWaypointID = path.GetWaypointID(firstTargetIndex);
            Transform firstTargetTransform = waypointManager.GetWaypointTransform(firstTargetWaypointID);

            if (firstTargetTransform == null)
            {
                 Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: First target waypoint with ID '{firstTargetWaypointID}' (index {firstTargetIndex}) for path '{path.PathID}' not found via WaypointManager! Cannot start path following.", this);
                 StopFollowingPath(); // Ensure state is clean
                 return false;
            }


            // --- Setup State ---
            currentPathSO = path;
            currentWaypointIndex = firstTargetIndex; // We are moving *towards* this index
            followReverse = reverse;
            isFollowingPath = true;
            HasReachedEndOfPath = false; // Reset completion flag
            currentTargetWaypointTransform = firstTargetTransform; // Cache the first target transform

            // --- Configure Components ---
            movementHandler.StopMoving(); // Stop any NavMesh movement
            movementHandler.DisableAgent(); // Disable the NavMesh Agent

            rb.isKinematic = true; // Ensure Rigidbody is kinematic for MovePosition

            Debug.Log($"NpcPathFollowingHandler on {gameObject.name}: Started following path '{path.PathID}' from index {startIndex} (targeting index {currentWaypointIndex}), reverse: {reverse}.", this);

            return true; // Successfully started
        }

        /// <summary>
        /// Restores the path following state from saved data (e.g., after activation).
        /// Bypasses the initial NavMesh leg and starts Rigidbody movement directly.
        /// </summary>
        /// <param name="path">The PathSO asset to follow.</param>
        /// <param name="waypointIndex">The index of the waypoint the NPC was moving *towards* when deactivated.</param>
        /// <param name="reverse">If true, follow the path in reverse.</param>
        /// <returns>True if path following was successfully restored, false otherwise.</returns>
        public bool RestorePathProgress(PathSO path, int waypointIndex, bool reverse)
        {
             if (path == null || path.WaypointCount < 2 || waypointManager == null)
             {
                  Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Cannot restore path progress. Path is null ({path == null}), has less than 2 waypoints ({path?.WaypointCount ?? 0}), or WaypointManager is null ({waypointManager == null}).", this);
                  StopFollowingPath(); // Ensure state is clean
                  return false;
             }

             // --- Validate the saved waypoint index ---
             // The saved index is the one they were moving *towards*.
             if (waypointIndex < 0 || waypointIndex >= path.WaypointCount)
             {
                  Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Cannot restore path progress. Saved waypoint index {waypointIndex} is out of bounds for path '{path.PathID}' (WaypointCount: {path.WaypointCount}). Starting path from beginning instead.", this);
                  // Fallback: Start the path from the beginning (index 0, forward)
                  return StartFollowingPath(path, 0, false); // Use existing method
             }
             // --- End Validation ---


             // Get the transform for the target waypoint to validate it exists
             string targetWaypointID = path.GetWaypointID(waypointIndex);
             Transform targetWaypointTransform = waypointManager.GetWaypointTransform(targetWaypointID);

             if (targetWaypointTransform == null)
             {
                  Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Target waypoint with ID '{targetWaypointID}' (index {waypointIndex}) for path '{path.PathID}' not found via WaypointManager during restore! Cannot restore path progress. Starting path from beginning instead.", this);
                  // Fallback: Start the path from the beginning (index 0, forward)
                  return StartFollowingPath(path, 0, false); // Use existing method
             }


             // --- Setup State ---
             currentPathSO = path;
             currentWaypointIndex = waypointIndex; // Restore the index they were moving *towards*
             followReverse = reverse;
             isFollowingPath = true;
             HasReachedEndOfPath = false; // Reset completion flag
             currentTargetWaypointTransform = targetWaypointTransform; // Cache the target transform

             // --- Configure Components ---
             movementHandler.StopMoving(); // Stop any NavMesh movement (should already be stopped/disabled)
             movementHandler.DisableAgent(); // Disable the NavMesh Agent (should already be disabled)

             rb.isKinematic = true; // Ensure Rigidbody is kinematic for MovePosition

             Debug.Log($"NpcPathFollowingHandler on {gameObject.name}: Restored path progress for path '{path.PathID}'. Resuming movement towards index {currentWaypointIndex}, reverse: {reverse}.", this);

             return true; // Successfully restored
        }


        /// <summary>
        /// Stops the NPC from following the current path.
        /// Re-enables NavMeshAgent and restores Rigidbody settings.
        /// Resets internal state.
        /// </summary>
        public void StopFollowingPath()
        {
            if (!isFollowingPath) return; // Only stop if currently following

            Debug.Log($"NpcPathFollowingHandler on {gameObject.name}: Stopping path following for path '{currentPathSO?.PathID ?? "NULL"}'.", this);

            // --- Restore Components ---
            movementHandler.EnableAgent(); // Re-enable the NavMesh Agent
            // Note: Rigidbody.isKinematic should be managed by the NavMeshAgent itself when enabled.
            // When Agent.enabled = true, it typically takes control of the Rigidbody.
            // Setting rb.isKinematic = false here might conflict. Let's rely on the Agent.
            // rb.isKinematic = false; // Restore Rigidbody to non-kinematic (if needed by Agent)

            // --- Reset State ---
            currentPathSO = null;
            currentWaypointIndex = -1; // Invalid index
            followReverse = false;
            isFollowingPath = false;
            HasReachedEndOfPath = false; // Reset completion flag
            currentTargetWaypointTransform = null; // Clear cached transform

            // Note: The state machine will handle the transition to the next state.
        }

        /// <summary>
        /// Gets the ID of the waypoint the NPC is currently moving towards.
        /// Returns null if not following a path or target is invalid.
        /// </summary>
        public string GetCurrentTargetWaypointID()
        {
            if (!isFollowingPath || currentPathSO == null || currentWaypointIndex < 0 || currentWaypointIndex >= currentPathSO.WaypointCount)
            {
                return null;
            }
            return currentPathSO.GetWaypointID(currentWaypointIndex);
        }

         /// <summary>
         /// Gets the index of the waypoint the NPC is currently moving towards.
         /// Returns -1 if not following a path or target is invalid.
         /// </summary>
         public int GetCurrentTargetWaypointIndex()
         {
              if (!isFollowingPath || currentPathSO == null || currentWaypointIndex < 0 || currentWaypointIndex >= currentPathSO.WaypointCount)
              {
                   return -1;
              }
              return currentWaypointIndex;
         }

         /// <summary>
         /// Gets the ID of the path currently being followed.
         /// Returns null if not following a path.
         /// </summary>
         public string GetCurrentPathID()
         {
              return currentPathSO?.PathID;
         }

         /// <summary>
         /// Gets the followReverse flag for the current path.
         /// Returns false if not following a path.
         /// </summary>
         public bool GetFollowReverse()
         {
              return followReverse;
         }

         /// <summary>
         /// Resets the handler's internal state. Called by the Runner's ResetRunnerTransientData.
         /// </summary>
         public void Reset()
         {
             // Ensure path following is stopped and agent is re-enabled
             StopFollowingPath();
             // The rest of the state is reset by StopFollowingPath
             Debug.Log($"{gameObject.name}: NpcPathFollowingHandler reset.");
         }

         // --- NEW DEBUG: Draw gizmos for the path being followed ---
         private void OnDrawGizmosSelected()
         {
             if (!isFollowingPath || currentPathSO == null || waypointManager == null) return;

             // Draw the full path
             List<Vector3> pathPoints = new List<Vector3>();
             for (int i = 0; i < currentPathSO.WaypointCount; i++)
             {
                 string wpID = currentPathSO.GetWaypointID(i);
                 Transform wpTransform = waypointManager.GetWaypointTransform(wpID);
                 if (wpTransform != null)
                 {
                     pathPoints.Add(wpTransform.position);
                 }
             }

             if (pathPoints.Count >= 2)
             {
                 Gizmos.color = Color.yellow; // Color for the full path
                 for (int i = 0; i < pathPoints.Count - 1; i++)
                 {
                     Gizmos.DrawLine(pathPoints[i], pathPoints[i + 1]);
                 }
             }

             // Draw the segment the NPC is currently traversing
             if (currentTargetWaypointTransform != null)
             {
                 Gizmos.color = Color.green; // Color for the current segment
                 Gizmos.DrawLine(transform.position, currentTargetWaypointTransform.position);

                 // Draw a sphere at the target waypoint
                 Gizmos.DrawSphere(currentTargetWaypointTransform.position, waypointArrivalThreshold * 0.5f);
             }

             // Draw a sphere at the NPC's current position
             Gizmos.color = Color.blue;
             Gizmos.DrawSphere(transform.position, 0.3f);

             // Optional: Draw direction arrow (requires UnityEditor.Handles)
             #if UNITY_EDITOR
             if (currentTargetWaypointTransform != null)
             {
                 Vector3 direction = (currentTargetWaypointTransform.position - transform.position).normalized;
                 if (direction.sqrMagnitude > 0.001f)
                 {
                      UnityEditor.Handles.color = Color.green;
                      UnityEditor.Handles.ArrowHandleCap(
                          0,
                          transform.position + direction * 0.5f, // Offset slightly from NPC
                          Quaternion.LookRotation(direction),
                          1f, // Size
                          EventType.Repaint
                      );
                 }
             }
             #endif
         }
         // --- END NEW DEBUG ---
    }
}

// --- END OF FILE NpcPathFollowingHandler.cs ---