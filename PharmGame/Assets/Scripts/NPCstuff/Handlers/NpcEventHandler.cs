// --- START OF FILE NpcEventHandler.cs ---

// --- START OF FILE NpcEventHandler.cs ---

// --- START OF FILE NpcEventHandler.cs ---

using UnityEngine;
using Game.Events; // Needed for EventManager and event structs
using Game.NPC; // Needed for CustomerState and GeneralState, CashierState enums
using CustomerManagement; // Needed for CustomerManager reference
using Game.NPC.TI; // Needed for TiNpcManager reference
using System; // Needed for Enum
using Game.NPC.States; // Added in previous step

namespace Game.NPC.Handlers // Placing handlers together
{
    /// <summary>
    /// Component responsible for subscribing to global events relevant to an NPC
    /// and relaying them to the NpcStateMachineRunner for state transitions or actions.
    /// Reduces the event handling burden on the Runner itself.
    /// ADDED: Handles CustomerReadyForCashierEvent for Cashier NPCs.
    /// </summary>
    [RequireComponent(typeof(NpcStateMachineRunner))]
    [RequireComponent(typeof(NpcInterruptionHandler))] // <-- NEW: Require interruption handler
    public class NpcEventHandler : MonoBehaviour
    {
        // Reference to the state machine runner on this GameObject
        private NpcStateMachineRunner runner;
        // Reference to the Interruption Handler (Will use its methods)
        private NpcInterruptionHandler interruptionHandler; // <-- Declare interruption handler reference

        private void Awake()
        {
            runner = GetComponent<NpcStateMachineRunner>();
            if (runner == null)
            {
                Debug.LogError($"NpcEventHandler on {gameObject.name}: NpcStateMachineRunner component not found! This handler requires it. Self-disabling.", this);
                enabled = false;
                return;
            }
            // Get Interruption Handler reference
            interruptionHandler = GetComponent<NpcInterruptionHandler>(); // <-- Get interruption handler reference
            if (interruptionHandler == null)
            {
                Debug.LogError($"NpcEventHandler on {gameObject.name}: NpcInterruptionHandler component not found! This handler requires it. Self-disabling.", this);
                enabled = false;
                return;
            }


            Debug.Log($"{gameObject.name}: NpcEventHandler Awake completed. Runner and InterruptionHandler references acquired.", this); // Updated log
        }

        private void OnEnable()
        {
            // Subscribe to events that directly trigger state transitions or Runner logic
            EventManager.Subscribe<NpcImpatientEvent>(HandleNpcImpatient);
            EventManager.Subscribe<ReleaseNpcFromSecondaryQueueEvent>(HandleReleaseNpcFromSecondaryQueue);
            EventManager.Subscribe<NpcStartedTransactionEvent>(HandleTransactionStarted);
            EventManager.Subscribe<NpcTransactionCompletedEvent>(HandleNpcTransactionCompleted);

            // Subscribe to Prescription Order Obtained event
            EventManager.Subscribe<NpcPrescriptionOrderObtainedEvent>(HandlePrescriptionOrderObtained);
            // --- NEW: Subscribe to Prescription Delivered event ---
            EventManager.Subscribe<NpcPrescriptionDeliveredEvent>(HandlePrescriptionDelivered);
            // --- END NEW ---

            // --- NEW: Subscribe to Cashier Specific Events --- // <-- NEW SUBSCRIPTIONS
            EventManager.Subscribe<CustomerReadyForCashierEvent>(HandleCustomerReadyForCashier);
            // --- END NEW ---


            // Subscribe to interruption events
            EventManager.Subscribe<NpcAttackedEvent>(HandleNpcAttacked);
            EventManager.Subscribe<NpcInteractedEvent>(HandleNpcInteracted);
            EventManager.Subscribe<TriggerNpcEmoteEvent>(HandleTriggerEmote);
            EventManager.Subscribe<NpcCombatEndedEvent>(HandleCombatEnded);
            EventManager.Subscribe<NpcInteractionEndedEvent>(HandleInteractionEnded);
            EventManager.Subscribe<NpcEmoteEndedEvent>(HandleEmoteEnded);


            Debug.Log($"{gameObject.name}: NpcEventHandler subscribed to relevant events.");
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            EventManager.Unsubscribe<NpcImpatientEvent>(HandleNpcImpatient);
            EventManager.Unsubscribe<ReleaseNpcFromSecondaryQueueEvent>(HandleReleaseNpcFromSecondaryQueue);
            EventManager.Unsubscribe<NpcStartedTransactionEvent>(HandleTransactionStarted);
            EventManager.Unsubscribe<NpcTransactionCompletedEvent>(HandleNpcTransactionCompleted);

            // Unsubscribe from Prescription Order Obtained event
            EventManager.Unsubscribe<NpcPrescriptionOrderObtainedEvent>(HandlePrescriptionOrderObtained);
            // --- NEW: Unsubscribe from Prescription Delivered event ---
            EventManager.Unsubscribe<NpcPrescriptionDeliveredEvent>(HandlePrescriptionDelivered);
            // --- END NEW ---

             // --- NEW: Unsubscribe from Cashier Specific Events --- // <-- NEW UNSUBSCRIPTIONS
            EventManager.Unsubscribe<CustomerReadyForCashierEvent>(HandleCustomerReadyForCashier);
            // --- END NEW ---


            // Unsubscribe from interruption events
            EventManager.Unsubscribe<NpcAttackedEvent>(HandleNpcAttacked);
            EventManager.Unsubscribe<NpcInteractedEvent>(HandleNpcInteracted);
            EventManager.Unsubscribe<TriggerNpcEmoteEvent>(HandleTriggerEmote);
            EventManager.Unsubscribe<NpcCombatEndedEvent>(HandleCombatEnded);
            EventManager.Unsubscribe<NpcInteractionEndedEvent>(HandleInteractionEnded);
            EventManager.Unsubscribe<NpcEmoteEndedEvent>(HandleEmoteEnded);


            Debug.Log($"{gameObject.name}: NpcEventHandler unsubscribed from events.");
        }


