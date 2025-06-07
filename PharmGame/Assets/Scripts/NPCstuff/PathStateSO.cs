// --- START OF FILE PathStateSO.cs (Final Refactored) ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.States; // Needed for NpcStateSO
using Game.Navigation; // Needed for PathSO and WaypointManager, PathTransitionDetails
using System.Collections.Generic; // Needed for List
using System.Linq; // Needed for FirstOrDefault, Contains
using Game.NPC.TI; // Needed for TiNpcData, TiNpcManager
using Game.NPC.Decisions; // Needed for DecisionPointSO, NpcDecisionHelper // Still needed for NpcDecisionHelper via PathSO

namespace Game.NPC.States // Place alongside other active states
{
    /// <summary>
    /// Active state for an NPC to follow a predefined waypoint path.
    /// Handles the initial NavMesh leg to the first waypoint, then uses the PathFollowingHandler.
    /// Now supports restoring progress for activated TI NPCs and triggers decision logic
    /// upon reaching a linked Decision Point.
    /// MODIFIED: End behavior (Decision Point or fixed transition) is now defined on the PathSO asset.
    /// Uses PathTransitionDetails to handle transitions to other paths.
    /// </summary>
    [CreateAssetMenu(fileName = "PathState_", menuName = "NPC/Path States/Follow Path", order = 50)] // Order appropriately
    public class PathStateSO : NpcStateSO
    {
        // --- State Configuration ---
        [Header("Path Settings")]
        [Tooltip("The PathSO asset defining the path to follow.")]
        [SerializeField] private PathSO pathAsset;
        [Tooltip("The index of the waypoint to start the path from (0-based).")]
        [SerializeField] private int startIndex = 0; // <-- This field is on the SO asset
        [Tooltip("If true, follow the path in reverse from the start index.")]
        [SerializeField] private bool followReverse = false; // <-- This field is on the SO asset

        // --- NpcStateSO Overrides ---
        public override System.Enum HandledState => Game.NPC.PathState.FollowPath; 

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // Validate basic dependencies
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

             // Log entry, noting that end behavior is defined on the PathSO
             Debug.Log($"{context.NpcObject.name}: Entering PathState '{name}' for path '{pathAsset.PathID}'. End behavior defined on PathSO asset.");


            // --- Check if returning from interruption using temporary flag ---
            if (context.Runner.wasInterruptedFromPathState)
            {
                 Debug.Log($"{context.NpcObject.name}: Returning from PathState interruption. Attempting to restore active path progress.", context.NpcObject);

                 // Use the temporary path data saved by NpcInterruptionHandler.TryInterrupt
                 // The pathAsset must match the interrupted path ID for a valid restore into *this* state
                 PathSO interruptedPathSO = WaypointManager.Instance.GetPath(context.Runner.interruptedPathID);

                 if (interruptedPathSO != null && interruptedPathSO.PathID == pathAsset.PathID)
                 {
                      bool restored = context.RestorePathProgress(
                          interruptedPathSO, // Use path SO from WaypointManager
                          context.Runner.interruptedWaypointIndex, // Use the saved index
                          context.Runner.interruptedFollowReverse // Use the saved direction
                      );

                       // --- Clear the temporary interruption path data AFTER attempting restore ---
                       // Clearing here handles both success and failure of RestorePathProgress
                       context.Runner.interruptedPathID = null;
                       context.Runner.interruptedWaypointIndex = -1;
                       context.Runner.interruptedFollowReverse = false;
                       context.Runner.wasInterruptedFromPathState = false; // Clear the flag
                       // --- END Clear ---

                      if (restored)
                      {
                           Debug.Log($"{context.NpcObject.name}: Successfully restored active path progress.", context.NpcObject);

                           // --- Check if already at the end waypoint after restoring ---
                           // If restored exactly at the end, trigger OnReachedDestination logic immediately
                           // PathFollowingHandler.IsFollowingPath will be true initially after RestorePathProgress,
                           // but HasReachedEndOfPath will be false. We need a separate check here.
                           // Check distance to the target waypoint stored *in the handler* after restore.
                           // The target waypoint ID is available via context.GetCurrentTargetWaypointID()
                           string targetWaypointID = context.GetCurrentTargetWaypointID();
                           Transform targetWaypointTransform = WaypointManager.Instance.GetWaypointTransform(targetWaypointID);

                           if (targetWaypointTransform != null && context.PathFollowingHandler != null && Vector3.Distance(context.NpcObject.transform.position, targetWaypointTransform.position) < context.PathFollowingHandler.waypointArrivalThreshold)
                           {
                                // We are at the target waypoint (which should be the end of the path).
                                Debug.Log($"{context.NpcObject.name}: Restored exactly at the target waypoint '{targetWaypointID}'. Triggering OnReachedDestination logic immediately.", context.NpcObject);
                                // Call OnReachedDestination directly
                                OnReachedDestination(context); // Call this SO's implementation
                                return; // Exit OnEnter early, decision/transition handled
                           }

                           // Path following handler is now active and not at the end yet. OnUpdate will monitor it.
                           // Return early, skipping the NavMesh logic below.
                           return;
                      }
                      else
                      {
                           Debug.LogError($"{context.NpcObject.name}: Failed to restore active path progress after interruption! Falling back to starting path from beginning via NavMesh.", context.NpcObject);
                           // Fall through to the standard NavMesh logic below.
                      }
                 } else {
                      // Path ID mismatch or interruptedPathSO is null - cannot restore into this state
                      Debug.LogWarning($"{context.NpcObject.name}: Was interrupted from a different path ('{context.Runner.interruptedPathID ?? "NULL"}') or interrupted path SO is null. Cannot restore into PathState '{name}' (PathID: '{pathAsset.PathID}'). Falling back to starting this path from beginning via NavMesh.", context.NpcObject);
                       // --- Clear the temporary interruption path data ---
                       context.Runner.interruptedPathID = null;
                       context.Runner.interruptedWaypointIndex = -1;
                       context.Runner.interruptedFollowReverse = false;
                       context.Runner.wasInterruptedFromPathState = false; // Clear the flag
                       // --- END Clear ---
                      // Fall through to the standard NavMesh logic below.
                 }
            }

