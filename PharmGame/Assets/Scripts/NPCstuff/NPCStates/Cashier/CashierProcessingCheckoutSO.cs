// --- START OF MODIFIED FILE CashierProcessingCheckoutSO.cs ---

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
using Game.NPC; // Needed for GeneralState enum

namespace Game.NPC.States // Place alongside other active states
{
    /// <summary>
    /// Active state for a Cashier NPC actively processing a customer's transaction.
    /// </summary>
    [CreateAssetMenu(fileName = "CashierProcessingCheckout", menuName = "NPC/Cashier States/Processing Checkout", order = 102)]
    public class CashierProcessingCheckoutSO : NpcStateSO
    {
        // Implement the HandledState property
        public override System.Enum HandledState => CashierState.CashierProcessingCheckout;

        // Define the base duration per item for the simulation
        [Header("Checkout Settings")]
        [Tooltip("Duration (in seconds) to simulate processing each item.")]
        [SerializeField] private float timePerItem = 0.5f;

        private Coroutine processingCoroutine;

        // --- ADDED: Fields to track transaction progress for accurate saving ---
        private float transactionElapsedTime = 0f;
        private float currentTransactionTotalTime = 0f;
        // --- END ADDED ---
        
        // Note: We no longer need to store currentTransactionTotalValue or currentCustomerRunnerRef
        // as local fields, as they are either used immediately or can be accessed from the context.


        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            Debug.Log($"{context.NpcObject.name}: Entering CashierProcessingCheckout state.", context.NpcObject);

            context.MovementHandler?.StopMoving();

            NpcStateMachineRunner currentCustomerRunnerRef = context.Runner.CurrentCustomerRunner;

            if (currentCustomerRunnerRef == null)
            {
                Debug.LogError($"{context.NpcObject.name}: CurrentCustomerRunner is null in CashierProcessingCheckout OnEnter! Cannot process checkout. Transitioning back to WaitingForCustomer.", context.NpcObject);
                context.TransitionToState(CashierState.CashierWaitingForCustomer); // Fallback
                return;
            }
            if (currentCustomerRunnerRef.Shopper == null)
            {
                Debug.LogError($"{context.NpcObject.name}: Current customer '{currentCustomerRunnerRef.gameObject.name}' is missing Shopper component! Cannot process checkout. Transitioning back to WaitingForCustomer.", context.NpcObject);
                 context.Runner.CurrentCustomerRunner = null;
                context.TransitionToState(CashierState.CashierWaitingForCustomer); // Fallback
                return;
            }


            List<(ItemDetails details, int quantity)> itemsToScan = currentCustomerRunnerRef.GetItemsToBuy();


            if (itemsToScan == null || itemsToScan.Sum(item => item.quantity) <= 0)
            {
                 Debug.LogWarning($"{context.NpcObject.name}: Customer '{currentCustomerRunnerRef.gameObject.name}' has no items or zero total quantity to buy. Skipping checkout. Transitioning back to WaitingForCustomer.", context.NpcObject);
                 currentCustomerRunnerRef.OnTransactionCompleted(0);
                 context.PublishEvent(new NpcTransactionCompletedEvent(currentCustomerRunnerRef.gameObject, 0));
                 context.Runner.CurrentCustomerRunner = null;
                 context.TransitionToState(CashierState.CashierWaitingForCustomer); // Transition back
                 return;
            }

            // --- MODIFIED: Calculate total time and value, reset elapsed timer ---
            int totalQuantity = itemsToScan.Sum(item => item.quantity);
            float currentTransactionTotalValue = itemsToScan.Sum(item => item.details.price * item.quantity);
            currentTransactionTotalTime = totalQuantity * timePerItem;
            transactionElapsedTime = 0f; // Reset elapsed time for the new transaction

            Debug.Log($"{context.NpcObject.name}: Starting checkout process for customer '{currentCustomerRunnerRef.gameObject.name}' with {totalQuantity} items. Total Value: {currentTransactionTotalValue:F2}, Total Time: {currentTransactionTotalTime:F2}s.", context.NpcObject);
            // --- END MODIFIED ---

            processingCoroutine = context.StartCoroutine(ProcessItemsRoutine(context, currentCustomerRunnerRef, currentTransactionTotalValue, currentTransactionTotalTime));
        }

