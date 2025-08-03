// --- START OF MODIFIED FILE BasicCashierProcessingCheckoutSO.cs ---

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
        public override System.Enum HandledBasicState => BasicState.BasicCashierProcessingCheckout;

        // This state does NOT use the standard timeout; transaction time handles progression.
        public override bool ShouldUseTimeout => false;

        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
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
            if (data.simulatedProcessingTimeRemaining > 0 && !string.IsNullOrEmpty(data.simulatedProcessingCustomerTiId))
            {
                data.simulatedProcessingTimeRemaining -= deltaTime;

                if (data.simulatedProcessingTimeRemaining <= 0)
                {
                    Debug.Log($"SIM {data.Id}: Simulated transaction completed for customer '{data.simulatedProcessingCustomerTiId}' (Value: {data.simulatedTransactionValue}).", data.NpcGameObject);

                    if (EconomyManager.Instance != null)
                    {
                        EconomyManager.Instance.AddCurrency(data.simulatedTransactionValue);
                        Debug.Log($"SIM {data.Id}: Added {data.simulatedTransactionValue} to player economy (simulated).", data.NpcGameObject);
                    }
                    else
                    {
                        Debug.LogError($"SIM {data.Id}: EconomyManager instance not found! Cannot process simulated payment.", data.NpcGameObject);
                    }

                    string customerId = data.simulatedProcessingCustomerTiId;
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

                    Debug.Log($"SIM {data.Id}: Simulated transaction finished. Transitioning to BasicCashierWaitingForCustomer.", data.NpcGameObject);
                    manager.TransitionToBasicState(data, BasicState.BasicCashierWaitingForCustomer);

                    data.simulatedProcessingCustomerTiId = null;
                    data.simulatedTransactionValue = 0f;
                    data.simulatedProcessingTimeRemaining = 0f;
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
             base.OnExit(data, manager);

             Debug.Log($"SIM {data.Id}: BasicCashierProcessingCheckout OnExit.");
         }
    }
}
// --- END OF MODIFIED FILE BasicCashierProcessingCheckoutSO.cs ---