// --- START OF FILE BasicCashierGoingHomeSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicNpcStateSO, BasicPathState enum
using Game.Navigation; // Needed for WaypointManager, PathSO

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    /// <summary>
    /// Basic state for a Cashier TI NPC simulating movement along a path back towards home when inactive.
    /// This state sets up the path data and transitions to the generic BasicPathState.BasicFollowPath.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicCashierGoingHome", menuName = "NPC/Basic States/Basic Cashier Going Home", order = 6)] // Order towards the end of customer states
    public class BasicCashierGoingHomeSO : BasicNpcStateSO
    {
        // Implement the HandledBasicState property
        public override System.Enum HandledBasicState => BasicState.BasicCashierGoingHome; // <-- Maps to the new BasicState enum

        // This state does NOT use the standard timeout; path following simulation handles progression.
        public override bool ShouldUseTimeout => false; // Override base property

        [Header("Basic Going Home Settings")] // Add a header for specific settings
        [Tooltip("The ID of the path for the Cashier to follow from the store back towards home (e.g., 'PharmacytoCashier').")]
        [SerializeField] private string pathToPharmacyPathID = "PharmacytoCashier"; // <-- NEW FIELD: Specific path ID


        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            // Call base OnEnter for standard logging.
            // Override timer initialization and target position clearing from base.
            base.OnEnter(data, manager); // This will call Debug.Log entry, set ShouldUseTimeout (to false)

            Debug.Log($"SIM {data.Id}: BasicCashierGoingHome OnEnter.", data.NpcGameObject);

            // --- Determine the path back home ---
            // Get the PathSO asset using the configured ID via the BasicNpcStateManager's WaypointManager reference
            PathSO pathToPharmacy = manager.tiNpcManager?.WaypointManager?.GetPath(pathToPharmacyPathID);

            if (pathToPharmacy != null)
            {
                // For "Going Home" using the configured path, we start at index 0 and go forward.
                int startWaypointIndex = 0; // <-- Set start index to 0
                bool followReverse = false; // <-- Set follow reverse to false

                // Validate the calculated start index (should be 0, ensure path has at least one waypoint)
                if (startWaypointIndex < 0 || startWaypointIndex >= pathToPharmacy.WaypointCount)
                {
                    // This should ideally not happen if start index is hardcoded to 0 unless the path is empty
                    Debug.LogError($"SIM {data.Id}: Configured path '{pathToPharmacyPathID}' is empty or has invalid start index {startWaypointIndex}! WaypointCount: {pathToPharmacy.WaypointCount}. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                    manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback if path is empty or index is bad
                    return; // Exit OnEnter
                }

                // --- Set the path data on the TiNpcData for the generic BasicPathState to read ---
                data.simulatedPathID = pathToPharmacyPathID;
                data.simulatedWaypointIndex = startWaypointIndex;
                data.simulatedFollowReverse = followReverse;
                data.isFollowingPathBasic = true; // Flag that they are now following a path simulation

                Debug.Log($"SIM {data.Id}: Prepared path simulation data for going home: path '{data.simulatedPathID}', start index {data.simulatedWaypointIndex}, reverse {data.simulatedFollowReverse}. Flagged isFollowingPathBasic = true.", data.NpcGameObject);

                // --- Transition to the generic BasicPathState.BasicFollowPath ---
                Debug.Log($"SIM {data.Id}: Transitioning to BasicPathState.BasicFollowPath to go home.", data.NpcGameObject);
                manager.TransitionToBasicState(data, BasicPathState.BasicFollowPath);
            }
            else
            {
                Debug.LogError($"SIM {data.Id}: PathSO with ID '{pathToPharmacyPathID ?? "NULL"}' not found via WaypointManager for going home simulation! Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback if path not found
            }
            // --- END Determine Path ---

            // Ensure timer is zeroed as this state doesn't use the timeout mechanic
            data.simulatedStateTimer = 0f;
            // Ensure target position is null as path following manages position
            data.simulatedTargetPosition = null;
        }

        public override void SimulateTick(TiNpcData data, float deltaTime, BasicNpcStateManager manager)
        {
            // This state is passive after setting up the path and transitioning.
            // The actual simulation logic runs in BasicPathState.BasicFollowPathSO.
        }

         public override void OnExit(TiNpcData data, BasicNpcStateManager manager)
         {
             base.OnExit(data, manager); // Call base OnExit (logs exit)

             Debug.Log($"SIM {data.Id}: BasicCashierGoingHome OnExit.");

             // Note: Clearing path simulation data (simulatedPathID, etc.) is handled by
             // BasicPathState.BasicFollowPathSO.OnExit when it completes the path,
             // or by TiNpcManager.RequestDeactivateTiNpc if deactivated while in BasicPathState.
             // We don't clear it here because the transition is *to* BasicPathState.
         }
    }
}
// --- END OF FILE BasicCashierGoingHomeSO.cs ---