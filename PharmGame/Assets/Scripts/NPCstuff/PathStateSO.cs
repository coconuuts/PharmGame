// --- START OF FILE PathStateSO.cs (Refactored Generic - FIX) ---

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
    /// Generic Active state for an NPC to follow a predefined waypoint path.
    /// Handles the initial NavMesh leg to the first waypoint, then uses the PathFollowingHandler.
    /// Now supports restoring progress for activated TI NPCs and triggers decision logic
    /// upon reaching a linked Decision Point.
    /// End behavior (Decision Point or fixed transition) is defined on the PathSO asset.
    /// Uses PathTransitionDetails to handle transitions to other paths.
    /// This single asset handles the logic for *any* path, receiving path data dynamically.
    /// FIX: Unified path end behavior handling to prevent issues when activating near path end.
    /// </summary>
    [CreateAssetMenu(fileName = "PathState_FollowPath", menuName = "NPC/Path States/Follow Path (Generic)", order = 50)] // Use a generic name
    public class PathStateSO : NpcStateSO
    {
        // --- State Configuration ---
        // REMOVED: Serialized fields for specific path asset, start index, and reverse flag.
        // These will be provided dynamically via the Runner or TiData.

        // --- NpcStateSO Overrides ---
        public override System.Enum HandledState => Game.NPC.PathState.FollowPath;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // Validate basic dependencies
            if (WaypointManager.Instance == null)
            {
                 Debug.LogError($"{context.NpcObject.name}: WaypointManager.Instance is null! Cannot follow path. Transitioning to fallback.", context.NpcObject);
                 context.TransitionToState(GeneralState.Idle); // Fallback
                 return;
             }

             Debug.Log($"{context.NpcObject.name}: Entering Generic PathState '{name}'. Determining path data source.");

            PathSO pathToFollow = null;
            int startWaypointIndex = -1;
            bool followReverseDirection = false;
            bool isResumingPath = false; // Flag to indicate if we are resuming an existing path

            // --- Determine path data source: TiNpcData (if resuming) or Runner (if starting new) ---

            // Priority 1: Check if returning from interruption (data saved on Runner)
            if (context.Runner.wasInterruptedFromPathState)
            {
                 Debug.Log($"{context.NpcObject.name}: PathState OnEnter: Resuming from interruption. Reading path data from Runner's temporary fields.", context.NpcObject);
                 pathToFollow = WaypointManager.Instance.GetPath(context.Runner.interruptedPathID);
                 startWaypointIndex = context.Runner.interruptedWaypointIndex; // This is the target index they were moving towards
                 followReverseDirection = context.Runner.interruptedFollowReverse;
                 isResumingPath = true;

                 // Clear the temporary interruption path data on the Runner *after* reading it
                 context.Runner.interruptedPathID = null;
                 context.Runner.interruptedWaypointIndex = -1;
                 context.Runner.interruptedFollowReverse = false;
                 context.Runner.wasInterruptedFromPathState = false; // Clear the flag
            }
            // Priority 2: Check if activating TI NPC mid-path simulation (data saved on TiData)
            // Note: TiNpcManager should clear tiData.isFollowingPathBasic BEFORE calling Activate,
            // but OnEnter might still need to read the path data from tiData if it was saved there.
            // The check for tiData.simulatedPathID being not null is more reliable here.
            else if (context.Runner.IsTrueIdentityNpc && context.TiData != null && !string.IsNullOrWhiteSpace(context.TiData.simulatedPathID))
            {
                 Debug.Log($"{context.NpcObject.name}: PathState OnEnter: TI NPC activating mid-path simulation. Reading path data from TiData.", context.NpcObject);
                 pathToFollow = WaypointManager.Instance.GetPath(context.TiData.simulatedPathID);
                 startWaypointIndex = context.TiData.simulatedWaypointIndex; // This is the target index they were moving towards
                 followReverseDirection = context.TiData.simulatedFollowReverse;
                 isResumingPath = true;

                 // Clear simulation path data on TiData *after* reading it (data is now transferred to active handler)
                 context.TiData.simulatedPathID = null;
                 context.TiData.simulatedWaypointIndex = -1;
                 context.TiData.simulatedFollowReverse = false;
                 context.TiData.isFollowingPathBasic = false; // Clear this flag now
            }
            // Priority 3: Starting a NEW path (data provided via Runner's temporary fields by the caller)
            else
            {
                 Debug.Log($"{context.NpcObject.name}: PathState OnEnter: Starting a NEW path. Reading path data from Runner's temporary fields.", context.NpcObject);
                 // Read the path data from the Runner's temporary fields
                 pathToFollow = context.Runner.tempPathSO;
                 startWaypointIndex = context.Runner.tempStartIndex;
                 followReverseDirection = context.Runner.tempFollowReverse;

                 // Clear the Runner's temporary fields *after* reading them
                 context.Runner.tempPathSO = null;
                 context.Runner.tempStartIndex = 0; // Reset to default
                 context.Runner.tempFollowReverse = false; // Reset to default
            }
            // --- END Determine path data source ---


            // --- Validate obtained path data ---
            if (pathToFollow == null)
            {
                 Debug.LogError($"{context.NpcObject.name}: PathState OnEnter: No PathSO provided or found from any source! Transitioning to fallback.", context.NpcObject);
                 context.TransitionToState(GeneralState.Idle); // Fallback
                 return;
            }
            if (startWaypointIndex < 0 || startWaypointIndex >= pathToFollow.WaypointCount)
            {
                 Debug.LogError($"{context.NpcObject.name}: PathState OnEnter: Invalid start/resume waypoint index {startWaypointIndex} for path '{pathToFollow.PathID}' (WaypointCount: {pathToFollow.WaypointCount})! Transitioning to fallback.", context.NpcObject);
                 context.TransitionToState(GeneralState.Idle); // Fallback
                 return;
            }
            // --- End Validate ---


            // --- Handle Resuming Path (from interruption or TI activation) ---
            if (isResumingPath)
            {
                 Debug.Log($"{context.NpcObject.name}: PathState OnEnter: Attempting to restore path progress for path '{pathToFollow.PathID}' towards index {startWaypointIndex}, reverse: {followReverseDirection}.", context.NpcObject);

                 // Call the PathFollowingHandler to restore its state directly.
                 // This bypasses the initial NavMesh leg.
                 bool restored = context.RestorePathProgress(
                      pathToFollow, // Use the obtained path asset
                      startWaypointIndex, // Use the obtained index (this is the target they were moving towards)
                      followReverseDirection // Use the obtained direction
                 );

                 if (restored)
                 {
                      Debug.Log($"{context.NpcObject.name}: Successfully restored path progress.", context.NpcObject);

                      // --- Check if the handler immediately reports being at the end ---
                      // This happens if the warp-in position was within the threshold of the final waypoint
                       if (context.PathFollowingHandler.HasReachedEndOfPath)
                       {
                            Debug.Log($"{context.NpcObject.name}: Restored and immediately at the end of path '{pathToFollow.PathID}'. Triggering end behavior.", context.NpcObject);
                            context.StopFollowingPath(); // Stop the handler before processing end behavior
                            HandlePathEndBehavior(context, pathToFollow); // Call the shared end behavior logic
                            return; // Exit OnEnter early
                       }

                      // Path following handler is now active and not at the end yet. OnUpdate will monitor it.
                      // Return early, skipping the NavMesh logic below.
                      return;
                 }
                 else
                 {
                      Debug.LogError($"{context.NpcObject.name}: Failed to restore path progress! Falling back to starting path from beginning via NavMesh.", context.NpcObject);
                      // Fall through to the standard NavMesh logic below.
                      // The path data (pathToFollow, startWaypointIndex, followReverseDirection) is still valid for this fallback.
                      // Note: The startWaypointIndex for the NavMesh leg will now be the *first* waypoint of the path,
                      // not the index they were previously heading towards.
                      startWaypointIndex = 0; // Reset start index for the fallback NavMesh leg
                      followReverseDirection = false; // Reset direction for the fallback NavMesh leg
                      Debug.Log($"{context.NpcObject.name}: BasicPathState fallback: Starting from path beginning (index 0, forward) via NavMesh.");
                 }
            }


            // --- Standard Logic: Start NavMesh movement to the first waypoint (only if NOT resuming progress or resume failed) ---
            // The target for the NavMesh leg is the *first waypoint* of the path segment we intend to follow.
            // If followReverse is true, the "first" waypoint is startIndex. If false, it's startIndex.
            // The PathFollowingHandler will handle the *next* waypoint after this one.
            string firstWaypointID = pathToFollow.GetWaypointID(startWaypointIndex); // Use the obtained start index (potentially reset for fallback)
            if (string.IsNullOrWhiteSpace(firstWaypointID))
            {
                 Debug.LogError($"{context.NpcObject.name}: Path '{pathToFollow.PathID}' has invalid start waypoint ID at index {startWaypointIndex}! Transitioning to fallback.", context.NpcObject);
                 context.TransitionToState(GeneralState.Idle); // Fallback
                 return;
            }

            Transform firstWaypointTransform = WaypointManager.Instance.GetWaypointTransform(firstWaypointID);

            if (firstWaypointTransform == null)
            {
                Debug.LogError($"{context.NpcObject.name}: Start waypoint with ID '{firstWaypointID}' (index {startWaypointIndex}) for path '{pathToFollow.PathID}' not found in scene via WaypointManager! Transitioning to fallback.", context.NpcObject);
                context.TransitionToState(GeneralState.Idle); // Fallback
                return;
            }

            Debug.Log($"{context.NpcObject.name}: PathState '{name}' OnEnter. Moving to first waypoint '{firstWaypointID}' (index {startWaypointIndex}) via NavMesh.", context.NpcObject);
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

            // Store the path data on the Runner temporarily for OnReachedDestination to pick up
            // This is needed because OnReachedDestination doesn't receive the same parameters as OnEnter.
            // We use the same temporary fields as for starting a new path.
            context.Runner.tempPathSO = pathToFollow;
            context.Runner.tempStartIndex = startWaypointIndex;
            context.Runner.tempFollowReverse = followReverseDirection;
            Debug.Log($"{context.NpcObject.name}: Stored path data on Runner temp fields for OnReachedDestination.", context.NpcObject);
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context); // Call base OnUpdate (empty)

            // --- Check if PathFollowingHandler has reached the end ---
            // This check is only relevant *after* the initial NavMesh leg is complete (if applicable)
            // and context.StartFollowingPath or context.RestorePathProgress has been called in OnReachedDestination/OnEnter.
            if (context.PathFollowingHandler != null && context.PathFollowingHandler.HasReachedEndOfPath)
            {
                // Get the PathSO that the handler just finished following
                PathSO completedPathSO = context.PathFollowingHandler.GetCurrentPathSO(); // <-- Get PathSO from handler

                Debug.Log($"{context.NpcObject.name}: PathFollowingHandler signalled end of path '{completedPathSO?.PathID ?? "NULL"}'.", context.NpcObject);

                // Stop the path following handler (this also re-enables the NavMeshAgent and resets HasReachedEndOfPath)
                context.StopFollowingPath(); // Safe to call multiple times

                // Call the shared end behavior logic
                HandlePathEndBehavior(context, completedPathSO);

                // Note: HandlePathEndBehavior will transition to the next state or start the next path,
                // so no further logic is needed here after the call.
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
            Debug.Log($"{context.NpcObject.name}: Reached initial NavMesh destination (first waypoint).", context.NpcObject);

            // Ensure NavMesh movement is stopped (Runner already does this before calling OnReachedDestination, but defensive)
            context.MovementHandler?.StopMoving();

            // --- Get the path data from the Runner's temporary fields ---
            // This data was stored in OnEnter for the initial NavMesh leg.
            PathSO pathSOToFollow = context.Runner.tempPathSO; // <-- Read from temporary field
            int startWaypointIndex = context.Runner.tempStartIndex; // <-- Read from temporary field
            bool followReverseDirection = context.Runner.tempFollowReverse; // <-- Read from temporary field

            // Clear the Runner's temporary fields *after* reading them
            context.Runner.tempPathSO = null;
            context.Runner.tempStartIndex = 0; // Reset to default
            context.Runner.tempFollowReverse = false; // Reset to default
            Debug.Log($"{context.NpcObject.name}: Cleared Runner temp path fields in OnReachedDestination.", context.NpcObject);


            // --- Validate obtained path data ---
            if (pathSOToFollow == null)
            {
                 Debug.LogError($"{context.NpcObject.name}: PathState OnReachedDestination: PathSO is null from Runner temp fields! Cannot start path following. Transitioning to fallback.", context.NpcObject);
                 context.TransitionToState(GeneralState.Idle); // Fallback
                 return;
            }
             if (startWaypointIndex < 0 || startWaypointIndex >= pathSOToFollow.WaypointCount)
             {
                  Debug.LogError($"{context.NpcObject.name}: PathState OnReachedDestination: Invalid start index {startWaypointIndex} from Runner temp fields for path '{pathSOToFollow.PathID}'! Cannot start path following. Transitioning to fallback.", context.NpcObject);
                  context.TransitionToState(GeneralState.Idle); // Fallback
                  return;
             }
            // --- End Validate ---


            // --- Determine the intended end of the path ---
             int intendedEndIndex = followReverseDirection ? 0 : pathSOToFollow.WaypointCount - 1;
             string intendedEndWaypointID = pathSOToFollow.GetWaypointID(intendedEndIndex);

             // Check if the waypoint we just reached via NavMesh is the same as the intended end waypoint
             // The waypoint we reached via NavMesh is the one we set the NavMesh destination to in OnEnter.
             // This is the waypoint ID derived from 'startWaypointIndex'.
             string reachedWaypointID = pathSOToFollow.GetWaypointID(startWaypointIndex); // The waypoint reached via NavMesh leg

            if (!string.IsNullOrWhiteSpace(intendedEndWaypointID) && intendedEndWaypointID == reachedWaypointID)
            {
                 // Reached the intended end waypoint directly via NavMesh (e.g., path had only 2 waypoints, or started at index before end)
                 Debug.Log($"{context.NpcObject.name}: Reached intended end waypoint ('{reachedWaypointID}') directly via NavMesh. Triggering end behavior.", context.NpcObject);
                 // Call the shared end behavior logic
                 HandlePathEndBehavior(context, pathSOToFollow);
                 // Note: HandlePathEndBehavior will transition to the next state or start the next path,
                 // so no further logic is needed here after the call.
            }
            else // Not at the intended end of the path (via initial NavMesh)
            {
                // --- Start Path Following using the handler ---
                // The handler will disable the NavMeshAgent and start Rigidbody movement.
                // We pass the path asset, the *start index* (where the NPC *is*), and the direction.
                // The handler's StartFollowingPath will determine the *first target* waypoint index.
                bool pathStarted = context.StartFollowingPath(pathSOToFollow, startWaypointIndex, followReverseDirection);

                if (!pathStarted)
                {
                     Debug.LogError($"{context.NpcObject.name}: Failed to start path following for path '{pathSOToFollow.PathID}'! Transitioning to fallback.", context.NpcObject);
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

            // Clear any lingering temporary path data on the Runner just in case
            context.Runner.tempPathSO = null;
            context.Runner.tempStartIndex = 0;
            context.Runner.tempFollowReverse = false;
        }

        /// <summary>
        /// Handles the shared logic for determining the next state or path after a path is completed.
        /// Called from OnUpdate (PathFollowingHandler completion), OnReachedDestination (NavMesh leg to end),
        /// or OnEnter (Activation warp-in directly at end).
        /// </summary>
        /// <param name="context">The current NPC state context.</param>
        /// <param name="pathThatFinished">The PathSO asset that was just completed.</param>
        private void HandlePathEndBehavior(NpcStateContext context, PathSO pathThatFinished)
        {
             if (pathThatFinished == null)
             {
                  Debug.LogError($"{context.NpcObject.name}: HandlePathEndBehavior called with null PathSO! Cannot determine next state. Transitioning to Idle fallback.", context.NpcObject);
                  context.TransitionToState(GeneralState.Idle); // Fallback
                  return;
             }

             // Ensure TiNpcManager is available via context
             if (context.TiNpcManager == null)
             {
                  Debug.LogError($"{context.NpcObject.name}: TiNpcManager is null in context! Cannot determine next state from PathSO end behavior. Transitioning to Idle fallback.", context.NpcObject);
                  context.TransitionToState(GeneralState.Idle); // Fallback
                  return;
             }

             PathTransitionDetails transitionDetails = pathThatFinished.GetNextActiveStateDetails(context.TiData, context.TiNpcManager);

             if (transitionDetails.HasValidTransition)
             {
                  Debug.Log($"{context.NpcObject.name}: Path '{pathThatFinished.PathID}' end behavior determined next Active State: '{transitionDetails.TargetStateEnum.GetType().Name}.{transitionDetails.TargetStateEnum.ToString() ?? "NULL"}'.", context.NpcObject);

                  // Check if the target state is PathState.FollowPath
                  if (transitionDetails.TargetStateEnum.GetType() == typeof(Game.NPC.PathState) && transitionDetails.TargetStateEnum.Equals(Game.NPC.PathState.FollowPath))
                  {
                       // If the next state is another path, start following it immediately using the handler
                       if (transitionDetails.PathAsset != null)
                       {
                            Debug.Log($"{context.NpcObject.name}: Transitioning to follow next path '{transitionDetails.PathAsset.PathID}' from index {transitionDetails.StartIndex}, reverse: {transitionDetails.FollowReverse}.", context.NpcObject);
                            // Start following the next path using the handler directly
                            // No state machine transition is needed here, just update the handler.
                            bool pathStarted = context.StartFollowingPath(transitionDetails.PathAsset, transitionDetails.StartIndex, transitionDetails.FollowReverse);
                            if (!pathStarted)
                            {
                                 Debug.LogError($"{context.NpcObject.name}: Failed to start next path '{transitionDetails.PathAsset.PathID}'! Transitioning to Idle fallback.", context.NpcObject);
                                 context.TransitionToState(GeneralState.Idle); // Fallback
                            }
                            // Note: The state remains PathState.FollowPath, but the handler is updated.
                            // The next tick will continue path following on the new path.
                       } else {
                            Debug.LogError($"{context.NpcObject.name}: Path '{pathThatFinished.PathID}' end behavior specified PathState.FollowPath but the next Path Asset is null in transition details! Transitioning to Idle fallback.", context.NpcObject);
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
                  Debug.LogError($"{context.NpcObject.name}: Path '{pathThatFinished.PathID}' end behavior returned invalid details or failed to determine next Active State. Transitioning to Idle fallback.", context.NpcObject);
                  context.TransitionToState(GeneralState.Idle); // Fallback
             }
        }
    }
}

// --- END OF FILE PathStateSO.cs ---