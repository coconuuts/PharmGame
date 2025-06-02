// --- START OF FILE PathStateSO.cs ---

// --- Updated PathStateSO.cs (Phase 4, Substep 2) ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.States; // Needed for NpcStateSO
using Game.Navigation; // Needed for PathSO and WaypointManager
using System.Collections.Generic; // Needed for List
using System.Linq; // Needed for FirstOrDefault, Contains
using Game.NPC.TI; // Needed for TiNpcData, TiNpcManager // <-- Added TiNpcManager
using Game.NPC.Decisions; // Needed for DecisionPointSO, NpcDecisionHelper // <-- Added NpcDecisionHelper

namespace Game.NPC.States // Place alongside other active states
{
    /// <summary>
    /// Active state for an NPC to follow a predefined waypoint path.
    /// Handles the initial NavMesh leg to the first waypoint, then uses the PathFollowingHandler.
    /// Now supports restoring progress for activated TI NPCs and triggers decision logic
    /// upon reaching a linked Decision Point.
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

        // --- NEW: Decision Point Configuration ---
        [Header("Decision Point")] // <-- NEW HEADER
        [Tooltip("Optional: If assigned, this path leads to a Decision Point. Upon reaching the end, the NPC will trigger decision logic instead of transitioning to a fixed next state.")]
        [SerializeField] private DecisionPointSO decisionPoint; // <-- NEW FIELD
        // --- END NEW ---


        [Header("Transitions (Used if NO Decision Point is assigned)")] // <-- Updated Header
        [Tooltip("The Enum key for the state to transition to upon reaching the end of the path (used if no Decision Point is assigned).")] // <-- Updated Tooltip
        [SerializeField] private string nextStateEnumKey;
        [Tooltip("The Type name of the Enum key for the next state (e.g., Game.NPC.CustomerState, Game.NPC.GeneralState) (used if no Decision Point is assigned).")] // <-- Updated Tooltip
        [SerializeField] private string nextStateEnumType;

        // --- NpcStateSO Overrides ---
        public override System.Enum HandledState => Game.NPC.PathState.FollowPath; // <-- Use the new enum

        // Path following is generally interruptible, but specific path states might override this.
        public override bool IsInterruptible => true; // <-- Set interruptible flag


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
             // --- NEW: Validate Decision Point link if assigned ---
             if (decisionPoint != null)
             {
                 // Check TiNpcManager instance needed for decision logic
                 if (TiNpcManager.Instance == null)
                 {
                     Debug.LogError($"{context.NpcObject.name}: PathStateSO '{name}' links to Decision Point '{decisionPoint.name}', but TiNpcManager.Instance is null! Cannot perform decision logic. Transitioning to fallback.", context.NpcObject);
                     context.TransitionToState(GeneralState.Idle); // Fallback
                     return;
                 }

                 if (string.IsNullOrWhiteSpace(decisionPoint.PointID) || string.IsNullOrWhiteSpace(decisionPoint.WaypointID))
                 {
                      Debug.LogError($"{context.NpcObject.name}: PathStateSO '{name}' links to Decision Point '{decisionPoint.name}', but Decision Point SO has invalid PointID ('{decisionPoint.PointID}') or WaypointID ('{decisionPoint.WaypointID}')! Transitioning to fallback.", context.NpcObject);
                      context.TransitionToState(GeneralState.Idle); // Fallback
                      return;
                 }
                 // Ensure the Decision Point Waypoint exists in the scene
                 if (WaypointManager.Instance.GetWaypointTransform(decisionPoint.WaypointID) == null)
                 {
                     Debug.LogError($"{context.NpcObject.name}: PathStateSO '{name}' links to Decision Point '{decisionPoint.name}' (WaypointID: '{decisionPoint.WaypointID}'), but the waypoint is not found in the scene via WaypointManager! Transitioning to fallback.", context.NpcObject);
                     context.TransitionToState(GeneralState.Idle); // Fallback
                     return;
                 }
                 // If a Decision Point is assigned, override the default next state logging
                 Debug.Log($"{context.NpcObject.name}: Entering PathState '{name}' leading to Decision Point '{decisionPoint.PointID}'.");

             } else {
                // Log the standard next state if no Decision Point is assigned
                Debug.Log($"{context.NpcObject.name}: Entering PathState '{name}' leading to fixed next state.");
            }
            // --- END NEW ---


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
                           // --- END NEW ---

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

