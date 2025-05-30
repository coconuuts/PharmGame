// --- START OF FILE PathStateSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.States; // Needed for NpcStateSO
using Game.Navigation; // Needed for PathSO and WaypointManager
using System.Collections.Generic; // Needed for List
using System.Linq; // Needed for FirstOrDefault
using Game.NPC.TI; // Needed for TiNpcData

namespace Game.NPC.States // Place alongside other active states
{
    /// <summary>
    /// Active state for an NPC to follow a predefined waypoint path.
    /// Handles the initial NavMesh leg to the first waypoint, then uses the PathFollowingHandler.
    /// Now supports restoring progress for activated TI NPCs.
    /// </summary>
    [CreateAssetMenu(fileName = "PathState_", menuName = "NPC/Path States/Follow Path", order = 50)] // Order appropriately
    public class PathStateSO : NpcStateSO
    {
        // --- State Configuration ---
        [Header("Path Settings")]
        [Tooltip("The PathSO asset defining the path to follow.")]
        [SerializeField] private PathSO pathAsset;
        [Tooltip("The index of the waypoint to start the path from (0-based).")]
        [SerializeField] private int startIndex = 0;
        [Tooltip("If true, follow the path in reverse from the start index.")]
        [SerializeField] private bool followReverse = false;

        [Header("Transitions")]
        [Tooltip("The Enum key for the state to transition to upon reaching the end of the path.")]
        [SerializeField] private string nextStateEnumKey;
        [Tooltip("The Type name of the Enum key for the next state (e.g., Game.NPC.CustomerState, Game.NPC.GeneralState).")]
        [SerializeField] private string nextStateEnumType;

        // --- NpcStateSO Overrides ---
        public override System.Enum HandledState => Game.NPC.PathState.FollowPath; // <-- Use the new enum

        // Path following is generally interruptible, but specific path states might override this.
        public override bool IsInterruptible => true; // <-- Set interruptible flag

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            if (pathAsset == null)
            {
                Debug.LogError($"{context.NpcObject.name}: PathStateSO '{name}' has no Path Asset assigned! Transitioning to fallback.", context.NpcObject);
                context.TransitionToState(GeneralState.Idle); // Fallback
                return;
            }
            if (WaypointManager.Instance == null)
            {
                 Debug.LogError($"{context.NpcObject.name}: WaypointManager.Instance is null! Cannot follow path '{pathAsset.PathID}'. Transitioning to fallback.", context.NpcObject);
                 context.TransitionToState(GeneralState.Idle); // Fallback
                 return;
            }

            // --- NEW: Check if this is a TI NPC and should restore path progress ---
            if (context.Runner.IsTrueIdentityNpc && context.TiData != null && context.TiData.isFollowingPathBasic)
            {
                 // Check if the saved path ID matches the path asset configured for *this* state SO.
                 // This prevents restoring progress on the wrong path if the TI NPC was deactivated
                 // while on a different path simulation.
                 if (context.TiData.simulatedPathID == pathAsset.PathID)
                 {
                      Debug.Log($"{context.NpcObject.name}: TI NPC activating into PathState '{name}'. Restoring path progress from data: PathID='{context.TiData.simulatedPathID}', Index={context.TiData.simulatedWaypointIndex}, Reverse={context.TiData.simulatedFollowReverse}.", context.NpcObject);

                      // Call the PathFollowingHandler to restore its state directly.
                      // This bypasses the NavMesh leg.
                      bool restored = context.RestorePathProgress(
                           pathAsset, // Use the path asset configured on this SO
                           context.TiData.simulatedWaypointIndex, // Use the saved index
                           context.TiData.simulatedFollowReverse // Use the saved direction
                      );

                      if (restored)
                      {
                           // Simulation data is cleared by TiNpcManager.RequestActivateTiNpc
                           // Path following handler is now active. PathStateSO.OnUpdate will monitor it.
                           // Return early, skipping the NavMesh logic below.
                           return;
                      }
                      else
                      {
                           Debug.LogError($"{context.NpcObject.name}: Failed to restore path progress for TI NPC '{context.TiData.Id}'! PathFollowingHandler.RestorePathProgress failed. Falling back to starting path from beginning via NavMesh.", context.NpcObject);
                           // Fall through to the standard NavMesh logic below.
                      }
                 } else {
                      Debug.LogWarning($"{context.NpcObject.name}: TI NPC activating into PathState '{name}' (PathID: {pathAsset.PathID}), but saved path data is for a different path ('{context.TiData.simulatedPathID}'). Starting path from beginning via NavMesh.", context.NpcObject);
                      // Fall through to the standard NavMesh logic below.
                      // Clear invalid path simulation data on TiData here for safety, though TiNpcManager should have done it.
                      context.TiData.simulatedPathID = null;
                      context.TiData.simulatedWaypointIndex = -1;
                      context.TiData.simulatedFollowReverse = false;
                      context.TiData.isFollowingPathBasic = false;
                 }
            }
            // --- END NEW ---


