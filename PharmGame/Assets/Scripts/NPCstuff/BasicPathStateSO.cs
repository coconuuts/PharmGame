// --- START OF FILE BasicPathStateSO.cs ---

// --- Updated BasicPathStateSO.cs (Step 6.1) ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData, TiNpcManager
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicPathState enum, BasicNpcStateSO, BasicNpcStateManager
using Game.Navigation; // Needed for PathSO and WaypointManager
using Random = UnityEngine.Random; // Specify UnityEngine.Random
using System.Linq; // Needed for Contains
using Game.NPC.Decisions; // Needed for DecisionPointSO, NpcDecisionHelper

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    /// <summary>
    /// Basic state for simulating an inactive TI NPC following a predefined waypoint path.
    /// Operates directly on TiNpcData.
    /// Now supports restoring progress for activated TI NPCs and triggers decision logic
    /// upon simulating reaching a linked Decision Point.
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

         // --- Decision Point Configuration ---
        [Header("Decision Point")] // <-- NEW HEADER
        [Tooltip("Optional: If assigned, this path leads to a Decision Point. Upon simulating reaching the end, the NPC will trigger decision logic instead of transitioning to a fixed next state.")]
        [SerializeField] private DecisionPointSO decisionPoint; // <-- NEW FIELD
        // --- END NEW ---


        [Header("Transitions (Used if NO Decision Point is assigned)")] // <-- Updated Header
        [Tooltip("The Enum key for the basic state to transition to upon reaching the end of the path simulation (used if no Decision Point is assigned).")] // <-- Updated Tooltip
        [SerializeField] private string nextBasicStateEnumKey;
        [Tooltip("The Type name of the Enum key for the next basic state (e.NPC.BasicStates.BasicState, Game.NPC.BasicStates.BasicPathState) (used if no Decision Point is assigned).")] // <-- Updated Tooltip
        [SerializeField] private string nextBasicStateEnumType;


        // --- BasicNpcStateSO Overrides ---
        public override System.Enum HandledBasicState => BasicPathState.BasicFollowPath; // <-- Use the new enum

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

            // --- NEW: Determine path data source: TiNpcData (if continuing) or SO Asset (if starting new) ---
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


             // --- NEW: Validate Decision Point link if assigned (Moved here) ---
             if (decisionPoint != null)
             {
                 // Check TiNpcManager instance needed for decision logic
                 if (TiNpcManager.Instance == null)
                 {
                     Debug.LogError($"SIM {data.Id}: BasicPathStateSO '{name}' links to Decision Point '{decisionPoint.name}', but TiNpcManager.Instance is null! Cannot perform decision logic. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                     manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                     return;
                 }

                 if (string.IsNullOrWhiteSpace(decisionPoint.PointID) || string.IsNullOrWhiteSpace(decisionPoint.WaypointID))
                 {
                      Debug.LogError($"SIM {data.Id}: BasicPathStateSO '{name}' links to Decision Point '{decisionPoint.name}', but Decision Point SO has invalid PointID ('{decisionPoint.PointID}') or WaypointID ('{decisionPoint.WaypointID}')! Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                      manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                      return;
                 }
                 // Ensure the Decision Point Waypoint exists in the scene for simulation target lookup
                 if (WaypointManager.Instance.GetWaypointTransform(decisionPoint.WaypointID) == null)
                 {
                      Debug.LogError($"SIM {data.Id}: BasicPathStateSO '{name}' links to Decision Point '{decisionPoint.name}' (WaypointID: '{decisionPoint.WaypointID}'), but the waypoint is not found in the scene via WaypointManager! Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                      manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                      return;
                 }
                  // If a Decision Point is assigned, override the default next state logging
                  Debug.Log($"SIM {data.Id}: Entering Basic Path State: {name} leading to Decision Point '{decisionPoint.PointID}'.");
             } else {
                 // Log the standard next state if no Decision Point is assigned
                 Debug.Log($"SIM {data.Id}: Entering Basic Path State: {name} leading to fixed next basic state.");
             }
             // --- END NEW ---


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

                     // Reset path state on data
                     data.simulatedPathID = null;
                     data.simulatedWaypointIndex = -1; // Invalid index
                     data.simulatedFollowReverse = false;
                     data.isFollowingPathBasic = false; // Clear the flag
                     data.simulatedTargetPosition = null; // Clear final target
                     data.simulatedStateTimer = 0f; // Ensure timer is zeroed

                     // --- NEW: Trigger decision logic if Decision Point is assigned, otherwise transition to fixed next basic state ---
                      if (decisionPoint != null)
                      {
                           Debug.Log($"SIM {data.Id}: Reached Decision Point '{decisionPoint.PointID}' (via simulation). Triggering decision logic.", data.NpcGameObject);
                           // Call the shared decision logic helper for simulation
                           MakeBasicDecisionAndTransition(data, decisionPoint, manager); // Uses data, decisionPoint, and manager

                      }
                      else
                      {
                           // No Decision Point, transition to the configured next basic state (standard path behavior)
                           Debug.Log($"SIM {data.Id}: Reached end of standard path (via simulation). Transitioning to fixed next basic state.", data.NpcGameObject);
                           TransitionToNextBasicState(data, manager); // Use standard fixed transition
                      }
                     // --- END NEW ---

                     return; // Stop processing simulation for this tick
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
        /// Handles the transition to the next basic state upon path simulation completion
        /// IF no Decision Point is assigned.
        /// </summary>
        private void TransitionToNextBasicState(TiNpcData data, BasicNpcStateManager manager)
        {
             // This method is only called if decisionPoint is null
             if (string.IsNullOrEmpty(nextBasicStateEnumKey) || string.IsNullOrEmpty(nextBasicStateEnumType))
             {
                  Debug.LogWarning($"SIM {data.Id}: BasicPathState '{name}' has no next basic state configured AND no Decision Point assigned. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                  manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Default fallback
                  return;
             }

             // Use the manager to transition by string key/type
             System.Enum targetEnum = null;
             try
             {
                  Type type = Type.GetType(nextBasicStateEnumType);
                  if (type == null || !type.IsEnum)
                  {
                       Debug.LogError($"SIM {data.Id}: Invalid Next Basic State Enum Type string '{nextBasicStateEnumType}' configured in BasicPathState '{name}'. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                       manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                       return;
                  }
                  // Attempt to parse the string key into an enum value
                  targetEnum = (System.Enum)Enum.Parse(type, nextBasicStateEnumKey);
             }
             catch (Exception e)
             {
                  Debug.LogError($"SIM {data.Id}: Error parsing Next Basic State config '{nextBasicStateEnumKey}' of type '{nextBasicStateEnumType}' in BasicPathState '{name}': {e.Message}. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                  manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                  return;
             }

             manager.TransitionToBasicState(data, targetEnum); // Transition using the parsed enum
        }

         /// <summary>
         /// Triggers the data-driven decision logic and transitions to the chosen basic state (simulation).
         /// This is called when the NPC simulates reaching the end of a path linked to a Decision Point.
         /// </summary>
         private void MakeBasicDecisionAndTransition(TiNpcData data, DecisionPointSO decisionPoint, BasicNpcStateManager manager)
         {
             if (data == null)
             {
                  Debug.LogError($"SIM Cannot make basic decision at point '{decisionPoint?.PointID ?? "NULL"}' - TiNpcData is null! Transitioning to BasicPatrol fallback.", data?.NpcGameObject);
                  manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                  return;
             }
             if (decisionPoint == null) // Should not happen due to calling context, but defensive
             {
                  Debug.LogError($"SIM {data.Id}: Cannot make basic decision - DecisionPointSO is null! Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                  manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                  return;
             }
             // Check TiNpcManager dependency
             if (TiNpcManager.Instance == null)
             {
                  Debug.LogError($"SIM {data.Id}: TiNpcManager.Instance is null! Cannot perform decision logic.", data.NpcGameObject);
                   manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                   return;
             }

             // --- NEW: Check endDay flag before making decision (Already added in Substep 3.2, keeping here) ---
             // If it's time to go home, override the decision and go straight to BasicExitingStore
             if (data.isEndingDay)
             {
                  Debug.Log($"SIM {data.Id}: Reached Decision Point '{decisionPoint.PointID}' but is in endDay schedule ({data.endDay}). Transitioning to BasicExitingStore instead of making decision.", data.NpcGameObject);
                  manager.TransitionToBasicState(data, BasicState.BasicExitingStore);
                  return; // Exit early
             }
             // --- END NEW ---


             // --- Call the shared Decision Logic helper ---
             // Call the MakeDecision helper from the static helper class. It returns an Active State Enum.
             System.Enum chosenActiveStateEnum = NpcDecisionHelper.MakeDecision(data, decisionPoint, TiNpcManager.Instance);

             if (chosenActiveStateEnum != null)
             {
                 // --- NEW: Map the chosen Active State Enum to a Basic State Enum ---
                 System.Enum chosenBasicStateEnum = TiNpcManager.Instance.GetBasicStateFromActiveState(chosenActiveStateEnum);

                 if (chosenBasicStateEnum != null)
                 {
                     // Need to ensure the chosen state is actually a BasicState or BasicPathState
                     // This check is technically redundant if GetBasicStateFromActiveState works correctly,
                     // but defensive coding is good.
                     if (manager.IsBasicState(chosenBasicStateEnum) || (chosenBasicStateEnum is BasicPathState && (BasicPathState)chosenBasicStateEnum != BasicPathState.None))
                     {
                         Debug.Log($"SIM {data.Id}: Decision made at '{decisionPoint.PointID}'. Chosen Active State '{chosenActiveStateEnum.GetType().Name}.{chosenActiveStateEnum.ToString() ?? "NULL"}' maps to chosen Basic State '{chosenBasicStateEnum.GetType().Name}.{chosenBasicStateEnum.ToString() ?? "NULL"}'. Transitioning.", data.NpcGameObject);
                         manager.TransitionToBasicState(data, chosenBasicStateEnum); // Transition using the mapped Basic Enum key
                     }
                     else
                     {
                          // Mapping returned something unexpected (not a BasicState or BasicPathState)
                          Debug.LogError($"SIM {data.Id}: Decision logic at '{decisionPoint.PointID}' returned Active State '{chosenActiveStateEnum.GetType().Name}.{chosenActiveStateEnum.ToString() ?? "NULL"}', which mapped to non-Basic Enum '{chosenBasicStateEnum.GetType().Name}.{chosenBasicStateEnum.ToString() ?? "NULL"}' for simulation! Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                          manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                     }
                 }
                 else
                 {
                      // Mapping failed
                      Debug.LogError($"SIM {data.Id}: Decision logic at '{decisionPoint.PointID}' returned Active State '{chosenActiveStateEnum.GetType().Name}.{chosenActiveStateEnum.ToString() ?? "NULL"}', but mapping to Basic State failed! Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                      manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                 }
                 // --- END NEW ---
             }
             else
             {
                  Debug.LogError($"SIM {data.Id}: Decision logic at '{decisionPoint.PointID}' returned a null state or no valid options! Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                  manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
             }
             // --- END Call ---
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

             // Validate Decision Point link
            if (decisionPoint != null)
            {
                 if (string.IsNullOrWhiteSpace(decisionPoint.PointID))
                 {
                      Debug.LogError($"BasicPathStateSO '{name}': Decision Point '{decisionPoint.name}' has an empty Point ID.", this);
                 }
                 if (string.IsNullOrWhiteSpace(decisionPoint.WaypointID))
                 {
                      Debug.LogError($"BasicPathStateSO '{name}': Decision Point '{decisionPoint.name}' has an empty Waypoint ID.", this);
                 }
                 // Warn if standard transitions are also configured when a Decision Point is linked
                 if (!string.IsNullOrEmpty(nextBasicStateEnumKey) || !string.IsNullOrEmpty(nextBasicStateEnumType))
                 {
                      Debug.LogWarning($"BasicPathStateSO '{name}': A Decision Point is assigned, but standard Next Basic State ('{nextBasicStateEnumKey}' of type '{nextBasicStateEnumType}') is also configured. The standard Next Basic State will be ignored when the Decision Point is reached.", this);
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
                           Debug.LogError($"BasicPathStateSO '{name}': Decision Point '{decisionPoint.PointID}' (Waypoint ID '{decisionPoint.WaypointID}') is NOT the end waypoint of the assigned Path Asset '{pathAsset.name}' (Expected Waypoint ID '{expectedEndWaypointID}' at index {expectedEndIndex} with reverse={followReverse})! This Decision Point path will likely not work correctly.", this);
                      }
                 } else if (pathAsset != null)
                 {
                      Debug.LogWarning($"BasicPathStateSO '{name}': Path Asset '{pathAsset.name}' has fewer than 1 waypoint. Cannot validate Decision Point waypoint.", this);
                 }
            }
             else // No Decision Point assigned, validate standard next state configuration
             {
                 if (!string.IsNullOrEmpty(nextBasicStateEnumKey) || !string.IsNullOrEmpty(nextBasicStateEnumType))
                 {
                      try
                      {
                           Type type = Type.GetType(nextBasicStateEnumType);
                           if (type == null || !type.IsEnum)
                           {
                                Debug.LogError($"BasicPathStateSO '{name}': Invalid Next Basic State Enum Type string '{nextBasicStateEnumType}' configured.", this);
                           } else
                           {
                                // Optional: Check if the key exists in the enum
                                if (!Enum.GetNames(type).Contains(nextBasicStateEnumKey))
                                {
                                     Debug.LogError($"BasicPathStateSO '{name}': Next Basic State Enum Type '{nextBasicStateEnumType}' is valid, but key '{nextBasicStateEnumKey}' does not exist in that enum.", this);
                                }
                                // Optional: Check if the enum type is actually a BasicState or BasicPathState
                                if (type != typeof(BasicState) && type != typeof(BasicPathState))
                                {
                                     Debug.LogWarning($"BasicPathStateSO '{name}': Next Basic State Enum Type '{nextBasicStateEnumType}' is configured, but it's not a BasicState or BasicPathState enum. Ensure this is intended.", this);
                                }
                           }
                      }
                      catch (Exception e)
                      {
                           Debug.LogError($"BasicPathStateSO '{name}': Error parsing Next Basic State config: {e.Message}", this);
                      }
                 }
                  else // Neither Decision Point nor standard next state is configured
                 {
                      Debug.LogWarning($"BasicPathStateSO '{name}': Neither a Decision Point nor a standard Next Basic State is configured. This state will transition to BasicPatrol fallback upon path completion.", this);
                 }
             }
        }
        #endif
    }
}

// --- END OF FILE BasicPathStateSO.cs ---