               // --- NEW: Check if this is a TI NPC activating and should restore path progress ---
               // This logic is now slightly different: we don't *call* RestorePathProgress here anymore.
               // We check if the data indicates they *were* following this specific path simulation.
               // If so, OnReachedDestination needs to know that the first NavMesh leg was skipped.
               // Let's add a flag to context or runner.
               // The check for isFollowingPathBasic and PathID match is done in TiNpcManager.RequestActivateTiNpc.
               // If that check passes, TiNpcManager sets the target state to PathState.FollowPath and preserves the path data on TiData.
               // PathStateSO.OnEnter is now responsible for *using* that data.

               bool wasActivatingFromPathSim = false; // Flag for OnReachedDestination - this flag is no longer needed as RestorePathProgress is called directly
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
                              // --- END NEW ---

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

                // --- Trigger decision logic if Decision Point is assigned, otherwise transition to fixed next state ---
                 if (decisionPoint != null)
                 {
                      Debug.Log($"{context.NpcObject.name}: Reached Decision Point '{decisionPoint.PointID}' (via PathFollowingHandler). Triggering decision logic.", context.NpcObject);
                       // Call the shared decision logic helper
                       MakeDecisionAndTransition(context, decisionPoint); // Uses context, decisionPoint, and reads from TiData

                 }
                 else
                 {
                      // No Decision Point, transition to the configured next state (standard path behavior)
                      Debug.Log($"{context.NpcObject.name}: Reached end of standard path (via PathFollowingHandler). Transitioning to fixed next state.", context.NpcObject);
                      TransitionToNextState(context); // Use standard fixed transition
                 }
                // --- END NEW ---
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

            // --- NEW: Check if the first waypoint is the end of the path AND it's a Decision Point ---
            // This handles the edge case where the path might only have 2 waypoints, and the NPC
            // reaches the *end* waypoint directly via the initial NavMesh leg, and that waypoint
            // is also a Decision Point.
            // First, determine the index of the *intended* end waypoint of the pathAsset based on startIndex and direction
             int intendedEndIndex = followReverse ? 0 : pathAsset.WaypointCount - 1;
             string intendedEndWaypointID = pathAsset.GetWaypointID(intendedEndIndex);

             // Check if the waypoint we just reached via NavMesh is the same as the intended end waypoint
             // The waypoint we reached via NavMesh is the one we set the NavMesh destination to in OnEnter.
             // This is the waypoint ID derived from 'startIndex'.
             string reachedWaypointID = pathAsset.GetWaypointID(startIndex); // The waypoint reached via NavMesh leg

            if (decisionPoint != null && !string.IsNullOrWhiteSpace(decisionPoint.WaypointID) && decisionPoint.WaypointID == reachedWaypointID)
            {
                 // Reached the Decision Point waypoint directly via NavMesh
                 Debug.Log($"{context.NpcObject.name}: Reached Decision Point '{decisionPoint.PointID}' waypoint ('{reachedWaypointID}') directly via NavMesh. Triggering decision logic immediately.", context.NpcObject);
                 // Trigger decision logic
                 MakeDecisionAndTransition(context, decisionPoint); // Use context, decisionPoint
            }
            else // Not at the end of the path (via initial NavMesh), or not a Decision Point path end
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
             // --- END NEW ---
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
        /// Handles the transition to the next state upon path completion IF no Decision Point is assigned.
        /// </summary>
        private void TransitionToNextState(NpcStateContext context)
        {
             // This method is only called if decisionPoint is null
             if (string.IsNullOrEmpty(nextStateEnumKey) || string.IsNullOrEmpty(nextStateEnumType))
             {
                  Debug.LogWarning($"{context.NpcObject.name}: PathState '{name}' has no next state configured AND no Decision Point assigned. Transitioning to Idle fallback.", context.NpcObject);
                  context.TransitionToState(GeneralState.Idle); // Default fallback if no next state is set
                  return;
             }

             // Use the context helper to transition by string key/type
             context.TransitionToState(nextStateEnumKey, nextStateEnumType);
        }

