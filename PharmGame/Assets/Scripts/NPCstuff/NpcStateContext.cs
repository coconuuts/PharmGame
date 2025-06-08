// --- START OF FILE NpcStateContext.cs (Modified for PrescriptionManager) ---

// --- START OF FILE NpcStateContext.cs --- // Keep original comment for history

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
using Game.Proximity; // Needed for ProximityManager.ProximityZone
using Game.Navigation; // Needed for PathSO, PathTransitionDetails
using Game.NPC.TI; // Needed for TiNpcData, TiNpcManager
using Game.Prescriptions; // Needed for PrescriptionManager // <-- NEW: Added using directive

namespace Game.NPC.States // Context is closely related to states
{
    /// <summary>
    /// Provides necessary references and helper methods to an NpcStateSO
    /// currently being executed by the NpcStateMachineRunner.
    /// Passed to OnEnter, OnUpdate, OnExit methods.
    /// MODIFIED: Includes references to TiNpcManager and PrescriptionManager.
    /// </summary>
    public class NpcStateContext
    {
        // --- References to Handlers (Component on the NPC GameObject) ---
        public NpcMovementHandler MovementHandler;
        public NpcAnimationHandler AnimationHandler;
        public CustomerShopper Shopper;
        // Reference to the interruption handler
        public NpcInterruptionHandler InterruptionHandler;
        // Reference to the Queue handler
        public NpcQueueHandler QueueHandler;
        // Reference to the Path Following handler
        public NpcPathFollowingHandler PathFollowingHandler;
         public TiNpcData TiData;


        // --- External References ---
        public CustomerManager Manager;
        // --- Reference to TiNpcManager ---
        public TiNpcManager TiNpcManager;
        // --- Reference to PrescriptionManager --- // <-- NEW Reference
        public PrescriptionManager PrescriptionManager;

        // Access the cached register via the Runner property
        public CashRegisterInteractable RegisterCached => Runner?.CachedCashRegister;
        public float DeltaTime { get; internal set; }


        // --- NPC-specific Data managed by the Runner/Context ---
        public GameObject NpcObject; // The GameObject the runner is on
        public NpcStateMachineRunner Runner; // Reference back to the runner (use cautiously to avoid circular calls)

        // Other NPC data fields (managed by Runner, accessed via Context)
        public BrowseLocation? CurrentTargetLocation;

        // Access the InteractorObject via the InterruptionHandler
        public GameObject InteractorObject => InterruptionHandler?.InteractorObject;

        // Public properties to access queue data via QueueHandler
        public int AssignedQueueSpotIndex => QueueHandler?.AssignedQueueSpotIndex ?? -1;
        public QueueType CurrentQueueMoveType => QueueHandler != null ? QueueHandler._currentQueueMoveType : QueueType.Main;

        // Public property to check if NPC is currently interrupted
        public bool IsInterrupted => InterruptionHandler?.IsInterrupted() ?? false;

        // Public properties/methods to access Path Following Handler functionality
        public bool IsFollowingPath => PathFollowingHandler?.IsFollowingPath ?? false;
        public bool HasReachedEndOfPath => PathFollowingHandler?.HasReachedEndOfPath ?? false;
        public PathSO GetCurrentPathSO() => PathFollowingHandler?.GetCurrentPathSO();


        /// <summary>
         /// Starts the NPC following a specific waypoint path using the PathFollowingHandler.
         /// </summary>
        public bool StartFollowingPath(PathSO path, int startIndex = 0, bool reverse = false)
        {
            if (PathFollowingHandler != null)
            {
                return PathFollowingHandler.StartFollowingPath(path, startIndex, reverse);
            }
            Debug.LogError($"Context({NpcObject.name}): Cannot start path following, PathFollowingHandler is null.", NpcObject);
            return false;
        }

/// <summary>
         /// Requests the PathFollowingHandler to restore path following progress.
         /// Bypasses the initial NavMesh leg.
         /// </summary>
         /// <returns>True if path following was successfully restored.</returns>
         public bool RestorePathProgress(PathSO path, int waypointIndex, bool reverse)
         {
             if (PathFollowingHandler != null && PathFollowingHandler.RestorePathProgress(path, waypointIndex, reverse))
             {
                 // PathFollowingHandler handles disabling the NavMeshAgent
                 // Runner will tick PathFollowingHandler.TickMovement in Update
                 return true;
             }
             return false;
         }

