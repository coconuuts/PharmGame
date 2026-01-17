// --- START OF FILE NpcQueueHandler.cs (Modified for Cleanup) ---

using UnityEngine;
using CustomerManagement; // Needed for CustomerManager and QueueType
using Game.NPC; // Needed for NpcStateMachineRunner, CustomerState
using Game.NPC.Handlers; // Needed for NpcMovementHandler
using Game.NPC.States; // Needed for NpcStateSO and CustomerState.
using Game.Prescriptions; // Needed for PrescriptionManager // <-- NEW: Added using directive

namespace Game.NPC.Handlers // Placing handlers together
{
    /// <summary>
    /// Handles the queue-specific logic and data for an NPC.
    /// Manages the NPC's assigned queue spot and communicates queue movement signals.
    /// Now supports multiple queue types (Main, Secondary, Prescription).
    /// Includes method to clear queue assignment.
    /// </summary>
    [RequireComponent(typeof(NpcStateMachineRunner))]
    [RequireComponent(typeof(NpcMovementHandler))]
    public class NpcQueueHandler : MonoBehaviour
    {
        // --- References to Required Components (on this GameObject) ---
        private NpcStateMachineRunner runner;
        private NpcMovementHandler movementHandler;

        // --- External References (Provided by Runner Initialization) ---
        // We need the CustomerManager and PrescriptionManager to interact with global queue data.
        // These will be assigned via an Initialize method.
        private CustomerManagement.CustomerManager customerManager; // Renamed for clarity
        private Game.Prescriptions.PrescriptionManager prescriptionManager; // <-- NEW: Reference to PrescriptionManager


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

        // --- Initialization Method ---
        /// <summary>
        /// Initializes the Queue Handler with necessary external references.
        /// Called by the NpcStateMachineRunner during its Initialize or Activate process.
        /// </summary>
        public void Initialize(CustomerManagement.CustomerManager customerManager) // Accepts CustomerManager
        {
            Debug.Log($"[DEBUG {gameObject.name}] NpcQueueHandler Initialize called.", this);
            if (customerManager == null)
            {
                Debug.LogError($"NpcQueueHandler ({gameObject.name}): Initialized with a null CustomerManager reference! Standard queue logic will fail.", this);
            }
            this.customerManager = customerManager; // Assign CustomerManager

            // Get PrescriptionManager instance here as well
            prescriptionManager = Game.Prescriptions.PrescriptionManager.Instance;
            if (prescriptionManager == null)
            {
                 Debug.LogError($"NpcQueueHandler ({gameObject.name}): PrescriptionManager instance not found! Prescription queue logic will fail.", this);
            }

            Debug.Log($"NpcQueueHandler ({gameObject.name}): Initialized with Manager references.");
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
            // _currentQueueMoveType and AssignedQueueSpotIndex are reset by ClearQueueAssignment
            ClearQueueAssignment(); // Use the new method for reset
            // Do NOT reset 'customerManager' or 'prescriptionManager' references here.
            Debug.Log($"NpcQueueHandler ({gameObject.name}): Transient queue data reset.");
        }

