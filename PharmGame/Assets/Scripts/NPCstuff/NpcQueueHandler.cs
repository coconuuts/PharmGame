// --- START OF FILE NpcQueueHandler.cs (Corrected Logic) ---

using UnityEngine;
using CustomerManagement; // Needed for CustomerManager and QueueType
using Game.NPC; // Needed for NpcStateMachineRunner, CustomerState
using Game.NPC.Handlers; // Needed for NpcMovementHandler
using Game.NPC.States; // <-- Ensure this is present. Needed for NpcStateSO and CustomerState.

namespace Game.NPC.Handlers // Placing handlers together
{
    /// <summary>
    /// Handles the queue-specific logic and data for an NPC.
    /// Manages the NPC's assigned queue spot and communicates queue movement signals.
    /// This logic was refactored from NpcStateMachineRunner.
    /// </summary>
    [RequireComponent(typeof(NpcStateMachineRunner))]
    [RequireComponent(typeof(NpcMovementHandler))]
    public class NpcQueueHandler : MonoBehaviour
    {
        // --- References to Required Components (on this GameObject) ---
        private NpcStateMachineRunner runner;
        private NpcMovementHandler movementHandler;

        // --- External Reference (Provided by Runner Initialization) ---
        // We need the CustomerManager to interact with the global queue data.
        // This will be assigned via an Initialize method.
        private CustomerManagement.CustomerManager manager;


        // --- Queue Data (Moved from NpcStateMachineRunner) ---
        // Public or Internal access is needed for NpcStateContext to read these.
        public bool _isMovingToQueueSpot { get; private set; } = false;
        public int _previousQueueSpotIndex { get; private set; } = -1;
        public QueueType _currentQueueMoveType { get; internal set; } = QueueType.Main; // Default to Main
        public int AssignedQueueSpotIndex { get; internal set; } = -1;

        // CurrentTargetLocation related to queue spots can be managed here if desired,
        // or continue to be managed on the Runner. Let's keep it on the Runner for now
        // as it's used by other movement states too.


        private void Awake()
        {
            // Get references to the required components on this GameObject
            runner = GetComponent<NpcStateMachineRunner>();
            movementHandler = GetComponent<NpcMovementHandler>();

            // Basic validation
            if (runner == null || movementHandler == null)
            {
                Debug.LogError($"NpcQueueHandler ({gameObject.name}): Missing one or more required components in Awake! Runner: {runner != null}, MovementHandler: {movementHandler != null}. Self-disabling.", this);
                enabled = false; // Cannot function
            }

            Debug.Log($"NpcQueueHandler ({gameObject.name}): Awake completed. Required components acquired.", this);
        }

        // No OnEnable/OnDisable subscriptions needed here currently.

        // --- Initialization Method (Called by NpcStateMachineRunner) ---
        /// <summary>
        /// Initializes the Queue Handler with necessary external references.
        /// Called by the NpcStateMachineRunner during its Initialize or Activate process.
        /// </summary>
        public void Initialize(CustomerManagement.CustomerManager manager)
        {
            if (manager == null)
            {
                Debug.LogError($"NpcQueueHandler ({gameObject.name}): Initialized with a null CustomerManager reference! Queue logic will fail.", this);
            }
            this.manager = manager;
            Debug.Log($"NpcQueueHandler ({gameObject.name}): Initialized with CustomerManager reference.");
        }

        // --- Reset Method (Called by NpcStateMachineRunner) ---
        /// <summary>
        /// Resets the transient queue data.
        /// Called by the NpcStateMachineRunner during its ResetRunnerTransientData process.
        /// </summary>
        public void Reset()
        {
            _isMovingToQueueSpot = false;
            _previousQueueSpotIndex = -1;
            _currentQueueMoveType = QueueType.Main; // This is where it's reset on Runner Init/Activate/Deactivate
            AssignedQueueSpotIndex = -1;
            // Do NOT reset 'manager' reference here.
            Debug.Log($"NpcQueueHandler ({gameObject.name}): Transient queue data reset.");
        }


        // --- Methods Moved from NpcStateMachineRunner (Step 3 Implementation) ---

