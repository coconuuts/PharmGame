using UnityEngine;
using Game.NPC;
using System.Collections;
using Game.Events;

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

        EventManager.Publish(new NpcReturningToPoolEvent(customerAI.gameObject));

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