        /// <summary>
        /// Clears the NPC's assigned queue spot index and type.
        /// Called when the NPC successfully leaves a queue state.
        /// </summary>
        public void ClearQueueAssignment() // <-- NEW METHOD Implementation
        {
             AssignedQueueSpotIndex = -1;
             _currentQueueMoveType = QueueType.Main; // Reset to a default/invalid type (Main as default)

             // Sync with TiData if applicable 
             if (runner != null && runner.IsTrueIdentityNpc && runner.TiData != null)
             {
                 runner.TiData.savedQueueIndex = -1;
             }

             Debug.Log($"NpcQueueHandler ({gameObject.name}): Queue assignment cleared.");
        }

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
                      // Clear queue assignment immediately upon leaving the queue state
                      ClearQueueAssignment(); // <-- Clear assignment here
                  }
                  else
                  {
                      Debug.LogError($"NpcQueueHandler ({gameObject.name}): Failed to get MovingToRegister state SO! Cannot transition. Transitioning to Exiting (fallback).", this);
                       // Fallback to Exiting if MovingToRegister is missing
                      NpcStateSO exitState = runner.GetStateSO(CustomerState.Exiting);
                       if(exitState != null) runner.TransitionToState(exitState);
                       else Debug.LogError($"NpcQueueHandler ({gameObject.name}): Neither MovingToRegister nor Exiting fallback states found!", this);
                       // Clear assignment on fallback as well
                       ClearQueueAssignment(); // <-- Clear assignment here
                  }
             }
             else
             {
                  Debug.LogError($"NpcQueueHandler ({gameObject.name}): Runner reference is null! Cannot transition state for GoToRegisterFromQueue.", this);
                  // No runner, no state machine. Cannot do anything.
             }
        }

        /// <summary>
        /// Signals this NPC to transition to the state for moving to the prescription claim spot.
        /// Called by the PrescriptionManager when this NPC reaches the front of the prescription queue.
        /// </summary>
        public void GoToPrescriptionClaimSpotFromQueue() // <-- NEW METHOD Implementation
        {
             Debug.Log($"{gameObject.name}: NpcQueueHandler received GoToPrescriptionClaimSpotFromQueue signal. Transitioning to PrescriptionEntering.");

             // Need to transition state via the Runner
             if (runner != null)
             {
                  // Use runner's method to get the state SO by Enum key
                  NpcStateSO nextState = runner.GetStateSO(CustomerState.PrescriptionEntering);
                  if (nextState != null)
                  {
                      runner.TransitionToState(nextState);
                      // Clear queue assignment immediately upon leaving the queue state
                      ClearQueueAssignment(); // <-- Clear assignment here
                  }
                  else
                  {
                      Debug.LogError($"NpcQueueHandler ({gameObject.name}): Failed to get PrescriptionEntering state SO! Cannot transition. Transitioning to Exiting (fallback).", this);
                       // Fallback to Exiting if PrescriptionEntering is missing
                      NpcStateSO exitState = runner.GetStateSO(CustomerState.Exiting);
                       if(exitState != null) runner.TransitionToState(exitState);
                       else Debug.LogError($"NpcQueueHandler ({gameObject.name}): Neither PrescriptionEntering nor Exiting fallback states found!", this);
                       // Clear assignment on fallback as well
                       ClearQueueAssignment(); // <-- Clear assignment here
                  }
             }
             else
             {
                  Debug.LogError($"NpcQueueHandler ({gameObject.name}): Runner reference is null! Cannot transition state for GoToPrescriptionClaimSpotFromQueue.", this);
                  // No runner, no state machine. Cannot do anything.
             }
        }


        /// <summary>
        /// Signals this NPC to move to a new spot in a queue line (used for moving up).
        /// Called by the CustomerManager or PrescriptionManager during the queue cascade.
        /// </summary>
        /// <param name="nextSpotTransform">The Transform of the spot to move to.</param>
        /// <param name="newSpotIndex">The index of the spot to move to.</param>
        /// <param name="queueType">The type of queue (Main, Secondary, or Prescription).</param>
        public void MoveToQueueSpot(Transform nextSpotTransform, int newSpotIndex, QueueType queueType)
        {
             // Check if required references are valid
             if (runner == null || movementHandler == null || (queueType != QueueType.Prescription && customerManager == null) || (queueType == QueueType.Prescription && prescriptionManager == null)) // Check manager based on queue type
             {
                  Debug.LogError($"NpcQueueHandler ({gameObject.name}): Cannot move to queue spot - missing Runner({runner != null}), MovementHandler({movementHandler != null}), or Manager for type {queueType}.", this);
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
             runner.CurrentTargetLocation = new BrowseLocation { browsePoint = nextSpotTransform, inventory = null }; // Still using BrowseLocation struct, but the transform is the key part


             // Ensure the current state is one where a queue move is expected
             // Check runner's current state using its method
             System.Enum currentStateEnum = runner.GetCurrentState()?.HandledState;

             bool isInCorrectQueueState = false;
             if (currentStateEnum != null)
             {
                 if (queueType == QueueType.Main && currentStateEnum.Equals(CustomerState.Queue)) isInCorrectQueueState = true;
                 else if (queueType == QueueType.Secondary && currentStateEnum.Equals(CustomerState.SecondaryQueue)) isInCorrectQueueState = true;
                 else if (queueType == QueueType.Prescription && currentStateEnum.Equals(CustomerState.PrescriptionQueue)) isInCorrectQueueState = true;
             }


             if (isInCorrectQueueState)
             {
                  // This flag is managed by the handler now
                  _isMovingToQueueSpot = true; // Use this handler's field
                  _previousQueueSpotIndex = tempPreviousSpotIndex; // Use this handler's field

                  // --- Signal Manager to Free Previous Spot --- // <-- MODIFIED LOGIC
                  // Call the correct manager based on the queue type
                  bool signaledManager = false;
                  if (_previousQueueSpotIndex != -1) // Only signal if there was a previous spot
                  {
                       if (queueType == QueueType.Main || queueType == QueueType.Secondary)
                       {
                            if (customerManager != null)
                            {
                                 Debug.Log($"{gameObject.name}: Starting move to queue spot {newSpotIndex} from {_previousQueueSpotIndex} in {queueType} queue. Signalling CustomerManager to free previous spot {_previousQueueSpotIndex} immediately.", this);
                                 signaledManager = customerManager.FreePreviousQueueSpotOnArrival(queueType, _previousQueueSpotIndex);
                            } else { Debug.LogWarning($"{gameObject.name}: CustomerManager is null! Cannot signal freeing previous spot {queueType} queue spot {_previousQueueSpotIndex}.", this); }
                       }
                       else if (queueType == QueueType.Prescription)
                       {
                            if (prescriptionManager != null)
                            {
                                 Debug.Log($"{gameObject.name}: Starting move to queue spot {newSpotIndex} from {_previousQueueSpotIndex} in {queueType} queue. Signalling PrescriptionManager to free previous spot {_previousQueueSpotIndex} immediately.", this);
                                 signaledManager = prescriptionManager.FreePreviousPrescriptionQueueSpotOnArrival(queueType, _previousQueueSpotIndex); // Use PrescriptionManager
                            } else { Debug.LogWarning($"{gameObject.name}: PrescriptionManager is null! Cannot signal freeing previous spot {queueType} queue spot {_previousQueueSpotIndex}.", this); }
                       }

                       if (signaledManager)
                       {
                            Debug.Log($"{gameObject.name}: Successfully signaled Manager to free previous spot {queueType} queue spot {_previousQueueSpotIndex} upon starting move.");
                            // Reset *some* move flags immediately as the Manager has been notified
                            _isMovingToQueueSpot = false; // Use this handler's field
                            _previousQueueSpotIndex = -1; // Use this handler's field
                            // _currentQueueMoveType is NOT reset here, it holds the type of the queue they are *in*.
                       }
                       else
                       {
                            Debug.LogWarning($"{gameObject.name}: Failed to signal Manager to free previous spot {queueType} queue spot {_previousQueueSpotIndex} upon starting move. Manager state might be inconsistent.", this);
                       }
                  }
                  // --- END MODIFIED LOGIC ---


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
                             // _currentQueueMoveType is NOT reset here.
                             // Clear assignment as the move failed and they are leaving the queue flow.
                             ClearQueueAssignment(); // <-- Clear assignment here

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
                  Debug.LogWarning($"NpcQueueHandler ({gameObject.name}): Received MoveToQueueSpot signal for {queueType} queue but not in a matching Queue state ({currentStateEnum})! Current State SO: {runner.GetCurrentState()?.name ?? "NULL"}. Ignoring move command.", this);
                  // Do NOT restore previous index/type here, the assignment was already received and stored.
                  // The NPC is just in the wrong state to *act* on the move command.
                  // The Manager thinks the spot is assigned and the NPC is moving.
                  // This is an inconsistency that needs state logic to prevent, or robust error handling.
                  // For now, just log and ignore the move command. The NPC will likely get stuck.
                  // Clearing the Runner's target position might be appropriate here?
                  // runner.CurrentTargetLocation = null; // Maybe clear? Let's leave it for now.
                  // runner.SetCurrentDestinationPosition(null);
             }
        }

        /// <summary>
        /// Called externally (e.g., by TiNpcManager) to manually set the NPC's assigned queue spot and type,
        /// and update the Runner's target. Used for NPCs activating directly into a queue spot.
        /// Does NOT trigger movement or notify the manager.
        /// </summary>
        /// <param name="spotTransform">The Transform of the spot to assign.</param>
        /// <param name="spotIndex">The index of the spot to assign.</param>
        /// <param name="queueType">The type of queue (Main or Secondary).</param>
        public void SetupQueueSpot(Transform spotTransform, int spotIndex, QueueType queueType)
        {
             if (runner == null || spotTransform == null)
             {
                  Debug.LogError($"NpcQueueHandler ({gameObject.name}): Cannot setup queue spot - missing Runner({runner != null}) or spotTransform is null.", this);
                  ClearQueueAssignment(); // Ensure clean state on error
                  runner.CurrentTargetLocation = null; // Ensure clean state on error
                  runner.SetCurrentDestinationPosition(null); // Ensure clean state on error
                  return;
             }

             Debug.Log($"{gameObject.name}: NpcQueueHandler setting up queue spot {spotIndex} in {queueType} queue.");

             // Update this handler's internal queue data
             AssignedQueueIndex_Internal(spotIndex); // Use internal helper method
             QueueType_Internal(queueType); // Use internal helper method

             // Update the Runner's target location and destination position fields.
             // This is the target the NPC needs to reach when it becomes active and enters the state.
             runner.CurrentTargetLocation = new BrowseLocation { browsePoint = spotTransform, inventory = null }; // Still using BrowseLocation struct
             runner.SetCurrentDestinationPosition(spotTransform.position); // Set the destination position

             // Do NOT set runner._hasReachedCurrentDestination = false here.
             // The state that receives this setup (e.g., QueueStateSO.OnEnter)
             // is responsible for initiating the movement and setting that flag.
        }

        /// <summary>
        /// Called by a manager (like CustomerManager or PrescriptionManager) to assign this NPC to a queue spot.
        /// Updates the internal queue data fields.
        /// </summary>
        /// <param name="index">The index of the assigned spot.</param>
        /// <param name="type">The type of queue (Main, Secondary, or Prescription).</param>
        public void ReceiveQueueAssignment(int index, QueueType type)
        {
            // Basic validation
            if (index < -1) // Allow -1 for 'no spot assigned'
            {
                Debug.LogError($"NpcQueueHandler ({gameObject.name}): Received invalid queue assignment index {index}! Ignoring.", this);
                return;
            }
            AssignedQueueIndex_Internal(index);
            QueueType_Internal(type);
            Debug.Log($"NpcQueueHandler ({gameObject.name}): Received queue assignment: Index {index}, Type {type}.");
        }

         // --- Internal Setters for clarity and potential logging/callbacks ---
        private void AssignedQueueIndex_Internal(int index)
        {
            // Optional: Add logging or validation here
            AssignedQueueSpotIndex = index;

            // Sync with TiData if applicable 
            if (runner != null && runner.IsTrueIdentityNpc && runner.TiData != null)
            {
                runner.TiData.savedQueueIndex = index;
            }
        }

         private void QueueType_Internal(QueueType type)
         {
             _currentQueueMoveType = type;

             // Sync with TiData if applicable 
             if (runner != null && runner.IsTrueIdentityNpc && runner.TiData != null)
             {
                 runner.TiData.savedQueueType = type;
             }
         }

        // --- End Moved Methods ---
    }
}

// --- END OF FILE NpcQueueHandler.cs (Modified for Cleanup) ---