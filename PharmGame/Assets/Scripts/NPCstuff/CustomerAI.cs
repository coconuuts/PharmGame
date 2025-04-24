using UnityEngine;
using UnityEngine.AI; // Required for NavMeshAgent
using System.Collections.Generic;
using Utils.Pooling;
using CustomerManagement; // Required for CustomerManager
using System.Collections; // Required for Coroutines
using Random = UnityEngine.Random; // Specify UnityEngine.Random

namespace Game.NPC // Your NPC namespace
{
    /// <summary>
    /// Defines the possible states for a customer NPC.
    /// </summary>
    public enum CustomerState
    {
        Inactive,          // In the pool, not active in the scene
        Initializing,      // Brief state after activation, before entering store
        Entering,          // Moving from spawn point into the store
        Browse,          // Moving between/simulating Browse at shelves
        MovingToRegister,  // Moving towards the cash register
        WaitingAtRegister, // Waiting for the player at the register
        TransactionActive, // Player is scanning items (minigame)
        Exiting,           // Moving towards an exit point
        ReturningToPool    // Signalling completion and waiting to be returned
    }

    /// <summary>
    /// Manages the behavior and movement of a customer NPC.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))] // Ensure the GameObject has a NavMeshAgent
    public class CustomerAI : MonoBehaviour
    {
        // --- Components ---
        private NavMeshAgent navMeshAgent;

        // --- State ---
        private CustomerState currentState = CustomerState.Inactive;
        // Store the time when a state that involves waiting was entered
        private float stateEntryTime;
        private Coroutine stateCoroutine; // Coroutine for managing timed states (like Browse) or rotation

        // --- References (Provided by CustomerManager during Initialize) ---
        private CustomerManager customerManager; // Reference to the manager to signal completion


        // --- Internal Data (Managed by AI script) ---
        private Transform currentMovementTarget; // The current point the NPC is moving towards (used for position AND rotation)
        private const float DestinationReachedThreshold = 0.5f; // How close the NPC needs to be to the target
        private float BrowseTime = 0f; // How long to browse at the current spot
        [SerializeField] private float rotationSpeed = 5f; // Speed of smooth rotation


        // Add fields for items to buy, etc. in Phase 2


        private void Awake()
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            if (navMeshAgent == null)
            {
                Debug.LogError($"CustomerAI ({gameObject.name}): NavMeshAgent component not found!", this);
                enabled = false; // Disable script if no NavMeshAgent
            }

             Debug.Log($"CustomerAI ({gameObject.name}): Awake completed.");
        }

        /// <summary>
        /// Initializes the NPC when it's retrieved from the pool.
        /// Should be called by the CustomerManager AFTER the GameObject is active.
        /// </summary>
        /// <param name="manager">The CustomerManager instance managing this NPC.</param>
        /// <param name="startPosition">The initial position for the NPC.</param>
        public void Initialize(CustomerManager manager, Vector3 startPosition)
        {
            this.customerManager = manager;
            ResetNPC();

            if (navMeshAgent != null)
            {
                 navMeshAgent.enabled = true; // Ensure agent is enabled before warping

                 if (navMeshAgent.Warp(startPosition))
                 {
                      Debug.Log($"CustomerAI ({gameObject.name}): Warped to {startPosition}.");
                      navMeshAgent.ResetPath();
                      navMeshAgent.isStopped = true; // Start stopped
                 }
                 else
                 {
                      Debug.LogWarning($"CustomerAI ({gameObject.name}): Failed to Warp to {startPosition}. Is the position on the NavMesh?", this);
                      SetState(CustomerState.ReturningToPool); // Return to pool
                      return;
                 }
            }
             else
             {
                 Debug.LogError($"CustomerAI ({gameObject.name}): NavMeshAgent is null during Initialize!", this);
                 SetState(CustomerState.ReturningToPool);
                 return;
             }

            SetState(CustomerState.Initializing);

            Debug.Log($"CustomerAI ({gameObject.name}): Initialized at {startPosition}.");
        }

