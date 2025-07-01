// --- START OF FILE BasicCashierWaitingForCustomerSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicNpcStateSO

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    /// <summary>
    /// Basic state for a Cashier TI NPC simulating waiting at the cash spot for a customer when inactive.
    /// This state relies on the base timeout mechanism to eventually transition if left inactive for too long.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicCashierWaitingForCustomer", menuName = "NPC/Basic States/Basic Cashier Waiting For Customer", order = 5)] // Order according to customer flow
    public class BasicCashierWaitingForCustomerSO : BasicNpcStateSO
    {
        // Implement the HandledBasicState property
        public override System.Enum HandledBasicState => BasicState.BasicCashierWaitingForCustomer;

        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            // Base OnEnter handles logging, setting ShouldUseTimeout (to true),
            // initializing the timer using the overridden Min/MaxInactiveTimeout,
            // and clearing the target position.
            base.OnEnter(data, manager);

            Debug.Log($"SIM {data.Id}: BasicCashierWaitingForCustomer OnEnter. Will remain frozen until timeout ({data.simulatedStateTimer:F2}s initial).");

            // Ensure simulated target position is null as they are waiting at a fixed spot
            data.simulatedTargetPosition = null;
        }

        public override void SimulateTick(TiNpcData data, float deltaTime, BasicNpcStateManager manager)
        {
            // This state is passive. The BasicNpcStateManager.SimulateTickForNpc
            // handles the timer decrement and the timeout transition check based on ShouldUseTimeout.
            // No custom logic is needed here.
        }

         public override void OnExit(TiNpcData data, BasicNpcStateManager manager)
         {
             base.OnExit(data, manager); // Call base OnExit (logs exit)

             // No specific cleanup needed for TiNpcData in this state's exit.
             // The base OnExit handles resetting the simulatedStateTimer if using timeout.
             Debug.Log($"SIM {data.Id}: BasicCashierWaitingForCustomer OnExit.");
         }
    }
}
// --- END OF FILE BasicCashierWaitingForCustomerSO.cs ---