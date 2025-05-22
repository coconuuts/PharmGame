using UnityEngine;
using Game.NPC;
using System.Collections; // Needed for IEnumerator
using CustomerManagement; // Needed for BrowseLocation

public class CustomerEnteringLogic : BaseCustomerStateLogic
{
    public override CustomerState HandledState => CustomerState.Entering;
    
    public override void OnEnter()
    {
        base.OnEnter(); // Call base OnEnter (enables Agent)
        Debug.Log($"{customerAI.gameObject.name}: Entering Entering state. Finding first browse location.");

        // Use MovementHandler via customerAI
        if (customerAI.MovementHandler != null)
        {
            // Get a random browse location from the CustomerManager (accessed via customerAI.Manager)
            BrowseLocation? firstBrowseLocation = customerAI.Manager?.GetRandomBrowseLocation();

            if (firstBrowseLocation.HasValue && firstBrowseLocation.Value.browsePoint != null)
            {
                customerAI.CurrentTargetLocation = firstBrowseLocation; // Store target location on AI

                // --- Use MovementHandler to set destination and start moving ---
                customerAI.MovementHandler.SetDestination(firstBrowseLocation.Value.browsePoint.position);
                // MovementHandler.SetDestination also ensures agent is not stopped
                // ----------------------------------------------------------------

                Debug.Log($"{customerAI.gameObject.name}: Set destination to first browse point: {firstBrowseLocation.Value.browsePoint.position}.");
            }
            else
            {
                Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): No Browse locations available for Entering state! Exiting empty-handed.");
                customerAI.SetState(CustomerState.Exiting); // Exit if no destination
            }
        }
        else // Fallback if movement handler is missing (shouldn't happen with RequireComponent)
        {
             Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): MovementHandler is null for Entering state entry!", this);
             customerAI.SetState(CustomerState.ReturningToPool);
        }
    }

    public override void OnUpdate()
    {
        base.OnUpdate(); // Calls Base OnUpdate (empty)

        // --- Use MovementHandler to check if destination is reached ---
        if (customerAI.MovementHandler != null && customerAI.MovementHandler.IsAtDestination())
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