         /// <summary>
         /// Resets the NPC's state and data when initialized from the pool.
         /// Cleans up properties from prior use.
         /// </summary>
         private void ResetNPC()
         {
             currentState = CustomerState.Inactive;
             stateEntryTime = 0f;
             StopStateCoroutine(); // Stop any running state coroutine

             if (navMeshAgent != null)
             {
                  if (navMeshAgent.isActiveAndEnabled)
                  {
                    navMeshAgent.ResetPath();
                    navMeshAgent.isStopped = true;
                  }
                   navMeshAgent.enabled = false;
             }
             currentMovementTarget = null;

             // Clear itemsToBuy list and other temporary data here in Phase 2+
         }

        /// <summary>
        /// Stops any currently running state coroutine.
        /// </summary>
        private void StopStateCoroutine()
        {
            if (stateCoroutine != null)
            {
                StopCoroutine(stateCoroutine);
                stateCoroutine = null;
            }
        }


        private void Update()
        {
             // Check if NavMeshAgent is valid and enabled for states that require it
             bool agentActiveAndEnabled = navMeshAgent != null && navMeshAgent.isActiveAndEnabled;

            switch (currentState)
            {
                case CustomerState.Initializing:
                    HandleInitializingState(agentActiveAndEnabled);
                    break;
                case CustomerState.Entering:
                    if (agentActiveAndEnabled) HandleEnteringState();
                    break;
                case CustomerState.Browse:
                    // State logic is handled by the coroutine started in SetState
                    // if (agentActiveAndEnabled) HandleBrowseState(); // Handled by Coroutine
                    break;
                case CustomerState.MovingToRegister:
                     if (agentActiveAndEnabled) HandleMovingToRegisterState();
                    break;
                case CustomerState.WaitingAtRegister:
                    // State logic is handled by the coroutine started in SetState
                    // HandleWaitingAtRegisterState(); // Handled by Coroutine
                    break;
                case CustomerState.TransactionActive:
                    HandleTransactionActiveState(); // No movement
                    break;
                case CustomerState.Exiting:
                     if (agentActiveAndEnabled) HandleExitingState();
                    break;
                case CustomerState.ReturningToPool:
                    HandleReturningToPoolState(); // State transition handles cleanup
                    break;
                case CustomerState.Inactive:
                    // Do nothing, NavMeshAgent should be disabled
                    if (navMeshAgent != null && navMeshAgent.enabled) navMeshAgent.enabled = false;
                    break;
            }
        }