        /// <summary>
        /// Signals this NPC to transition to the state for moving to the cash register.
        /// Called by the CustomerManager when this NPC reaches the front of the main queue.
        /// </summary>
        public void GoToRegisterFromQueue()
        {
            Debug.Log($"{gameObject.name}: NpcQueueHandler received GoToRegisterFromQueue signal. Transitioning to MovingToRegister.");

            // Need to transition state via the Runner
             if (runner != null)
             {
                  // Use runner's method to get the state SO by Enum key
                  NpcStateSO nextState = runner.GetStateSO(CustomerState.MovingToRegister);
                  if (nextState != null)
                  {
                      runner.TransitionToState(nextState);
                  }
                  else
                  {
                      Debug.LogError($"NpcQueueHandler ({gameObject.name}): Failed to get MovingToRegister state SO! Cannot transition. Transitioning to Exiting (fallback).", this);
                       // Fallback to Exiting if MovingToRegister is missing
                      NpcStateSO exitState = runner.GetStateSO(CustomerState.Exiting);
                       if(exitState != null) runner.TransitionToState(exitState);
                       else Debug.LogError($"NpcQueueHandler ({gameObject.name}): Neither MovingToRegister nor Exiting fallback states found!", this);
                  }
             }
             else
             {
                  Debug.LogError($"NpcQueueHandler ({gameObject.name}): Runner reference is null! Cannot transition state for GoToRegisterFromQueue.", this);
                  // No runner, no state machine. Cannot do anything.
             }
        }

