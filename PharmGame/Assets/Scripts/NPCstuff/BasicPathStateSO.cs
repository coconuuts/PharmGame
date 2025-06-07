// --- START OF FILE BasicPathStateSO.cs (Refactored Generic) ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData, TiNpcManager
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicPathState enum, BasicNpcStateSO, BasicNpcStateManager
using Game.Navigation; // Needed for PathSO and WaypointManager, PathTransitionDetails
using Random = UnityEngine.Random; // Specify UnityEngine.Random
using System.Linq; // Needed for Contains
using Game.NPC.Decisions; // Needed for DecisionPointSO, NpcDecisionHelper // Still needed for NpcDecisionHelper via PathSO

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    /// <summary>
    /// Generic Basic state for simulating an inactive TI NPC following a predefined waypoint path.
    /// Operates directly on TiNpcData.
    /// Now supports resuming progress for activated TI NPCs and triggers decision logic
    /// upon simulating reaching a linked Decision Point.
    /// End behavior (Decision Point or fixed transition) is now defined on the PathSO asset.
    /// Uses PathTransitionDetails to handle transitions to other paths.
    /// This single asset handles the simulation logic for *any* path data stored on TiNpcData.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicPathState_FollowPath", menuName = "NPC/Basic States/Basic Follow Path (Generic)", order = 50)] // Use a generic name
    public class BasicPathStateSO : BasicNpcStateSO
    {
        // --- State Configuration ---
        // REMOVED: Serialized fields for specific path asset, start index, and reverse flag.
        // These will be read dynamically from TiNpcData.

        // --- BasicNpcStateSO Overrides ---
        public override System.Enum HandledBasicState => BasicPathState.BasicFollowPath;

        // Basic Path states do not use the standard timeout; their 'timeout' is reaching the end of the path.
        // The simulatedStateTimer is used by other states (like BasicLookToShop).
         public override bool ShouldUseTimeout => false;


        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            base.OnEnter(data, manager); // Call base OnEnter (logs entry, resets base timer/target/path data)

            // Validate dependencies early
             if (WaypointManager.Instance == null)
             {
                  Debug.LogError($"SIM {data.Id}: WaypointManager.Instance is null! Cannot simulate path. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                  manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                  return;
             }

            // --- Determine path data source: TiNpcData (MUST be present) ---
            // This state should ONLY be entered if the TiNpcData already has valid path simulation data primed.
            // This happens during LoadDummyNpcData or when transitioning from another Basic State that decides the next path.
            if (!data.isFollowingPathBasic || string.IsNullOrWhiteSpace(data.simulatedPathID) || data.simulatedWaypointIndex < 0)
            {
                 Debug.LogError($"SIM {data.Id}: BasicPathState '{name}' OnEnter: TiNpcData does NOT have valid path simulation data primed! isFollowingPathBasic: {data.isFollowingPathBasic}, simulatedPathID: '{data.simulatedPathID ?? "NULL"}', simulatedWaypointIndex: {data.simulatedWaypointIndex}. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                 // Clear potentially invalid path data on TiNpcData
                 data.simulatedPathID = null;
                 data.simulatedWaypointIndex = -1;
                 data.simulatedFollowReverse = false;
                 data.isFollowingPathBasic = false;
                 manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                 return;
            }

            // Path data is expected to be valid on TiNpcData. Read it.
            PathSO currentPath = WaypointManager.Instance.GetPath(data.simulatedPathID);
            int currentPathStartIndex = data.simulatedWaypointIndex; // This is the target they were moving towards
            bool currentPathFollowReverse = data.simulatedFollowReverse;

            if (currentPath == null)
            {
                 Debug.LogError($"SIM {data.Id}: PathSO with ID '{data.simulatedPathID}' from TiNpcData not found via WaypointManager during BasicPathState OnEnter! Cannot continue simulation. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                 // Clear invalid path data on TiNpcData
                 data.simulatedPathID = null;
                 data.simulatedWaypointIndex = -1;
                 data.simulatedFollowReverse = false;
                 data.isFollowingPathBasic = false;
                 manager.TransitionToBasicState(data, BasicState.BasicPatrol);
                 return;
            }

            // The position should already be set in the previous state's SimulateTick or OnEnter,
            // or by TiNpcManager.LoadDummyNpcData if starting here.
            // The target position needs to be set based on the saved index.
            string nextTargetWaypointID = currentPath.GetWaypointID(currentPathStartIndex); // Use the saved index
            Transform nextTargetTransform = WaypointManager.Instance.GetWaypointTransform(nextTargetWaypointID);

            if (nextTargetTransform != null)
            {
                 data.simulatedTargetPosition = nextTargetTransform.position;
                 // Simulate rotation towards the target waypoint from the NPC's *current* (saved) position
                 // data.CurrentWorldPosition was saved by the Runner just before deactivation OR set by previous simulation tick.
                 Vector3 direction = (data.simulatedTargetPosition.Value - data.CurrentWorldPosition).normalized;
                 if (direction.sqrMagnitude > 0.001f)
                 {
                      data.CurrentWorldRotation = Quaternion.LookRotation(direction);
                 }
                 // Debug.Log($"SIM {data.Id}: Continuing path. Set simulated target position to {data.simulatedTargetPosition.Value}.", data.NpcGameObject); // Too noisy
            }
            else
            {
                 Debug.LogError($"SIM {data.Id}: Saved target waypoint with ID '{nextTargetWaypointID}' (index {currentPathStartIndex}) for path '{currentPath.PathID}' not found via WaypointManager during simulation OnEnter! Cannot continue path. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                 // Clear invalid path data on TiNpcData
                 data.simulatedPathID = null;
                 data.simulatedWaypointIndex = -1;
                 data.simulatedFollowReverse = false;
                 data.isFollowingPathBasic = false;
                 manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                 return;
            }

            // Log entry, noting that end behavior is defined on the PathSO
             Debug.Log($"SIM {data.Id}: Entering Generic Basic Path State: {name} for path '{currentPath?.PathID ?? "NULL"}'. End behavior defined on PathSO asset.");

            data.simulatedStateTimer = 0f; // Timer is not used for waiting in this state
        }

        public override void SimulateTick(TiNpcData data, float deltaTime, BasicNpcStateManager manager)
        {
            // Check dependencies and state validity at the start of the tick
            if (!data.isFollowingPathBasic || string.IsNullOrWhiteSpace(data.simulatedPathID) || WaypointManager.Instance == null)
            {
                // Should not happen if OnEnter was successful, but defensive
                Debug.LogError($"SIM {data.Id}: In BasicPathState but path state is invalid! isFollowingPathBasic: {data.isFollowingPathBasic}, simulatedPathID: {data.simulatedPathID}. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                manager.TransitionToBasicState(data, BasicState.BasicPatrol);
                return;
            }

            PathSO currentPath = WaypointManager.Instance.GetPath(data.simulatedPathID);
            if (currentPath == null)
            {
                 Debug.LogError($"SIM {data.Id}: PathSO with ID '{data.simulatedPathID}' not found via WaypointManager during simulation tick! Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                 manager.TransitionToBasicState(data, BasicState.BasicPatrol);
                 return;
            }


            // --- Determine Next Target Waypoint if current target is reached or null ---
            // This block is entered if simulatedTargetPosition was null on entry (new path)
            // OR if the NPC just arrived at the previous target in the *last* simulation tick.
            if (!data.simulatedTargetPosition.HasValue)
            {
                 // Arrived at the previous waypoint or just started the state (new path).
                 // Determine the *next* waypoint index to move towards.
                 // The index stored in data.simulatedWaypointIndex is the one they *just reached* or *started at*.
                 // The next target is the one after this index (or before if reverse).
                 int nextTargetIndex = data.simulatedFollowReverse ? data.simulatedWaypointIndex - 1 : data.simulatedWaypointIndex + 1;

                 // --- Check if the next index is valid (within path bounds) ---
                 bool reachedEndOfPathSimulation = data.simulatedFollowReverse ? (nextTargetIndex < 0) : (nextTargetIndex >= currentPath.WaypointCount);

                 if (!reachedEndOfPathSimulation)
                 {
                     // There is a next waypoint, update state to target it
                     data.simulatedWaypointIndex = nextTargetIndex; // Update the index to the *new target*
                     string nextTargetWaypointID = currentPath.GetWaypointID(data.simulatedWaypointIndex);
                     Transform nextTargetTransform = WaypointManager.Instance.GetWaypointTransform(nextTargetWaypointID);

                     if (nextTargetTransform != null)
                     {
                          data.simulatedTargetPosition = nextTargetTransform.position;
                          // Simulate rotation towards the next waypoint
                          SimulateRotationTowardsNextWaypoint(data, currentPath, data.simulatedWaypointIndex, data.simulatedFollowReverse); // <-- Pass data's state
                          // Debug.Log($"SIM {data.Id}: Reached waypoint. Targeting next waypoint '{nextTargetWaypointID}' (index {data.simulatedWaypointIndex}).", data.NpcGameObject); // Too noisy
                     }
                     else
                     {
                          Debug.LogError($"SIM {data.Id}: Next target waypoint with ID '{nextTargetWaypointID}' (index {data.simulatedWaypointIndex}) for path '{currentPath.PathID}' not found via WaypointManager! Stopping simulation path following. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                          manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                          return;
                     }
                 }
                 else
                 {
                     // Reached the end of the path simulation
                     Debug.Log($"SIM {data.Id}: Reached end of path simulation '{currentPath.PathID}'.", data.NpcGameObject);

                     // Reset path state on data (will be re-primed if the next state is a path)
                     data.simulatedPathID = null;
                     data.simulatedWaypointIndex = -1; // Invalid index
                     data.simulatedFollowReverse = false;
                     data.isFollowingPathBasic = false; // Clear the flag
                     data.simulatedTargetPosition = null; // Clear final target
                     data.simulatedStateTimer = 0f; // Ensure timer is zeroed

                     // --- Determine next state using PathSO's GetNextActiveStateDetails ---
                      // Ensure TiNpcManager is available via manager
                      if (manager?.tiNpcManager == null)
                      {
                           Debug.LogError($"SIM {data.Id}: TiNpcManager is null in BasicNpcStateManager! Cannot determine next state from PathSO end behavior. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                           manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                           return;
                      }

                      // Get the Active State details from the PathSO
                      PathTransitionDetails transitionDetails = currentPath?.GetNextActiveStateDetails(data, manager.tiNpcManager) ?? new PathTransitionDetails(null); 

                      if (transitionDetails.HasValidTransition)
                      {
                          Debug.Log($"SIM {data.Id}: Path '{currentPath.PathID}' end behavior determined next Active State: '{transitionDetails.TargetStateEnum.GetType().Name}.{transitionDetails.TargetStateEnum.ToString() ?? "NULL"}'.", data.NpcGameObject);

                          // Map the chosen Active State Enum to a Basic State Enum for simulation
                          System.Enum chosenBasicStateEnum = manager.tiNpcManager.GetBasicStateFromActiveState(transitionDetails.TargetStateEnum);

                          if (chosenBasicStateEnum != null)
                          {
                              // Need to ensure the chosen state is actually a BasicState or BasicPathState
                              // This check is technically redundant if GetBasicStateFromActiveState works correctly,
                              // but defensive coding is good.
                              if (manager.IsBasicState(chosenBasicStateEnum) || (chosenBasicStateEnum is BasicPathState && chosenBasicStateEnum.Equals(BasicPathState.BasicFollowPath))) // Explicitly check for BasicPathState.BasicFollowPath
                              {
                                  Debug.Log($"SIM {data.Id}: Mapped to Basic State '{chosenBasicStateEnum.GetType().Name}.{chosenBasicStateEnum.ToString() ?? "NULL"}'. Transitioning.", data.NpcGameObject);

                                   // --- If the chosen basic state is BasicPathState, prime the simulation path data ---
                                   if (chosenBasicStateEnum.Equals(BasicPathState.BasicFollowPath))
                                   {
                                        if (transitionDetails.PathAsset != null)
                                        {
                                             // Use the path details from the transitionDetails struct to initialize the simulated path data
                                             data.simulatedPathID = transitionDetails.PathAsset.PathID;
                                             data.simulatedWaypointIndex = transitionDetails.StartIndex; // Start *at* this index, the BasicPathStateSO.OnEnter will target the next one
                                             data.simulatedFollowReverse = transitionDetails.FollowReverse;
                                             data.isFollowingPathBasic = true; // Flag as following a path simulation
                                             Debug.Log($"SIM {data.Id}: Priming path simulation data for BasicPathState transition: PathID='{data.simulatedPathID}', Index={data.simulatedWaypointIndex}, Reverse={data.simulatedFollowReverse}.", data.NpcGameObject);

                                             // Set the NPC's position to the start waypoint of the *new* path
                                             string startWaypointID = transitionDetails.PathAsset.GetWaypointID(transitionDetails.StartIndex);
                                             Transform startWaypointTransform = WaypointManager.Instance.GetWaypointTransform(startWaypointID);
                                             if (startWaypointTransform != null)
                                             {
                                                  data.CurrentWorldPosition = startWaypointTransform.position;
                                                  // Simulate initial rotation towards the next waypoint of the new path
                                                  SimulateRotationTowardsNextWaypoint(data, transitionDetails.PathAsset, transitionDetails.StartIndex, transitionDetails.FollowReverse);
                                                  Debug.Log($"SIM {data.Id}: Initialized position for new path to {data.CurrentWorldPosition}.", data.NpcGameObject);
                                             } else {
                                                  Debug.LogError($"SIM {data.Id}: Start waypoint '{startWaypointID}' for next path '{transitionDetails.PathAsset.PathID}' not found during simulation transition! Cannot initialize position. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                                                  manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                                                  return; // Exit tick processing
                                             }
                                        } else {
                                             Debug.LogError($"SIM {data.Id}: Path '{currentPath.PathID}' end behavior specified PathState.FollowPath but the next Path Asset is null in transition details! Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                                             manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                                             return; // Exit tick processing
                                        }
                                   }
                                   // If the chosen basic state is NOT BasicPathState, the path simulation data is already cleared above.
                                   // The new state's OnEnter will handle its own initialization (timer, target, etc.).

                                   // Trigger the state transition
                                   manager.TransitionToBasicState(data, chosenBasicStateEnum);

                                   // Note: If transitioning to BasicPathState, the OnEnter of BasicPathState will run *after* this block,
                                   // but it will detect data.isFollowingPathBasic is true and use the data we just primed.
                                   // If transitioning to a non-path state, the new state's OnEnter will run.

                                   return; // Stop processing simulation for this tick after transition
                              }
                              else
                              {
                                   // Mapping returned something unexpected (not a BasicState or BasicPathState)
                                   Debug.LogError($"SIM {data.Id}: Path '{currentPath.PathID}' end behavior returned Active State '{transitionDetails.TargetStateEnum.GetType().Name}.{transitionDetails.TargetStateEnum.ToString() ?? "NULL"}', which mapped to non-Basic Enum '{chosenBasicStateEnum.GetType().Name}.{chosenBasicStateEnum.ToString() ?? "NULL"}' for simulation! Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                                   manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                                   return; // Exit tick processing
                              }
                          }
                          else
                          {
                               // Mapping failed
                               Debug.LogError($"SIM {data.Id}: Path '{currentPath.PathID}' end behavior returned Active State '{transitionDetails.TargetStateEnum.GetType().Name}.{transitionDetails.TargetStateEnum.ToString() ?? "NULL"}', but mapping to Basic State failed! Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                               manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                               return; // Exit tick processing
                          }
                      }
                      else
                      {
                           // GetNextActiveStateDetails returned invalid details (invalid config or decision failed)
                           Debug.LogError($"SIM {data.Id}: Path '{currentPath.PathID}' end behavior returned invalid details or failed to determine next Active State. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                           manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                           return; // Exit tick processing
                      }
                 }
            }
            // --- End Determine Next Target Waypoint ---


            // --- Simulate Movement Towards Current Target Waypoint ---
            // This block is entered if simulatedTargetPosition was set in OnEnter (continuing path)
            // OR if it was set in the previous SimulateTick (new path or moved to next segment).
            if (data.simulatedTargetPosition.HasValue)
            {
                Vector3 targetPosition = data.simulatedTargetPosition.Value;
                Vector3 currentPosition = data.CurrentWorldPosition;

                Vector3 direction = (targetPosition - currentPosition).normalized;
                float moveDistance = 2.0f * deltaTime; // Assuming a fixed simulation speed of 2.0f (Match BasicPatrol)

                // Check if the remaining distance is less than the move distance
                // If so, move exactly to the target to avoid overshooting
                if (Vector3.Distance(currentPosition, targetPosition) <= moveDistance)
                {
                    data.CurrentWorldPosition = targetPosition;
                    // Simulate rotation to face the direction of the segment just completed
                     if (direction.sqrMagnitude > 0.001f)
                     {
                          data.CurrentWorldRotation = Quaternion.LookRotation(direction);
                     }
                    data.simulatedTargetPosition = null; // Clear target, will get next in the next tick
                    // Debug.Log($"SIM {data.Id}: Simulated arrival at target position {targetPosition}. Clearing target.", data.NpcGameObject); // Too noisy
                }
                else
                {
                    // Move towards the target
                    data.CurrentWorldPosition += direction * moveDistance;
                    // Simulate rotation to face the direction of movement
                     if (direction.sqrMagnitude > 0.001f)
                     {
                          data.CurrentWorldRotation = Quaternion.LookRotation(direction);
                     }
                    // Debug.Log($"SIM {data.Id}: Simulating movement towards {targetPosition}. New Pos: {data.CurrentWorldPosition}."); // Too noisy
                }
            }
            // --- End Simulate Movement ---
        }

         public override void OnExit(TiNpcData data, BasicNpcStateManager manager)
         {
             base.OnExit(data, manager); // Call base OnExit (logs exit)

             // Note: Path state on data (simulatedPathID, simulatedWaypointIndex, etc.)
             // is reset in SimulateTick when the path ends, or in OnEnter if starting a new path.
             // If exiting mid-path (e.g., due to timeout, though ShouldUseTimeout is false, or activation),
             // the path state on data should ideally be preserved for activation.
             // Clearing simulatedTargetPosition here is safe.
             data.simulatedTargetPosition = null;
             // data.simulatedStateTimer is reset by base OnExit if ShouldUseTimeout is false, which is correct here.
         }

        /// <summary>
         /// Simulates rotation towards the next waypoint when starting a path or reaching a waypoint.
         /// </summary>
         private void SimulateRotationTowardsNextWaypoint(TiNpcData data, PathSO path, int currentWaypointIndex, bool followReverse)
         {
              // Determine the *next* target waypoint index
              int nextTargetIndex = followReverse ? currentWaypointIndex - 1 : currentWaypointIndex + 1;

              // Check if there is a next waypoint to look towards
              bool hasNextWaypoint = followReverse ? (nextTargetIndex >= 0) : (nextTargetIndex < path.WaypointCount);

              if (hasNextWaypoint)
              {
                   string nextTargetWaypointID = path.GetWaypointID(nextTargetIndex);
                   Transform nextTargetTransform = WaypointManager.Instance.GetWaypointTransform(nextTargetWaypointID);

                   if (nextTargetTransform != null)
                   {
                        // Look direction is from current position towards the next target's position
                        Vector3 direction = (nextTargetTransform.position - data.CurrentWorldPosition).normalized;
                        if (direction.sqrMagnitude > 0.001f)
                        {
                             data.CurrentWorldRotation = Quaternion.LookRotation(direction);
                        }
                   }
                   // If next waypoint transform is null, rotation won't be updated, which is acceptable.
              }
              // If no next waypoint (reached end), rotation stays as it was towards the final segment.
         }


        // REMOVED: OnValidate method as it only validated the removed serialized fields.
    }
}

// --- END OF FILE BasicPathStateSO.cs (Refactored Generic) ---