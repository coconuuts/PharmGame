using UnityEngine;
using Game.NPC;
using System.Collections;

public class CustomerInitializingLogic : BaseCustomerStateLogic
{
    public override CustomerState HandledState => CustomerState.Initializing;

    public override IEnumerator StateCoroutine()
    {
        Debug.Log($"{customerAI.gameObject.name}: InitializeRoutine started in CustomerInitializingLogic.");
        if (customerAI.NavMeshAgent != null && customerAI.NavMeshAgent.isActiveAndEnabled)
        {
            customerAI.NavMeshAgent.isStopped = true; // Ensure agent is stopped
            customerAI.NavMeshAgent.ResetPath();
        }
        yield return null; // Wait one frame
        Debug.Log($"{customerAI.gameObject.name}: InitializeRoutine finished. Transitioning to Entering.");
        customerAI.SetState(CustomerState.Entering); // Call SetState on the main AI script
    }
}
