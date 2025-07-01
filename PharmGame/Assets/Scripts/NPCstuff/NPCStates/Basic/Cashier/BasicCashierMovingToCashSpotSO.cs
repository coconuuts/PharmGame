// --- START OF FILE BasicCashierMovingToCashSpotSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicNpcStateSO
using Game.NPC; // Needed for CashierManager (via BasicNpcStateManager)

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    /// <summary>
    /// Basic state for a Cashier TI NPC simulating movement from their current location
    /// to the specific cash register spot when inactive.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicCashierMovingToCashSpot", menuName = "NPC/Basic States/Basic Cashier Moving To Cash Spot", order = 4)] // Order according to customer flow
    public class BasicCashierMovingToCashSpotSO : BasicNpcStateSO
    {
        // Implement the HandledBasicState property to return the corresponding enum value
        public override System.Enum HandledBasicState => BasicState.BasicCashierMovingToCashSpot;

        // This state does NOT use the standard timeout; movement simulation handles progression.
        public override bool ShouldUseTimeout => false; // Override base property

        [Header("Basic Moving Settings")] // Add a header for specific settings
        [Tooltip("Simulated speed for off-screen movement (units per second).")]
        [SerializeField] private float simulatedMovementSpeed = 2f; // Example speed

        [Tooltip("The simulated distance threshold to consider the NPC 'arrived' at the target position.")]
        [SerializeField] private float simulatedArrivalThreshold = 0.5f; // Example threshold


        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            // Call base OnEnter for standard logging.
            // Override timer initialization and target position clearing from base.
            base.OnEnter(data, manager); // This will call Debug.Log entry, set ShouldUseTimeout (to false)

            Debug.Log($"SIM {data.Id}: BasicCashierMovingToCashSpot OnEnter.", data.NpcGameObject);

            // Get the Cashier Spot from the CashierManager via the BasicNpcStateManager's TiNpcManager reference
            Transform cashierSpot = manager.tiNpcManager?.CashierManager?.GetCashierSpot();

            if (cashierSpot != null)
            {
                // Set the simulated target position to the cashier spot's position
                data.simulatedTargetPosition = cashierSpot.position;
                Debug.Log($"SIM {data.Id}: Setting simulated target to Cashier spot: {data.simulatedTargetPosition.Value}");
            }
            else
            {
                Debug.LogError($"SIM {data.Id}: No Cashier spot assigned in CashierManager! Cannot simulate movement. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                // Fallback: If no spot found, transition to a safe state like BasicPatrol
                manager.TransitionToBasicState(data, BasicState.BasicPatrol);
                // Clear target position as we are exiting this state
                data.simulatedTargetPosition = null;
            }

            // Ensure timer is reset/zeroed as this state doesn't use the timeout mechanic
            data.simulatedStateTimer = 0f;
        }

        public override void SimulateTick(TiNpcData data, float deltaTime, BasicNpcStateManager manager)
        {
            // Check if we have a target position to move towards
            if (data.simulatedTargetPosition.HasValue)
            {
                // Check for simulated arrival first
                if (Vector3.Distance(data.CurrentWorldPosition, data.simulatedTargetPosition.Value) < simulatedArrivalThreshold)
                {
                    // Reached the target position, transition to the next state
                    Debug.Log($"SIM {data.Id}: Reached simulated Cashier spot target {data.simulatedTargetPosition.Value}. Transitioning to BasicCashierWaitingForCustomer.");
                    manager.TransitionToBasicState(data, BasicState.BasicCashierWaitingForCustomer);
                    // The TransitionToBasicState call will handle calling OnExit for this state,
                    // which clears the simulatedTargetPosition.
                    return; // Exit tick processing after transition
                }
                else // Not yet at target, simulate movement
                {
                    Vector3 direction = (data.simulatedTargetPosition.Value - data.CurrentWorldPosition).normalized;
                    float moveDistance = simulatedMovementSpeed * deltaTime;
                    data.CurrentWorldPosition += direction * moveDistance;
                    // Simulate rotation towards the movement direction
                    if (direction.sqrMagnitude > 0.001f) // Avoid Quaternion.LookRotation(Vector3.zero)
                    {
                         data.CurrentWorldRotation = Quaternion.LookRotation(direction);
                    }
                    // Debug.Log($"SIM {data.Id}: Moving towards Cashier spot. Pos: {data.CurrentWorldPosition}"); // Too noisy
                }
            }
            // If simulatedTargetPosition is null, OnEnter should have handled it by transitioning away.
            // If we somehow get here with a null target, the NPC is stuck in simulation.
            // A robust system might add a fallback transition here after a long time,
            // but for now, we rely on OnEnter setting the target or transitioning.
        }

         public override void OnExit(TiNpcData data, BasicNpcStateManager manager)
         {
             base.OnExit(data, manager); // Call base OnExit (logs exit)

             // Clear the simulated target position on exit
             data.simulatedTargetPosition = null;
             // Ensure timer is zeroed on exit (defensive)
             data.simulatedStateTimer = 0f;

             Debug.Log($"SIM {data.Id}: BasicCashierMovingToCashSpot OnExit. Target cleared.");
         }
    }
}
// --- END OF FILE BasicCashierMovingToCashSpotSO.cs ---