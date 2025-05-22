// --- Updated CustomerExitingStateSO.cs ---
using UnityEngine;
using System.Collections;
using CustomerManagement;
using Game.NPC;
using Game.Events;
using Game.NPC.States;

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "CustomerExitingState", menuName = "NPC/Customer States/Exiting", order = 9)]
    public class CustomerExitingStateSO : NpcStateSO
    {
        public override CustomerState HandledState => CustomerState.Exiting;
        
         // Exiting state is typically not interruptible or has different rules
        // public override bool IsInterruptible => false; // Example override if needed

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context);
            Debug.Log($"{context.NpcObject.name}: Entering Exiting state. Signaling departure and finding exit point.", context.NpcObject);

             // Signal departure to the CashRegisterInteractable and signal Manager (via Register)
            if (context.GetPreviousState() != null &&
                (context.GetPreviousState().HandledState == CustomerState.WaitingAtRegister || context.GetPreviousState().HandledState == CustomerState.TransactionActive))
            {
                if (context.CachedCashRegister != null)
                {
                    context.CachedCashRegister.CustomerDeparted(); // Keep direct call for now
                    context.Runner.CachedCashRegister = null; // Clear cached reference on Runner
                }
                else
                {
                    Debug.LogWarning($"{context.NpcObject.name}: CachedCashRegister is null when entering Exiting state!", context.NpcObject);
                }
            }
            else
            {
                Debug.Log($"{context.NpcObject.name}: Exiting from non-register state ({context.GetPreviousState()?.HandledState.ToString() ?? "NULL"}). No need for register cleanup.", context.NpcObject);
                 context.Runner.CachedCashRegister = null;
            }

            Transform exitTarget = context.GetRandomExitPoint();

            if (exitTarget != null)
            {
                context.Runner.CurrentTargetLocation = new BrowseLocation { browsePoint = exitTarget, inventory = null };
                Debug.Log($"{context.NpcObject.name}: Setting exit destination to {exitTarget.position}.", context.NpcObject);
                context.MoveToDestination(exitTarget.position);
            }
            else
            {
                Debug.LogWarning($"{context.NpcObject.name}: No exit points available for Exiting state! Publishing NpcReturningToPoolEvent.", context.NpcObject);
                context.PublishEvent(new NpcReturningToPoolEvent(context.NpcObject));
            }

            // Note: Animation handler could be used here
            // context.PlayAnimation("Walking");
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context); // Call base OnUpdate (empty)
        }
        
        public override void OnReachedDestination(NpcStateContext context) // <-- Implement OnReachedDestination
        {
            // This logic was previously in CustomerExitingLogic.OnUpdate
            Debug.Log($"{context.NpcObject.name}: Reached exit destination (detected by Runner). Transitioning to ReturningToPool.", context.NpcObject);
            context.TransitionToState(CustomerState.ReturningToPool); // Transition via context
        }

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)
            // Logic from CustomerExitingLogic.OnExit (currently empty)
            // Example: Stop walking animation
            // context.PlayAnimation("Idle");
        }
    }
}