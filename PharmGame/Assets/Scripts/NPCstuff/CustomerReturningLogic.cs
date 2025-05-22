using UnityEngine;
using Game.NPC;
using System.Collections;
using Game.Events;

public class CustomerReturningLogic : BaseCustomerStateLogic
{
    public override CustomerState HandledState => CustomerState.ReturningToPool;

    public override void OnEnter()
    {
        base.OnEnter(); // Call base OnEnter (enables Agent initially, but we immediately disable it here)
        Debug.Log($"{customerAI.gameObject.name}: Entering ReturningToPool state. Signaling manager.");

        // --- Use MovementHandler.Agent to disable NavMeshAgent ---
        // Check for the handler and agent instead of the NavMeshAgent property directly
        if (customerAI.MovementHandler?.Agent != null && customerAI.MovementHandler.Agent.enabled)
        {
             // Agent should be stopped and path cleared by BaseCustomerStateLogic.OnExit *before* this state's OnEnter
             // However, as this state's OnEnter happens *instead* of BaseCustomerStateLogic.OnExit
             // when SetState(ReturningToPool) is called directly from, e.g., Initialization failure,
             // it's safer to explicitly stop and reset here before disabling.
             customerAI.MovementHandler.Agent.ResetPath(); // Clear any current path
             customerAI.MovementHandler.Agent.isStopped = true; // Stop movement
             customerAI.MovementHandler.Agent.enabled = false; // Disable the agent
        }
        // ---------------------------------------------------------

        // This event publishing remains correct from Substep 3
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
