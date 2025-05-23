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
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // --- Logic from CustomerTransactionActiveLogic.OnEnter (Migration) ---
            context.MovementHandler?.StopMoving(); // Use context helper

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