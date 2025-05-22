using UnityEngine;
using Game.NPC;
using System.Collections; // Needed for IEnumerator
using CustomerManagement;

public class CustomerMovingToRegisterLogic : BaseCustomerStateLogic
{
    public override CustomerState HandledState => CustomerState.MovingToRegister;

    public override void OnEnter()
    {
        base.OnEnter(); // Call base OnEnter (enables Agent)
        Debug.Log($"{customerAI.gameObject.name}: Entering MovingToRegister state. Finding register point.");

        // Use MovementHandler via customerAI
        if (customerAI.MovementHandler != null)
        {
            // Get the register point from the CustomerManager (accessed via customerAI.Manager)
            Transform registerTarget = customerAI.Manager?.GetRegisterPoint();

            if (registerTarget != null)
            {
                // Store the register Transform point
                customerAI.CurrentTargetLocation = new BrowseLocation { browsePoint = registerTarget, inventory = null }; // Store target location on AI

                // --- Use MovementHandler to set destination and start moving ---
                customerAI.MovementHandler.SetDestination(registerTarget.position);
                 // -------------------------------------------------------------

                Debug.Log($"CustomerAI ({customerAI.gameObject.name}): Set destination to register point: {registerTarget.position}.");

                // --- Signal CustomerManager that this customer is occupying the register spot ---
                // This direct call remains for now as it updates manager's internal state about register occupancy
                if (customerAI.Manager != null)
                {
                    customerAI.Manager.SignalCustomerAtRegister(customerAI);
                }
                else
                {
                    Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): CustomerManager reference is null when signalling customer at register!", this);
                }
                // --------------------------------------------------------------------------------
            }
            else
            {
                Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): No register point assigned! Exiting.", this);
                customerAI.SetState(CustomerState.Exiting); // Exit if no register
            }
        }
         else // Fallback if movement handler is missing
         {
              Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): MovementHandler is null for MovingToRegister state entry!", this);
              customerAI.SetState(CustomerState.ReturningToPool);
         }
    }

    public override void OnUpdate()
    {
        base.OnUpdate(); // Calls Base OnUpdate (empty)

        // --- Use MovementHandler to check if destination is reached ---
        if (customerAI.MovementHandler != null && customerAI.MovementHandler.IsAtDestination())
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