               // --- Check if this is a TI NPC activating and should restore path progress ---
               // This logic is now slightly different: we don't *call* RestorePathProgress here anymore.
               // We check if the data indicates they *were* following this specific path simulation.
               // If so, OnReachedDestination needs to know that the first NavMesh leg was skipped.
               // Let's add a flag to context or runner.
               // The check for isFollowingPathBasic and PathID match is done in TiNpcManager.RequestActivateTiNpc.
               // If that check passes, TiNpcManager sets the target state to PathState.FollowPath and preserves the path data on TiData.
               // PathStateSO.OnEnter is now responsible for *using* that data.
               if (context.Runner.IsTrueIdentityNpc && context.TiData != null && context.TiData.isFollowingPathBasic)
               {
                    // Check if the saved path ID on data matches the path asset configured for *this* state SO.
                    if (context.TiData.simulatedPathID == pathAsset.PathID)
                    {
                         Debug.Log($"{context.NpcObject.name}: TI NPC activating into PathState '{name}'. Sim data indicates mid-path. PathFollowingHandler.RestorePathProgress will be called.", context.NpcObject);

                         // Call the PathFollowingHandler to restore its state directly.
                         // This bypasses the NavMesh leg.
                         bool restored = context.RestorePathProgress(
                              pathAsset, // Use the path asset configured on this SO
                              context.TiData.simulatedWaypointIndex, // Use the saved index
                              context.TiData.simulatedFollowReverse // Use the saved direction
                         );

                         // Clear simulation path data *after* attempting restore
                         // This indicates the data has now been transferred to the active handler
                         context.TiData.simulatedPathID = null;
                         context.TiData.simulatedWaypointIndex = -1;
                         context.TiData.simulatedFollowReverse = false;
                         context.TiData.isFollowingPathBasic = false; // Clear this flag now

                         if (restored)
                         {
                              Debug.Log($"{context.NpcObject.name}: Successfully restored path progress from TiData.", context.NpcObject);

                              // --- Check if already at the end waypoint after restoring ---
                              // If restored exactly at the end, trigger OnReachedDestination logic immediately
                              // PathFollowingHandler.IsFollowingPath will be true initially after RestorePathProgress,
                              // but HasReachedEndOfPath will be false. We need a separate check here.
                              // Check distance to the target waypoint stored *in the handler* after restore.
                              // The target waypoint ID is available via context.GetCurrentTargetWaypointID()
                              string targetWaypointID = context.GetCurrentTargetWaypointID();
                              Transform targetWaypointTransform = WaypointManager.Instance.GetWaypointTransform(targetWaypointID);

                                // Use the PathFollowingHandler's threshold for consistency
                              if (targetWaypointTransform != null && context.PathFollowingHandler != null && Vector3.Distance(context.NpcObject.transform.position, targetWaypointTransform.position) < context.PathFollowingHandler.waypointArrivalThreshold)
                              {
                                   // We are at the target waypoint (which should be the end of the path).
                                   Debug.Log($"{context.NpcObject.name}: Restored exactly at the target waypoint '{targetWaypointID}'. Triggering OnReachedDestination logic immediately.", context.NpcObject);
                                   // Call OnReachedDestination directly
                                   OnReachedDestination(context); // Call this SO's implementation
                                   return; // Exit OnEnter early, decision/transition handled
                              }

                              // Path following handler is now active and not at the end yet. OnUpdate will monitor it.
                              // Return early, skipping the NavMesh logic below.
                              return;
                         }
                         else
                         {
                              Debug.LogError($"{context.NpcObject.name}: Failed to restore path progress for TI NPC '{context.TiData.Id}' from TiData! Falling back to starting path from beginning via NavMesh.", context.NpcObject);
                              // Fall through to the standard NavMesh logic below.
                         }
                    }
                    else
                    {
                         // TI NPC activating, but sim data for this path asset isn't present or doesn't match.
                         // Treat as starting a new path from the beginning.
                         Debug.LogWarning($"{context.NpcObject.name}: TI NPC activating into PathState '{name}', but saved path data doesn't match ('{context.TiData.simulatedPathID ?? "NULL"}' vs '{pathAsset.PathID}'). Starting path from beginning via NavMesh.", context.NpcObject);
                         // Clear any potentially stale path simulation data on TiData here for safety
                         context.TiData.simulatedPathID = null;
                         context.TiData.simulatedWaypointIndex = -1;
                         context.TiData.simulatedFollowReverse = false;
                         context.TiData.isFollowingPathBasic = false;
                         // Fall through to the standard NavMesh logic below.
                    }
               }


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