        /// <summary>
        /// Sets the NPC's current state and performs any state entry logic.
        /// Manages starting/stopping state coroutines.
        /// </summary>
        /// <param name="newState">The state to transition to.</param>
        protected void SetState(CustomerState newState)
        {
            Debug.Log($"CustomerAI ({gameObject.name}): Transitioning from {currentState} to {newState}");

            // --- State Exit Logic (Before changing state) ---
            StopStateCoroutine(); // Stop any coroutine from the previous state
            // -----------------------------------------------

            currentState = newState; // Set the new state
            stateEntryTime = Time.time; // Record time of entry into the new state

            // --- State Entry Logic ---
            // Ensure agent is valid and enabled for states requiring movement
            bool agentActiveAndEnabled = navMeshAgent != null && navMeshAgent.isActiveAndEnabled;

            if (agentActiveAndEnabled)
            {
                 navMeshAgent.isStopped = false; // Assume movement by default in moving states
                 // navMeshAgent.enabled = true; // Should already be true if agentActiveAndEnabled is true
            }


            switch (currentState)
            {
                 case CustomerState.Initializing:
                     // Agent is enabled and warped in Initialize.
                     // Waiting one frame for the agent to be fully ready.
                      if (agentActiveAndEnabled)
                      {
                           navMeshAgent.isStopped = true; // Start stopped
                           stateCoroutine = StartCoroutine(InitializeRoutine()); // Start init routine
                      }
                      else // Should not happen if Initialize succeeded, but defensive
                      {
                           Debug.LogError($"CustomerAI ({gameObject.name}): NavMeshAgent not ready for Initializing state entry!", this);
                           SetState(CustomerState.ReturningToPool);
                      }
                     break;

                 case CustomerState.Entering:
                      if (agentActiveAndEnabled)
                      {
                           currentMovementTarget = customerManager?.GetRandomBrowsePoint();
                           if (currentMovementTarget != null)
                           {
                                navMeshAgent.SetDestination(currentMovementTarget.position);
                                navMeshAgent.isStopped = false; // Start moving
                           }
                           else
                           {
                                Debug.LogWarning($"CustomerAI ({gameObject.name}): No Browse points available for Entering state!");
                                SetState(CustomerState.Exiting);
                           }
                      }
                      else // Should not happen if coming from Initializing
                      {
                          Debug.LogError($"CustomerAI ({gameObject.name}): NavMeshAgent not ready for Entering state entry!", this);
                          SetState(CustomerState.ReturningToPool);
                      }
                     break;

                 case CustomerState.Browse:
                       // Reached a Browse point, stop and simulate Browse and rotation
                       if (agentActiveAndEnabled && currentMovementTarget != null) // Check if agent is ready and target exists
                       {
                           navMeshAgent.isStopped = true; // Stop movement
                           // Start the Browse and rotation routine
                           stateCoroutine = StartCoroutine(BrowseRoutine(currentMovementTarget.rotation));
                       }
                       else // Should have reached from Entering or another Browse point
                       {
                           Debug.LogError($"CustomerAI ({gameObject.name}): Agent not ready or no target for Browse state entry!", this);
                           SetState(CustomerState.Exiting); // Cannot browse, exit
                       }
                     break;

                 case CustomerState.MovingToRegister:
                      if (agentActiveAndEnabled)
                      {
                          currentMovementTarget = customerManager?.GetRegisterPoint();
                           if (currentMovementTarget != null)
                          {
                                navMeshAgent.SetDestination(currentMovementTarget.position);
                                navMeshAgent.isStopped = false; // Start moving
                          }
                          else
                          {
                                Debug.LogWarning($"CustomerAI ({gameObject.name}): No register point assigned!");
                                SetState(CustomerState.Exiting);
                          }
                      }
                       else // Should not happen if coming from Browse
                      {
                           Debug.LogError($"CustomerAI ({gameObject.name}): Agent not ready for MovingToRegister state entry!", this);
                           SetState(CustomerState.Exiting);
                      }
                     break;

                 case CustomerState.WaitingAtRegister:
                      // Reached the register point, stop and rotate towards it
                       if (agentActiveAndEnabled && currentMovementTarget != null) // currentMovementTarget should be the register point
                      {
                           navMeshAgent.isStopped = true; // Stop movement
                           navMeshAgent.ResetPath(); // Clear path
                           // Start the waiting and rotation routine
                           stateCoroutine = StartCoroutine(WaitingAtRegisterRoutine(currentMovementTarget.rotation));
                      }
                       else // Should have reached from MovingToRegister
                       {
                           Debug.LogError($"CustomerAI ({gameObject.name}): Agent not ready or no target for WaitingAtRegister state entry!", this);
                           SetState(CustomerState.Exiting); // Cannot wait properly, exit
                       }
                     break;

                 case CustomerState.TransactionActive:
                       // Entered this state externally via StartTransaction()
                       // Agent should already be stopped from WaitingAtRegister
                       if (agentActiveAndEnabled) // Still ensure agent is ready if possible
                       {
                           navMeshAgent.isStopped = true; // Keep stopped
                           navMeshAgent.ResetPath(); // Clear path
                       }
                       // No coroutine needed unless there's timed logic during scanning
                      break;

                 case CustomerState.Exiting:
                      if (agentActiveAndEnabled)
                      {
                          currentMovementTarget = customerManager?.GetRandomExitPoint();
                           if (currentMovementTarget != null)
                           {
                                navMeshAgent.SetDestination(currentMovementTarget.position);
                                navMeshAgent.isStopped = false; // Start moving
                           }
                           else
                           {
                                Debug.LogWarning($"CustomerAI ({gameObject.name}): No exit points available for Exiting state!");
                                SetState(CustomerState.ReturningToPool);
                           }
                      }
                       else // Should not happen if coming from TransactionActive or Browse (fallback)
                      {
                           Debug.LogError($"CustomerAI ({gameObject.name}): Agent not ready for Exiting state entry!", this);
                           SetState(CustomerState.ReturningToPool);
                      }
                     break;

                 case CustomerState.ReturningToPool:
                      if (navMeshAgent != null) // Clean up NavMeshAgent before returning
                      {
                           navMeshAgent.ResetPath();
                           navMeshAgent.isStopped = true;
                           navMeshAgent.enabled = false; // Disable agent before returning to pool
                      }
                      // Signal manager happens in HandleReturningToPoolState
                      break;

                 case CustomerState.Inactive:
                      if (navMeshAgent != null) // Ensure agent is disabled
                      {
                           navMeshAgent.enabled = false;
                           navMeshAgent.isStopped = true;
                           navMeshAgent.ResetPath();
                      }
                      currentMovementTarget = null; // Clear target
                      break;
            }
        }

