// --- Updated CustomerTransactionActiveStateSO.cs ---
using UnityEngine;
using System.Collections;
using System;
using Game.NPC;
using Game.NPC.States;

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "CustomerTransactionActiveState", menuName = "NPC/Customer States/Transaction Active", order = 6)]
    public class CustomerTransactionActiveStateSO : NpcStateSO
    {
        public override System.Enum HandledState => CustomerState.TransactionActive;
        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, but does not stop rotation as that's in OnExit of previous state)

            // --- Logic from CustomerTransactionActiveLogic.OnEnter (Migration) ---
            context.MovementHandler?.StopMoving(); // Use context helper to ensure no sliding

            // --- Ensure the NPC completes its rotation towards the register ---
            if (context.CurrentTargetLocation.HasValue && context.CurrentTargetLocation.Value.browsePoint != null)
            {
                Debug.Log($"{context.NpcObject.name}: Ensuring rotation towards register in TransactionActive state.", context.NpcObject);
                // This will start a new rotation coroutine. If the NPC was already facing the right way,
                // it will complete almost instantly. If it was interrupted, it will now finish.
                context.RotateTowardsTarget(context.CurrentTargetLocation.Value.browsePoint.rotation);
            }
            else
            {
                 Debug.LogWarning($"{context.NpcObject.name}: No valid target location stored for TransactionActive rotation!", context.NpcObject);
            }

            // Note: Animation handler could be used here
            // context.PlayAnimation("Transaction");
        }

        // OnUpdate remains empty or base call

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)
            // Logic from CustomerTransactionActiveLogic.OnExit (currently empty)
            // Example: Stop transaction animation
            // context.PlayAnimation("Idle");
        }

        // StateCoroutine is not needed for this state's logic
    }
}