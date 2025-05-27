// --- START OF FILE NpcInterruptionHandler.cs ---
//yp
using UnityEngine;
using System.Collections.Generic; // Needed for Stack
using System; // Needed for System.Enum
using Game.NPC.States; // Needed for NpcStateSO
using Game.NPC; // Needed for GeneralState enum

namespace Game.NPC.Handlers // Placing handlers together
{
    /// <summary>
    /// Component responsible for managing NPC interruptions (Combat, Social, Emote, etc.)
    /// using a state stack to return to the previous behavior.
    /// Reduces the interruption handling burden on the Runner itself.
    /// </summary>
    [RequireComponent(typeof(NpcStateMachineRunner))]
    public class NpcInterruptionHandler : MonoBehaviour
    {
        // Reference to the state machine runner on this GameObject
        private NpcStateMachineRunner runner;

        // --- Interruption Data (Moved from NpcStateMachineRunner) ---
        private Stack<NpcStateSO> stateStack; // The state stack for managing interruptions
        // Making InteractorObject public internal for NpcStateContext access
        public GameObject InteractorObject { get; internal set; }
        // --- END Moved Data ---


        private void Awake()
        {
            runner = GetComponent<NpcStateMachineRunner>();
            if (runner == null)
            {
                Debug.LogError($"NpcInterruptionHandler on {gameObject.name}: NpcStateMachineRunner component not found! This handler requires it. Self-disabling.", this);
                enabled = false;
                return;
            }
            // Initialize the state stack here
            stateStack = new Stack<NpcStateSO>();

            Debug.Log($"{gameObject.name}: NpcInterruptionHandler Awake completed. Runner reference acquired, state stack initialized.", this);
        }

         /// <summary>
         /// Resets the interruption state, clearing the stack and interactor.
         /// Called by the Runner during its ResetRunnerTransientData.
         /// </summary>
         public void Reset()
         {
             stateStack.Clear();
             InteractorObject = null;
             Debug.Log($"{gameObject.name}: NpcInterruptionHandler reset.");
         }

         /// <summary>
         /// Attempts to interrupt the current state and transition to an interruption state.
         /// Called by NpcEventHandler when an interruption trigger event is received.
         /// </summary>
         /// <param name="interruptStateEnum">The enum key for the desired interruption state (e.g., GeneralState.Combat).</param>
         /// <param name="interactor">The GameObject that caused the interruption (e.g., player).</param>
         /// <returns>True if the interruption was successfully initiated, false otherwise (e.g., current state is not interruptible).</returns>
         public bool TryInterrupt(Enum interruptStateEnum, GameObject interactor)
         {
             if (runner == null) { Debug.LogError($"InterruptionHandler on {gameObject.name}: Runner is null during TryInterrupt!", this); return false; }
             NpcStateSO currentStateSO = runner.GetCurrentState();

             if (currentStateSO != null && currentStateSO.IsInterruptible)
             {
                 Debug.Log($"{runner.gameObject.name}: Current state '{currentStateSO.name}' is interruptible. Pushing to stack and attempting transition to {interruptStateEnum.GetType().Name}.{interruptStateEnum.ToString()}.", runner.gameObject);
                 stateStack.Push(currentStateSO);
                 InteractorObject = interactor; // Store the interactor on this handler

                 NpcStateSO interruptStateSO = runner.GetStateSO(interruptStateEnum);
                 if (interruptStateSO != null)
                 {
                     runner.TransitionToState(interruptStateSO); // Transition via the Runner
                     return true;
                 }
                 else
                 {
                      Debug.LogError($"InterruptionHandler ({runner.gameObject.name}): Interruption state SO not found for enum '{interruptStateEnum.GetType().Name}.{interruptStateEnum.ToString()}'! Cannot transition. State stack inconsistency likely. Clearing stack.", runner.gameObject);
                      stateStack.Clear(); // Clear stack on failure to prevent permanent block
                      InteractorObject = null; // Clear interactor
                      // Transition to a safe state - use Runner's GetStateSO for Idle/ReturningToPool
                      NpcStateSO fallbackState = runner.GetStateSO(GeneralState.Idle);
                      if (fallbackState == null) fallbackState = runner.GetStateSO(GeneralState.ReturningToPool);

                      if (fallbackState != null) runner.TransitionToState(fallbackState);
                      else Debug.LogError($"InterruptionHandler ({runner.gameObject.name}): Neither Idle nor ReturningToPool fallback states found after failed interruption state lookup! NPC is stuck.", this);

                     return false;
                 }
             }
             else
             {
                 Debug.Log($"{runner.gameObject.name}: Current state '{currentStateSO?.name ?? "NULL"}' is NOT interruptible. Ignoring interruption trigger.", runner.gameObject);
                 InteractorObject = null; // Clear any lingering interactor just in case
                 return false;
             }
         }

