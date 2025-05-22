// --- Updated CustomerReturningToPoolStateSO.cs ---
using UnityEngine;
using System.Collections;
using Game.NPC;
using Game.Events;
using Game.NPC.States;

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "ReturningToPoolState", menuName = "NPC/General States/Returning To Pool", order = 10)]
    public class ReturningToPoolStateSO : NpcStateSO
    {
        public override CustomerState HandledState => CustomerState.ReturningToPool;

        public override void OnEnter(NpcStateContext context)
         {
             // DO NOT call base.OnEnter(context) here as it enables the Agent.
             Debug.Log($"{context.NpcObject.name}: Entering ReturningToPool state. Signaling manager.", context.NpcObject);

             // Disable NavMeshAgent manually via context/handler
             if (context.MovementHandler?.Agent != null && context.MovementHandler.Agent.enabled)
             {
                  context.MovementHandler.Agent.ResetPath();
                  context.MovementHandler.Agent.isStopped = true;
                  context.MovementHandler.Agent.enabled = false;
             }

             context.PublishEvent(new NpcReturningToPoolEvent(context.NpcObject));

             // The object is likely deactivated immediately after this event is processed by the Manager.
         }

         public override void OnExit(NpcStateContext context)
         {
             Debug.Log($"{context.NpcObject.name}: Exiting ReturningToPool state (should not happen).", context.NpcObject);
         }
    }
}