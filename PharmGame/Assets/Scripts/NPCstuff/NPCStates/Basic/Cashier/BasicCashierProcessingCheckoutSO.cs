// --- START OF FILE BasicCashierProcessingCheckoutSO.cs (Modified OnExit) ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicNpcStateSO
using System.Collections; // Needed for Coroutine
using Systems.Inventory; // Needed for ItemDetails
using System.Collections.Generic; // Needed for List
using System.Linq; // Needed for Sum
using Systems.Economy; // Needed for EconomyManager
using Game.Events; // Needed for EventManager and SimulatedTransactionCompletedEvent

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    /// <summary>
    /// Basic state for a Cashier TI NPC simulating processing a customer's transaction when inactive.
    /// Continues transaction simulation if deactivated mid-process.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicCashierProcessingCheckout", menuName = "NPC/Basic States/Basic Cashier Processing Checkout", order = 7)] // Adjust order
    public class BasicCashierProcessingCheckoutSO : BasicNpcStateSO
    {
        // Implement the HandledBasicState property
        public override System.Enum HandledBasicState => BasicState.BasicCashierProcessingCheckout;

        // This state does NOT use the standard timeout; transaction time handles progression.
        public override bool ShouldUseTimeout => false; // Override base property

        // Define the base duration per item for the simulation (should match active state)
        [Header("Basic Checkout Settings")]
        [Tooltip("Duration (in seconds) to simulate processing each item.")]
        [SerializeField] private float timePerItem = 0.5f; // Should match CashierProcessingCheckoutSO

        // Note: No fields here to store the transaction data itself,
        // as that lives directly on the TiNpcData instance being simulated.


        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            // ... (OnEnter logic as implemented in Substep 4)
             base.OnEnter(data, manager);

            Debug.Log($"SIM {data.Id}: BasicCashierProcessingCheckout OnEnter.", data.NpcGameObject);

            data.simulatedTargetPosition = null;
            data.simulatedStateTimer = 0f;

            if (data.simulatedProcessingTimeRemaining > 0)
            {
                Debug.Log($"SIM {data.Id}: Resuming simulated transaction for customer '{data.simulatedProcessingCustomerTiId}' (Value: {data.simulatedTransactionValue}, Time Remaining: {data.simulatedProcessingTimeRemaining:F2}s).", data.NpcGameObject);
            }
            else
            {
                Debug.LogError($"SIM {data.Id}: Entered BasicCashierProcessingCheckout state, but no transaction data found on TiNpcData! (simulatedProcessingTimeRemaining <= 0). Transitioning to BasicCashierWaitingForCustomer fallback.", data.NpcGameObject);
                manager.TransitionToBasicState(data, BasicState.BasicCashierWaitingForCustomer);
            }
        }

        public override void SimulateTick(TiNpcData data, float deltaTime, BasicNpcStateManager manager)
        {
            // ... (SimulateTick logic as implemented in Substep 5)
             // Only simulate if there is time remaining and a customer ID
            if (data.simulatedProcessingTimeRemaining > 0 && !string.IsNullOrEmpty(data.simulatedProcessingCustomerTiId))
            {
                data.simulatedProcessingTimeRemaining -= deltaTime;
                // Debug.Log($"SIM {data.Id}: Processing transaction for '{data.simulatedProcessingCustomerTiId}'. Time remaining: {data.simulatedProcessingTimeRemaining:F2}s."); // Too noisy

                // Check if the transaction is now complete
                if (data.simulatedProcessingTimeRemaining <= 0)
                {
                    Debug.Log($"SIM {data.Id}: Simulated transaction completed for customer '{data.simulatedProcessingCustomerTiId}' (Value: {data.simulatedTransactionValue}).", data.NpcGameObject);

                    // --- Transaction Completion Actions ---

                    // 1. Add money to the player economy
                    if (EconomyManager.Instance != null)
                    {
                        EconomyManager.Instance.AddCurrency(data.simulatedTransactionValue);
                        Debug.Log($"SIM {data.Id}: Added {data.simulatedTransactionValue} to player economy (simulated).", data.NpcGameObject);
                    }
                    else
                    {
                        Debug.LogError($"SIM {data.Id}: EconomyManager instance not found! Cannot process simulated payment.", data.NpcGameObject);
                    }

                    // 2. Queue the SimulatedTransactionCompletedEvent for the customer
                    string customerId = data.simulatedProcessingCustomerTiId; // Get customer ID before clearing data
                    TiNpcData customerData = manager.tiNpcManager?.GetTiNpcData(customerId);

                    if (customerData != null)
                    {
                        Debug.Log($"SIM {data.Id}: Queuing SimulatedTransactionCompletedEvent for customer '{customerId}'.", data.NpcGameObject);
                        customerData.AddPendingSimulatedEvent(new SimulatedTransactionCompletedEvent(data.Id, customerId, data.simulatedTransactionValue));
                    }
                    else
                    {
                        Debug.LogWarning($"SIM {data.Id}: Customer TiNpcData '{customerId}' not found when completing simulated transaction! Cannot queue completion event for customer.", data.NpcGameObject);
                    }

                    // 3. Transition the Cashier to the waiting state
                    Debug.Log($"SIM {data.Id}: Simulated transaction finished. Transitioning to BasicCashierWaitingForCustomer.", data.NpcGameObject);
                    manager.TransitionToBasicState(data, BasicState.BasicCashierWaitingForCustomer);

                    // 4. Cleanup transaction data on the Cashier's TiNpcData
                    data.simulatedProcessingCustomerTiId = null;
                    data.simulatedTransactionValue = 0f;
                    data.simulatedProcessingTimeRemaining = 0f;

                    // --- End Transaction Completion Actions ---
                }
            }
            else if (data.simulatedProcessingTimeRemaining <= 0 && !string.IsNullOrEmpty(data.simulatedProcessingCustomerTiId))
            {
                 Debug.LogWarning($"SIM {data.Id}: In BasicCashierProcessingCheckout state with timer <= 0 ({data.simulatedProcessingTimeRemaining:F2}s) but customer ID '{data.simulatedProcessingCustomerTiId}' present. Assuming transaction already completed. Transitioning to BasicCashierWaitingForCustomer.", data.NpcGameObject);
                 manager.TransitionToBasicState(data, BasicState.BasicCashierWaitingForCustomer);
                 data.simulatedProcessingCustomerTiId = null;
                 data.simulatedTransactionValue = 0f;
                 data.simulatedProcessingTimeRemaining = 0f;
            }
        }

         public override void OnExit(TiNpcData data, BasicNpcStateManager manager)
         {
             base.OnExit(data, manager); // Call base OnExit (logs exit)

             Debug.Log($"SIM {data.Id}: BasicCashierProcessingCheckout OnExit.");

             // Transaction data fields (simulatedProcessingCustomerTiId, etc.)
             // are cleared in SimulateTick upon completion.
             // If exiting for activation, the data remains on TiNpcData and is handled by activation logic.
             // No explicit save/clear needed here.
         }
    }
}
// --- END OF FILE BasicCashierProcessingCheckoutSO.cs (Modified OnExit) ---