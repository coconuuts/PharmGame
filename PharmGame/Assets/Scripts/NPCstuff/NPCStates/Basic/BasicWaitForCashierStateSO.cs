// --- START OF FILE BasicWaitForCashierStateSO.cs --- 

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicNpcStateSO

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    [CreateAssetMenu(fileName = "BasicWaitForCashierState", menuName = "NPC/Basic States/Basic Wait For Cashier", order = 5)] // Order towards the end of customer states
    public class BasicWaitForCashierStateSO : BasicNpcStateSO
    {
        public override System.Enum HandledBasicState => BasicState.BasicWaitForCashier;

        // This state uses the standard timeout to force progression if inactive for too long.
        // Waiting in queue or at register might take longer, so let's override the timeout range.
        public override bool ShouldUseTimeout => true; // Override base property

        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            // Base OnEnter handles logging, setting ShouldUseTimeout, initializing the timer (using overridden fields), and clearing the target position.
            base.OnEnter(data, manager);

            // Additional setup specific to waiting simulation if needed (likely none for now)
            // The NPC's simulated position is the location where they became inactive while in a queue or moving to register.
             Debug.Log($"SIM {data.Id}: BasicWaitForCashierState OnEnter. Will remain frozen until timeout ({data.simulatedStateTimer:F2}s initial).");
        }

        public override void SimulateTick(TiNpcData data, float deltaTime, BasicNpcStateManager manager)
        {

        }

         public override void OnExit(TiNpcData data, BasicNpcStateManager manager)
         {
             // Base OnExit handles logging.
             base.OnExit(data, manager);

             // No specific cleanup needed for TiNpcData in this state's exit.
         }
    }
}
// --- END OF FILE BasicWaitForCashierStateSO.cs ---