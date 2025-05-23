using UnityEngine;
using System.Collections;
using System;
using CustomerManagement;
using Game.NPC;
using Game.NPC.States; // Ensure this is present

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "CustomerMovingToRegisterState", menuName = "NPC/Customer States/Moving To Register", order = 4)]
    public class CustomerMovingToRegisterStateSO : NpcStateSO
    {
        public override System.Enum HandledState => CustomerState.MovingToRegister;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context);

            Transform registerTarget = context.GetRegisterPoint();

            if (registerTarget != null)
            {
                context.Runner.CurrentTargetLocation = new BrowseLocation { browsePoint = registerTarget, inventory = null };
                context.MoveToDestination(registerTarget.position);
                Debug.Log($"{context.NpcObject.name}: Set destination to register point: {registerTarget.position}.", context.NpcObject);
                context.SignalCustomerAtRegister();
            }
            else
            {
                Debug.LogWarning($"{context.NpcObject.name}: No register point assigned! Exiting.", context.NpcObject);
                context.TransitionToState(CustomerState.Exiting);
            }
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context);
        }

        public override void OnReachedDestination(NpcStateContext context)
        {
             Debug.Log($"{context.NpcObject.name}: Reached register destination (detected by Runner). Transitioning to WaitingAtRegister.", context.NpcObject);
             context.TransitionToState(CustomerState.WaitingAtRegister);
        }

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context);
        }
    }
}