         /// <summary>
         /// Ends the current interruption state and returns to the previous state on the stack,
         /// with conditional logic for queue/register states.
         /// Called by NpcEventHandler when an interruption completion event is received.
         /// </summary>
         public void EndInterruption()
         {
              if (runner == null) { Debug.LogError($"InterruptionHandler on {gameObject.name}: Runner is null during EndInterruption!", this); return; }
              if (runner.Manager == null) { Debug.LogError($"InterruptionHandler on {gameObject.name}: Runner's Manager is null during EndInterruption! Cannot check queue/register status. Transitioning to fallback.", this);
                 InteractorObject = null; // Clear interactor
                 stateStack.Clear(); // Clear stack as we cannot reliably return
                 // Transition to a safe state
                  NpcStateSO fallbackState = runner.GetStateSO(GeneralState.Idle);
                  if (fallbackState == null) fallbackState = runner.GetStateSO(GeneralState.ReturningToPool);
                  if (fallbackState != null) runner.TransitionToState(fallbackState);
                  else Debug.LogError($"InterruptionHandler ({gameObject.name}): Neither Idle nor ReturningToPool fallback states found when Manager is null on EndInterruption!", this);
                 return; // Exit
             }


             InteractorObject = null; // Clear the interactor regardless

              if (stateStack.Count > 0)
              {
                  NpcStateSO poppedState = stateStack.Pop();
                   Debug.Log($"{runner.gameObject.name}: State stack not empty ({stateStack.Count} remaining). Popped state '{poppedState.name}' from stack.", runner.gameObject);

                  // --- Conditional Return Logic ---
                  // Check the HandledState enum of the popped state
                  Enum poppedStateEnum = poppedState.HandledState;

                  if (poppedStateEnum != null)
                  {
                       // Case 1: Interrupted while MovingToRegister
                       if (poppedStateEnum.Equals(CustomerState.MovingToRegister))
                       {
                            Debug.Log($"{runner.gameObject.name}: Interruption ended. Previous state was MovingToRegister. Checking register availability.", runner.gameObject);
                            if (runner.Manager.IsRegisterOccupied())
                            {
                                 Debug.Log($"{runner.gameObject.name}: Register is now occupied. Attempting to rejoin Main Queue.", runner.gameObject);
                                  Transform assignedSpot;
                                  int spotIndex;
                                 // Attempt to join the main queue
                                 if (runner.Manager.TryJoinQueue(runner, out assignedSpot, out spotIndex))
                                 {
                                      // Successfully joined the queue, transition to Queue state
                                      runner.AssignedQueueSpotIndex = spotIndex; // Set Runner's assigned index
                                      runner._currentQueueMoveType = QueueType.Main; // Set Runner's queue type
                                      Debug.Log($"{runner.gameObject.name}: Successfully rejoined queue at spot {spotIndex}. Transitioning to Queue.", runner.gameObject);
                                      runner.TransitionToState(runner.GetStateSO(CustomerState.Queue));
                                 }
                                 else
                                 {
                                      // Main queue is full, cannot rejoin. Give up and exit.
                                      Debug.LogWarning($"{runner.gameObject.name}: Main Queue is full, cannot rejoin after interruption from MovingToRegister. Transitioning to Exiting.", runner.gameObject);
                                      runner.TransitionToState(runner.GetStateSO(CustomerState.Exiting));
                                 }
                            }
                            else
                            {
                                 // Register is still free, return to MovingToRegister state
                                 Debug.Log($"{runner.gameObject.name}: Register is still free. Returning to MovingToRegister.", runner.gameObject);
                                 runner.TransitionToState(poppedState); // Return to the original state
                            }
                       }
                       // Case 2: Interrupted while in Main Queue
                       else if (poppedStateEnum.Equals(CustomerState.Queue))
                       {
                            Debug.Log($"{runner.gameObject.name}: Interruption ended. Previous state was Main Queue. Checking if Main Queue is full.", runner.gameObject);
                            if (runner.Manager.IsMainQueueFull())
                            {
                                 // Main queue is now full, cannot rejoin their spot. Give up and exit.
                                 Debug.LogWarning($"{runner.gameObject.name}: Main Queue is now full! Cannot rejoin after interruption from Queue. Transitioning to Exiting.", runner.gameObject);
                                 // Note: Their old spot was already freed by the interruption handler
                                 // They effectively lost their place.
                                 runner.TransitionToState(runner.GetStateSO(CustomerState.Exiting));
                            }
                            else
                            {
                                 // Main queue is NOT full. Attempt to rejoin the queue.
                                  // Note: TryJoinQueue will find the *first available spot*, which might be different from their old spot.
                                  Debug.Log($"{runner.gameObject.name}: Main Queue is not full. Attempting to rejoin Main Queue.", runner.gameObject);
                                  Transform assignedSpot;
                                  int spotIndex;
                                  if (runner.Manager.TryJoinQueue(runner, out assignedSpot, out spotIndex))
                                  {
                                       // Successfully rejoined the queue, transition to Queue state
                                       runner.AssignedQueueSpotIndex = spotIndex; // Set Runner's assigned index
                                       runner._currentQueueMoveType = QueueType.Main; // Set Runner's queue type
                                       Debug.Log($"{runner.gameObject.name}: Successfully rejoined queue at spot {spotIndex}. Transitioning to Queue.", runner.gameObject);
                                       runner.TransitionToState(runner.GetStateSO(CustomerState.Queue));
                                  }
                                  else
                                  {
                                       // Should theoretically not happen if IsMainQueueFull is false, but defensive.
                                       Debug.LogError($"{runner.gameObject.name}: Manager.TryJoinQueue failed unexpectedly when Main Queue reported not full! Transitioning to Exiting.", runner.gameObject);
                                       runner.TransitionToState(runner.GetStateSO(CustomerState.Exiting));
                                  }
                            }
                       }
                       // Case 3: Interrupted while in Secondary Queue
                       else if (poppedStateEnum.Equals(CustomerState.SecondaryQueue))
                       {
                           Debug.Log($"{runner.gameObject.name}: Interruption ended. Previous state was Secondary Queue. Checking if Secondary Queue is full.", runner.gameObject);
                            if (runner.Manager.IsSecondaryQueueFull()) // Use the new method
                           {
                                // Secondary queue is now full, cannot rejoin their spot. Give up and exit.
                                Debug.LogWarning($"{runner.gameObject.name}: Secondary Queue is now full! Cannot rejoin after interruption from Secondary Queue. Transitioning to Exiting.", runner.gameObject);
                                runner.TransitionToState(runner.GetStateSO(CustomerState.Exiting));
                           }
                           else
                           {
                                // Secondary queue is NOT full. Attempt to rejoin.
                                Debug.Log($"{runner.gameObject.name}: Secondary Queue is not full. Attempting to rejoin Secondary Queue.", runner.gameObject);
                                Transform assignedSpot;
                                int spotIndex;
                                if (runner.Manager.TryJoinSecondaryQueue(runner, out assignedSpot, out spotIndex))
                                {
                                     // Successfully rejoined the queue, transition to Secondary Queue state
                                     runner.AssignedQueueSpotIndex = spotIndex; // Set Runner's assigned index
                                     runner._currentQueueMoveType = QueueType.Secondary; // Set Runner's queue type
                                     Debug.Log($"{runner.gameObject.name}: Successfully rejoined secondary queue at spot {spotIndex}. Transitioning to Secondary Queue.", runner.gameObject);
                                     runner.TransitionToState(runner.GetStateSO(CustomerState.SecondaryQueue));
                                }
                                else
                                {
                                     // Should theoretically not happen if IsSecondaryQueueFull is false, but defensive.
                                     Debug.LogError($"{runner.gameObject.name}: Manager.TryJoinSecondaryQueue failed unexpectedly when Secondary Queue reported not full! Transitioning to Exiting.", runner.gameObject);
                                     runner.TransitionToState(runner.GetStateSO(CustomerState.Exiting));
                                }
                           }
                       }
                       // Case 4: Any other state - just return to it
                       else
                       {
                           Debug.Log($"{runner.gameObject.name}: Interruption ended. Returning to previous state '{poppedState.name}'.", runner.gameObject);
                           runner.TransitionToState(poppedState); // Return to the original state
                       }
                  }
                   else // poppedStateEnum is null
                   {
                        Debug.LogError($"InterruptionHandler ({runner.gameObject.name}): Popped state '{poppedState.name}' has a null HandledState! Cannot determine proper return logic. Transitioning to fallback.", runner.gameObject);
                         // Transition to a safe state - use Runner's GetStateSO for Idle/ReturningToPool
                         NpcStateSO fallbackState = runner.GetStateSO(GeneralState.Idle);
                         if (fallbackState == null) fallbackState = runner.GetStateSO(GeneralState.ReturningToPool);
                         if (fallbackState != null) runner.TransitionToState(fallbackState);
                         else Debug.LogError($"InterruptionHandler ({gameObject.name}): Neither Idle nor ReturningToPool fallback states found when popped state has null HandledState!", this);
                   }
                  // --- End Conditional Return Logic ---

              }
              else // State stack is empty - this means EndInterruption was called without a prior TryInterrupt
              {
                  Debug.LogWarning($"{runner.gameObject.name}: EndInterruption called but state stack is empty! Telling Runner to Transition to Idle/Fallback.", runner.gameObject);
                  // Transition to a safe state - use Runner's GetStateSO for Idle/ReturningToPool
                  NpcStateSO idleState = runner.GetStateSO(GeneralState.Idle);
                  if (idleState == null) idleState = runner.GetStateSO(GeneralState.ReturningToPool);

                  if (idleState != null) runner.TransitionToState(idleState);
                   else Debug.LogError($"InterruptionHandler ({gameObject.name}): Neither Idle nor ReturningToPool fallback states found when stack is empty on EndInterruption!", this);
              }
         }
    }
}

// --- END OF FILE NpcInterruptionHandler.cs ---