            // --- Standard Logic: Start NavMesh movement to the first waypoint (only if NOT restoring progress) ---
            // This code is now only reached if it's a transient NPC OR a TI NPC that wasn't mid-path simulation,
            // or if restoration failed.
            string firstWaypointID = pathAsset.GetWaypointID(startIndex);
            if (string.IsNullOrWhiteSpace(firstWaypointID))
            {
                 Debug.LogError($"{context.NpcObject.name}: Path '{pathAsset.PathID}' has invalid start waypoint ID at index {startIndex}! Transitioning to fallback.", context.NpcObject);
                 context.TransitionToState(GeneralState.Idle); // Fallback
                 return;
            }

            Transform firstWaypointTransform = WaypointManager.Instance.GetWaypointTransform(firstWaypointID);

            if (firstWaypointTransform == null)
            {
                Debug.LogError($"{context.NpcObject.name}: Start waypoint with ID '{firstWaypointID}' (index {startIndex}) for path '{pathAsset.PathID}' not found in scene via WaypointManager! Transitioning to fallback.", context.NpcObject);
                context.TransitionToState(GeneralState.Idle); // Fallback
                return;
            }

            Debug.Log($"{context.NpcObject.name}: PathState '{name}' OnEnter. Moving to first waypoint '{firstWaypointID}' (index {startIndex}) via NavMesh.", context.NpcObject);
            // context.MoveToDestination handles enabling the agent and setting _hasReachedCurrentDestination = false
            bool moveStarted = context.MoveToDestination(firstWaypointTransform.position);

            if (!moveStarted)
            {
                 Debug.LogError($"{context.NpcObject.name}: Failed to start NavMesh movement to first waypoint {firstWaypointTransform.position}! Is it on the NavMesh? Transitioning to fallback.", context.NpcObject);
                 context.TransitionToState(GeneralState.Idle); // Fallback
                 return;
            }

            // Note: Animation speed should be set here for the NavMesh walk
            // context.SetAnimationSpeed(context.MovementHandler.Agent.speed); // Assuming Agent.speed is set correctly
            // Or a fixed walk speed animation:
            // context.PlayAnimation("Walk");
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context); // Call base OnUpdate (empty)