            // Since NavMesh movement is starting, ensure PathFollowingHandler is stopped
            context.StopFollowingPath(); // Defensive call, should be stopped by base OnEnter/Awake/Reset
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context); // Call base OnUpdate (empty)

            // --- Check if PathFollowingHandler has reached the end ---
            // This check is only relevant *after* the initial NavMesh leg is complete (if applicable)
            // and context.StartFollowingPath or context.RestorePathProgress has been called in OnReachedDestination/OnEnter.
            // MODIFIED: Check only HasReachedEndOfPath, as IsFollowingPath becomes false in the same tick.
            if (context.PathFollowingHandler != null && context.PathFollowingHandler.HasReachedEndOfPath)
            {
                Debug.Log($"{context.NpcObject.name}: PathFollowingHandler signalled end of path '{pathAsset.PathID}'.", context.NpcObject);

                // Stop the path following handler (this also re-enables the NavMeshAgent and resets HasReachedEndOfPath)
                context.StopFollowingPath(); // Safe to call multiple times

                // --- Determine next state using PathSO's GetNextActiveStateDetails ---
                 // Ensure TiNpcManager is available via context
                 if (context.TiNpcManager == null)
                 {
                      Debug.LogError($"{context.NpcObject.name}: TiNpcManager is null in context! Cannot determine next state from PathSO end behavior. Transitioning to Idle fallback.", context.NpcObject);
                      context.TransitionToState(GeneralState.Idle); // Fallback
                      return;
                 }

                 PathTransitionDetails transitionDetails = pathAsset?.GetNextActiveStateDetails(context.TiData, context.TiNpcManager) ?? new PathTransitionDetails(null); 

                 if (transitionDetails.HasValidTransition)
                 {
                      Debug.Log($"{context.NpcObject.name}: Path '{pathAsset.PathID}' end behavior determined next Active State: '{transitionDetails.TargetStateEnum.GetType().Name}.{transitionDetails.TargetStateEnum.ToString() ?? "NULL"}'.", context.NpcObject);

                      // Check if the target state is PathState.FollowPath
                      if (transitionDetails.TargetStateEnum.GetType() == typeof(Game.NPC.PathState) && transitionDetails.TargetStateEnum.Equals(Game.NPC.PathState.FollowPath))
                      {
                           // If the next state is another path, start following it immediately
                           if (transitionDetails.PathAsset != null)
                           {
                                Debug.Log($"{context.NpcObject.name}: Transitioning to follow next path '{transitionDetails.PathAsset.PathID}' from index {transitionDetails.StartIndex}, reverse: {transitionDetails.FollowReverse}.", context.NpcObject);
                                // Start following the next path using the handler
                                bool pathStarted = context.StartFollowingPath(transitionDetails.PathAsset, transitionDetails.StartIndex, transitionDetails.FollowReverse);
                                if (!pathStarted)
                                {
                                     Debug.LogError($"{context.NpcObject.name}: Failed to start next path '{transitionDetails.PathAsset.PathID}'! Transitioning to Idle fallback.", context.NpcObject);
                                     context.TransitionToState(GeneralState.Idle); // Fallback
                                }
                                // Note: The state remains PathState.FollowPath, but the handler is updated.
                                // No state machine transition is needed here.
                           } else {
                                Debug.LogError($"{context.NpcObject.name}: Path '{pathAsset.PathID}' end behavior specified PathState.FollowPath but the next Path Asset is null! Transitioning to Idle fallback.", context.NpcObject);
                                context.TransitionToState(GeneralState.Idle); // Fallback
                           }
                      }
                      else
                      {
                           // If the next state is not a path, transition to that state
                           Debug.Log($"{context.NpcObject.name}: Transitioning to non-path state '{transitionDetails.TargetStateEnum.GetType().Name}.{transitionDetails.TargetStateEnum.ToString() ?? "NULL"}'.", context.NpcObject);
                           context.TransitionToState(transitionDetails.TargetStateEnum); // Use the context helper to transition by Enum key
                      }
                 }
                 else
                 {
                      // GetNextActiveStateDetails returned invalid details (invalid config or decision failed)
                      Debug.LogError($"{context.NpcObject.name}: Path '{pathAsset.PathID}' end behavior returned invalid details or failed to determine next Active State. Transitioning to Idle fallback.", context.NpcObject);
                      context.TransitionToState(GeneralState.Idle); // Fallback
                 }
            }
            // Note: Animation speed during path following needs to be managed by the PathFollowingHandler or here.
            // If PathFollowingHandler doesn't set animation speed, you might do it here based on context.IsFollowingPath
            // and context.PathFollowingHandler.pathFollowingSpeed.
        }

        public override void OnReachedDestination(NpcStateContext context) // Called by Runner when NavMesh destination is reached
        {
            base.OnReachedDestination(context); // Call base OnReachedDestination (logging)

            // This method is ONLY called by the Runner if the NavMeshAgent reached its destination
            // AND CheckMovementArrival is true AND PathFollowingHandler.IsFollowingPath is false.
            // This means it's triggered when the NPC reaches the *first waypoint* via NavMesh,
            // which happens when we are NOT restoring path progress for a TI NPC.
            Debug.Log($"{context.NpcObject.name}: Reached first waypoint (index {startIndex}) via NavMesh.", context.NpcObject);

            // Ensure NavMesh movement is stopped (Runner already does this before calling OnReachedDestination, but defensive)
            context.MovementHandler?.StopMoving();

            // --- Check if the first waypoint is the intended end of the path ---
            // Determine the index of the *intended* end waypoint of the pathAsset based on startIndex and direction
             int intendedEndIndex = followReverse ? 0 : pathAsset.WaypointCount - 1;
             string intendedEndWaypointID = pathAsset.GetWaypointID(intendedEndIndex);

             // Check if the waypoint we just reached via NavMesh is the same as the intended end waypoint
             // The waypoint we reached via NavMesh is the one we set the NavMesh destination to in OnEnter.
             // This is the waypoint ID derived from 'startIndex'.
             string reachedWaypointID = pathAsset.GetWaypointID(startIndex); // The waypoint reached via NavMesh leg

            if (!string.IsNullOrWhiteSpace(intendedEndWaypointID) && intendedEndWaypointID == reachedWaypointID)
            {
                 // Reached the intended end waypoint directly via NavMesh (e.g., path had only 2 waypoints, or started at index before end)
                 Debug.Log($"{context.NpcObject.name}: Reached intended end waypoint ('{reachedWaypointID}') directly via NavMesh.", context.NpcObject);

                 // --- Determine next state using PathSO's GetNextActiveStateDetails ---
                 // Ensure TiNpcManager is available via context
                 if (context.TiNpcManager == null)
                 {
                      Debug.LogError($"{context.NpcObject.name}: TiNpcManager is null in context! Cannot determine next state from PathSO end behavior. Transitioning to Idle fallback.", context.NpcObject);
                      context.TransitionToState(GeneralState.Idle); // Fallback
                      return;
                 }

                 PathTransitionDetails transitionDetails = pathAsset?.GetNextActiveStateDetails(context.TiData, context.TiNpcManager) ?? new PathTransitionDetails(null); 

                 if (transitionDetails.HasValidTransition)
                 {
                      Debug.Log($"{context.NpcObject.name}: Path '{pathAsset.PathID}' end behavior determined next Active State: '{transitionDetails.TargetStateEnum.GetType().Name}.{transitionDetails.TargetStateEnum.ToString() ?? "NULL"}'.", context.NpcObject);

                      // Check if the target state is PathState.FollowPath
                      if (transitionDetails.TargetStateEnum.GetType() == typeof(Game.NPC.PathState) && transitionDetails.TargetStateEnum.Equals(Game.NPC.PathState.FollowPath))
                      {
                           // If the next state is another path, start following it immediately
                           if (transitionDetails.PathAsset != null)
                           {
                                Debug.Log($"{context.NpcObject.name}: Transitioning to follow next path '{transitionDetails.PathAsset.PathID}' from index {transitionDetails.StartIndex}, reverse: {transitionDetails.FollowReverse}.", context.NpcObject);
                                // Start following the next path using the handler
                                bool pathStarted = context.StartFollowingPath(transitionDetails.PathAsset, transitionDetails.StartIndex, transitionDetails.FollowReverse);
                                if (!pathStarted)
                                {
                                     Debug.LogError($"{context.NpcObject.name}: Failed to start next path '{transitionDetails.PathAsset.PathID}'! Transitioning to Idle fallback.", context.NpcObject);
                                     context.TransitionToState(GeneralState.Idle); // Fallback
                                }
                                // Note: The state remains PathState.FollowPath, but the handler is updated.
                                // No state machine transition is needed here.
                           } else {
                                Debug.LogError($"{context.NpcObject.name}: Path '{pathAsset.PathID}' end behavior specified PathState.FollowPath but the next Path Asset is null! Transitioning to Idle fallback.", context.NpcObject);
                                context.TransitionToState(GeneralState.Idle); // Fallback
                           }
                      }
                      else
                      {
                           // If the next state is not a path, transition to that state
                           Debug.Log($"{context.NpcObject.name}: Transitioning to non-path state '{transitionDetails.TargetStateEnum.GetType().Name}.{transitionDetails.TargetStateEnum.ToString() ?? "NULL"}'.", context.NpcObject);
                           context.TransitionToState(transitionDetails.TargetStateEnum); // Use the context helper to transition by Enum key
                      }
                 }
                 else
                 {
                      // GetNextActiveStateDetails returned invalid details (invalid config or decision failed)
                      Debug.LogError($"{context.NpcObject.name}: Path '{pathAsset.PathID}' end behavior returned invalid details or failed to determine next Active State. Transitioning to Idle fallback.", context.NpcObject);
                      context.TransitionToState(GeneralState.Idle); // Fallback
                 }
            }
            else // Not at the intended end of the path (via initial NavMesh)
            {
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
        }

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)

            // Ensure path following is stopped when exiting this state
            context.StopFollowingPath(); // This also re-enables the NavMeshAgent

            // Note: Animation speed should be reset here
            // context.PlayAnimation("Idle"); // Assuming idle is the default after movement
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

             // Add validation that pathAsset is assigned
             if (pathAsset == null)
             {
                  Debug.LogError($"PathStateSO '{name}': Path Asset is not assigned! This state requires a PathSO.", this);
             }
        }
        #endif
    }
}

// --- END OF FILE PathStateSO.cs (Final Refactored) ---