// --- NpcStateContext.cs ---
using UnityEngine;
using CustomerManagement; // Needed for CustomerManager
using Game.NPC.Handlers; // Needed for Handlers
using Systems.Inventory; // Needed for ItemDetails (for GetItemsToBuy)
using System.Collections.Generic; // Needed for List
using System.Collections;
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


        // --- Helper Methods (Accessing Handlers or Runner functionality) ---

        /// <summary>
        /// Helper for state SOs to smoothly rotate the NPC via the MovementHandler.
        /// </summary>
        public void RotateTowardsTarget(Quaternion targetRotation)
        {
            MovementHandler?.StartRotatingTowards(targetRotation);
        }

        /// <summary>
        /// Helper for state SOs to set movement destination via the MovementHandler.
        /// </summary>
        public bool MoveToDestination(Vector3 position)
        {
             return MovementHandler != null && MovementHandler.SetDestination(position);
        }

        /// <summary>
        /// Helper for state SOs to check if destination is reached via the MovementHandler.
        /// </summary>
        public bool IsAtDestination()
        {
             return MovementHandler != null && MovementHandler.IsAtDestination();
        }

        /// <summary>
        /// Helper for state SOs to trigger state transition via the Runner.
        /// </summary>
        public void TransitionToState(NpcStateSO nextState)
        {
             Runner?.TransitionToState(nextState);
        }

         /// <summary>
         /// Helper for state SOs to trigger state transition using the old enum (temporary).
         /// </summary>
         public void TransitionToState(CustomerState nextStateEnum)
         {
              Runner?.TransitionToState(Runner.GetStateSO(nextStateEnum));
         }


        /// <summary>
        /// Helper for state SOs to start a coroutine managed by the Runner.
        /// </summary>
        public Coroutine StartCoroutine(IEnumerator routine)
        {
             return Runner?.StartManagedStateCoroutine(routine);
        }

         /// <summary>
         /// Helper for state SOs to stop a managed coroutine.
         /// </summary>
         public void StopCoroutine(Coroutine routine)
         {
              Runner?.StopManagedStateCoroutine(routine);
         }


         /// <summary>
         /// Helper to get the current state SO being executed.
         /// </summary>
         public NpcStateSO GetCurrentState()
         {
              return Runner?.GetCurrentState();
         }

         /// <summary>
         /// Helper to get the previous state SO.
         /// </summary>
         public NpcStateSO GetPreviousState()
         {
             return Runner?.GetPreviousState();
         }

         // Access to Shopper methods
         public List<(ItemDetails details, int quantity)> GetItemsToBuy()
         {
             return Shopper?.GetItemsToBuy() ?? new List<(ItemDetails, int)>();
         }

         // Access to Manager methods
         public BrowseLocation? GetRandomBrowseLocation()
         {
             return Manager?.GetRandomBrowseLocation();
         }

         public Transform GetRegisterPoint()
         {
             return Manager?.GetRegisterPoint();
         }

         public Transform GetRandomExitPoint()
         {
             return Manager?.GetRandomExitPoint();
         }

         public bool IsRegisterOccupied()
         {
             return Manager != null && Manager.IsRegisterOccupied();
         }

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


        // Access to AnimationHandler methods (can add more as needed)
        public void SetAnimationSpeed(float speed)
        {
            AnimationHandler?.SetSpeed(speed);
        }

        public void PlayAnimation(string stateName, int layer = 0, float normalizedTime = 0f)
        {
            AnimationHandler?.Play(stateName, layer, normalizedTime);
        }

        // Access to CashRegisterInteractable caching
        public void CacheCashRegister(CashRegisterInteractable register)
        {
            CachedCashRegister = register;
        }

         // Access to publishing events via EventManager (can be done directly or via context helper)
         // Example helper:
         public void PublishEvent<T>(T eventArgs)
         {
             EventManager.Publish(eventArgs);
         }
    }
}