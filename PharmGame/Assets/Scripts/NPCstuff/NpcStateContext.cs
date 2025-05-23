// --- NpcStateContext.cs ---
using UnityEngine;
using CustomerManagement; // Needed for CustomerManager
using Game.NPC.Handlers; // Needed for Handlers
using Systems.Inventory; // Needed for ItemDetails (for GetItemsToBuy)
using System.Collections.Generic; // Needed for List
using System.Collections;
using System;
using Game.Events; // Needed for publishing events
using Game.NPC;

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

        // --- External References ---
        public CustomerManager Manager;
        public CashRegisterInteractable CachedCashRegister; // Cached by a state (e.g., Waiting)

        // --- NPC-specific Data managed by the Runner/Context ---
        public GameObject NpcObject; // The GameObject the runner is on
        public NpcStateMachineRunner Runner; // Reference back to the runner (use cautiously to avoid circular calls)

        // Other NPC data needed by states could be added here (e.g., CurrentTargetLocation, AssignedQueueSpotIndex)
        // For now, let's keep those on the Runner/AI and access via Runner if needed,
        // but moving them here makes context more self-contained for states.
        // Let's add the commonly used ones:
        public BrowseLocation? CurrentTargetLocation;
        public int AssignedQueueSpotIndex;
        public GameObject InteractorObject;


        // --- Helper Methods (Accessing Handlers or Runner functionality) ---

        public void RotateTowardsTarget(Quaternion targetRotation) => Runner?.MovementHandler?.StartRotatingTowards(targetRotation);
        public bool MoveToDestination(Vector3 position) => Runner != null && Runner.MovementHandler != null && Runner.MovementHandler.SetDestination(position);
        public bool IsAtDestination() => Runner != null && Runner.MovementHandler != null && Runner.MovementHandler.IsAtDestination();
        
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
            CachedCashRegister = register;
        }

         // Access to publishing events via EventManager
         public void PublishEvent<T>(T eventArgs) where T : struct // Constrain to struct as per EventManager
         {
             EventManager.Publish(eventArgs);
         }
    }
}