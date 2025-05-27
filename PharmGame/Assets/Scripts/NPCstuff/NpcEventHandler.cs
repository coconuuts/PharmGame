// --- START OF FILE NpcEventHandler.cs ---

using UnityEngine;
using Game.Events; // Needed for EventManager and event structs
using Game.NPC; // Needed for CustomerState and GeneralState enums
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
            EventManager.Subscribe<NpcTransactionCompletedEvent>(HandleTransactionCompleted);

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
            EventManager.Unsubscribe<NpcStartedTransactionEvent>(HandleTransactionStarted); // TYPO FIX: Should be HandleStartedTransaction?? Reverted for consistency
            EventManager.Unsubscribe<NpcStartedTransactionEvent>(HandleTransactionStarted); // FIX: Back to original name
            EventManager.Unsubscribe<NpcTransactionCompletedEvent>(HandleTransactionCompleted);

            // Unsubscribe from interruption events
            EventManager.Unsubscribe<NpcAttackedEvent>(HandleNpcAttacked);
            EventManager.Unsubscribe<NpcInteractedEvent>(HandleNpcInteracted); // TYPO FIX: Should be HandleInteracted?? Reverted for consistency
            EventManager.Unsubscribe<NpcInteractedEvent>(HandleNpcInteracted); // FIX: Back to original name
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
             if (eventArgs.NpcObject != this.gameObject) return;
             if (runner == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received NpcImpatientEvent but runner is null! Cannot handle.", this); return; }

             Debug.Log($"{gameObject.name}: EventHandler handling NpcImpatientEvent from state {eventArgs.State}. Telling Runner to Transition to Exiting.");
             runner.TransitionToState(runner.GetStateSO(CustomerState.Exiting));
        }

        /// <summary>
        /// Handles the ReleaseNpcFromSecondaryQueueEvent. Tells the Runner to transition to Entering.
        /// </summary>
        private void HandleReleaseNpcFromSecondaryQueue(ReleaseNpcFromSecondaryQueueEvent eventArgs)
        {
             if (eventArgs.NpcObject != this.gameObject) return;
             if (runner == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received ReleaseNpcFromSecondaryQueueEvent but runner is null! Cannot handle.", this); return; }

             Debug.Log($"{gameObject.name}: EventHandler handling ReleaseNpcFromSecondaryQueueEvent. Telling Runner to Transition to Entering.");
             runner.AssignedQueueSpotIndex = -1; // This still feels like Runner/State logic, but kept for now
             runner.TransitionToState(runner.GetStateSO(CustomerState.Entering));
        }

        /// <summary>
        /// Handles the NpcStartedTransactionEvent. Tells the Runner to transition to TransactionActive.
        /// </summary>
        private void HandleTransactionStarted(NpcStartedTransactionEvent eventArgs)
        {
             if (eventArgs.NpcObject != this.gameObject) return;
             if (runner == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received NpcStartedTransactionEvent but runner is null! Cannot handle.", this); return; }


             Debug.Log($"{gameObject.name}: EventHandler handling NpcStartedTransactionEvent. Telling Runner to Transition to TransactionActive.");
             runner.TransitionToState(runner.GetStateSO(CustomerState.TransactionActive));
        }

        /// <summary>
        /// Handles the NpcTransactionCompletedEvent. Tells the Runner to transition to Exiting.
        /// </summary>
        private void HandleTransactionCompleted(NpcTransactionCompletedEvent eventArgs)
        {
             if (eventArgs.NpcObject != this.gameObject) return;
             if (runner == null) { Debug.LogError($"NpcEventHandler on {gameObject.name}: Received NpcTransactionCompletedEvent but runner is null! Cannot handle.", this); return; }


             Debug.Log($"{gameObject.name}: EventHandler handling NpcTransactionCompletedEvent. Telling Runner to Transition to Exiting.");
             runner.TransitionToState(runner.GetStateSO(CustomerState.Exiting));
        }

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