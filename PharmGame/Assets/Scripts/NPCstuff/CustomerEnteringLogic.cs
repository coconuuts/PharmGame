using UnityEngine;
using Game.NPC;
using System.Collections; // Needed for IEnumerator
using CustomerManagement; // Needed for BrowseLocation

public class CustomerEnteringLogic : BaseCustomerStateLogic
{
    public override CustomerState HandledState => CustomerState.Entering;
    
    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log($"{customerAI.gameObject.name}: Entering Entering state. Finding first browse location.");

        if (customerAI.NavMeshAgent != null && customerAI.NavMeshAgent.enabled)
        {
            // Get a random browse location from the CustomerManager (accessed via customerAI.Manager)
            BrowseLocation? firstBrowseLocation = customerAI.Manager?.GetRandomBrowseLocation();

            if (firstBrowseLocation.HasValue && firstBrowseLocation.Value.browsePoint != null)
            {
                customerAI.CurrentTargetLocation = firstBrowseLocation; // Store target location on AI
                customerAI.NavMeshAgent.SetDestination(firstBrowseLocation.Value.browsePoint.position);
                customerAI.NavMeshAgent.isStopped = false; // Start moving
                Debug.Log($"{customerAI.gameObject.name}: Set destination to first browse point: {firstBrowseLocation.Value.browsePoint.position}.");
            }
            else
            {
                Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): No Browse locations available for Entering state! Exiting empty-handed.");
                customerAI.SetState(CustomerState.Exiting); // Exit if no destination
            }
        }
        else
        {
             Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): NavMeshAgent not ready for Entering state entry!", this);
             customerAI.SetState(CustomerState.ReturningToPool);
        }
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        // Check if the NavMeshAgent has reached the destination
        if (customerAI.NavMeshAgent != null && customerAI.NavMeshAgent.enabled && customerAI.HasReachedDestination()) // Use public property and helper
        {
            Debug.Log($"{customerAI.gameObject.name}: Reached initial browse destination. Transitioning to Browse.");
            customerAI.SetState(CustomerState.Browse); // Transition to the Browse state
        }
    }

    public override IEnumerator StateCoroutine()
    {
        // This state's logic is handled in OnEnter and OnUpdate
        yield break;
    }

    // OnExit can be empty or have simple logging
    // public override void OnExit() { base.OnExit(); }
}