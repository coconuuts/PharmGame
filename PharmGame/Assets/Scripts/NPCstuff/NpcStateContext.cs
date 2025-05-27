// --- START OF FILE NpcStateContext.cs ---

// --- NpcStateContext.cs ---
using UnityEngine;
using CustomerManagement; // Needed for CustomerManager
using Game.NPC.Handlers; // Needed for Handlers
using Systems.Inventory; // Needed for ItemDetails (for GetItemsToBuy)
using System.Collections.Generic; // Needed for List
using System.Collections;
using System;
using Game.Events; // Needed for publishing events and event args like NpcEnteredStoreEvent
using Game.NPC; // Needed for CustomerState and GeneralState enums
using Game.NPC.States; // Needed for NpcStateSO

namespace Game.NPC.States // Context is closely related to states
{
    /// <summary>
    /// Provides necessary references and helper methods to an NpcStateSO
    /// currently being executed by the NpcStateMachineRunner.
    /// Passed to OnEnter, OnUpdate, OnExit methods.
    /// </summary>
    public struct NpcStateContext
    {
        // --- References to Handlers (Component on the NPC GameObject) ---
        public NpcMovementHandler MovementHandler;
        public NpcAnimationHandler AnimationHandler;
        public CustomerShopper Shopper;
        // Reference to the new interruption handler
        public NpcInterruptionHandler InterruptionHandler; // <-- NEW FIELD


        // --- External References ---
        public CustomerManager Manager;
        public CashRegisterInteractable CachedCashRegister; // Cached by a state (e.g., Waiting)
        // Access the cached register via the Runner property
        public CashRegisterInteractable RegisterCached => Runner?.CachedCashRegister;


        // --- NPC-specific Data managed by the Runner/Context ---
        public GameObject NpcObject; // The GameObject the runner is on
        public NpcStateMachineRunner Runner; // Reference back to the runner (use cautiously to avoid circular calls)

        // Other NPC data fields (managed by Runner, accessed via Context)
        public BrowseLocation? CurrentTargetLocation;
        public int AssignedQueueSpotIndex;
        // InteractorObject is now managed by NpcInterruptionHandler
        // public GameObject InteractorObject { get; internal set; } // <-- REMOVED

        // Access the InteractorObject via the InterruptionHandler
        public GameObject InteractorObject => InterruptionHandler?.InteractorObject; // <-- UPDATED GETTER


        // This is the field the Runner sets in the struct instances passed to states.
        public QueueType _currentQueueMoveType;


        // --- Helper Methods (Accessing Handlers or Runner functionality) ---

        public void RotateTowardsTarget(Quaternion targetRotation) => Runner?.MovementHandler?.StartRotatingTowards(targetRotation);
        public bool IsAtDestination() => Runner != null && Runner.MovementHandler != null && Runner.MovementHandler.IsAtDestination();
        public bool MoveToDestination(Vector3 position)
        {
            if (Runner != null && Runner.MovementHandler != null)
            {
                bool success = Runner.MovementHandler.SetDestination(position); // Call handler method
                if (success)
                {
                    Runner._hasReachedCurrentDestination = false; // <-- Reset flag here on success
                    // --- NEW: Store the target position on the Runner ---
                    Runner.SetCurrentDestinationPosition(position);
                    // --- END NEW ---
                    // Debug.Log($"Context({NpcObject.name}): SetDestination successful, _hasReachedCurrentDestination = false."); // Keep logging for debugging
                } else {
                     // --- NEW: Clear the target position on failure ---
                     Runner.SetCurrentDestinationPosition(null);
                     // --- END NEW ---
                }
                // MovementHandler.SetDestination logs its own warnings/errors if agent is null/disabled/position invalid
                return success;
            }
            Debug.LogWarning($"Context({NpcObject.name}): Cannot set destination to {position}, Runner or MovementHandler is null.", NpcObject);
            // --- NEW: Clear the target position if Runner/Handler is null ---
            Runner?.SetCurrentDestinationPosition(null);
            // --- END NEW ---
            return false;
        }

        /// <summary>
        /// Helper for state SOs to trigger state transition via the Runner.
        /// </summary>
        public void TransitionToState(NpcStateSO nextState)
        {
            Runner?.TransitionToState(nextState);
        }

        /// <summary>
         /// Helper for state SOs to trigger state transition via the Runner using an Enum key.
         /// Finds the state SO using the Enum key and then transitions.
         /// </summary>
         public void TransitionToState(Enum enumKey)
         {
            if (enumKey == null)
            {
                Debug.LogError($"NpcStateContext: Attempted to transition using a null Enum key!");
                return;
            }
              // Get the state SO from the Runner using the generic GetStateSO
              NpcStateSO nextState = Runner?.GetStateSO(enumKey);
              // Then transition to the found state SO
              TransitionToState(nextState); // Calls the NpcStateSO overload
         }


        /// <summary>
        /// Helper for state SOs to start a coroutine managed by the Runner.
        /// </summary>
        public Coroutine StartCoroutine(IEnumerator routine) => Runner?.StartManagedStateCoroutine(routine);

