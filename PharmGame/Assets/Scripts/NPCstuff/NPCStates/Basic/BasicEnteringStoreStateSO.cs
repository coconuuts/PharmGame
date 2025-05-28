// --- START OF FILE BasicEnteringStoreStateSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicNpcStateSO

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    [CreateAssetMenu(fileName = "BasicEnteringStoreState", menuName = "NPC/Basic States/Basic Entering Store", order = 3)] // Order according to customer flow
    public class BasicEnteringStoreStateSO : BasicNpcStateSO
    {
        public override System.Enum HandledBasicState => BasicState.BasicEnteringStore;

        // This state uses the standard timeout to force progression if inactive for too long.
        public override bool ShouldUseTimeout => true; // Override base property

        // Timeout range is inherited from the base class (minInactiveTimeout, maxInactiveTimeout)
        // You could override these fields here if you wanted a specific timeout for entering.

        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            // Base OnEnter handles logging, setting ShouldUseTimeout, initializing the timer, and clearing the target position.
            base.OnEnter(data, manager);

            // Additional setup specific to entering store simulation if needed (likely none for now)
             Debug.Log($"SIM {data.Id}: BasicEnteringStore OnEnter. Will remain frozen until timeout ({data.simulatedStateTimer:F2}s initial).");
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
// --- END OF FILE BasicEnteringStoreStateSO.cs ---