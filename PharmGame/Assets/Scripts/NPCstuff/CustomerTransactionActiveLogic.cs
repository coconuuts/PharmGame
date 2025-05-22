using UnityEngine;
using Game.NPC;
using System.Collections;

public class CustomerTransactionActiveLogic : BaseCustomerStateLogic
{
    public override CustomerState HandledState => CustomerState.TransactionActive;

    public override void OnEnter()
    {
        base.OnEnter(); // Call base OnEnter (enables Agent)
        Debug.Log($"{customerAI.gameObject.name}: Entering TransactionActive state. Waiting for player to finish transaction.");

         // --- Use MovementHandler to stop movement ---
         // Check for the handler instead of the NavMeshAgent directly
         if (customerAI.MovementHandler != null)
         {
              customerAI.MovementHandler.StopMoving(); // StopMoving handles isStopped and ResetPath
              // REMOVED: customerAI.NavMeshAgent.isStopped = true;
              // REMOVED: customerAI.NavMeshAgent.ResetPath();
         }
         // -------------------------------------------
    }

    // OnUpdate can be empty
    // public override void OnUpdate() { base.OnUpdate(); }

    public override IEnumerator StateCoroutine()
    {
        // This state doesn't require a continuous coroutine, just waiting for external event
        yield break;
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log($"{customerAI.gameObject.name}: Exiting TransactionActive state.");
    }
}