        /// <summary>
        /// Signals this NPC to move to a new spot in a queue line (used for moving up).
        /// Called by the CustomerManager during the queue cascade.
        /// </summary>
        /// <param name="nextSpotTransform">The Transform of the spot to move to.</param>
        /// <param name="newSpotIndex">The index of the spot to move to.</param>
        /// <param name="queueType">The type of queue (Main or Secondary).</param>
        public void MoveToQueueSpot(Transform nextSpotTransform, int newSpotIndex, QueueType queueType)
        {
             // Check if required references are valid
             if (runner == null || movementHandler == null || manager == null)
             {
                  Debug.LogError($"NpcQueueHandler ({gameObject.name}): Cannot move to queue spot - missing Runner({runner != null}), MovementHandler({movementHandler != null}), or Manager({manager != null}).", this);
                  // Fallback: Transition to exiting? Or let the current state handle it?
                  // If called by manager, the state should already be Queue/SecondaryQueue.
                  // Failure to move here is critical. Let's transition to Exiting.
                  if (runner != null) runner.TransitionToState(runner.GetStateSO(CustomerState.Exiting));
                  return;
             }

             Debug.Log($"{gameObject.name}: NpcQueueHandler received MoveToQueueSpot signal for spot {newSpotIndex} in {queueType} queue.");

             int tempPreviousSpotIndex = AssignedQueueSpotIndex; // Use this handler's field

             // Update this handler's internal queue data
             AssignedQueueIndex_Internal(newSpotIndex); // Use internal helper method
             QueueType_Internal(queueType); // Use internal helper method
             // Update the Runner's target location field - this field is used by state SOs.
             // We keep this field on the Runner as it's used by ALL movement states.
             runner.CurrentTargetLocation = new BrowseLocation { browsePoint = nextSpotTransform, inventory = null };


             // Ensure the current state is one where a queue move is expected
             // Check runner's current state using its method
             CustomerState currentStateEnum = CustomerState.Inactive; // Default
             NpcStateSO currentStateSO = runner.GetCurrentState();
             if (currentStateSO != null)
             {
                 // Attempt to cast the HandledState enum to CustomerState
                 if (currentStateSO.HandledState is CustomerState customerEnum)
                 {
                     currentStateEnum = customerEnum;
                 }
                 // Add check for GeneralState.Idle or other states if queue moves are allowed from them?
                 // For now, stick to only allowing moves from Queue states.
                 // else if (currentStateSO.HandledState is GeneralState generalEnum) { /* Handle if needed */ }
             }


             if ((currentStateEnum == CustomerState.Queue && queueType == QueueType.Main) ||
                 (currentStateEnum == CustomerState.SecondaryQueue && queueType == QueueType.Secondary))
             {
                  // This flag is managed by the handler now
                  _isMovingToQueueSpot = true; // Use this handler's field
                  _previousQueueSpotIndex = tempPreviousSpotIndex; // Use this handler's field

                  if (manager != null && _previousQueueSpotIndex != -1) // Use this handler's manager reference and previous index
                  {
                       Debug.Log($"{gameObject.name}: Starting move to queue spot {newSpotIndex} from {_previousQueueSpotIndex} in {_currentQueueMoveType} queue. Signalling Manager to free previous spot {_previousQueueSpotIndex} immediately.", this); // Use this handler's fields

                       // Call the Manager method directly using this handler's manager reference
                       if (manager.FreePreviousQueueSpotOnArrival(_currentQueueMoveType, _previousQueueSpotIndex)) // Use this handler's fields
                       {
                            Debug.Log($"{gameObject.name}: Successfully signaled Manager to free previous spot {_currentQueueMoveType} queue spot {_previousQueueSpotIndex} upon starting move."); // Use this handler's fields
                            // Reset *some* move flags immediately as the Manager has been notified
                            _isMovingToQueueSpot = false; // Use this handler's field
                            _previousQueueSpotIndex = -1; // Use this handler's field
                            // --- REMOVED: DO NOT reset _currentQueueMoveType here! ---
                            // _currentQueueMoveType = QueueType.Main; // Use this handler's field (Reset to default) <-- REMOVE THIS LINE
                            // --- END REMOVED ---
                       }
                       else
                       {
                            Debug.LogWarning($"{gameObject.name}: Failed to signal Manager to free previous spot {_currentQueueMoveType} queue spot {_previousQueueSpotIndex} upon starting move.", this); // Use this handler's fields
                            // Decide error handling: Should we still move? Yes. Should we try signaling again on arrival? No, Manager expects signal on *start* of move.
                            // Just log the warning, the Manager state might be inconsistent.
                       }
                  }
                  else if (_previousQueueSpotIndex != -1) // Use this handler's previous index
                  {
                       // This shouldn't happen if the Runner came from a queue state and was correctly assigned.
                       Debug.LogWarning($"{gameObject.name}: Starting move to queue spot {newSpotIndex} from {_previousQueueSpotIndex}, but Manager is null! Cannot free previous spot.", this); // Use this handler's previous index
                  }

                  // Initiate the physical movement to the new spot using this handler's movementHandler
                  // Note: The Context helper method MoveToDestination is NOT available here.
                  // We must call the MovementHandler directly.
                   if (movementHandler != null) // Double check handler reference
                   {
                        bool success = movementHandler.SetDestination(nextSpotTransform.position); // Use this handler's movementHandler

                        // The Runner needs to know if movement was successfully initiated
                        // so its Update loop knows to start checking IsAtDestination again.
                        // The Context helper `MoveToDestination` does this by setting `runner._hasReachedCurrentDestination = false;`
                        // We need to replicate that logic here, accessing the runner directly.
                        if (success)
                        {
                           runner._hasReachedCurrentDestination = false; // Update runner's flag
                           runner.SetCurrentDestinationPosition(nextSpotTransform.position); // Update runner's last destination
                           Debug.Log($"{gameObject.name}: Successfully called MovementHandler.SetDestination for new queue spot {newSpotIndex} ({nextSpotTransform.position}). Runner flag _hasReachedCurrentDestination set to false.", this);
                            // Wait for OnReachedDestination to trigger arrival logic (handled by Runner's Update).
                        }
                        else
                        {
                             Debug.LogError($"NpcQueueHandler ({gameObject.name}): Failed to set destination for new queue spot {newSpotIndex}! Cannot move up. Transitioning to Exiting.", this);
                             // Movement failed - NPC is stuck. Exit flow.
                             // Reset move flags on failure using this handler's fields
                             _isMovingToQueueSpot = false;
                             _previousQueueSpotIndex = -1;
                             // Keep _currentQueueMoveType as is, transition to Exiting handles cleanup
                             // _currentQueueMoveType = QueueType.Main; // Reset to default <-- DO NOT RESET HERE

                             // Transition via the runner
                            if (runner != null) runner.TransitionToState(runner.GetStateSO(CustomerState.Exiting));
                        }
                   }
                   else
                   {
                       // movementHandler was null, logged error above.
                       // Transition via the runner
                        if (runner != null) runner.TransitionToState(runner.GetStateSO(CustomerState.Exiting));
                   }


             }
             else
             {
                  // Received a move command while not in the correct queue state. Log warning and ignore.
                  Debug.LogWarning($"NpcQueueHandler ({gameObject.name}): Received MoveToQueueSpot signal for {queueType} queue but not in a matching Queue state ({currentStateEnum})! Current State SO: {currentStateSO?.name ?? "NULL"}. Ignoring move command.", this);
                  AssignedQueueIndex_Internal(tempPreviousSpotIndex); // Use internal helper (Restore previous index if move was ignored)
                  // Also clear runner's target as this move was ignored.
                  runner.CurrentTargetLocation = null;
                  runner.SetCurrentDestinationPosition(null);
             }
        }

         // --- Internal Setters for clarity and potential logging/callbacks ---
         private void AssignedQueueIndex_Internal(int index)
         {
             // Optional: Add logging or validation here
             AssignedQueueSpotIndex = index;
         }

         private void QueueType_Internal(QueueType type)
         {
             // Optional: Add logging or validation here
             _currentQueueMoveType = type;
         }

        // --- End Moved Methods ---
    }
}

// --- END OF FILE NpcQueueHandler.cs ---