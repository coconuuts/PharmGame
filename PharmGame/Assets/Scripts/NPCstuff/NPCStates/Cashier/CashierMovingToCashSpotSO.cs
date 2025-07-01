// --- START OF FILE CashierMovingToCashSpotSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC; // Needed for CashierState enum
using Game.NPC.States; // Needed for NpcStateSO, NpcStateContext
using CustomerManagement;

namespace Game.NPC.States // Place alongside other active states
{
    /// <summary>
    /// Active state for a Cashier NPC moving from their arrival point (e.g., end of path from home)
    /// to the specific cash register spot.
    /// </summary>
    [CreateAssetMenu(fileName = "CashierMovingToCashSpot", menuName = "NPC/Cashier States/Moving To Cash Spot", order = 100)] // Use a high order to group Cashier states
    public class CashierMovingToCashSpotSO : NpcStateSO
    {
        // Implement the HandledState property to return the corresponding enum value
        public override System.Enum HandledState => CashierState.CashierMovingToCashSpot;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context);

            Debug.Log($"{context.NpcObject.name}: Entering CashierMovingToCashSpot state.", context.NpcObject);

            // Ensure movement is stopped (defensive)
            context.MovementHandler?.StopMoving();

            // --- Get the Cashier Spot from the CashierManager via context ---
            Transform cashierSpot = context.CashierManager?.GetCashierSpot();
            // --- END GET ---

            if (cashierSpot != null)
            {
                // Store the target location (including rotation) on the Runner for OnReachedDestination
                context.Runner.CurrentTargetLocation = new BrowseLocation { browsePoint = cashierSpot, inventory = null };
                Debug.Log($"{context.NpcObject.name}: Set destination to Cashier spot: {cashierSpot.position}.", context.NpcObject);
                // Initiate movement using the context helper (resets _hasReachedCurrentDestination flag)
                bool moveStarted = context.MoveToDestination(cashierSpot.position);

                 if (!moveStarted) // Add check for move failure from SetDestination
                 {
                      Debug.LogError($"{context.NpcObject.name}: Failed to start movement to Cashier spot! Is the point on the NavMesh?", context.NpcObject);
                      // Fallback: If movement fails, maybe transition to Idle or ReturningToPool?
                      // For now, let's transition to WaitingForCustomer, assuming they are close enough to just stand there.
                      // A more robust system might try a different spot or go home.
                      Debug.LogWarning($"{context.NpcObject.name}: Movement failed, transitioning directly to CashierWaitingForCustomer.", context.NpcObject);
                      context.TransitionToState(CashierState.CashierWaitingForCustomer); // Fallback on movement failure
                      return; // Exit OnEnter early if movement failed
                 }

                // Note: Animation handler could be used here
                // context.SetAnimationSpeed(context.MovementHandler.Agent.speed); // Assuming Agent.speed is set correctly
                // context.PlayAnimation("Walking");
            }
            else
            {
                Debug.LogError($"{context.NpcObject.name}: No Cashier spot assigned in CashierManager! Cannot move to spot. Transitioning to Idle fallback.", context.NpcObject);
                context.TransitionToState(GeneralState.Idle); // Fallback if no spot found
                 return; // Exit OnEnter early
            }
        }

        // OnUpdate is typically not needed for simple movement states
        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context);
        }

        /// <summary>
        /// Called by the state machine runner when the NPC reaches its current movement destination (the Cashier spot).
        /// </summary>
        public override void OnReachedDestination(NpcStateContext context)
        {
            base.OnReachedDestination(context); // Call base OnReachedDestination (logging)

            Debug.Log($"{context.NpcObject.name}: Reached Cashier spot destination (detected by Runner). Transitioning to CashierWaitingForCustomer.", context.NpcObject);

            // Ensure movement is stopped (Runner already does this before calling OnReachedDestination, but defensive)
            context.MovementHandler?.StopMoving();

            // Rotate towards the Cashier spot's rotation (stored in CurrentTargetLocation)
            if (context.Runner.CurrentTargetLocation.HasValue && context.Runner.CurrentTargetLocation.Value.browsePoint != null)
            {
                context.RotateTowardsTarget(context.Runner.CurrentTargetLocation.Value.browsePoint.rotation);
            }
            else
            {
                Debug.LogWarning($"{context.NpcObject.name}: No valid target location stored for Cashier spot rotation in CashierMovingToCashSpot!", context.NpcObject);
            }

            // Transition to the WaitingForCustomer state
            context.TransitionToState(CashierState.CashierWaitingForCustomer);
        }

        // OnExit is typically not needed for simple movement states unless cleanup is required
        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context);
        }
    }
}

// --- END OF FILE CashierMovingToCashSpotSO.cs ---