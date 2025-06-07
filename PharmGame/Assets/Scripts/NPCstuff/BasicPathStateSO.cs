// --- START OF FILE BasicPathStateSO.cs (Final Refactored) ---

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
    /// Basic state for simulating an inactive TI NPC following a predefined waypoint path.
    /// Operates directly on TiNpcData.
    /// Now supports restoring progress for activated TI NPCs and triggers decision logic
    /// upon simulating reaching a linked Decision Point.
    /// MODIFIED: End behavior (Decision Point or fixed transition) is now defined on the PathSO asset.
    /// Uses PathTransitionDetails to handle transitions to other paths.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicPathState_", menuName = "NPC/Basic States/Basic Follow Path", order = 50)] // Order appropriately
    public class BasicPathStateSO : BasicNpcStateSO
    {
        // --- State Configuration ---
        [Header("Basic Path Settings")]
        [Tooltip("The PathSO asset defining the path to simulate.")]
        [SerializeField] private PathSO pathAsset;
        [Tooltip("The index of the waypoint to start the path from (0-based).")]
        [SerializeField] private int startIndex = 0; // <-- This field is on the SO asset
        [Tooltip("If true, simulate following the path in reverse from the start index.")]
        [SerializeField] private bool followReverse = false; // <-- This field is on the SO asset

        // --- BasicNpcStateSO Overrides ---
        public override System.Enum HandledBasicState => BasicPathState.BasicFollowPath;

        // Basic Path states do not use the standard timeout; their 'timeout' is reaching the end of the path.
        // However, they still need to decrement the *general* simulation timer on TiNpcData
        // if their SimulateTick doesn't fully consume the deltaTime (e.g., waiting at a waypoint).
        // For now, let's keep ShouldUseTimeout false, as the timeout is transition to BasicExitingStore,
        // which is not the primary way this state transitions.
        // The simulatedStateTimer *is* used by BasicLookToShop for its specific timeout.
        // Let's revert ShouldUseTimeout to read the base value, but override the timer initialization in OnEnter.
        // The base OnEnter already handles resetting the timer if ShouldUseTimeout is false. Let's stick to that.
        // If a PathState *could* time out (e.g., stuck simulation), we'd set this to true.
        // public override bool ShouldUseTimeout => base.ShouldUseTimeout; // Revert to base property
         public override bool ShouldUseTimeout => false; // Keep false as per original intent


        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            base.OnEnter(data, manager); // Call base OnEnter (logs entry, resets base timer/target/path data)

            // Validate WaypointManager dependency early
             if (WaypointManager.Instance == null)
             {
                  Debug.LogError($"SIM {data.Id}: WaypointManager.Instance is null! Cannot simulate path. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                  manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                  return;
             }

            PathSO currentPath;
            int currentPathStartIndex;
            bool currentPathFollowReverse;

            // --- Determine path data source: TiNpcData (if continuing) or SO Asset (if starting new) ---
            if (data.isFollowingPathBasic) // Check if TiNpcData indicates continuing a path simulation
            {
                 // Continuing an existing path simulation (e.g., transitioned from BasicIdleAtHome or another BasicPathState)
                 Debug.Log($"SIM {data.Id}: BasicPathState '{name}' OnEnter. Continuing path simulation from TiNpcData.", data.NpcGameObject);
                 currentPath = WaypointManager.Instance.GetPath(data.simulatedPathID); // Get path reference from data
                 currentPathStartIndex = data.simulatedWaypointIndex; // Use index from data (this is the target they were moving towards)
                 currentPathFollowReverse = data.simulatedFollowReverse; // Use direction from data

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

                 // The position should already be set in the previous state's SimulateTick or OnEnter.
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

            }
            else // Not continuing a path simulation, start a new one based on this SO's configuration
            {
                 Debug.Log($"SIM {data.Id}: BasicPathState '{name}' OnEnter. Starting NEW path simulation from SO asset.", data.NpcGameObject);
                 currentPath = pathAsset; // Use path reference from SO asset
                 currentPathStartIndex = this.startIndex; // Use index from SO asset
                 currentPathFollowReverse = this.followReverse; // Use direction from SO asset

                 // Validate SO asset dependencies
                 if (currentPath == null)
                 {
                     Debug.LogError($"SIM {data.Id}: BasicPathStateSO '{name}' has no Path Asset assigned and is not continuing a path! Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                     manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                     return;
                 }

                 // Initialize path state on TiNpcData based on SO asset config
                 data.simulatedPathID = currentPath.PathID;
                 // For simulation, we assume they start *at* the first waypoint of the path segment.
                 // The initial NavMesh leg is skipped in simulation.
                 data.simulatedWaypointIndex = currentPathStartIndex; // Start *at* this index
                 data.simulatedFollowReverse = currentPathFollowReverse;
                 data.isFollowingPathBasic = true; // <-- Set the flag

                 // Set the initial position to the start waypoint's position
                 string startWaypointID = currentPath.GetWaypointID(currentPathStartIndex);
                 Transform startWaypointTransform = WaypointManager.Instance.GetWaypointTransform(startWaypointID);
                 if (startWaypointTransform != null)
                 {
                      data.CurrentWorldPosition = startWaypointTransform.position;
                      // Simulate initial rotation towards the next waypoint
                      SimulateRotationTowardsNextWaypoint(data, currentPath, data.simulatedWaypointIndex, data.simulatedFollowReverse); // <-- Pass data's state
                 }
                 else
                 {
                      Debug.LogError($"SIM {data.Id}: Start waypoint with ID '{startWaypointID}' (index {currentPathStartIndex}) for path '{currentPath.PathID}' not found via WaypointManager during BasicPathState OnEnter! Cannot initialize position. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                      // Clear invalid path data on TiNpcData
                      data.simulatedPathID = null;
                      data.simulatedWaypointIndex = -1;
                      data.simulatedFollowReverse = false;
                      data.isFollowingPathBasic = false;
                      manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                      return;
                 }

                 // The first target waypoint will be determined in the first SimulateTick.
                 data.simulatedTargetPosition = null; // Ensure target is null to trigger next waypoint lookup in SimulateTick
            }
            // --- END Determine path data source ---


             // Log entry, noting that end behavior is defined on the PathSO
             Debug.Log($"SIM {data.Id}: Entering Basic Path State: {name} for path '{currentPath?.PathID ?? "NULL"}'. End behavior defined on PathSO asset.");


            data.simulatedStateTimer = 0f; // Timer is not used for waiting in this state


            // Basic path states don't check timeout while moving.
            // The timeout logic in BasicNpcStateManager.SimulateTickForNpc checks !data.simulatedTargetPosition.HasValue,
            // so the timer is implicitly 'paused' while the NPC is moving towards a waypoint.
            // The timer will only count down when data.simulatedTargetPosition is null,
            // which happens right after arriving at a waypoint (and before getting the next target).
            // This means timeout could potentially occur while "waiting" briefly at a waypoint,
            // if that wait lasted longer than the timeout, or if the state logic explicitly set a target and timer.
            // For this BasicPathState, the timer should likely remain 0 unless the derived state *specifically* uses it.
            // The base OnEnter already handles resetting the timer if ShouldUseTimeout is false, which is correct here.
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
                          SimulateRotationTowardsNextWaypoint(data, currentPath, data.simulatedWaypointIndex, data.simulatedFollowReverse); // <-- Pass data.simulatedFollowReverse
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

                      PathTransitionDetails transitionDetails = currentPath?.GetNextActiveStateDetails(data, manager.tiNpcManager) ?? new PathTransitionDetails(null); 

                      if (transitionDetails.HasValidTransition)
                      {
                          Debug.Log($"SIM {data.Id}: Path '{currentPath.PathID}' end behavior determined next Active State: '{transitionDetails.TargetStateEnum.GetType().Name}.{transitionDetails.TargetStateEnum.ToString() ?? "NULL"}'.", data.NpcGameObject);

                          // Map the chosen Active State Enum to a Basic State Enum
                          System.Enum chosenBasicStateEnum = manager.tiNpcManager.GetBasicStateFromActiveState(transitionDetails.TargetStateEnum);

                          if (chosenBasicStateEnum != null)
                          {
                              // Need to ensure the chosen state is actually a BasicState or BasicPathState
                              // This check is technically redundant if GetBasicStateFromActiveState works correctly,
                              // but defensive coding is good.
                              if (manager.IsBasicState(chosenBasicStateEnum) || (chosenBasicStateEnum is BasicPathState && (BasicPathState)chosenBasicStateEnum != BasicPathState.None))
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
             // data.simulatedStateTimer = 0f; // Timer is reset by base OnExit if ShouldUseTimeout is false
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


        // Optional: Add validation in editor
        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (pathAsset != null)
            {
                if (startIndex < 0 || startIndex >= pathAsset.WaypointCount)
                {
                    Debug.LogWarning($"BasicPathStateSO '{name}': Start Index ({startIndex}) is out of bounds for Path Asset '{pathAsset.name}' (Waypoint Count: {pathAsset.WaypointCount}).", this);
                }
            }

             // Add validation that pathAsset is assigned
             if (pathAsset == null)
             {
                  Debug.LogError($"BasicPathStateSO '{name}': Path Asset is not assigned! This state requires a PathSO.", this);
             }
        }
        #endif
    }
}

// --- END OF FILE BasicPathStateSO.cs (Final Refactored) ---