        /// <summary>
        /// Stops the NPC from following the current path using the PathFollowingHandler.
        /// </summary>
        public void StopFollowingPath()
        {
            PathFollowingHandler?.StopFollowingPath();
        }

        /// <summary>
        /// Gets the ID of the path currently being followed via the PathFollowingHandler.
        /// </summary>
        public string GetCurrentPathID() => PathFollowingHandler?.GetCurrentPathSO()?.PathID;

        /// <summary>
         /// Gets the ID of the waypoint the NPC is currently moving towards via the PathFollowingHandler.
         /// </summary>
         public string GetCurrentTargetWaypointID() => PathFollowingHandler?.GetCurrentTargetWaypointID();

         /// <summary>
         /// Gets the index of the waypoint the NPC is currently moving towards via the PathFollowingHandler.
         /// </summary>
         public int GetCurrentTargetWaypointIndex() => PathFollowingHandler?.GetCurrentTargetWaypointIndex() ?? -1;

         /// <summary>
         /// Gets the followReverse flag for the current path via the PathFollowingHandler.
         /// </summary>
         public bool GetFollowReverse() => PathFollowingHandler?.GetFollowReverse() ?? false;


        // --- Helper Methods (Accessing Handlers or Runner functionality) ---

        public void RotateTowardsTarget(Quaternion targetRotation) => Runner?.MovementHandler?.StartRotatingTowards(targetRotation);
        public bool IsAtDestination() => Runner != null && Runner.MovementHandler != null && Runner.MovementHandler.IsAtDestination();
        public bool MoveToDestination(Vector3 position)
        {
            if (Runner != null && Runner.MovementHandler != null)
            {
                // Ensure NavMeshAgent is enabled before setting destination
                Runner.MovementHandler.EnableAgent(); // <-- Ensure Agent is enabled

                bool success = Runner.MovementHandler.SetDestination(position); // Call handler method
                if (success)
                {
                    Runner._hasReachedCurrentDestination = false; // Reset flag here on success
                    // Store the target position on the Runner
                    Runner.SetCurrentDestinationPosition(position);
                } else {
                     // Clear the target position on failure
                     Runner.SetCurrentDestinationPosition(null);
                }
                // MovementHandler.SetDestination logs its own warnings/errors if agent is null/disabled/position invalid
                return success;
            }
            Debug.LogWarning($"Context({NpcObject.name}): Cannot set destination to {position}, Runner or MovementHandler is null.", NpcObject);
            // Clear the target position if Runner/Handler is null
            Runner?.SetCurrentDestinationPosition(null);
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
         /// Helper for state SOs to trigger state transition via the Runner using string key and type.
         /// Useful when the target state enum type is not known at compile time in the SO.
         /// </summary>
         public void TransitionToState(string stateEnumKey, string stateEnumType)
         {
             Runner?.TransitionToState(stateEnumKey, stateEnumType); // Delegate to the Runner's method
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

        // Access to Manager methods (These already correctly call Manager, passing Runner)
        public BrowseLocation? GetRandomBrowseLocation() => Manager?.GetRandomBrowseLocation();
        public Transform GetRegisterPoint() => Manager?.GetRegisterPoint();
        public Transform GetRandomExitPoint() => Manager?.GetRandomExitPoint();
        public Transform GetQueuePoint(int index) => Manager?.GetQueuePoint(index);
        public Transform GetSecondaryQueuePoint(int index) => Manager?.GetSecondaryQueuePoint(index);

        public bool IsRegisterOccupied() => Manager != null && Manager.IsRegisterOccupied();

        public bool TryJoinQueue(NpcStateMachineRunner Runner, out Transform assignedSpot, out int spotIndex)
        {
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

        // --- Access to PrescriptionManager methods via context --- // <-- NEW Accessors
        public Transform GetPrescriptionClaimPoint() => PrescriptionManager?.GetPrescriptionClaimPoint();
        public bool IsPrescriptionClaimSpotOccupied() => PrescriptionManager != null && PrescriptionManager.IsPrescriptionClaimSpotOccupied();
        public bool IsPrescriptionQueueFull() => PrescriptionManager != null && PrescriptionManager.IsPrescriptionQueueFull();
        public bool TryJoinPrescriptionQueue(out Transform assignedSpot, out int spotIndex)
         {
            // Let's add a super-early debug here
             bool isPmNull = PrescriptionManager == null;
            Debug.Log($"[DEBUG {NpcObject.name}] CONTEXT_TRY_JOIN_TOP: Entering context.TryJoinPrescriptionQueue. Context.PrescriptionManager is null: {isPmNull}", NpcObject);

             assignedSpot = null;
             spotIndex = -1;
             
             Debug.Log($"[DEBUG {NpcObject.name}] CONTEXT_TRY_JOIN_BEFORE_IF: Just before if check.", NpcObject);

             if (PrescriptionManager != null)
            {
                // User's previous log was likely here: Debug.Log("NpcStateContext: calling TryJoinPrescriptionQueue in PrescriptionManager");
                Debug.Log($"[DEBUG {NpcObject.name}] CONTEXT_TRY_JOIN_PM_CHECK: PrescriptionManager is NOT null.", NpcObject); // <-- Add this
                return PrescriptionManager.TryJoinPrescriptionQueue(Runner, out assignedSpot, out spotIndex); // Pass context.Runner
            }
            else
            {
                // This log was expected but didn't show up
                Debug.LogWarning($"NpcStateContext: PrescriptionManager reference is null when calling TryJoinPrescriptionQueue!", NpcObject);
                assignedSpot = null;
                spotIndex = -1;
                return false; // <-- Returns false if PM is null
            }
         }
        public Transform GetPrescriptionQueuePoint(int index) => PrescriptionManager?.GetPrescriptionQueuePoint(index);
        // Add other PrescriptionManager methods as needed by states...
        // public void StartPrescriptionSuppressionCoroutine(TiNpcData data, float duration) => PrescriptionManager?.StartPrescriptionSuppressionCoroutine(data, duration); // This is the method causing the error, needs to be called on PM, not TiNpcManager
        // The call site in PrescriptionEnteringSO will be fixed to use context.PrescriptionManager directly.
        // --- END NEW ---


        // Access to AnimationHandler methods
        public void SetAnimationSpeed(float speed) => AnimationHandler?.SetSpeed(speed);
        public void PlayAnimation(string stateName, int layer = 0, float normalizedTime = 0f) => AnimationHandler?.Play(stateName, layer, normalizedTime);


        // Access to CashRegisterInteractable caching
        public void CacheCashRegister(CashRegisterInteractable register)
        {
            if (Runner != null)
            {
                Runner.CachedCashRegister = register; // This sets the FIELD on the NpcStateMachineRunner class instance
            }
            else
            {
                Debug.LogWarning($"Context({NpcObject.name}): Cannot cache register '{register?.name ?? "NULL"}' - Runner is null.", NpcObject);
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
            // Debug.Log($"DEBUG Context ({NpcObject.name}): Publishing Event: {typeof(T).Name}", NpcObject); // Too noisy
            EventManager.Publish(eventArgs);
         }

         // --- Helper methods for Interruption Handling via the InterruptionHandler ---
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
         /// Note: NpcEventHandler also calls this on interruption end events. State logic should usually yield until the event handler triggers this.
         /// </summary>
         public void EndInterruption()
         {
              InterruptionHandler?.EndInterruption();
         }
    }
}

// --- END OF FILE NpcStateContext.cs (Modified for PrescriptionManager) ---