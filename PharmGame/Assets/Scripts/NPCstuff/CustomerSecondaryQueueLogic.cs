using UnityEngine;
using Game.NPC;
using System.Collections;
using CustomerManagement; // Needed for interactions with CustomerManager or BrowseLocation
using Game.Events;

// Inherit from BaseQueueLogic
public class CustomerSecondaryQueueLogic : BaseQueueLogic
{
    // Implement the abstract HandledState property from BaseCustomerStateLogic
    public override CustomerState HandledState => CustomerState.SecondaryQueue;

    // Implement the abstract QueueType property from BaseQueueLogic
    // This tells the BaseQueueLogic which queue this represents
    protected override QueueType QueueType => QueueType.Secondary;

    private float impatientTimer; // Tracks how long the customer has been waiting in this state
    private float impatientDuration; // The random duration they will wait before becoming impatient

    // Initialize is handled by the base class (receives customerAI reference)

    public override void OnEnter()
    {
        base.OnEnter(); // Call the base BaseQueueLogic OnEnter (enables Agent via MovementHandler)

        // --- Impatience Timer Start ---
        impatientDuration = Random.Range(10f, 15f);
        impatientTimer = 0f;
        Debug.Log($"{customerAI.gameObject.name}: Starting impatience timer for {impatientDuration:F2} seconds.", this);
        // ------------------------------

        // --- Call the AssignQueueSpot logic to set destination ---
        if (customerAI.CurrentTargetLocation.HasValue && customerAI.CurrentTargetLocation.Value.browsePoint != null && customerAI.AssignedQueueSpotIndex != -1)
        {
            Transform assignedSpotTransform = customerAI.CurrentTargetLocation.Value.browsePoint;
            int assignedSpotIndex = customerAI.AssignedQueueSpotIndex;

            AssignQueueSpot(assignedSpotTransform, assignedSpotIndex); // This method uses customerAI.MovementHandler internally
        }
        else
        {
            Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): Entering Secondary Queue state without a valid assigned queue spot! Exiting.", this);
            customerAI.SetState(CustomerState.ReturningToPool); // Cannot queue without a spot
        }

        // TODO: Consider starting a coroutine here for initial rotation towards the next spot or register (Optional)
        // The rotation logic on arrival is already in BaseQueueLogic.OnUpdate triggered by IsAtDestination.
    }

    // OnUpdate logic is now handled by BaseQueueLogic.OnUpdate
    // BaseQueueLogic.OnUpdate checks HasReachedDestination, stops, rotates, and calls OnReachedEndOfQueue
    public override void OnUpdate()
    {
        base.OnUpdate(); // BaseQueueLogic OnUpdate handles destination checking, stopping, rotation, and calling OnReachedEndOfQueue
        // --- Impatience Timer Update and Check ---
        impatientTimer += Time.deltaTime; // Increment the timer

        if (impatientTimer >= impatientDuration) // Check if timer has reached the duration
        {
            Debug.Log($"{customerAI.gameObject.name}: IMPATIENT in Secondary Queue state after {impatientTimer:F2} seconds. Publishing NpcImpatientEvent.", this); // Log timeout
            // --- Publish NpcImpatientEvent instead of setting state directly ---
            EventManager.Publish(new NpcImpatientEvent(customerAI.gameObject, CustomerState.SecondaryQueue)); // Use the event struct
            // -------------------------------------------------------------------
            return; // Exit the OnUpdate method early
        }
        // -------------------------------------------
    }

    // Implement the abstract StateCoroutine from BaseQueueLogic
    public override IEnumerator StateCoroutine()
    {
        // This coroutine runs while the customer is in the Secondary Queue state.
        Debug.Log($"{customerAI.gameObject.name}: Secondary Queue StateCoroutine started.");
        yield break; // Basic waiting, no continuous coroutine logic needed here for function
    }

    // Implement the abstract method from BaseQueueLogic
    protected override void OnReachedEndOfQueue()
    {
         // This method is called by BaseQueueLogic.OnUpdate when the customer reaches
         // their assigned spot.
         // In the Secondary Queue, reaching your spot means you just stop and wait
         // for the Manager to call ReleaseFromSecondaryQueue (on this component).
         Debug.Log($"{customerAI.gameObject.name}: Reached end of movement for Secondary Queue state (spot {myQueueSpotIndex}). Waiting for release signal.");
         // No specific action needed here other than stopping (handled by BaseQueueLogic.OnUpdate)
         // The state remains CustomerState.SecondaryQueue until ReleaseFromSecondaryQueue is called.
    }


    // OnExit logic is now mostly handled by BaseQueueLogic.OnExit and the specific signal here
    public override void OnExit()
    {
        base.OnExit(); // Call the base BaseQueueLogic OnExit
        Debug.Log($"{customerAI.gameObject.name}: Exiting Secondary Queue state from spot {myQueueSpotIndex}. Publishing QueueSpotFreedEvent."); // myQueueSpotIndex is in BaseQueueLogic
        impatientTimer = 0f; // <-- RESET TIMER ON EXIT

        // --- Signal CustomerManager that this spot is now free using an Event ---
        if (myQueueSpotIndex != -1) // No need to check customerAI.Manager here, EventManager is static
        {
             // Publish the event instead of calling the Manager directly
             EventManager.Publish(new QueueSpotFreedEvent(QueueType.Secondary, myQueueSpotIndex)); // Use the event struct
             // myQueueSpotIndex is typically reset to -1 by the base OnExit or reset logic.
         }
         else
         {
              Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): Queue spot index not set when exiting Secondary Queue state!", this);
         }
         // Ensure myQueueSpotIndex is reset on exit.
    }

    /// <summary>
    /// Called by CustomerManager to signal that this customer is released from the secondary queue
    /// and should proceed to enter the store.
    /// </summary>
    public void ReleaseFromSecondaryQueue()
    {
        Debug.Log($"{customerAI.gameObject.name}: Signalled by manager to leave secondary queue from spot {myQueueSpotIndex} and enter the store.");
        // Transition to the Entering state to enter the store
        // The OnExit for this state will handle signaling the spot free.
        customerAI.SetState(CustomerState.Entering); // Transition via the AI
    }
}