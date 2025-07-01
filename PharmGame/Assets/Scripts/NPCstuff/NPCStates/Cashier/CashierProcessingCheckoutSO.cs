// --- START OF FILE CashierProcessingCheckoutSO.cs (Modified OnExit) ---

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

        // --- NEW: Field to store the reference to the running coroutine ---
        // This field is only relevant for the active state and is not saved persistently.
        private Coroutine processingCoroutine;
        // --- END NEW ---

        // --- NEW: Fields to track transaction progress for saving ---
        // These fields are used by the active state to track progress
        // and are saved to TiNpcData on deactivation.
        private float transactionElapsedTime = 0f;
        private float currentTransactionTotalTime = 0f;
        private float currentTransactionTotalValue = 0f;
        private NpcStateMachineRunner currentCustomerRunnerRef = null; // Cache customer runner here
        // --- END NEW ---


        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            Debug.Log($"{context.NpcObject.name}: Entering CashierProcessingCheckout state.", context.NpcObject);

            // Ensure movement is stopped (defensive)
            context.MovementHandler?.StopMoving();

            // --- Get and store the customer Runner reference ---
            // This reference is stored on the Cashier Runner, but also cache it locally for easier access in this state.
            currentCustomerRunnerRef = context.Runner.CurrentCustomerRunner;
            // --- END GET ---

            if (currentCustomerRunnerRef == null)
            {
                Debug.LogError($"{context.NpcObject.name}: CurrentCustomerRunner is null in CashierProcessingCheckout OnEnter! Cannot process checkout. Transitioning back to WaitingForCustomer.", context.NpcObject);
                context.TransitionToState(CashierState.CashierWaitingForCustomer); // Fallback
                return;
            }
            if (currentCustomerRunnerRef.Shopper == null)
            {
                Debug.LogError($"{context.NpcObject.name}: Current customer '{currentCustomerRunnerRef.gameObject.name}' is missing Shopper component! Cannot process checkout. Transitioning back to WaitingForCustomer.", context.NpcObject);
                 // Clear the invalid customer reference
                 context.Runner.CurrentCustomerRunner = null;
                 currentCustomerRunnerRef = null; // Clear local cache
                context.TransitionToState(CashierState.CashierWaitingForCustomer); // Fallback
                return;
            }


            // --- Get the customer's purchase list ---
            List<(ItemDetails details, int quantity)> itemsToScan = currentCustomerRunnerRef.GetItemsToBuy(); // Get items from the customer's Shopper
            // --- END GET ---


            if (itemsToScan == null || itemsToScan.Sum(item => item.quantity) <= 0)
            {
                 Debug.LogWarning($"{context.NpcObject.name}: Customer '{currentCustomerRunnerRef.gameObject.name}' has no items or zero total quantity to buy. Skipping checkout. Transitioning back to WaitingForCustomer.", context.NpcObject);
                 // Signal customer transaction completed with 0 payment
                 currentCustomerRunnerRef.OnTransactionCompleted(0);
                 // Publish event for other systems that might be listening
                 context.PublishEvent(new NpcTransactionCompletedEvent(currentCustomerRunnerRef.gameObject, 0));
                 // Clear the customer reference
                 context.Runner.CurrentCustomerRunner = null;
                 currentCustomerRunnerRef = null; // Clear local cache
                 context.TransitionToState(CashierState.CashierWaitingForCustomer); // Transition back
                 return;
            }

            // --- Calculate total quantity and value ---
            int totalQuantity = itemsToScan.Sum(item => item.quantity);
            currentTransactionTotalValue = itemsToScan.Sum(item => item.details.price * item.quantity); // Calculate and store total value
            currentTransactionTotalTime = totalQuantity * timePerItem; // Calculate and store total time
            transactionElapsedTime = 0f; // Reset elapsed time

            Debug.Log($"{context.NpcObject.name}: Starting checkout process for customer '{currentCustomerRunnerRef.gameObject.name}' with {totalQuantity} items. Total Value: {currentTransactionTotalValue:F2}, Total Time: {currentTransactionTotalTime:F2}s.", context.NpcObject);
            // --- END Calculate ---


            // --- Start the checkout simulation coroutine and store the reference ---
            // Pass necessary data to the coroutine
            // The coroutine now only handles the timed wait and completion logic.
            // Time tracking is done in OnUpdate or a separate coroutine if needed.
            // Let's simplify and just use a timed coroutine for now, assuming OnExit captures state correctly.
            // The coroutine will handle the completion actions (add money, signal customer, transition cashier).
            processingCoroutine = context.StartCoroutine(ProcessItemsRoutine(context, currentCustomerRunnerRef, currentTransactionTotalValue, currentTransactionTotalTime)); // Pass calculated values
            // --- END Start Coroutine ---

            // Note: Animation handler could be used here
            // context.PlayAnimation("Processing"); // Play a cashier animation
        }

        // OnUpdate is not needed for this state as logic is in a coroutine
        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context);

            // --- NEW: Track elapsed time if using a separate timer ---
            // If you were using a separate timer in OnUpdate instead of a timed coroutine,
            // you would increment transactionElapsedTime here.
            // For now, we rely on the coroutine's WaitForSeconds for timing,
            // and the OnExit logic will use a simplified calculation for simulation time remaining.
            // If you need more precise mid-transaction saving, you would add timer logic here.
            // transactionElapsedTime += context.DeltaTime; // Example if using OnUpdate timer
            // --- END NEW ---
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

            Debug.Log($"{context.NpcObject.name}: Exiting CashierProcessingCheckout state.", context.NpcObject);

            // --- Stop the processing coroutine ---
            if (processingCoroutine != null)
            {
                 context.StopCoroutine(processingCoroutine); // Use the stored reference
                 processingCoroutine = null; // Clear the reference
                 Debug.Log($"{context.NpcObject.name}: Stopped checkout processing coroutine.", context.NpcObject);
            }
            // --- END Stop Coroutine ---

            // --- NEW: Save transaction data to TiNpcData if exiting due to deactivation ---
            // Check if the Runner is a TI NPC and the next state is ReturningToPool
            // The Runner's TransitionToState sets the *new* state before calling OnExit.
            // So, we check if the *current* state (which is this state) is being exited
            // and the *next* state is ReturningToPool.
            // We can check the Runner's GetCurrentState() to see what it's transitioning *to*.
            // However, a simpler check is to see if the Runner is a TI NPC and its TiData is valid.
            // The TiNpcManager.RequestDeactivateTiNpc ensures the Runner transitions to ReturningToPool.
            // So, if this OnExit is called on a TI NPC Runner, it's likely part of the deactivation flow.
            if (context.Runner.IsTrueIdentityNpc && context.TiData != null)
            {
                 Debug.Log($"{context.NpcObject.name}: TI Cashier exiting ProcessingCheckout. Checking if customer is also TI for simulation data save.", context.NpcObject);

                 // Check if the customer being processed is also a TI NPC
                 // The customer reference should still be valid on the Runner at this point.
                 NpcStateMachineRunner customerRunner = context.Runner.CurrentCustomerRunner; // Get the customer Runner from the Cashier Runner
                 if (customerRunner != null && customerRunner.IsTrueIdentityNpc && customerRunner.TiData != null)
                 {
                      Debug.Log($"{context.NpcObject.name}: Customer '{customerRunner.gameObject.name}' is also a TI NPC. Saving transaction data to TiNpcData for simulation.", context.NpcObject);

                      // Calculate remaining time and value for simulation
                      // Using a simplified calculation based on total value for simulation time
                      float totalValue = customerRunner.GetItemsToBuy().Sum(item => item.details.price * item.quantity); // Recalculate total value from customer's shopper
                      // Calculate a simulated time remaining based on total value, ensuring a minimum time
                      float simulatedTimeRemaining = Mathf.Max(0.1f, totalValue / 50f); // Example: 0.1s per $50 value, minimum 0.1s

                      // Save the data to the Cashier's TiNpcData
                      context.TiData.simulatedProcessingCustomerTiId = customerRunner.TiData.Id; // Save customer's TI ID
                      context.TiData.simulatedTransactionValue = totalValue; // Save total value
                      context.TiData.simulatedProcessingTimeRemaining = simulatedTimeRemaining; // Save calculated simulated time remaining

                      Debug.Log($"SIM {context.NpcObject.name}: Saved transaction data to TiNpcData: Customer='{context.TiData.simulatedProcessingCustomerTiId}', Value={context.TiData.simulatedTransactionValue:F2}, Time Remaining={context.TiData.simulatedProcessingTimeRemaining:F2}s.", context.NpcObject);
                 }
                 else if (customerRunner != null)
                 {
                      // TI Cashier processing a Transient customer. No simulation data needed.
                      Debug.Log($"{context.NpcObject.name}: TI Cashier exiting ProcessingCheckout while processing a Transient customer ('{customerRunner.gameObject.name}'). No simulation data saved.", context.NpcObject);
                      // Ensure simulation data fields are cleared on the cashier's TiData if they were somehow set
                      context.TiData.simulatedProcessingCustomerTiId = null;
                      context.TiData.simulatedTransactionValue = 0f;
                      context.TiData.simulatedProcessingTimeRemaining = 0f;
                 }
                 else
                 {
                      // TI Cashier exiting ProcessingCheckout but customer reference is null. Inconsistency.
                      Debug.LogWarning($"{context.NpcObject.name}: TI Cashier exiting ProcessingCheckout but CurrentCustomerRunner is null! Cannot save transaction data.", context.NpcObject);
                       // Ensure simulation data fields are cleared on the cashier's TiData if they were somehow set
                      context.TiData.simulatedProcessingCustomerTiId = null;
                      context.TiData.simulatedTransactionValue = 0f;
                      context.TiData.simulatedProcessingTimeRemaining = 0f;
                 }
            }
            else
            {
                 // This is a Transient Cashier (if you have them) or a TI Cashier exiting for a reason other than pooling.
                 // No simulation data needs to be saved.
                 Debug.Log($"{context.NpcObject.name}: Non-TI Cashier or TI Cashier exiting ProcessingCheckout for non-pooling reason. No simulation data saved.", context.NpcObject);
                 // Ensure simulation data fields are cleared on the cashier's TiData if they were somehow set
                 if (context.TiData != null)
                 {
                      context.TiData.simulatedProcessingCustomerTiId = null;
                      context.TiData.simulatedTransactionValue = 0f;
                      context.TiData.simulatedProcessingTimeRemaining = 0f;
                 }
            }
            // --- END NEW ---


            // --- Clear the customer reference on the Runner ---
            // This must happen when exiting this state, regardless of NPC type or reason for exit.
            if (context.Runner.CurrentCustomerRunner != null)
            {
                 Debug.Log($"{context.NpcObject.name}: Clearing CurrentCustomerRunner reference on Runner.", context.NpcObject);
                 context.Runner.CurrentCustomerRunner = null; // Clear the field on the Cashier Runner
            }
             // Clear local cache reference as well
             currentCustomerRunnerRef = null;
            // --- END Clear Customer Reference ---


            // Note: Animation handler could be used here
            // context.PlayAnimation("Idle"); // Reset animation
        }

        // The coroutine logic (modified to use passed values)
        private IEnumerator ProcessItemsRoutine(NpcStateContext context, NpcStateMachineRunner customerRunner, float totalValue, float totalTime) // Use passed values
        {
             Debug.Log($"{context.NpcObject.name}: ProcessItemsRoutine started for {customerRunner.gameObject.name}. Total Value: {totalValue:F2}, Total Time: {totalTime:F2}s.", context.NpcObject);

             // Simulate processing time
             yield return new WaitForSeconds(totalTime);

             Debug.Log($"{context.NpcObject.name}: Checkout simulation complete.", context.NpcObject);

             // --- Give money to the player ---
             if (EconomyManager.Instance != null)
             {
                  EconomyManager.Instance.AddCurrency(totalValue); // Use passed totalValue
                  Debug.Log($"{context.NpcObject.name}: Added {totalValue} to player economy.", context.NpcObject);
             }
             else
             {
                  Debug.LogError($"{context.NpcObject.name}: EconomyManager instance not found! Cannot process payment.", context.NpcObject);
             }
             // --- END Give Money ---


             // --- Signal the customer that their transaction is completed ---
             // The customer's Runner will receive this and transition to Exiting.
             if (customerRunner != null)
             {
                 Debug.Log($"{context.NpcObject.name}: Signalling customer '{customerRunner.gameObject.name}' transaction completed.", context.NpcObject);
                 customerRunner.OnTransactionCompleted(totalValue); // Use passed totalValue
                  // Publish event for other systems that might be listening
                  context.PublishEvent(new NpcTransactionCompletedEvent(customerRunner.gameObject, totalValue));
             } else {
                 Debug.LogError($"{context.NpcObject.name}: Customer Runner became null during checkout routine!", context.NpcObject);
             }
             // --- END Signal Customer ---


             // --- Clean up and Transition back to Waiting ---
             // Note: Clearing the customer reference is handled in OnExit.
             Debug.Log($"{context.NpcObject.name}: Checkout finished. Transitioning back to CashierWaitingForCustomer.", context.NpcObject);
             // Transition the Cashier back to the waiting state
             context.TransitionToState(CashierState.CashierWaitingForCustomer);
             // --- END Cleanup ---

             // The coroutine is now finished naturally.
        }
    }
}
// --- END OF FILE CashierProcessingCheckoutSO.cs (Modified OnExit) ---