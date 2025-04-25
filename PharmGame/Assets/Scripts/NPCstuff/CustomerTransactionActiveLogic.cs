using UnityEngine;
using Game.NPC;
using System.Collections;

public class CustomerTransactionActiveLogic : BaseCustomerStateLogic
{
    public override CustomerState HandledState => CustomerState.TransactionActive;

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log($"{customerAI.gameObject.name}: Entering TransactionActive state. Waiting for player to finish transaction.");
         // Optional: Ensure agent is stopped
         if (customerAI.NavMeshAgent != null)
         {
              customerAI.NavMeshAgent.isStopped = true;
              customerAI.NavMeshAgent.ResetPath(); // Clear path if any
         }
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
