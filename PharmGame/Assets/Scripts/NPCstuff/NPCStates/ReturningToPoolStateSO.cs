// --- Updated CustomerReturningToPoolStateSO.cs ---
using UnityEngine;
using System.Collections;
using System;
using Game.NPC;
using Game.Events;
using Game.NPC.States;

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "ReturningToPoolState", menuName = "NPC/General States/Returning To Pool", order = 10)]
    public class ReturningToPoolStateSO : NpcStateSO
    {
        public override System.Enum HandledState => GeneralState.ReturningToPool;

        public override void OnEnter(NpcStateContext context)
        {
            string enumTypeName = HandledState?.GetType().Name ?? "NULL_TYPE";
            string enumValueName = HandledState?.ToString() ?? "NULL_VALUE";
            Debug.Log($"{context.NpcObject.name}: Entering State: {name} ({GetType().Name}) (Enum: {enumTypeName}.{enumValueName})", context.NpcObject);


            // Disable NavMeshAgent manually via context/handler
            if (context.MovementHandler?.Agent != null && context.MovementHandler.Agent.enabled)
            {
                context.MovementHandler.Agent.ResetPath();
                context.MovementHandler.Agent.isStopped = true;
                context.MovementHandler.Agent.enabled = false;
            }

            context.PublishEvent(new NpcReturningToPoolEvent(context.NpcObject));
        }

         public override void OnExit(NpcStateContext context)
         {
            string enumTypeName = HandledState?.GetType().Name ?? "NULL_TYPE";
            string enumValueName = HandledState?.ToString() ?? "NULL_VALUE";
            Debug.Log($"{context.NpcObject.name}: Exiting State: {name} ({GetType().Name}) (Enum: {enumTypeName}.{enumValueName}) (should not happen).", context.NpcObject);
         }
    }
}