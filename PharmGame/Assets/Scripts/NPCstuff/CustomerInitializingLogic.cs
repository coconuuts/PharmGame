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
        if (customerAI.NavMeshAgent != null && customerAI.NavMeshAgent.isActiveAndEnabled)
        {
            customerAI.NavMeshAgent.isStopped = true; // Ensure agent is stopped
            customerAI.NavMeshAgent.ResetPath();
        }
        yield return null; // Wait one frame
        Debug.Log($"{customerAI.gameObject.name}: InitializeRoutine finished.");

        // --- Check if the main queue is full and decide next state ---
        if (customerAI.Manager != null && customerAI.Manager.IsMainQueueFull())
        {
            Debug.Log($"{customerAI.gameObject.name}: Main queue is full. Attempting to join secondary queue.");
            if (customerAI.Manager.TryJoinSecondaryQueue(customerAI, out Transform assignedSpot, out int spotIndex))
            {
                Debug.Log($"{customerAI.gameObject.name}: Successfully joined secondary queue at spot {spotIndex}.");

                // Store the assigned spot and index on the AI
                customerAI.CurrentTargetLocation = new BrowseLocation { browsePoint = assignedSpot, inventory = null }; // Store the assigned secondary queue spot as target
                customerAI.AssignedQueueSpotIndex = spotIndex; // Store the assigned spot index

                // --- REMOVED THE CALL TO AssignQueueSpot HERE ---
                // The OnEnter method of CustomerSecondaryQueueLogic will now call AssignQueueSpot.

                customerAI.SetState(CustomerState.SecondaryQueue); // Transition to the Secondary Queue state
            }
            else
            {
                Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): Main queue and secondary queue are full! Exiting to pool (fallback).");
                customerAI.SetState(CustomerState.ReturningToPool);
            }
        }
        else
        {
            // Main queue is not full, proceed normally to enter the store
            Debug.Log($"{customerAI.gameObject.name}: Main queue is not full. Transitioning to Entering.");
            customerAI.SetState(CustomerState.Entering); // Transition to the Entering state
        }
        // ---------------------------------------------------------------

        // Removed: The original SetState(CustomerState.Entering) line

        Debug.Log($"{customerAI.gameObject.name}: InitializeRoutine finished.");
    }
}
