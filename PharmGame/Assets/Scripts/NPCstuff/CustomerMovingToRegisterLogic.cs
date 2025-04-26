using UnityEngine;
using Game.NPC;
using System.Collections; // Needed for IEnumerator
using CustomerManagement;

public class CustomerMovingToRegisterLogic : BaseCustomerStateLogic
{
    public override CustomerState HandledState => CustomerState.MovingToRegister;

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log($"{customerAI.gameObject.name}: Entering MovingToRegister state. Finding register point.");

        if (customerAI.NavMeshAgent != null && customerAI.NavMeshAgent.enabled)
        {
            // Get the register point from the CustomerManager (accessed via customerAI.Manager)
            Transform registerTarget = customerAI.Manager?.GetRegisterPoint();

            if (registerTarget != null)
            {
                // Store the register Transform point
                customerAI.CurrentTargetLocation = new BrowseLocation { browsePoint = registerTarget, inventory = null }; // Store target location on AI
                customerAI.NavMeshAgent.SetDestination(registerTarget.position);
                customerAI.NavMeshAgent.isStopped = false; // Start moving
                Debug.Log($"CustomerAI ({customerAI.gameObject.name}): Set destination to register point: {registerTarget.position}.");

                if (customerAI.Manager != null)
                {
                    customerAI.Manager.SignalCustomerAtRegister(customerAI); // <-- ADD THIS LINE
                }
                else
                {
                    Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): CustomerManager reference is null when signalling customer at register!", this);
                }
            }
            else
            {
                Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): No register point assigned! Exiting.", this);
                customerAI.SetState(CustomerState.Exiting); // Exit if no register
            }
        }
        else
        {
            Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): NavMeshAgent not ready for MovingToRegister state entry!", this);
            customerAI.SetState(CustomerState.ReturningToPool);
        }
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        // Check if the NavMeshAgent has reached the destination
        if (customerAI.NavMeshAgent != null && customerAI.NavMeshAgent.enabled && customerAI.HasReachedDestination()) // Use public property and helper
        {
            Debug.Log($"{customerAI.gameObject.name}: Reached register destination. Transitioning to WaitingAtRegister.");
            customerAI.SetState(CustomerState.WaitingAtRegister); // Transition to waiting state
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