using UnityEngine;
using Game.NPC;
using System.Collections;
using CustomerManagement; // Needed for interactions with CustomerManager or BrowseLocation

// Inherit from BaseQueueLogic
public class CustomerQueueLogic : BaseQueueLogic
{
    // Implement the abstract HandledState property from BaseCustomerStateLogic
    // This should have been here already, just ensure it has 'override'
    public override CustomerState HandledState => CustomerState.Queue;

    // Implement the abstract QueueType property from BaseQueueLogic
    // This tells the BaseQueueLogic which queue this represents
    protected override QueueType QueueType => QueueType.Main;

    private float impatientTimer; // Tracks how long the customer has been waiting in this state
    private float impatientDuration; // The random duration they will wait before becoming impatient

    // Initialize is handled by the base class (receives customerAI reference)

    public override void OnEnter()
    {
        base.OnEnter(); // Call the base BaseQueueLogic OnEnter
        // --- Impatience Timer Start ---
        impatientDuration = Random.Range(10f, 15f); // Set a random duration
        impatientTimer = 0f; // Reset the timer
        Debug.Log($"{customerAI.gameObject.name}: Starting impatience timer for {impatientDuration:F2} seconds.", this); // Log timer start
        // ------------------------------


        // --- Call the AssignQueueSpot logic to set destination ---
        // The assigned spot and index were stored on the AI just before the state transition.
        // Call the method from BaseQueueLogic to set the destination and internal index.
        if (customerAI.CurrentTargetLocation.HasValue && customerAI.CurrentTargetLocation.Value.browsePoint != null && customerAI.AssignedQueueSpotIndex != -1)
        {
            Transform assignedSpotTransform = customerAI.CurrentTargetLocation.Value.browsePoint;
            int assignedSpotIndex = customerAI.AssignedQueueSpotIndex;

            // Call the AssignQueueSpot logic inherited from BaseQueueLogic
            // This method handles setting myQueueSpotIndex and NavMeshAgent destination
            AssignQueueSpot(assignedSpotTransform, assignedSpotIndex); // <-- ENSURE THIS CALL IS PRESENT AND UNCOMMENTED
        }
        else
        {
            Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): Entering Main Queue state without a valid assigned queue spot! Exiting.", this);
            customerAI.SetState(CustomerState.Exiting); // Cannot queue without a spot
        }
        // ----------------------------------------------------------

        // Ensure agent is enabled (AssignQueueSpot also handles isStopped=false)
        if (customerAI.NavMeshAgent != null && !customerAI.NavMeshAgent.enabled)
        {
            customerAI.NavMeshAgent.enabled = true;
        }

        // TODO: Consider starting a coroutine here for initial rotation towards the next spot or register (Optional)
        // The rotation logic on arrival is already in BaseQueueLogic.OnUpdate triggered by HasReachedDestination.
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
            Debug.Log($"{customerAI.gameObject.name}: IMPATIENT in Main Queue state after {impatientTimer:F2} seconds. Exiting.", this); // Log timeout
            customerAI.SetState(CustomerState.Exiting); // Transition to the Exiting state
            return; // Exit the OnUpdate method early
        }
        // -------------------------------------------
        // (e.g., looking around, animation state checks)
    }

    // Implement the abstract StateCoroutine from BaseQueueLogic
    public override IEnumerator StateCoroutine()
    {
        // This coroutine runs while the customer is in the Main Queue state.
        // It could handle waiting animations, looking around, etc.
        // The movement and arrival detection is handled by BaseQueueLogic.OnUpdate.
        // The signal to move to the register is handled by the GoToRegisterFromQueue method (on CustomerAI).
        Debug.Log($"{customerAI.gameObject.name}: Main Queue StateCoroutine started.");
        yield break; // Basic waiting, no continuous coroutine logic needed here for function
    }

    // Implement the abstract method from BaseQueueLogic
    protected override void OnReachedEndOfQueue()
    {
         // This method is called by BaseQueueLogic.OnUpdate when the customer reaches
         // their assigned spot (which is the end of their current movement).
         // In the Main Queue, reaching your spot means you just stop and wait
         // for the Manager to call GoToRegisterFromQueue (on customerAI).
         Debug.Log($"{customerAI.gameObject.name}: Reached end of movement for Main Queue state (spot {myQueueSpotIndex}). Waiting for signal to go to register.");
         // No specific action needed here other than stopping (handled by BaseQueueLogic.OnUpdate)
         // The state remains CustomerState.Queue until customerAI.GoToRegisterFromQueue is called.
    }


    // OnExit logic is now mostly handled by BaseQueueLogic.OnExit and the specific signal here
    public override void OnExit()
    {
        base.OnExit(); // Call the base BaseQueueLogic OnExit
        Debug.Log($"{customerAI.gameObject.name}: Exiting Main Queue state from spot {myQueueSpotIndex}."); // myQueueSpotIndex is in BaseQueueLogic
        impatientTimer = 0f; // <-- RESET TIMER ON EXIT

        // Signal CustomerManager that this spot is now free using the correct QueueType
        // myQueueSpotIndex is the *last* spot this customer was assigned.
        if (customerAI.Manager != null && myQueueSpotIndex != -1)
        {
             customerAI.Manager.SignalQueueSpotFree(QueueType.Main, myQueueSpotIndex); // <-- Use QueueType.Main
             // myQueueSpotIndex is typically reset to -1 by the base OnExit or reset logic.
         }
         else
         {
              Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): Manager or queue spot index not set when exiting Main Queue state!", this);
         }
         // Ensure myQueueSpotIndex is reset on exit. BaseQueueLogic.OnExit might handle this.
         // If not, add: myQueueSpotIndex = -1;
    }

    // The GoToRegisterFromQueue method is on CustomerAI, not here.
    // The AssignQueueSpot method is now in BaseQueueLogic.
    // The MoveToNextQueueSpot method is now in BaseQueueLogic.
}