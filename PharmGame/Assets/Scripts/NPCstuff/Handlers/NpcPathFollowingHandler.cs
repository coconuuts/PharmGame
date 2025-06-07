// --- START OF FILE NpcPathFollowingHandler.cs (Fix PathSO Clearing) ---

using UnityEngine;
using System.Collections.Generic; // Needed for List
using Game.Navigation; // Needed for PathSO and WaypointManager
using Game.NPC.Handlers; // Needed for NpcMovementHandler, MovementTickResult
using System.Linq; // Needed for Select, Where, ToList

namespace Game.NPC.Handlers // Place alongside other handlers
{
    /// <summary>
    /// Handles the movement and state of an active NPC while it is following a waypoint path.
    /// Disables the NavMeshAgent and uses Rigidbody.MovePosition.
    /// Now includes logic for following paths in reverse and restoring progress after activation.
    /// Includes debug visualization for the path being followed.
    /// Modified to calculate movement and rotation but return the results for external interpolation.
    /// ADDED: Public getter for the current PathSO.
    /// FIXED: Ensure currentPathSO is not cleared prematurely when path ends.
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
        [SerializeField] internal float waypointArrivalThreshold = 0.75f; // Match BasicPatrol/BasicExiting threshold
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


        /// <summary>
        /// Gets the PathSO asset currently being followed by this handler.
        /// Returns null if not currently following a path.
        /// </summary>
        public PathSO GetCurrentPathSO()
        {
            return currentPathSO;
        }


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
            // NOTE: Rigidbody.MovePosition requires the Rigidbody to be non-kinematic if it's affected by physics.
            // However, if we are *only* using MovePosition and not relying on physics interactions,
            // kinematic can be fine. Let's set it to non-kinematic when path following starts.
            rb.isKinematic = true; // Start as kinematic, NavMeshAgent handles non-kinematic physics