        // --- ADDED: OnUpdate to track elapsed time for accurate saving ---
        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context);
            // Increment the elapsed time every frame this state is active.
            // This is crucial for accurately calculating remaining time in OnExit.
            transactionElapsedTime += context.DeltaTime;
        }
        // --- END ADDED ---

        public override void OnReachedDestination(NpcStateContext context)
        {
            base.OnReachedDestination(context);
        }

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); 

            Debug.Log($"{context.NpcObject.name}: Exiting CashierProcessingCheckout state.", context.NpcObject);

            if (processingCoroutine != null)
            {
                 context.StopCoroutine(processingCoroutine); 
                 processingCoroutine = null; 
                 Debug.Log($"{context.NpcObject.name}: Stopped checkout processing coroutine.", context.NpcObject);
            }
            
            if (context.Runner.IsTrueIdentityNpc && context.TiData != null)
            {
                 Debug.Log($"{context.NpcObject.name}: TI Cashier exiting ProcessingCheckout. Checking if customer is also TI for simulation data save.", context.NpcObject);

                 NpcStateMachineRunner customerRunner = context.Runner.CurrentCustomerRunner;
                 if (customerRunner != null && customerRunner.IsTrueIdentityNpc && customerRunner.TiData != null)
                 {
                      Debug.Log($"{context.NpcObject.name}: Customer '{customerRunner.gameObject.name}' is also a TI NPC. Saving transaction data to TiNpcData for simulation.", context.NpcObject);

                      // --- MODIFIED: Save ACCURATE remaining time and value ---
                      float totalValue = customerRunner.GetItemsToBuy().Sum(item => item.details.price * item.quantity);
                      
                      // Calculate the actual time remaining in the transaction.
                      float timeRemaining = currentTransactionTotalTime - transactionElapsedTime;

                      // Save the data to the Cashier's TiNpcData. Ensure a minimum time remains to avoid instant completion in simulation.
                      context.TiData.simulatedProcessingCustomerTiId = customerRunner.TiData.Id;
                      context.TiData.simulatedTransactionValue = totalValue; 
                      context.TiData.simulatedProcessingTimeRemaining = Mathf.Max(0.1f, timeRemaining);

                      Debug.Log($"SIM {context.NpcObject.name}: Saved transaction data to TiNpcData: Customer='{context.TiData.simulatedProcessingCustomerTiId}', Value={context.TiData.simulatedTransactionValue:F2}, Time Remaining={context.TiData.simulatedProcessingTimeRemaining:F2}s.", context.NpcObject);
                      // --- END MODIFIED ---
                 }
                 else if (customerRunner != null)
                 {
                      Debug.Log($"{context.NpcObject.name}: TI Cashier exiting ProcessingCheckout while processing a Transient customer ('{customerRunner.gameObject.name}'). No simulation data saved.", context.NpcObject);
                      context.TiData.simulatedProcessingCustomerTiId = null;
                      context.TiData.simulatedTransactionValue = 0f;
                      context.TiData.simulatedProcessingTimeRemaining = 0f;
                 }
                 else
                 {
                      Debug.LogWarning($"{context.NpcObject.name}: TI Cashier exiting ProcessingCheckout but CurrentCustomerRunner is null! Cannot save transaction data.", context.NpcObject);
                      context.TiData.simulatedProcessingCustomerTiId = null;
                      context.TiData.simulatedTransactionValue = 0f;
                      context.TiData.simulatedProcessingTimeRemaining = 0f;
                 }
            }
            else
            {
                 Debug.Log($"{context.NpcObject.name}: Non-TI Cashier or TI Cashier exiting ProcessingCheckout for non-pooling reason. No simulation data saved.", context.NpcObject);
                 if (context.TiData != null)
                 {
                      context.TiData.simulatedProcessingCustomerTiId = null;
                      context.TiData.simulatedTransactionValue = 0f;
                      context.TiData.simulatedProcessingTimeRemaining = 0f;
                 }
            }

            if (context.Runner.CurrentCustomerRunner != null)
            {
                 Debug.Log($"{context.NpcObject.name}: Clearing CurrentCustomerRunner reference on Runner.", context.NpcObject);
                 context.Runner.CurrentCustomerRunner = null;
            }
        }

        private IEnumerator ProcessItemsRoutine(NpcStateContext context, NpcStateMachineRunner customerRunner, float totalValue, float totalTime)
        {
             Debug.Log($"{context.NpcObject.name}: ProcessItemsRoutine started for {customerRunner.gameObject.name}. Total Value: {totalValue:F2}, Total Time: {totalTime:F2}s.", context.NpcObject);

             yield return new WaitForSeconds(totalTime);

             Debug.Log($"{context.NpcObject.name}: Checkout simulation complete.", context.NpcObject);

             if (EconomyManager.Instance != null)
             {
                  EconomyManager.Instance.AddCurrency(totalValue);
                  Debug.Log($"{context.NpcObject.name}: Added {totalValue} to player economy.", context.NpcObject);
             }
             else
             {
                  Debug.LogError($"{context.NpcObject.name}: EconomyManager instance not found! Cannot process payment.", context.NpcObject);
             }

             if (customerRunner != null)
             {
                 Debug.Log($"{context.NpcObject.name}: Signalling customer '{customerRunner.gameObject.name}' transaction completed.", context.NpcObject);
                 customerRunner.OnTransactionCompleted(totalValue);
                 context.PublishEvent(new NpcTransactionCompletedEvent(customerRunner.gameObject, totalValue));
             } else {
                 Debug.LogError($"{context.NpcObject.name}: Customer Runner became null during checkout routine!", context.NpcObject);
             }

             Debug.Log($"{context.NpcObject.name}: Checkout finished. Transitioning back to CashierWaitingForCustomer.", context.NpcObject);
             context.TransitionToState(CashierState.CashierWaitingForCustomer);
        }
    }
}
// --- END OF MODIFIED FILE CashierProcessingCheckoutSO.cs ---