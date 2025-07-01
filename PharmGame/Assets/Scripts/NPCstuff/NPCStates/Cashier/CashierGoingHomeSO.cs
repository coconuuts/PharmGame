// --- START OF FILE CashierGoingHomeSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC; // Needed for CashierState enum
using Game.NPC.States; // Needed for NpcStateSO, NpcStateContext
using Game.Navigation; // Needed for PathSO, WaypointManager

namespace Game.NPC.States // Place alongside other active states
{
    /// <summary>
    /// Active state for a Cashier NPC moving from the cash spot back towards their home path/exit.
    /// This state now uses a specifically configured path (e.g., "PharmacytoCashier")
    /// starting from its beginning (index 0) to guide the NPC out of the pharmacy.
    /// </summary>
    [CreateAssetMenu(fileName = "CashierGoingHome", menuName = "NPC/Cashier States/Going Home", order = 103)]
    public class CashierGoingHomeSO : NpcStateSO
    {
        // Implement the HandledState property
        public override System.Enum HandledState => CashierState.CashierGoingHome;

        [Header("Going Home Settings")] // Add a header for specific settings
        [Tooltip("The ID of the path for the Cashier to follow from the store back towards home (e.g., 'PharmacytoCashier').")]
        [SerializeField] private string pathToPharmacyPathID = "PharmacytoCashier"; // <-- NEW FIELD: Specific path ID

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            Debug.Log($"{context.NpcObject.name}: Entering CashierGoingHome state.", context.NpcObject);

            // Ensure movement is stopped (defensive)
            context.MovementHandler?.StopMoving();

            // --- Determine the path back home ---
            // We are now using a specific path asset designated for going FROM the pharmacy,
            // defined by the 'pathToPharmacyPathID' field in this SO.

            // Check if this is a TI NPC, as only they go "home" in this sense
            // This state should only be entered by a TI NPC with TiData
            if (!context.Runner.IsTrueIdentityNpc || context.TiData == null)
            {
                 Debug.LogError($"{context.NpcObject.name}: CashierGoingHome state entered by non-TI NPC or NPC without TiData! Transitioning to ReturningToPool.", context.NpcObject);
                 // Transient NPCs go to the pool from Exiting, but this is a safety fallback for TI NPCs.
                 context.TransitionToState(GeneralState.ReturningToPool); // Fallback for invalid NPC type
                 return; // Exit OnEnter
            }


            // Get the PathSO asset using the configured ID
            PathSO pathToPharmacy = WaypointManager.Instance?.GetPath(pathToPharmacyPathID);

            if (pathToPharmacy != null)
            {
                // For "Going Home" using the configured path, we start at index 0 and go forward.
                int startWaypointIndex = 0; // <-- Set start index to 0
                bool followReverse = false; // <-- Set follow reverse to false

                // Validate the calculated start index (should be 0, ensure path has at least one waypoint)
                if (startWaypointIndex < 0 || startWaypointIndex >= pathToPharmacy.WaypointCount)
                {
                    // This should ideally not happen if start index is hardcoded to 0 unless the path is empty
                    Debug.LogError($"{context.NpcObject.name}: Configured path '{pathToPharmacyPathID}' is empty or has invalid start index {startWaypointIndex}! WaypointCount: {pathToPharmacy.WaypointCount}. Transitioning to Idle fallback.", context.NpcObject);
                    context.TransitionToState(GeneralState.Idle); // Fallback if path is empty or index is bad
                    return;
                }

                // --- Prepare the Runner with the path transition data ---
                // This data will be read by the generic PathStateSO.OnEnter
                context.Runner.PreparePathTransition(pathToPharmacy, startWaypointIndex, followReverse);
                Debug.Log($"{context.NpcObject.name}: Prepared path transition data for going home: path '{pathToPharmacyPathID}', start index {startWaypointIndex}, reverse {followReverse}.", context.NpcObject);

                // --- Transition to the generic PathState.FollowPath ---
                Debug.Log($"{context.NpcObject.name}: Transitioning to PathState.FollowPath to go home.", context.NpcObject);
                context.TransitionToState(Game.NPC.PathState.FollowPath);
            }
            else
            {
                Debug.LogError($"{context.NpcObject.name}: PathSO with ID '{pathToPharmacyPathID ?? "NULL"}' not found via WaypointManager for going home! Transitioning to Idle fallback.", context.NpcObject);
                context.TransitionToState(GeneralState.Idle); // Fallback if path not found
            }
            // --- END Determine Path ---

            // Note: Animation handler could be used here
            // context.PlayAnimation("Walking"); // Start walking animation
        }

        // OnUpdate is typically not needed for simple movement states (PathState handles movement)
        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context);
        }

        // OnReachedDestination is not applicable for this state (PathState handles arrival)
        public override void OnReachedDestination(NpcStateContext context)
        {
            // This state transitions to PathState, which handles reaching the final destination.
            // This method should generally not be called while in this state.
            base.OnReachedDestination(context); // Call base just for logging if it somehow gets called
        }

        /// <summary>
        /// Called when the state machine exits this state. Use for cleanup.
        /// </summary>
        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)

            Debug.Log($"{context.NpcObject.name}: Exiting CashierGoingHome state.", context.NpcObject);

            // Note: Animation handler could be used here
            // context.PlayAnimation("Idle"); // Reset animation
        }
    }
}
// --- END OF FILE CashierGoingHomeSO.cs ---