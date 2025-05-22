// BaseQueueLogic.cs
using System.Collections;
using UnityEngine;
using Game.NPC; // Needed for CustomerState and QueueType enum
using CustomerManagement; // Needed for CustomerManager and BrowseLocation
using UnityEngine.AI; // Needed for NavMeshAgent
using Game.NPC.Handlers;
using Game.Events;

namespace Game.NPC // Your NPC namespace
{
    /// <summary>
    /// Base class for specific Customer AI queue state logic components (Main or Secondary).
    /// Handles common logic for moving to spots, waiting, rotation, and signaling spot freeing on exit.
    /// </summary>
    public abstract class BaseQueueLogic : BaseCustomerStateLogic // Inherit from BaseCustomerStateLogic
    {
        // Common variable to store the index of the queue spot this customer is currently assigned to
        protected int myQueueSpotIndex = -1;

        /// <summary>
        /// Defines the type of queue this logic component handles (Main or Secondary).
        /// Must be implemented by derived classes.
        /// </summary>
        protected abstract QueueType QueueType { get; } // Abstract property

        // Initialize is handled by the base class (receives customerAI reference)

        public override void OnEnter()
        {
            base.OnEnter(); // Call BaseCustomerStateLogic OnEnter
        }

        /// <summary>
        /// Called externally to assign this customer to an initial queue spot and tell them to move there.
        /// This happens before transitioning to the Queue or SecondaryQueue state.
        /// </summary>
        /// <param name="assignedSpotTransform">The transform of the assigned queue spot.</param>
        /// <param name="spotIndex">The index of the assigned queue spot.</param>
        protected virtual void AssignQueueSpot(Transform assignedSpotTransform, int spotIndex) // Made virtual
        {
             if (assignedSpotTransform == null)
             {
                  Debug.LogError($"{customerAI.gameObject.name}: Received null assigned spot transform for {QueueType} queue assignment!");
                  customerAI.SetState(CustomerState.Exiting); // Example fallback
                  return;
             }

             myQueueSpotIndex = spotIndex; // Store the index internally
             customerAI.AssignedQueueSpotIndex = spotIndex; // Also store index on the main AI component
             Debug.Log($"{customerAI.gameObject.name}: Assigned to {QueueType} queue spot {myQueueSpotIndex}. Setting destination to {assignedSpotTransform.position}.");

             // Set the destination on the AI's NavMeshAgent
             if (customerAI.MovementHandler != null)
             {
                  customerAI.MovementHandler.SetDestination(assignedSpotTransform.position);
                  // SetDestination ensures agent is not stopped
             }
             else
             {
                  Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): NavMeshAgent not ready to set destination for {QueueType} queue assignment!", this);
                  customerAI.SetState(CustomerState.Exiting); // Example fallback
             }
             // Note: The state transition to CustomerState.Queue/SecondaryQueue happens AFTER this is called.
        }


        /// <summary>
        /// Called by CustomerManager to tell this customer to move to a new queue spot within the queue.
        /// </summary>
        /// <param name="nextSpotTransform">The transform of the new queue spot.</param>
        /// <param name="newSpotIndex">The index of the new queue spot.</param>
        public virtual void MoveToNextQueueSpot(Transform nextSpotTransform, int newSpotIndex) // Made virtual
        {
            if (nextSpotTransform == null)
            {
                Debug.LogError($"{customerAI.gameObject.name}: Received null next spot transform for {QueueType} queue movement!");
                // Decide fallback: Stay put? Exit queue?
                return;
            }

            Debug.Log($"{customerAI.gameObject.name}: Signalled to move from {QueueType} spot {myQueueSpotIndex} to spot {newSpotIndex}.");
            myQueueSpotIndex = newSpotIndex; // Update assigned spot index internally
            customerAI.AssignedQueueSpotIndex = newSpotIndex; // Also update on main AI

            // Set the destination to the new spot
            if (customerAI.MovementHandler != null)
            {
                customerAI.MovementHandler.SetDestination(nextSpotTransform.position);
                 Debug.Log($"{customerAI.gameObject.name}: Set new {QueueType} queue destination to {nextSpotTransform.position} via MovementHandler.");
            }
            else
            {
                 Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): NavMeshAgent not ready to move to next {QueueType} queue spot!", this);
                 customerAI.SetState(CustomerState.Exiting); // Example fallback
            }
        }


        public override void OnUpdate()
        {
            base.OnUpdate(); // Call BaseCustomerStateLogic OnUpdate (empty)

            // --- Use MovementHandler to check if destination is reached ---
            if (customerAI.MovementHandler != null && customerAI.MovementHandler.Agent != null && customerAI.MovementHandler.IsAtDestination())
            {
                 // Reached the current assigned spot. Stop and wait.
                 // Check if the agent is currently moving (based on velocity) before stopping/rotating
                 // to avoid re-running the arrival logic every frame while stopped.
                 if (customerAI.MovementHandler.Agent.velocity.sqrMagnitude > 0.01f) // Simple velocity check
                 {
                    Debug.Log($"{customerAI.gameObject.name}: Reached {QueueType} queue spot {myQueueSpotIndex}. Stopping movement and starting rotation.");
                    // --- Use MovementHandler to stop movement ---
                    customerAI.MovementHandler.StopMoving();
                    // --------------------------------------------

                    // --- Use MovementHandler to Start Rotation Logic ---
                    // Get the Transform of the currently assigned queue spot
                    Transform currentQueueSpotTransform = null;
                    if (QueueType == QueueType.Main)
                    {
                        currentQueueSpotTransform = customerAI.Manager?.GetQueuePoint(myQueueSpotIndex);
                    }
                    else if (QueueType == QueueType.Secondary)
                    {
                        currentQueueSpotTransform = customerAI.Manager?.GetSecondaryQueuePoint(myQueueSpotIndex);
                    }

                    if (currentQueueSpotTransform != null)
                    {
                        // Set the target rotation directly to the rotation of the queue point Transform
                        Quaternion targetRotation = currentQueueSpotTransform.rotation;
                        Debug.Log($"CustomerAI ({customerAI.gameObject.name}): Starting rotation towards queue spot rotation {targetRotation.eulerAngles} via MovementHandler.");
                       // Start the rotation coroutine managed by the MovementHandler
                       customerAI.MovementHandler.StartRotatingTowards(targetRotation);
                    }
                    else
                    {
                        Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): Could not get {QueueType} queue spot Transform {myQueueSpotIndex} for rotation!", this);
                    }
                       // --- End Rotation Logic ---

                       // --- Signal Reached End of *This* Queue Spot ---
                       OnReachedEndOfQueue(); // <-- Call abstract method for derived logic
                       // ----------------------------------------------
                 }
                 // Else: Agent is already stopped at the destination, do nothing in Update.
            }
        }


        /// <summary>
        /// Called by BaseQueueLogic.OnUpdate when the customer reaches their assigned queue spot and stops.
        /// Derived classes must implement what happens next (e.g., wait for signal).
        /// </summary>
        protected abstract void OnReachedEndOfQueue();


        public override abstract IEnumerator StateCoroutine();

        public override void OnExit()
        {
            base.OnExit(); // Call BaseCustomerStateLogic OnExit
            // Derived classes MUST signal their spot free on exit using SignalQueueSpotFree
            // customerAI.Manager.SignalQueueSpotFree(this.QueueType, myQueueSpotIndex);
             myQueueSpotIndex = -1; // Reset index on exit
        }

        // Need getter for secondary queue points in CustomerManager for rotation logic
        // and for SignalQueueSpotFree.
        // Add public Transform GetSecondaryQueuePoint(int index) to CustomerManager
    }
}