using UnityEngine;
using Game.NPC;
using System.Collections; // Needed for IEnumerator
using CustomerManagement;
using Game.Events;

public class CustomerExitingLogic : BaseCustomerStateLogic
{
    public override CustomerState HandledState => CustomerState.Exiting;

    public override void OnEnter()
    {
        base.OnEnter(); // Call base OnEnter (enables Agent)
        Debug.Log($"{customerAI.gameObject.name}: Entering Exiting state. Signaling departure and finding exit point.");

         // --- Signal departure to the CashRegisterInteractable ---
         // Access the cached register via the public property on customerAI
        if (customerAI.PreviousState == CustomerState.WaitingAtRegister || customerAI.PreviousState == CustomerState.TransactionActive)
        {
            if (customerAI.CachedCashRegister != null)
            {
                customerAI.CachedCashRegister.CustomerDeparted();
                customerAI.CachedCashRegister = null;
            }
            else
            {
                Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): CachedCashRegister is null when entering Exiting state!", this);
            }
        }
        else
        {
            Debug.Log($"{customerAI.gameObject.name}: Exiting from non-register state ({customerAI.PreviousState}). No need to signal register free.", this);
            customerAI.CachedCashRegister = null;
        }


        // --- Use MovementHandler to set destination ---
        // Check for the handler instead of the NavMeshAgent directly
        if (customerAI.MovementHandler != null)
        {
            // Get a random exit point from the CustomerManager via customerAI.Manager
            Transform exitTarget = customerAI.Manager?.GetRandomExitPoint();

            if (exitTarget != null)
            {
                customerAI.CurrentTargetLocation = new BrowseLocation { browsePoint = exitTarget, inventory = null }; // Store target location on AI
                Debug.Log($"CustomerAI ({customerAI.gameObject.name}): Setting exit destination to {exitTarget.position} via MovementHandler.");
                customerAI.MovementHandler.SetDestination(exitTarget.position); // Use handler method
                // REMOVED: customerAI.NavMeshAgent.isStopped = false; // Handled by SetDestination
            }
            else
            {
                Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): No exit points available for Exiting state! Publishing NpcReturningToPoolEvent.", this);
                EventManager.Publish(new NpcReturningToPoolEvent(customerAI.gameObject)); // Correct event publishing
            }
        }
        else // Fallback if movement handler is missing
        {
             Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): MovementHandler not ready for Exiting state entry! Publishing NpcReturningToPoolEvent.", this);
             EventManager.Publish(new NpcReturningToPoolEvent(customerAI.gameObject)); // Correct event publishing
        }
        // ----------------------------------------------
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        // --- Use MovementHandler to check if destination is reached ---
        // Check for the handler instead of the NavMeshAgent directly, but use the AI's helper
        if (customerAI.MovementHandler != null && customerAI.HasReachedDestination()) // customerAI.HasReachedDestination() now uses MovementHandler.IsAtDestination()
        {
            Debug.Log($"{customerAI.gameObject.name}: Reached exit destination. Transitioning to ReturningToPool.");
            customerAI.SetState(CustomerState.ReturningToPool); // Transition to returning state
        }
        // --------------------------------------------------------------
    }

    public override IEnumerator StateCoroutine()
    {
        // This state's logic is handled in OnEnter and OnUpdate
        yield break;
    }

    // OnExit can be empty or have simple logging
    // public override void OnExit() { base.OnExit(); }
}