            Debug.Log($"{gameObject.name}: NpcPathFollowingHandler Awake completed. References acquired.");
        }

        private void OnEnable()
        {
            // Ensure state is clean on enable
            // Note: StopFollowingPath now clears currentPathSO when called NOT from path end.
            // This is correct for OnEnable cleanup.
            StopFollowingPath(); // Resets internal state and enables NavMeshAgent
        }

        private void OnDisable()
        {
            // Ensure state is clean on disable
            // Note: StopFollowingPath now clears currentPathSO when called NOT from path end.
            // This is correct for OnDisable cleanup.
            StopFollowingPath(); // Resets internal state and enables NavMeshAgent
        }

        /// <summary>
        /// Handles movement and waypoint progression when actively following a path.
        /// Called by the Runner's ThrottledTick loop, respecting throttling.
        /// Calculates the next position and rotation but does NOT apply them directly to the transform.
        /// </summary>
        /// <param name="deltaTime">The time elapsed since the last tick (can be throttled).</param>
        /// <returns>A MovementTickResult containing the calculated next world position and rotation for the NPC.</returns>
        public MovementTickResult TickMovement(float deltaTime) // <-- MODIFIED: Return MovementTickResult
        {
            Vector3 currentPosition = transform.position; // Use current visual position as start
            Quaternion currentRotation = transform.rotation; // Use current visual rotation as start

            if (!isFollowingPath || currentPathSO == null || waypointManager == null)
            {
                // Not following a path, or dependencies missing
                // Return current position and rotation, no movement calculated
                return new MovementTickResult(currentPosition, currentRotation);
            }

            // --- Get Target Waypoint Transform ---
            // If currentTargetWaypointTransform is null, it means we just advanced to the next waypoint index
            // and need to look up the transform for that index.
            if (currentTargetWaypointTransform == null)
            {
                 if (currentWaypointIndex < 0 || currentWaypointIndex >= currentPathSO.WaypointCount)
                 {
                      Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Invalid currentWaypointIndex {currentWaypointIndex} for path '{currentPathSO.PathID}' (WaypointCount: {currentPathSO.WaypointCount})! Stopping path following.", this);
                      StopFollowingPath(); // This will set HasReachedEndOfPath to false, which is correct here.
                      return new MovementTickResult(currentPosition, currentRotation); // Return current state on error
                 }

                 string targetWaypointID = currentPathSO.GetWaypointID(currentWaypointIndex);
                 if (string.IsNullOrWhiteSpace(targetWaypointID))
                 {
                      Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Current path '{currentPathSO.PathID}' has null/empty waypoint ID at index {currentWaypointIndex}! Stopping path following.", this);
                      StopFollowingPath(); // This will set HasReachedEndOfPath to false, which is correct here.
                      return new MovementTickResult(currentPosition, currentRotation); // Return current state on error
                 }

                 currentTargetWaypointTransform = waypointManager.GetWaypointTransform(targetWaypointID);

                 if (currentTargetWaypointTransform == null)
                 {
                      Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Waypoint with ID '{targetWaypointID}' (index {currentWaypointIndex} in path '{currentPathSO.PathID}') not found in scene via WaypointManager! Stopping path following.", this);
                      StopFollowingPath(); // This will set HasReachedEndOfPath to false, which is correct here.
                      return new MovementTickResult(currentPosition, currentRotation); // Return current state on error
                 }
                 // Debug.Log($"NpcPathFollowingHandler on {gameObject.name}: Targeting waypoint '{targetWaypointID}' (index {currentWaypointIndex}).", this); // Too noisy
            }
            // --- End Get Target Waypoint Transform ---


            Vector3 targetPosition = currentTargetWaypointTransform.position;

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
                    // Recalculate targetPosition for the new waypoint for this tick's movement calculation
                    if (currentWaypointIndex >= 0 && currentWaypointIndex < currentPathSO.WaypointCount)
                    {
                         string nextTargetWaypointID = currentPathSO.GetWaypointID(currentWaypointIndex);
                         currentTargetWaypointTransform = waypointManager.GetWaypointTransform(nextTargetWaypointID);
                         if (currentTargetWaypointTransform != null)
                         {
                              targetPosition = currentTargetWaypointTransform.position;
                         } else {
                             // Fallback if next waypoint lookup fails immediately after advancing index
                             Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Next target waypoint with ID '{nextTargetWaypointID}' (index {currentWaypointIndex}) not found immediately after advancing index! Stopping path following.", this);
                             StopFollowingPath(); // This will set HasReachedEndOfPath to false, which is correct here.
                             return new MovementTickResult(currentPosition, currentRotation); // Return current state on error
                         }
                    } else {
                         // Should not happen if !reachedEnd check was correct, but defensive
                         Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Advanced to invalid next index {currentWaypointIndex}! Stopping path following.", this);
                         StopFollowingPath(); // This will set HasReachedEndOfPath to false, which is correct here.
                         return new MovementTickResult(currentPosition, currentRotation); // Return current state on error
                    }

                }
                else
                {
                    // Reached the end of the path
                    Debug.Log($"NpcPathFollowingHandler on {gameObject.name}: Reached end of path '{currentPathSO.PathID}'. Signalling completion.", this);
                    // DO NOT CALL StopFollowingPath() HERE.
                    // The state machine needs to read currentPathSO *after* completion is signalled.
                    // StopFollowingPath will be called by the state after it reads the PathSO.
                    isFollowingPath = false; // Stop the handler's tick loop
                    HasReachedEndOfPath = true; // Signal completion
                    // currentPathSO is INTENTIONALLY NOT CLEARED here.
                    return new MovementTickResult(currentPosition, currentRotation); // Return current state as movement is finished
                }
            }
            // --- End Check for Arrival ---


            // --- Calculate Movement Towards Current Target Waypoint ---
            Vector3 direction = (targetPosition - currentPosition).normalized;
            float moveStep = pathFollowingSpeed * deltaTime;

            // Calculate the new position
            Vector3 newPosition = currentPosition + direction * moveStep;

            // --- Calculate Rotation Towards Movement Direction ---
            Quaternion targetRotation = currentRotation; // Default to current rotation
            if (direction.sqrMagnitude > 0.001f) // Avoid LookRotation with zero vector
            {
                 Quaternion desiredRotation = Quaternion.LookRotation(direction);
                 // Use Slerp to calculate the rotation for this tick
                 // The 't' value for Slerp should be based on rotation speed and delta time
                 // rotationSpeed is degrees per second, Slerp t is 0-1 over the duration.
                 // We want to rotate 'rotationSpeed * deltaTime' degrees this tick.
                 // The fraction of the remaining angle to cover this tick is (rotationSpeed * deltaTime) / remainingAngle.
                 // However, Slerp(a, b, t) interpolates from a to b over t. A simpler approach is to Slerp towards the desired rotation by a fixed amount per tick.
                 // Let's use a fixed Slerp factor per tick scaled by deltaTime.
                 float slerpFactor = rotationSpeed * deltaTime; // Scale rotation speed by deltaTime
                 targetRotation = Quaternion.Slerp(currentRotation, desiredRotation, slerpFactor);
            }
            // --- End Calculate Rotation ---

            // --- Return the calculated new position and rotation ---
            return new MovementTickResult(newPosition, targetRotation);
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
             // FIX: Clear the previous path reference here, as a new path is starting.
             currentPathSO = null; // <-- ADDED CLEAR HERE

            if (path == null || path.WaypointCount < 2 || waypointManager == null)
            {
                Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Cannot start path following. Path is null ({path == null}), has less than 2 waypoints ({path?.WaypointCount ?? 0}), or WaypointManager is null ({waypointManager == null}).", this);
                StopFollowingPath(); // Ensure state is clean (this won't clear currentPathSO if called from here now)
                return false;
            }

            // Determine the index of the *first target* waypoint based on direction
            int firstTargetIndex = reverse ? startIndex - 1 : startIndex + 1;

            // Check if the path is effectively completed immediately (e.g., starting at the very end)
            bool reachedEndImmediately = reverse ? (firstTargetIndex < 0) : (firstTargetIndex >= path.WaypointCount);

            if (reachedEndImmediately)
            {
                 Debug.LogWarning($"NpcPathFollowingHandler on {gameObject.name}: Path '{path.PathID}' is effectively completed immediately (start index {startIndex}, reverse {reverse}). Signalling completion.", this);
                 // DO NOT CALL StopFollowingPath() HERE.
                 // The state machine needs to read currentPathSO *after* completion is signalled.
                 // StopFollowingPath will be called by the state after it reads the PathSO.
                 // We need to set currentPathSO *before* signalling completion so GetCurrentPathSO works.
                 currentPathSO = path; // Set the path so it can be read
                 isFollowingPath = false; // Stop the handler's tick loop
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
            currentPathSO = path; // <-- Set the path here
            currentWaypointIndex = firstTargetIndex; // We are moving *towards* this index
            followReverse = reverse;
            isFollowingPath = true;
            HasReachedEndOfPath = false; // Reset completion flag
            currentTargetWaypointTransform = firstTargetTransform; // Cache the first target transform

            // --- Configure Components ---
            movementHandler.StopMoving(); // Stop any NavMesh movement
            movementHandler.DisableAgent(); // Disable the NavMesh Agent

            // Rigidbody should be non-kinematic for MovePosition to potentially work with physics (though we're not using rb.MovePosition anymore)
            // Let's keep it non-kinematic if Agent is disabled, as per standard practice.
            rb.isKinematic = false; // Set Rigidbody to non-kinematic

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
             // FIX: Clear the previous path reference here, as a new path is starting (restoring is like starting).
             currentPathSO = null; // <-- ADDED CLEAR HERE

             if (path == null || path.WaypointCount < 2 || waypointManager == null)
             {
                  Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Cannot restore path progress. Path is null ({path == null}), has less than 2 waypoints ({path?.WaypointCount ?? 0}), or WaypointManager is null ({waypointManager == null}).", this);
                  StopFollowingPath(); // Ensure state is clean (won't clear currentPathSO if called from here)
                  return false;
             }

             // --- Validate the saved waypoint index ---
             // The saved index is the one they were moving *towards*.
             if (waypointIndex < 0 || waypointIndex >= path.WaypointCount)
             {
                  Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Cannot restore path progress. Saved waypoint index {waypointIndex} is out of bounds for path '{path.PathID}' (WaypointCount: {path.WaypointCount}). Starting path from beginning instead.", this);
                  // Fallback: Start the path from the beginning (index 0, forward)
                  return StartFollowingPath(path, 0, false); // Use existing method (will clear currentPathSO again)
             }
             // --- End Validation ---


             // Get the transform for the target waypoint to validate it exists
             string targetWaypointID = path.GetWaypointID(waypointIndex);
             Transform targetWaypointTransform = waypointManager.GetWaypointTransform(targetWaypointID);

             if (targetWaypointTransform == null)
             {
                  Debug.LogError($"NpcPathFollowingHandler on {gameObject.name}: Target waypoint with ID '{targetWaypointID}' (index {waypointIndex}) for path '{path.PathID}' not found via WaypointManager during restore! Cannot restore path progress. Starting path from beginning instead.", this);
                  // Fallback: Start the path from the beginning (index 0, forward)
                  return StartFollowingPath(path, 0, false); // Use existing method (will clear currentPathSO again)
             }


             // --- Setup State ---
             currentPathSO = path; // <-- Set the path here
             currentWaypointIndex = waypointIndex; // Restore the index they were moving *towards*
             followReverse = reverse;
             isFollowingPath = true;
             HasReachedEndOfPath = false; // Reset completion flag
             currentTargetWaypointTransform = targetWaypointTransform; // Cache the target transform

             // --- Configure Components ---
             movementHandler.StopMoving(); // Stop any NavMesh movement (should already be stopped/disabled)
             movementHandler.DisableAgent(); // Disable the NavMesh Agent (should already be disabled)

             // Rigidbody should be non-kinematic if Agent is disabled.
             rb.isKinematic = false; // Set Rigidbody to non-kinematic

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
            // Check if currently following a path OR if HasReachedEndOfPath is true (meaning we just finished)
            // We need to allow this to run even if isFollowingPath is false, IF it's called immediately after path end.
            if (!isFollowingPath && !HasReachedEndOfPath) return; // Only stop if currently following or just finished

            Debug.Log($"NpcPathFollowingHandler on {gameObject.name}: Stopping path following for path '{currentPathSO?.PathID ?? "NULL"}'.", this);

            // --- Restore Components ---
            movementHandler.EnableAgent(); // Re-enable the NavMesh Agent
            // Note: Rigidbody.isKinematic should be managed by the NavMeshAgent itself when enabled.
            // When Agent.enabled = true, it typically takes control of the Rigidbody and sets isKinematic=false.
            // We don't need to explicitly set rb.isKinematic = false here.

            // --- Reset State ---
            // currentPathSO is now cleared in StartFollowingPath/RestorePathProgress.
            // DO NOT CLEAR currentPathSO HERE. It is needed by the state machine immediately after this call.
            currentWaypointIndex = -1; // Invalid index
            followReverse = false;
            isFollowingPath = false; // Stop the handler's tick loop
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
             // Note: StopFollowingPath is called here, and it will NOT clear currentPathSO.
             // We need to explicitly clear currentPathSO during a full reset.
             StopFollowingPath();
             currentPathSO = null; // <-- ADDED CLEAR HERE for full reset
             // The rest of the state is reset by StopFollowingPath
             Debug.Log($"{gameObject.name}: NpcPathFollowingHandler reset.");
         }

         // --- Draw gizmos for the path being followed ---
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
                 // Draw line from the *visual* position to the target
                 Gizmos.DrawLine(transform.position, currentTargetWaypointTransform.position);

                 // Draw a sphere at the target waypoint
                 Gizmos.DrawSphere(currentTargetWaypointTransform.position, waypointArrivalThreshold * 0.5f);
             }

             // Draw a sphere at the NPC's current visual position
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
    }
}

// --- END OF FILE NpcPathFollowingHandler.cs (Fix PathSO Clearing) ---