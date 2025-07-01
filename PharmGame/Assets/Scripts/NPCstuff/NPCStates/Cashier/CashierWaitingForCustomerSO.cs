// --- START OF FILE CashierWaitingForCustomerSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC; // Needed for CashierState enum
using Game.NPC.States; // Needed for NpcStateSO, NpcStateContext
using Game.Events; // Needed for NpcImpatientEvent (if used in OnUpdate)

namespace Game.NPC.States // Place alongside other active states
{
    /// <summary>
    /// Active state for a Cashier NPC waiting at the cash spot for a customer to arrive.
    /// </summary>
    [CreateAssetMenu(fileName = "CashierWaitingForCustomer", menuName = "NPC/Cashier States/Waiting For Customer", order = 101)]
    public class CashierWaitingForCustomerSO : NpcStateSO
    {
        // Implement the HandledState property
        public override System.Enum HandledState => CashierState.CashierWaitingForCustomer;

        // This state should be interruptible
        public override bool IsInterruptible => true; // Explicitly set to true (inherits from base)

        // OnEnter will be implemented in a later substep (Phase 2, Substep 2.1)
        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context);

            Debug.Log($"{context.NpcObject.name}: Entering CashierWaitingForCustomer state.", context.NpcObject);

            // Ensure movement is stopped (defensive)
            context.MovementHandler?.StopMoving();

            // Rotate towards the register point's rotation (optional, but common)
            // The target location should still be the cashier spot from the previous state
            if (context.Runner.CurrentTargetLocation.HasValue && context.Runner.CurrentTargetLocation.Value.browsePoint != null)
            {
                 context.RotateTowardsTarget(context.Runner.CurrentTargetLocation.Value.browsePoint.rotation);
            }
            else
            {
                 Debug.LogWarning($"{context.NpcObject.name}: No valid target location stored for Cashier spot rotation in CashierWaitingForCustomer!", context.NpcObject);
            }

            // --- Find the CashRegisterInteractable and signal Cashier arrival ---
            GameObject registerGO = GameObject.FindGameObjectWithTag("CashRegister"); // Assumes your register has this tag
            if (registerGO != null)
            {
                 CashRegisterInteractable register = registerGO.GetComponent<CashRegisterInteractable>();
                 if (register != null)
                 {
                      Debug.Log($"{context.NpcObject.name}: Found CashRegisterInteractable '{registerGO.name}'. Signalling Cashier arrived.", context.NpcObject);
                      register.SignalCashierArrived(); // <-- Signal arrival
                 }
                 else
                 {
                      Debug.LogError($"{context.NpcObject.name}: Found GameObject with tag 'CashRegister' but it's missing the CashRegisterInteractable component!", context.NpcObject);
                 }
            }
            else
            {
                 Debug.LogError($"{context.NpcObject.name}: Could not find GameObject with tag 'CashRegister'! Cannot signal Cashier arrival.", context.NpcObject);
            }
            // --- END Signal Arrival ---

            Debug.Log($"{context.NpcObject.name}: Cashier is now waiting for a customer.", context.NpcObject);

            // This state is passive, waiting for an external event (CustomerReadyForCashierEvent)
            // or the endDay schedule check in OnUpdate.
        }

        /// <summary>
        /// Called every frame while the state machine is in this state. Use for continuous logic.
        /// Checks if it's time for the Cashier to go home based on schedule.
        /// </summary>
        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context); // Call base OnUpdate (empty)

            // --- Check if it's time to go home ---
            // We only check this if the NPC is a TI NPC with valid TiData
            if (context.Runner.IsTrueIdentityNpc && context.TiData != null)
            {
                // The 'isEndingDay' flag is managed by the ProximityManager based on the endDay schedule
                if (context.TiData.isEndingDay)
                {
                    Debug.Log($"{context.NpcObject.name}: Cashier's endDay schedule has started ({context.TiData.endDay}). Transitioning to CashierGoingHome.", context.NpcObject);
                    // Transition to the state for going home
                    context.TransitionToState(CashierState.CashierGoingHome);
                    // No need for further logic in this OnUpdate after transitioning
                }
            }
            // --- END Check Go Home ---

            // Add other passive waiting logic here if needed (e.g., idle animation checks)
        }

        // OnReachedDestination is not applicable for this state
        public override void OnReachedDestination(NpcStateContext context)
        {
            base.OnReachedDestination(context);
        }

        /// <summary>
        /// Called when the state machine exits this state. Use for cleanup.
        /// </summary>
        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)

            Debug.Log($"{context.NpcObject.name}: Exiting CashierWaitingForCustomer state.", context.NpcObject);

            // --- Find the CashRegisterInteractable and signal Cashier departure ---
            // This ensures the player can use the register if the cashier leaves the spot
            GameObject registerGO = GameObject.FindGameObjectWithTag("CashRegister"); // Assumes your register has this tag
            if (registerGO != null)
            {
                 CashRegisterInteractable register = registerGO.GetComponent<CashRegisterInteractable>();
                 if (register != null)
                 {
                      Debug.Log($"{context.NpcObject.name}: Found CashRegisterInteractable '{registerGO.name}'. Signalling Cashier departed.", context.NpcObject);
                      register.SignalCashierDeparted(); // <-- Signal departure
                 }
                 else
                 {
                      Debug.LogError($"{context.NpcObject.name}: Found GameObject with tag 'CashRegister' but it's missing the CashRegisterInteractable component! Cannot signal Cashier departure.", context.NpcObject);
                 }
            }
            else
            {
                 Debug.LogError($"{context.NpcObject.name}: Could not find GameObject with tag 'CashRegister'! Cannot signal Cashier departure.", context.NpcObject);
            }
            // --- END Signal Departure ---


            // Note: Animation handler could be used here
            // context.PlayAnimation("Idle"); // Or walking if going home
        }
    }
}

// --- END OF FILE CashierWaitingForCustomerSO.cs ---