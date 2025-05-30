// --- START OF FILE BasicPathStateSO.cs ---

// --- START OF FILE BasicPathStateSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicPathState enum, BasicNpcStateSO
using Game.Navigation; // Needed for PathSO and WaypointManager
using Random = UnityEngine.Random; // Specify UnityEngine.Random
using System.Linq; // Needed for Contains

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    /// <summary>
    /// Basic state for simulating an inactive TI NPC following a predefined waypoint path.
    /// Operates directly on TiNpcData.
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

        [Header("Transitions")]
        [Tooltip("The Enum key for the basic state to transition to upon reaching the end of the path simulation.")]
        [SerializeField] private string nextBasicStateEnumKey;
        [Tooltip("The Type name of the Enum key for the next basic state (e.g., Game.NPC.BasicStates.BasicState, Game.NPC.BasicStates.BasicPathState).")]
        [SerializeField] private string nextBasicStateEnumType;


        // --- BasicNpcStateSO Overrides ---
        public override System.Enum HandledBasicState => BasicPathState.BasicFollowPath; // <-- Use the new enum

        // Basic Path states do not use the standard timeout; their 'timeout' is reaching the end of the path.
        public override bool ShouldUseTimeout => false; // Override base property


        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            base.OnEnter(data, manager); // Call base OnEnter (logs entry, resets base timer/target)

            if (pathAsset == null)
            {
                Debug.LogError($"SIM {data.Id}: BasicPathStateSO '{name}' has no Path Asset assigned! Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                return;
            }
             if (WaypointManager.Instance == null)
             {
                  Debug.LogError($"SIM {data.Id}: WaypointManager.Instance is null! Cannot simulate path '{pathAsset.PathID}'. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                  manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                  return;
             }

            PathSO currentPath = WaypointManager.Instance.GetPath(data.simulatedPathID); // Get path reference early
            if (currentPath == null)
            {
                 Debug.LogError($"SIM {data.Id}: PathSO with ID '{data.simulatedPathID}' not found via WaypointManager during BasicPathState OnEnter! Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                 manager.TransitionToBasicState(data, BasicState.BasicPatrol);
                 return;
            }


            // --- Initialize Path Following State on TiNpcData ---
            // Check if we are entering this state *while already following a path* (e.g., deactivated mid-path)
            // or if we are starting a *new* path.
            // We check data.isFollowingPathBasic AND if the path ID matches.
            // If data.isFollowingPathBasic is true but the path ID doesn't match, it means they were on a *different* path
            // when deactivated, so we should treat this as starting a new path.
            if (!data.isFollowingPathBasic || data.simulatedPathID != pathAsset.PathID) // <-- Check path ID match
            {
                 // Starting a new path or path ID mismatch - initialize from state config
                 Debug.Log($"SIM {data.Id}: BasicPathState '{name}' OnEnter. Starting new path '{pathAsset.PathID}'.", data.NpcGameObject);
                 data.simulatedPathID = pathAsset.PathID;
                 // For simulation, we assume they start *at* the first waypoint of the path segment.
                 // The initial NavMesh leg is skipped in simulation.
                 // --- MODIFIED: Access startIndex and followReverse from 'this' SO asset ---
                 data.simulatedWaypointIndex = this.startIndex; // Start *at* this index
                 data.simulatedFollowReverse = this.followReverse;
                 // --- END MODIFIED ---
                 data.isFollowingPathBasic = true;

                 // Set the initial position to the start waypoint's position
                 string startWaypointID = pathAsset.GetWaypointID(this.startIndex); // <-- Access startIndex from 'this'
                 Transform startWaypointTransform = WaypointManager.Instance.GetWaypointTransform(startWaypointID);
                 if (startWaypointTransform != null)
                 {
                      data.CurrentWorldPosition = startWaypointTransform.position;
                      // Simulate initial rotation towards the next waypoint
                      SimulateRotationTowardsNextWaypoint(data, pathAsset, data.simulatedWaypointIndex, data.simulatedFollowReverse);
                 }
                 else
                 {
                      Debug.LogError($"SIM {data.Id}: Start waypoint with ID '{startWaypointID}' (index {this.startIndex}) for path '{pathAsset.PathID}' not found via WaypointManager! Cannot initialize position. Transitioning to BasicPatrol fallback.", data.NpcGameObject); // <-- Access startIndex from 'this'
                      manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                      return;
                 }

                 // The first target waypoint will be determined in the first SimulateTick.
                 data.simulatedTargetPosition = null; // Ensure target is null to trigger next waypoint lookup in SimulateTick
                 data.simulatedStateTimer = 0f; // Timer is not used for waiting in this state
            }
            else
            {
                 // Continuing an existing path (deactivated mid-path)
                 // The data already has the correct path ID, target index, and reverse flag saved from deactivation.
                 Debug.Log($"SIM {data.Id}: BasicPathState '{name}' OnEnter. Continuing existing path '{data.simulatedPathID}' towards index {data.simulatedWaypointIndex}, reverse: {data.simulatedFollowReverse}.", data.NpcGameObject); // Log towards index

                 // --- MODIFIED: Set the simulated target position based on the saved index ---
                 // The saved index (data.simulatedWaypointIndex) is the waypoint they were moving *towards*.
                 // We need to set the simulated target position to this waypoint's location.
                 string nextTargetWaypointID = currentPath.GetWaypointID(data.simulatedWaypointIndex); // Use the saved index
                 Transform nextTargetTransform = WaypointManager.Instance.GetWaypointTransform(nextTargetWaypointID);

                 if (nextTargetTransform != null)
                 {
                      data.simulatedTargetPosition = nextTargetTransform.position;
                      // Simulate rotation towards the target waypoint from the NPC's *current* (saved) position
                      // data.CurrentWorldPosition was saved by the Runner just before deactivation.
                      Vector3 direction = (data.simulatedTargetPosition.Value - data.CurrentWorldPosition).normalized;
                      if (direction.sqrMagnitude > 0.001f)
                      {
                           data.CurrentWorldRotation = Quaternion.LookRotation(direction);
                      }
                      Debug.Log($"SIM {data.Id}: Continuing path. Set simulated target position to {data.simulatedTargetPosition.Value}.", data.NpcGameObject);
                 }
                 else
                 {
                      Debug.LogError($"SIM {data.Id}: Saved target waypoint with ID '{nextTargetWaypointID}' (index {data.simulatedWaypointIndex}) for path '{currentPath.PathID}' not found via WaypointManager during simulation OnEnter! Cannot continue path. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                      manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                      return;
                 }
                 // --- END MODIFIED ---

                 data.simulatedStateTimer = 0f; // Timer is not used for waiting in this state
                 // data.isFollowingPathBasic is already true
                 // data.simulatedPathID is already set
                 // data.simulatedWaypointIndex is already set
                 // data.simulatedFollowReverse is already set
            }
            // --- End Initialize ---
        }

        public override void SimulateTick(TiNpcData data, float deltaTime, BasicNpcStateManager manager)
        {
            if (!data.isFollowingPathBasic || data.simulatedPathID == null || WaypointManager.Instance == null)
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
                 // --- MODIFIED: Access simulatedFollowReverse from data ---
                 int nextTargetIndex = data.simulatedFollowReverse ? data.simulatedWaypointIndex - 1 : data.simulatedWaypointIndex + 1;
                 // --- END MODIFIED ---

                 // --- Check if the next index is valid (within path bounds) ---
                 // --- MODIFIED: Access simulatedFollowReverse from data ---
                 bool reachedEnd = data.simulatedFollowReverse ? (nextTargetIndex < 0) : (nextTargetIndex >= currentPath.WaypointCount);
                 // --- END MODIFIED ---

                 if (!reachedEnd)
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
                     Debug.Log($"SIM {data.Id}: Reached end of path simulation '{currentPath.PathID}'. Transitioning to next basic state.", data.NpcGameObject);
                     // Reset path state on data
                     data.simulatedPathID = null;
                     data.simulatedWaypointIndex = -1;
                     data.simulatedFollowReverse = false;
                     data.isFollowingPathBasic = false;
                     data.simulatedTargetPosition = null; // Clear final target

                     // Transition to the configured next basic state
                     TransitionToNextBasicState(data, manager);
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
                float moveDistance = 2.0f * deltaTime; // Assuming a fixed simulation speed of 2.0f

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
             data.simulatedStateTimer = 0f; // Ensure timer is zeroed
         }

        /// <summary>
        /// Handles the transition to the next basic state upon path simulation completion.
        /// </summary>
        private void TransitionToNextBasicState(TiNpcData data, BasicNpcStateManager manager)
        {
             if (string.IsNullOrEmpty(nextBasicStateEnumKey) || string.IsNullOrEmpty(nextBasicStateEnumType))
             {
                  Debug.LogWarning($"SIM {data.Id}: BasicPathState '{name}' has no next basic state configured. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                  manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Default fallback
                  return;
             }

             // Use the manager to transition by string key/type
             // Note: BasicNpcStateManager.TransitionToBasicState currently only takes System.Enum.
             // We need to modify BasicNpcStateManager to add a string key/type overload,
             // or parse the enum here. Let's parse here for now.

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
                        Vector3 direction = (nextTargetTransform.position - data.CurrentWorldPosition).normalized;
                        if (direction.sqrMagnitude > 0.001f)
                        {
                             data.CurrentWorldRotation = Quaternion.LookRotation(direction);
                        }
                   }
                   // If next waypoint transform is null, rotation won't be updated, which is acceptable.
              }
              // If no next waypoint (reached end), rotation stays as it was.
         }


        // Optional: Add validation in editor
        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (pathAsset == null)
            {
                 Debug.LogWarning($"BasicPathStateSO '{name}': No Path Asset assigned.", this);
            }
            else
            {
                if (startIndex < 0 || startIndex >= pathAsset.WaypointCount)
                {
                    Debug.LogWarning($"BasicPathStateSO '{name}': Start Index ({startIndex}) is out of bounds for Path Asset '{pathAsset.name}' (Waypoint Count: {pathAsset.WaypointCount}).", this);
                }
            }

            // Validate next basic state configuration
            if (!string.IsNullOrEmpty(nextBasicStateEnumKey) && !string.IsNullOrEmpty(nextBasicStateEnumType))
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
             else if (!string.IsNullOrEmpty(nextBasicStateEnumKey) || !string.IsNullOrEmpty(nextBasicStateEnumType))
             {
                  Debug.LogWarning($"BasicPathStateSO '{name}': Next Basic State is partially configured (key or type is missing). It will default to BasicPatrol.", this);
             }
        }
        #endif
    }
}

// --- END OF FILE BasicPathStateSO.cs ---