         /// <summary>
         /// Helper for state SOs to stop a managed coroutine.
         /// </summary>
         public void StopCoroutine(Coroutine routine) => Runner?.StopManagedStateCoroutine(routine);


         /// <summary>
         /// Helper to get the current state SO being executed.
         /// </summary>
         public NpcStateSO GetCurrentState() => Runner?.GetCurrentState();

         /// <summary>
         /// Helper to get the previous state SO.
         /// </summary>
         public NpcStateSO GetPreviousState() => Runner?.GetPreviousState();

         // Access to Shopper methods
         public List<(ItemDetails details, int quantity)> GetItemsToBuy() => Shopper?.GetItemsToBuy() ?? new List<(ItemDetails, int)>();

        // Access to Manager methods
        public BrowseLocation? GetRandomBrowseLocation() => Manager?.GetRandomBrowseLocation();
        public Transform GetRegisterPoint() => Manager?.GetRegisterPoint();
        public Transform GetRandomExitPoint() => Manager?.GetRandomExitPoint();
        public Transform GetQueuePoint(int index) => Manager?.GetQueuePoint(index); // Add helper for queue points
        public Transform GetSecondaryQueuePoint(int index) => Manager?.GetSecondaryQueuePoint(index);

        public bool IsRegisterOccupied() => Manager != null && Manager.IsRegisterOccupied();

        public bool TryJoinQueue(NpcStateMachineRunner Runner, out Transform assignedSpot, out int spotIndex)
        {
            // The Manager.TryJoinQueue method expects the Runner instance itself.
            // We have the Runner instance available via context.Runner.
            // We also need to pass the out parameters correctly.
            // Call the Manager method directly via context.Manager
            if (Manager != null)
            {
                // Call the Manager method, passing the Runner from the context
                return Manager.TryJoinQueue(Runner, out assignedSpot, out spotIndex); // Pass context.Runner
            }
            else
            {
                Debug.LogWarning($"NpcStateContext: Manager reference is null when calling TryJoinQueue!", NpcObject);
                assignedSpot = null;
                spotIndex = -1;
                return false;
            }
        }

        public bool TryJoinSecondaryQueue(out Transform assignedSpot, out int spotIndex) // REMOVED NpcStateMachineRunner parameter
        {
             // The Manager.TryJoinSecondaryQueue method expects the Runner instance.
             // Call the Manager method directly via context.Manager
             if (Manager != null)
             {
                  return Manager.TryJoinSecondaryQueue(Runner, out assignedSpot, out spotIndex); // Pass context.Runner
             }
             else
             {
                  Debug.LogWarning($"NpcStateContext: Manager reference is null when calling TryJoinSecondaryQueue!", NpcObject);
                  assignedSpot = null;
                  spotIndex = -1;
                  return false;
             }
        }
        public void SignalCustomerAtRegister()
        {
             Manager?.SignalCustomerAtRegister(Runner); // Pass context.Runner
        }


        // Access to AnimationHandler methods
        public void SetAnimationSpeed(float speed) => AnimationHandler?.SetSpeed(speed);
        public void PlayAnimation(string stateName, int layer = 0, float normalizedTime = 0f) => AnimationHandler?.Play(stateName, layer, normalizedTime);


        // Access to CashRegisterInteractable caching
        public void CacheCashRegister(CashRegisterInteractable register)
        {
            if (Runner != null)
            {
                Runner.CachedCashRegister = register; // <-- This sets the FIELD on the NpcStateMachineRunner class instance
            }
            else
            {
                Debug.LogWarning($"NpcStateContext: Cannot cache register '{register?.name ?? "NULL"}' - Runner is null!", NpcObject);
            }
        }
         // Access to publishing events via EventManager
         /// <summary>
         /// Publishes an event using the global EventManager.
         /// </summary>
         /// <typeparam name="T">The type of the event arguments (must be a struct).</typeparam>
         /// <param name="eventArgs">The event arguments instance.</param>
         public void PublishEvent<T>(T eventArgs) where T : struct // Constrain to struct as per EventManager
         {
            Debug.Log($"DEBUG Context ({NpcObject.name}): Publishing Event: {typeof(T).Name}", NpcObject);
            EventManager.Publish(eventArgs);
         }

         // --- NEW: Helper methods for Interruption Handling via the InterruptionHandler ---
         /// <summary>
         /// Attempts to interrupt the current state and transition to an interruption state.
         /// </summary>
         /// <param name="interruptStateEnum">The enum key for the desired interruption state (e.g., GeneralState.Combat).</param>
         /// <param name="interactor">The GameObject that caused the interruption (e.g., player).</param>
         /// <returns>True if the interruption was successfully initiated, false otherwise.</returns>
         public bool TryInterrupt(Enum interruptStateEnum, GameObject interactor)
         {
             return InterruptionHandler?.TryInterrupt(interruptStateEnum, interactor) ?? false;
         }

         /// <summary>
         /// Ends the current interruption state and returns to the previous state on the stack.
         /// Called from an interruption state's logic when the interruption is over.
         /// </summary>
         public void EndInterruption()
         {
              InterruptionHandler?.EndInterruption();
         }
         // --- END NEW ---
    }
}
// --- END OF FILE NpcStateContext.cs ---