        // --- State Handling Coroutines (Replace Update() Handlers) ---

        private IEnumerator InitializeRoutine()
        {
             // Wait one frame for the NavMeshAgent to be fully ready after activation/Warp.
             yield return null;

             // Now transition to the first actual movement state.
             SetState(CustomerState.Entering);
        }

        private IEnumerator BrowseRoutine(Quaternion targetRotation)
        {
            // --- Rotate towards the target point's facing direction ---
            yield return StartCoroutine(RotateTowardsTargetRoutine(targetRotation));
            // ---------------------------------------------------------

            // Now that rotation is complete, simulate Browse time
            BrowseTime = Random.Range(3f, 8f); // Re-randomize Browse duration
            Debug.Log($"CustomerAI ({gameObject.name}): Browse for {BrowseTime} seconds.");
            yield return new WaitForSeconds(BrowseTime);

            // --- Shopping Logic Goes Here (Phase 2) ---
            bool shoppingComplete = SimulateShopping(); // New method for shopping logic
            // ----------------------------------------

            if (shoppingComplete)
            {
                SetState(CustomerState.MovingToRegister); // Done shopping, go to register
            }
            else
            {
                // Not done shopping, move to another Browse point (optional)
                Debug.Log($"CustomerAI ({gameObject.name}): Shopping not complete, moving to next Browse point.");
                // Get next Browse point and set destination
                currentMovementTarget = customerManager?.GetRandomBrowsePoint(); // Move to another random point
                 if (currentMovementTarget != null)
                 {
                     SetState(CustomerState.MovingToRegister); // Transition to moving state to set new destination
                 }
                 else
                 {
                     Debug.LogWarning($"CustomerAI ({gameObject.name}): No Browse points available for next Browse state!");
                     SetState(CustomerState.Exiting); // Exit if nowhere to browse
                 }
            }
        }

         private IEnumerator WaitingAtRegisterRoutine(Quaternion targetRotation)
         {
             // --- Rotate towards the target point's facing direction ---
             yield return StartCoroutine(RotateTowardsTargetRoutine(targetRotation));
             // ---------------------------------------------------------

             // NPC waits here until the player triggers the register interaction.
             // The CashRegister script will call StartTransaction().
             // This coroutine will just yield until the state changes externally.
             while(currentState == CustomerState.WaitingAtRegister)
             {
                 yield return null; // Wait until state is changed externally
             }

             // Once state changes (e.g., to TransactionActive or Exiting), routine ends.
             Debug.Log($"CustomerAI ({gameObject.name}): No longer waiting at register.");
         }


         // --- NEW: Smooth Rotation Coroutine ---
         private IEnumerator RotateTowardsTargetRoutine(Quaternion targetRotation)
         {
              Debug.Log($"CustomerAI ({gameObject.name}): Starting rotation towards {targetRotation.eulerAngles}.");
              // Ensure the agent is stopped before rotating
              if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled)
              {
                   navMeshAgent.isStopped = true;
              }

              Quaternion startRotation = transform.rotation;
              float timeElapsed = 0f;
              float duration = Quaternion.Angle(startRotation, targetRotation) / (rotationSpeed * 360f); // Estimate duration based on angle and speed

              // Prevent division by zero if already facing the right way
              if (duration <= 0f) duration = 0.1f; // Small duration even if angle is tiny

              while (timeElapsed < duration)
              {
                  transform.rotation = Quaternion.Slerp(startRotation, targetRotation, timeElapsed / duration);
                  timeElapsed += Time.deltaTime;
                  yield return null; // Wait until next frame
              }

