using UnityEngine;
using Game.NPC;
using System.Collections;
using CustomerManagement;

public class CustomerInitializingLogic : BaseCustomerStateLogic
{
    public override CustomerState HandledState => CustomerState.Initializing;

    public override IEnumerator StateCoroutine()
    {
        Debug.Log($"{customerAI.gameObject.name}: InitializeRoutine started in CustomerInitializingLogic.");
        // MovementHandler.Agent is enabled in BaseCustomerStateLogic.OnEnter now.
        // Agent was warped and stopped in CustomerAI.Initialize.
        // No specific agent calls needed here.

        yield return null; // Wait one frame
        Debug.Log($"{customerAI.gameObject.name}: InitializeRoutine finished processing wait.");


        // --- Check if the main queue is full and decide next state ---
        // This logic remains as it determines the *next* state transition based on Manager state.
        if (customerAI.Manager != null && customerAI.Manager.IsMainQueueFull())
        {
            Debug.Log($"{customerAI.gameObject.name}: Main queue is full. Attempting to join secondary queue.");
            if (customerAI.Manager.TryJoinSecondaryQueue(customerAI, out Transform assignedSpot, out int spotIndex))
            {
                Debug.Log($"{customerAI.gameObject.name}: Successfully joined secondary queue at spot {spotIndex}.");

                // Store the assigned spot and index on the AI
                customerAI.CurrentTargetLocation = new BrowseLocation { browsePoint = assignedSpot, inventory = null }; // Store the assigned secondary queue spot as target
                customerAI.AssignedQueueSpotIndex = spotIndex; // Store the assigned spot index

                customerAI.SetState(CustomerState.SecondaryQueue); // Transition to the Secondary Queue state
            }
            else
            {
                Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): Main queue and secondary queue are full! Exiting to pool (fallback).");
                 // Publish event instead of direct SetState for this fallback
                 // EventManager.Publish(new NpcReturningToPoolEvent(customerAI.gameObject)); // Or SetState(ReturningToPool) if that's the event publisher
                 customerAI.SetState(CustomerState.ReturningToPool); // SetState(ReturningToPool) publishes the event now
            }
        }
        else
        {
            // Main queue is not full, proceed normally to enter the store
            Debug.Log($"{customerAI.gameObject.name}: Main queue is not full. Transitioning to Entering.");
            customerAI.SetState(CustomerState.Entering); // Transition to the Entering state
        }

        Debug.Log($"{customerAI.gameObject.name}: InitializeRoutine finished.");
    }
}