        // --- Event Handlers ---

        /// <summary>
        /// Handles the NpcImpatientEvent. Tells the Runner to transition to Exiting.
        /// </summary>
        private void HandleNpcImpatient(NpcImpatientEvent eventArgs)
        {
             // Only handle if this event is for THIS NPC
             if (eventArgs.NpcObject != this.gameObject) return;
             if (runner == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received NpcImpatientEvent but runner is null! Cannot handle.", this); return; }

             Debug.Log($"{gameObject.name}: EventHandler handling NpcImpatientEvent from state {eventArgs.State}. Telling Runner to Transition to Exiting.");
             // Transition to Exiting if the NPC is a Customer (or any type that uses Exiting)
             // We need to be careful not to make a Cashier exit if they become impatient (though Cashiers likely won't have impatience states).
             // Assuming NpcImpatientEvent is only published by Customer states like WaitingAtRegister/Queue, this should be fine.
             runner.TransitionToState(runner.GetStateSO(CustomerState.Exiting));
        }

        /// <summary>
        /// Handles the ReleaseNpcFromSecondaryQueueEvent. Tells the Runner to transition to Entering.
        /// </summary>
        private void HandleReleaseNpcFromSecondaryQueue(ReleaseNpcFromSecondaryQueueEvent eventArgs)
        {
             // Only handle if this event is for THIS NPC
             if (eventArgs.NpcObject != this.gameObject) return;
             if (runner == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received ReleaseNpcFromSecondaryQueueEvent but runner is null! Cannot handle.", this); return; }

             Debug.Log($"{gameObject.name}: EventHandler handling ReleaseNpcFromSecondaryQueueEvent. Telling Runner to Transition to Entering.");
             // This event is only relevant for Customer NPCs
             runner.TransitionToState(runner.GetStateSO(CustomerState.Entering));
        }

        /// <summary>
        /// Handles the NpcStartedTransactionEvent. Tells the *Customer* Runner to transition to TransactionActive.
        /// This event is published by the CashRegisterInteractable (for player checkout) or the Cashier NPC (for cashier checkout).
        /// </summary>
        private void HandleTransactionStarted(NpcStartedTransactionEvent eventArgs)
        {
             // Only handle if this event is for THIS NPC (which is the customer in this case)
             if (eventArgs.NpcObject != this.gameObject) return;
             if (runner == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received NpcStartedTransactionEvent but runner is null! Cannot handle.", this); return; }

             Debug.Log($"{gameObject.name}: EventHandler handling NpcStartedTransactionEvent. Telling Runner to Transition to TransactionActive.");
             // This event is only relevant for Customer NPCs
             runner.TransitionToState(runner.GetStateSO(CustomerState.TransactionActive));
        }

        /// <summary>
        /// Handles the NpcTransactionCompletedEvent. Tells the *Customer* Runner to transition to Exiting.
        /// This event is published by the CashRegisterInteractable (for player checkout) or the Cashier NPC (for cashier checkout).
        /// </summary>
        private void HandleNpcTransactionCompleted(NpcTransactionCompletedEvent eventArgs)
        {
             // Only handle if this event is for THIS NPC (which is the customer in this case)
             if (eventArgs.NpcObject != this.gameObject) return;
             if (runner == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received NpcTransactionCompletedEvent but runner is null! Cannot handle.", this); return; }

             Debug.Log($"{gameObject.name}: EventHandler handling NpcTransactionCompletedEvent. Telling Runner to Transition to Exiting.");
             // This event is only relevant for Customer NPCs
             runner.TransitionToState(runner.GetStateSO(CustomerState.Exiting));
        }

        /// <summary>
        /// Handles the NpcPrescriptionOrderObtainedEvent. Tells the *Customer* Runner to transition to WaitingForDelivery.
        /// </summary>
        private void HandlePrescriptionOrderObtained(NpcPrescriptionOrderObtainedEvent eventArgs)
        {
             // Only handle if this event is for THIS NPC (which is the customer)
             if (eventArgs.NpcObject != this.gameObject) return;
             if (runner == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received NpcPrescriptionOrderObtainedEvent but runner is null! Cannot handle.", this); return; }

             Debug.Log($"{gameObject.name}: EventHandler handling NpcPrescriptionOrderObtainedEvent. Telling Runner to Transition to WaitingForDelivery.");
             // Transition to the new state
             runner.TransitionToState(runner.GetStateSO(CustomerState.WaitingForDelivery));
        }

        /// <summary>
        /// Handles the NpcPrescriptionDeliveredEvent. Tells the *Customer* Runner to transition to Exiting.
        /// </summary>
        private void HandlePrescriptionDelivered(NpcPrescriptionDeliveredEvent eventArgs) // <-- NEW HANDLER
        {
             // Only handle if this event is for THIS NPC (which is the customer)
             if (eventArgs.NpcObject != this.gameObject) return;
             if (runner == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received NpcPrescriptionDeliveredEvent but runner is null! Cannot handle.", this); return; }

             Debug.Log($"{gameObject.name}: EventHandler handling NpcPrescriptionDeliveredEvent. Telling Runner to Transition to Exiting.");
             // Transition to the Exiting state after successful delivery
             runner.TransitionToState(runner.GetStateSO(CustomerState.Exiting));
        }
        // --- END NEW HANDLER ---

        // --- NEW: Handler for CustomerReadyForCashierEvent --- // <-- NEW HANDLER
        /// <summary>
        /// Handles the CustomerReadyForCashierEvent. Tells the *Cashier* Runner to transition to ProcessingCheckout.
        /// </summary>
        private void HandleCustomerReadyForCashier(CustomerReadyForCashierEvent eventArgs)
        {
             // Only handle if this event is for THIS NPC (which is the cashier)
             if (eventArgs.CashierObject != this.gameObject) return;
             if (runner == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received CustomerReadyForCashierEvent but runner is null! Cannot handle.", this); return; }

             Debug.Log($"{gameObject.name}: EventHandler handling CustomerReadyForCashierEvent. Customer: {eventArgs.CustomerObject?.name ?? "NULL"}.", this);

             // Check if the Cashier is in the correct state to receive a customer
             if (runner.GetCurrentState() != null && runner.GetCurrentState().HandledState.Equals(CashierState.CashierWaitingForCustomer))
             {
                  Debug.Log($"{gameObject.name}: Cashier is in CashierWaitingForCustomer state. Proceeding with checkout transition.", this);

                  // Get the customer's Runner component
                  NpcStateMachineRunner customerRunner = eventArgs.CustomerObject?.GetComponent<NpcStateMachineRunner>();
                  if (customerRunner != null)
                  {
                       // --- Store the customer Runner reference on the Cashier Runner ---
                       runner.CurrentCustomerRunner = customerRunner; // <-- Set the new field on the Runner
                       // --- END Store ---

                       // Transition the Cashier to the ProcessingCheckout state
                       Debug.Log($"{gameObject.name}: Telling Runner to Transition to CashierProcessingCheckout.", this);
                       runner.TransitionToState(runner.GetStateSO(CashierState.CashierProcessingCheckout));
                  }
                  else
                  {
                       Debug.LogError($"{gameObject.name}: Received CustomerReadyForCashierEvent with null or invalid CustomerObject!", this);
                  }
             }
             else
             {
                  // This event was received, but the Cashier is not in the waiting state.
                  // Ignore the event. The customer will likely become impatient.
                  Debug.LogWarning($"{gameObject.name}: Received CustomerReadyForCashierEvent, but Cashier is not in CashierWaitingForCustomer state ({runner.GetCurrentState()?.HandledState.ToString() ?? "NULL"}). Ignoring event. Customer will likely become impatient.", this);
             }
        }
        // --- END NEW HANDLER ---


        // --- Interruption Event Handlers (Now using NpcInterruptionHandler) ---

        private void HandleNpcAttacked(NpcAttackedEvent eventArgs)
        {
               if (eventArgs.NpcObject != this.gameObject) return;
               if (runner == null || interruptionHandler == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received NpcAttackedEvent but runner or interruptionHandler is null! Cannot handle.", this); return; }

               Debug.Log($"{gameObject.name}: EventHandler handling NpcAttackedEvent. Calling InterruptionHandler.TryInterrupt(Combat)."); // Updated log

               // --- NEW LOGIC: Call the InterruptionHandler ---
               interruptionHandler.TryInterrupt(GeneralState.Combat, eventArgs.AttackerObject);
               // --- END NEW LOGIC ---
        }

        private void HandleNpcInteracted(NpcInteractedEvent eventArgs)
        {
               if (eventArgs.NpcObject != this.gameObject) return;
               if (runner == null || interruptionHandler == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received NpcInteractedEvent but runner or interruptionHandler is null! Cannot handle.", this); return; }

               Debug.Log($"{gameObject.name}: EventHandler handling NpcInteractedEvent. Calling InterruptionHandler.TryInterrupt(Social)."); // Updated log

               // --- NEW LOGIC: Call the InterruptionHandler ---
               interruptionHandler.TryInterrupt(GeneralState.Social, eventArgs.InteractorObject);
               // --- END NEW LOGIC ---
        }

        private void HandleTriggerEmote(TriggerNpcEmoteEvent eventArgs)
        {
               if (eventArgs.NpcObject != this.gameObject) return;
               if (runner == null || interruptionHandler == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received TriggerNpcEmoteEvent but runner or interruptionHandler is null! Cannot handle.", this); return; }


               Debug.Log($"{gameObject.name}: EventHandler handling TriggerNpcEmoteEvent. Calling InterruptionHandler.TryInterrupt(Emoting)."); // Updated log

               // --- NEW LOGIC: Call the InterruptionHandler ---
               interruptionHandler.TryInterrupt(GeneralState.Emoting, null); // Emote trigger doesn't have an interactor
               // --- END NEW LOGIC ---
        }

        private void HandleCombatEnded(NpcCombatEndedEvent eventArgs)
        {
               if (eventArgs.NpcObject != this.gameObject) return;
               if (runner == null || interruptionHandler == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received NpcCombatEndedEvent but runner or interruptionHandler is null! Cannot handle.", this); return; }


               Debug.Log($"{gameObject.name}: EventHandler handling NpcCombatEndedEvent. Calling InterruptionHandler.EndInterruption()."); // Updated log

               // --- NEW LOGIC: Call the InterruptionHandler ---
               interruptionHandler.EndInterruption();
               // --- END NEW LOGIC ---
        }

        private void HandleInteractionEnded(NpcInteractionEndedEvent eventArgs)
        {
               if (eventArgs.NpcObject != this.gameObject) return;
               if (runner == null || interruptionHandler == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received NpcInteractionEndedEvent but runner or interruptionHandler is null! Cannot handle.", this); return; }

               Debug.Log($"{gameObject.name}: EventHandler handling NpcInteractionEndedEvent. Calling InterruptionHandler.EndInterruption()."); // Updated log

                // --- NEW LOGIC: Call the InterruptionHandler ---
               interruptionHandler.EndInterruption();
               // --- END NEW LOGIC ---
        }

        private void HandleEmoteEnded(NpcEmoteEndedEvent eventArgs)
        {
               if (eventArgs.NpcObject != this.gameObject) return;
               if (runner == null || interruptionHandler == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received NpcEmoteEndedEvent but runner or interruptionHandler is null! Cannot handle.", this); return; }

               Debug.Log($"{gameObject.name}: EventHandler handling NpcEmoteEndedEvent. Calling InterruptionHandler.EndInterruption()."); // Updated log

               // --- NEW LOGIC: Call the InterruptionHandler ---
               interruptionHandler.EndInterruption();
               // --- END NEW LOGIC ---
        }
        // --- END Event Handlers ---
    }
}

// --- END OF FILE NpcEventHandler.cs ---