              transform.rotation = targetRotation; // Snap to the final rotation to ensure accuracy
              Debug.Log($"CustomerAI ({gameObject.name}): Rotation complete.");
         }
         // --------------------------------------


         // --- Placeholder for shopping logic (Phase 2) ---
         private bool SimulateShopping()
         {
              Debug.Log($"CustomerAI ({gameObject.name}): Simulating shopping at {currentMovementTarget?.position}. (Actual shopping logic in Phase 2)");
              // Placeholder: In Phase 2, this will contain logic to select items,
              // check inventory, remove items, and populate itemsToBuy list.
              // Return true when shopping is considered "done" (enough items, or can't find more).
              // For now, just return true after one browse for testing flow.
              return true; // Assume shopping is complete after the first browse cycle for now
         }
         // ---------------------------------------------------


        private void HandleMovingToRegisterState()
        {
             if (HasReachedDestination())
             {
                 SetState(CustomerState.WaitingAtRegister); // Reached register point
             }
        }

        private void HandleTransactionActiveState()
        {
             // NPC is passive during scanning. No logic here.
             // Waiting for external call to OnTransactionCompleted().
        }

        private void HandleExitingState()
        {
             if (HasReachedDestination())
             {
                 SetState(CustomerState.ReturningToPool); // Reached exit point
             }
        }

        private void HandleReturningToPoolState()
        {
             if (customerManager != null)
             {
                 customerManager.ReturnCustomerToPool(this.gameObject);
             }
             else
             {
                 Debug.LogError($"CustomerAI ({gameObject.name}): CustomerManager reference is null! Cannot return to pool. Destroying instead.", this);
                 Destroy(this.gameObject);
             }
        }

        /// <summary>
        /// Helper to check if the NavMeshAgent has reached its current destination.
        /// Accounts for path pending, remaining distance, and stopping.
        /// </summary>
        private bool HasReachedDestination()
        {
            if (navMeshAgent == null || !navMeshAgent.enabled || navMeshAgent.pathPending)
            {
                return false;
            }

            // Check if the agent is close enough to the destination.
            // Use a tolerance relative to stoppingDistance.
            bool isCloseEnough = navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance + DestinationReachedThreshold;

            // Check if the agent has a path AND is close enough.
            // This covers the normal case where the agent is actively moving towards and reaches the target.
            if (navMeshAgent.hasPath && isCloseEnough)
            {
                 // Additionally, check if the agent's velocity is very low.
                 if (navMeshAgent.velocity.sqrMagnitude < 0.1f * 0.1f)
                 {
                      return true; // Destination reached (close and stopped)
                 }
            }

            // Special case: Sometimes when an agent reaches its destination, navMeshAgent.hasPath becomes false,
            // but the agent has clearly stopped.
             if (!navMeshAgent.hasPath && navMeshAgent.velocity.sqrMagnitude == 0f)
             {
                 return true; // Destination reached (stopped without a path)
             }

            return false;
        }


         // --- Public methods for external systems to call (Phase 3) ---

         /// <summary>
         /// Called by the CashRegister to initiate the transaction minigame.
         /// </summary>
         // Add parameters for items to buy in Phase 3
         public void StartTransaction(/* List<(ItemDetails details, int quantity)> itemsToScan */)
         {
              SetState(CustomerState.TransactionActive);
              // Store the items to be scanned (Phase 3)
              Debug.Log($"CustomerAI ({gameObject.name}): Transaction started.");
         }

         /// <summary>
         /// Called by the CashRegister/Minigame system when the transaction is completed.
         /// </summary>
         /// <param name="paymentReceived">The amount of money the player received.</param>
         public void OnTransactionCompleted(float paymentReceived)
         {
              Debug.Log($"CustomerAI ({gameObject.name}): Transaction completed. Player received {paymentReceived} money.");
              // Maybe play a happy animation/sound
              SetState(CustomerState.Exiting); // Move to exit
         }

         // --- Optional: OnDrawGizmos for debugging paths ---
         // private void OnDrawGizmos() { ... }
            private void HandleInitializingState(bool agentActiveAndEnabled)
            {
                // Wait one frame for the NavMeshAgent to be fully ready after activation/Warp.
                // This is done via the coroutine now, so this method might be simplified.
                // The state transition happens in the coroutine.
                // We can keep this method empty as the logic is in InitializeRoutine.
                // Debug.Log($"CustomerAI ({gameObject.name}): Handling Initializing state.");
            }


            // Ensure this method is present and correctly named
            private void HandleEnteringState()
            {
                // Move towards the initial Browse point.
                // Check if destination is reached:
                if (HasReachedDestination())
                {
                    // Note: The destination was set in SetState(CustomerState.Entering)
                    SetState(CustomerState.Browse); // Finished entering, start Browse
                }
            }
    }
}