            // --- Check if PathFollowingHandler has reached the end ---
            // This check is only relevant *after* the initial NavMesh leg is complete (if applicable)
            // and context.StartFollowingPath or context.RestorePathProgress has been called in OnReachedDestination/OnEnter.
            // MODIFIED: Check only HasReachedEndOfPath, as IsFollowingPath becomes false in the same tick.
            if (context.PathFollowingHandler != null && context.HasReachedEndOfPath)
            {
                Debug.Log($"{context.NpcObject.name}: PathFollowingHandler signalled end of path '{pathAsset.PathID}'. Transitioning to next state.", context.NpcObject);

                // Stop the path following handler (this also re-enables the NavMeshAgent and resets HasReachedEndOfPath)
                // Although the handler might have already stopped itself, calling StopFollowingPath() again is safe
                // as it has a check !isFollowingPath at the start. Calling it ensures cleanup if it somehow didn't stop.
                context.StopFollowingPath();

                // Transition to the configured next state
                TransitionToNextState(context);
            }
            // Note: Animation speed during path following needs to be managed by the PathFollowingHandler or here.
            // If PathFollowingHandler doesn't set animation speed, you might do it here based on context.IsFollowingPath
            // and context.PathFollowingHandler.pathFollowingSpeed.
        }

        public override void OnReachedDestination(NpcStateContext context) // Called by Runner when NavMesh destination is reached
        {
            base.OnReachedDestination(context); // Call base OnReachedDestination (logging)

            // This method is ONLY called by the Runner if the NavMeshAgent reached its destination
            // AND CheckMovementArrival is true AND IsFollowingPath is false.
            // This means it's triggered when the NPC reaches the *first waypoint* via NavMesh,
            // which happens when we are NOT restoring path progress for a TI NPC.
            Debug.Log($"{context.NpcObject.name}: Reached first waypoint (index {startIndex}) via NavMesh. Starting path following.", context.NpcObject);

            // Ensure NavMesh movement is stopped (Runner already does this before calling OnReachedDestination, but defensive)
            context.MovementHandler?.StopMoving();

            // --- Start Path Following using the handler ---
            // The handler will disable the NavMeshAgent and start Rigidbody movement.
            // We pass the path asset, the *start index* (where the NPC *is*), and the direction.
            // The handler's StartFollowingPath will determine the *first target* waypoint index.
            bool pathStarted = context.StartFollowingPath(pathAsset, startIndex, followReverse);

            if (!pathStarted)
            {
                 Debug.LogError($"{context.NpcObject.name}: Failed to start path following for path '{pathAsset.PathID}'! Transitioning to fallback.", context.NpcObject);
                 context.TransitionToState(GeneralState.Idle); // Fallback
                 return;
            }

            // Note: Animation speed should be set here for the path following movement
            // context.SetAnimationSpeed(context.PathFollowingHandler.pathFollowingSpeed); // Assuming speed is public or accessible
            // Or a fixed walk speed animation:
            // context.PlayAnimation("Walk"); // Assuming walk animation is appropriate for path speed
        }

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)

            // Ensure path following is stopped when exiting this state
            context.StopFollowingPath(); // This also re-enables the NavMeshAgent

            // Note: Animation speed should be reset here
            // context.PlayAnimation("Idle"); // Assuming idle is the default after movement
        }

        /// <summary>
        /// Handles the transition to the next state upon path completion.
        /// </summary>
        private void TransitionToNextState(NpcStateContext context)
        {
             if (string.IsNullOrEmpty(nextStateEnumKey) || string.IsNullOrEmpty(nextStateEnumType))
             {
                  Debug.LogWarning($"{context.NpcObject.name}: PathState '{name}' has no next state configured. Transitioning to Idle fallback.", context.NpcObject);
                  context.TransitionToState(GeneralState.Idle); // Default fallback if no next state is set
                  return;
             }

             // Use the context helper to transition by string key/type
             context.TransitionToState(nextStateEnumKey, nextStateEnumType);
        }

        // Optional: Add validation in editor
        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (pathAsset != null)
            {
                if (startIndex < 0 || startIndex >= pathAsset.WaypointCount)
                {
                    Debug.LogWarning($"PathStateSO '{name}': Start Index ({startIndex}) is out of bounds for Path Asset '{pathAsset.name}' (Waypoint Count: {pathAsset.WaypointCount}).", this);
                }
            }

            // Validate next state configuration
            if (!string.IsNullOrEmpty(nextStateEnumKey) && !string.IsNullOrEmpty(nextStateEnumType))
            {
                 try
                 {
                      Type type = Type.GetType(nextStateEnumType);
                      if (type == null || !type.IsEnum)
                      {
                           Debug.LogError($"PathStateSO '{name}': Invalid Next State Enum Type string '{nextStateEnumType}' configured.", this);
                      } else
                      {
                           // Optional: Check if the key exists in the enum
                           if (!Enum.GetNames(type).Contains(nextStateEnumKey))
                           {
                                Debug.LogError($"PathStateSO '{name}': Next State Enum Type '{nextStateEnumType}' is valid, but key '{nextStateEnumKey}' does not exist in that enum.", this);
                           }
                      }
                 }
                 catch (Exception e)
                 {
                      Debug.LogError($"PathStateSO '{name}': Error parsing Next State config: {e.Message}", this);
                 }
            }
             else if (!string.IsNullOrEmpty(nextStateEnumKey) || !string.IsNullOrEmpty(nextStateEnumType))
             {
                  Debug.LogWarning($"PathStateSO '{name}': Next State is partially configured (key or type is missing). It will default to Idle.", this);
             }
        }
        #endif
    }
}
// --- END OF FILE PathStateSO.cs ---