        /// <summary>
        /// Triggers the data-driven decision logic and transitions to the chosen state.
        /// This is called when the NPC reaches the end of a path linked to a Decision Point.
        /// </summary>
        private void MakeDecisionAndTransition(NpcStateContext context, DecisionPointSO decisionPoint)
        {
             if (context.TiData == null)
             {
                  Debug.LogError($"{context.NpcObject.name}: Cannot make decision at point '{decisionPoint?.PointID ?? "NULL"}' - TiNpcData is null! Transitioning to fallback.", context.NpcObject);
                  context.TransitionToState(GeneralState.Idle); // Fallback
                  return;
             }
             if (decisionPoint == null) // Should not happen due to calling context, but defensive
             {
                  Debug.LogError($"{context.NpcObject.name}: Cannot make decision - DecisionPointSO is null! Transitioning to fallback.", context.NpcObject);
                  context.TransitionToState(GeneralState.Idle); // Fallback
                  return;
             }
             // Check TiNpcManager dependency
             if (TiNpcManager.Instance == null)
             {
                  Debug.LogError($"{context.NpcObject.name}: TiNpcManager.Instance is null! Cannot perform decision logic.", context.NpcObject);
                   context.TransitionToState(GeneralState.Idle); // Fallback
                   return;
             }

             // --- NEW: Check endDay flag before making decision (Already added in Substep 3.1, keeping here) ---
             // If it's time to go home, override the decision and go straight to ReturningToPool
             if (context.TiData.isEndingDay)
             {
                  Debug.Log($"{context.NpcObject.name}: Reached Decision Point '{decisionPoint.PointID}' but is in endDay schedule ({context.TiData.endDay}). Transitioning to ReturningToPool instead of making decision.", context.NpcObject);
                  context.TransitionToState(GeneralState.ReturningToPool);
                  return; // Exit early
             }
             // --- END NEW ---


             // --- Call the shared Decision Logic helper ---
             // Call the MakeDecision helper from the static helper class
             System.Enum chosenStateEnum = NpcDecisionHelper.MakeDecision(context.TiData, decisionPoint, TiNpcManager.Instance);

             if (chosenStateEnum != null)
             {
                  Debug.Log($"{context.NpcObject.name}: Decision made at '{decisionPoint.PointID}'. Transitioning to chosen state '{chosenStateEnum.GetType().Name}.{chosenStateEnum.ToString() ?? "NULL"}'.", context.NpcObject);
                  context.TransitionToState(chosenStateEnum); // Use the context helper to transition by Enum key
             }
             else
             {
                  Debug.LogError($"{context.NpcObject.name}: Decision logic at '{decisionPoint.PointID}' returned a null state or no valid options! Transitioning to fallback.", context.NpcObject);
                  context.TransitionToState(GeneralState.Idle); // Fallback
             }
             // --- END Call ---
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

            // Validate Decision Point link
            if (decisionPoint != null)
            {
                 if (string.IsNullOrWhiteSpace(decisionPoint.PointID))
                 {
                      Debug.LogError($"PathStateSO '{name}': Decision Point '{decisionPoint.name}' has an empty Point ID.", this);
                 }
                 if (string.IsNullOrWhiteSpace(decisionPoint.WaypointID))
                 {
                      Debug.LogError($"PathStateSO '{name}': Decision Point '{decisionPoint.name}' has an empty Waypoint ID.", this);
                 }
                 // Warn if standard transitions are also configured when a Decision Point is linked
                 if (!string.IsNullOrEmpty(nextStateEnumKey) || !string.IsNullOrEmpty(nextStateEnumType))
                 {
                      Debug.LogWarning($"PathStateSO '{name}': A Decision Point is assigned, but standard Next State ('{nextStateEnumKey}' of type '{nextStateEnumType}') is also configured. The standard Next State will be ignored when the Decision Point is reached.", this);
                 }
                 // TODO: Add validation that the Decision Point WaypointID is actually the LAST waypoint in the pathAsset
                 // This is a crucial constraint for Decision Point paths.
                 // Requires WaypointManager access in OnValidate, or ensuring this in the workflow.
                 // For now, rely on manual setup and runtime checks.
                 // A simple check: get the last waypoint of the path based on direction
                 if (pathAsset != null && pathAsset.WaypointCount > 0)
                 {
                      int expectedEndIndex = followReverse ? 0 : pathAsset.WaypointCount - 1;
                      string expectedEndWaypointID = pathAsset.GetWaypointID(expectedEndIndex);
                      if (decisionPoint.WaypointID != expectedEndWaypointID)
                      {
                           Debug.LogError($"PathStateSO '{name}': Decision Point '{decisionPoint.PointID}' (Waypoint ID '{decisionPoint.WaypointID}') is NOT the end waypoint of the assigned Path Asset '{pathAsset.name}' (Expected Waypoint ID '{expectedEndWaypointID}' at index {expectedEndIndex} with reverse={followReverse})! This Decision Point path will likely not work correctly.", this);
                      }
                 } else if (pathAsset != null)
                 {
                      Debug.LogWarning($"PathStateSO '{name}': Path Asset '{pathAsset.name}' has fewer than 1 waypoint. Cannot validate Decision Point waypoint.", this);
                 }
            }
             else // No Decision Point assigned, validate standard next state configuration
             {
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
                 else // Neither Decision Point nor standard next state is configured
                 {
                      Debug.LogWarning($"PathStateSO '{name}': Neither a Decision Point nor a standard Next State is configured. This state will transition to Idle fallback upon path completion.", this);
                 }
             }
        }
        #endif
    }
}
// --- END OF FILE PathStateSO.cs ---