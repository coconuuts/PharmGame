using UnityEngine;
using Game.NPC;
using System.Collections; // Needed for IEnumerator
using CustomerManagement;

public class CustomerExitingLogic : BaseCustomerStateLogic
{
    public override CustomerState HandledState => CustomerState.Exiting;

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log($"{customerAI.gameObject.name}: Entering Exiting state. Signaling departure and finding exit point.");

         // --- Signal departure to the CashRegisterInteractable ---
         // Access the cached register via the public property on customerAI
        if (customerAI.PreviousState == CustomerState.WaitingAtRegister || customerAI.PreviousState == CustomerState.TransactionActive) // <-- ADD THIS CONDITION
        {
            if (customerAI.CachedCashRegister != null)
            {
                customerAI.CachedCashRegister.CustomerDeparted();
                customerAI.CachedCashRegister = null; // Clear the cached reference on AI once departure is signaled
            }
            else
            {
                Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): CachedCashRegister is null when entering Exiting state!", this);
            }
            
            
            // --- Signal CustomerManager that the register is now free ---
            if (customerAI.Manager != null)
            {
                customerAI.Manager.SignalRegisterFree(); // <-- ADD THIS LINE
            }
            else
            {
                Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): CustomerManager reference is null when signaling register free!", this);
            }
        }
        else
        {
            // Log when exiting from a non-register state
            Debug.Log($"{customerAI.gameObject.name}: Exiting from non-register state ({customerAI.PreviousState}). No need to signal register free.", this);
            // Ensure CachedCashRegister is null even if exiting from non-register state, just in case.
            customerAI.CachedCashRegister = null;
        }


        if (customerAI.NavMeshAgent != null && customerAI.NavMeshAgent.enabled)
        {
            // Get a random exit point from the CustomerManager via customerAI.Manager
            Transform exitTarget = customerAI.Manager?.GetRandomExitPoint();

            if (exitTarget != null)
            {
                customerAI.CurrentTargetLocation = new BrowseLocation { browsePoint = exitTarget, inventory = null }; // Store target location on AI
                Debug.Log($"CustomerAI ({customerAI.gameObject.name}): Setting exit destination to {exitTarget.position}.");
                customerAI.NavMeshAgent.SetDestination(exitTarget.position);
                customerAI.NavMeshAgent.isStopped = false; // Start moving
            }
            else
            {
                Debug.LogWarning($"CustomerAI ({customerAI.gameObject.name}): No exit points available for Exiting state! Returning to pool.", this);
                customerAI.SetState(CustomerState.ReturningToPool); // Cannot exit, return directly
            }
        }
        else
        {
            Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): NavMeshAgent not ready for Exiting state entry!", this);
            customerAI.SetState(CustomerState.ReturningToPool);
        }
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        // Check if the NavMeshAgent has reached the destination
        if (customerAI.NavMeshAgent != null && customerAI.NavMeshAgent.enabled && customerAI.HasReachedDestination()) // Use public property and helper
        {
            Debug.Log($"{customerAI.gameObject.name}: Reached exit destination. Transitioning to ReturningToPool.");
            customerAI.SetState(CustomerState.ReturningToPool); // Transition to returning state
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