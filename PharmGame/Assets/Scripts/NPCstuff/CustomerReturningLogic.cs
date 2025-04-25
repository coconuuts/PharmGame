using UnityEngine;
using Game.NPC;
using System.Collections;

public class CustomerReturningLogic : BaseCustomerStateLogic
{
    public override CustomerState HandledState => CustomerState.ReturningToPool;

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log($"{customerAI.gameObject.name}: Entering ReturningToPool state. Signaling manager.");

        // Disable NavMeshAgent
        if (customerAI.NavMeshAgent != null && customerAI.NavMeshAgent.enabled)
        {
            customerAI.NavMeshAgent.ResetPath(); // Clear any current path
            customerAI.NavMeshAgent.isStopped = true; // Stop movement
            customerAI.NavMeshAgent.enabled = false; // Disable the agent
        }

        // Signal the CustomerManager to return this GameObject to the pool
        if (customerAI.Manager != null) // Use the public property
        {
            customerAI.Manager.ReturnCustomerToPool(customerAI.gameObject); // Pass the AI's GameObject
        }
        else
        {
            Debug.LogError($"CustomerAI ({customerAI.gameObject.name}): CustomerManager reference is null! Cannot return to pool. Destroying instead.", this);
            // Fallback: Destroy the GameObject if manager is missing
            Destroy(customerAI.gameObject);
        }

        // Note: The ReturnCustomerToPool method likely deactivates the GameObject,
        // so OnExit and subsequent Update/Coroutines won't typically run.
    }

    // OnUpdate can be empty
    // public override void OnUpdate() { base.OnUpdate(); }

    public override IEnumerator StateCoroutine()
    {
         // This state doesn't require a coroutine
         yield break;
    }

    // OnExit is typically not needed as the GameObject is deactivated/destroyed
    // public override void OnExit() { base.OnExit(); }
}
