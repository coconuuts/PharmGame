// --- Updated CustomerExitingStateSO.cs ---
using UnityEngine;
using System.Collections;
using System;
using CustomerManagement;
using Game.NPC;
using Game.Events;
using Game.NPC.States;

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "CustomerExitingState", menuName = "NPC/Customer States/Exiting", order = 9)]
    public class CustomerExitingStateSO : NpcStateSO
    {
        public override System.Enum HandledState => CustomerState.Exiting;
        
        // Exiting state is typically not interruptible or has different rules
        // public override bool IsInterruptible => false; // Example override if needed

        public override void OnEnter(NpcStateContext context)
{
    base.OnEnter(context);

    // Signal departure to the CashRegisterInteractable and signal Manager (via Register)
    // This logic only runs if coming from *any* register-related state where the spot was claimed.
    if (context.GetPreviousState() != null &&
        (context.GetPreviousState().HandledState.Equals(CustomerState.MovingToRegister) || // <-- ADD THIS CHECK
         context.GetPreviousState().HandledState.Equals(CustomerState.WaitingAtRegister) ||
         context.GetPreviousState().HandledState.Equals(CustomerState.TransactionActive)))
    {
        if (context.RegisterCached != null) // Use the property reading from Runner.CachedCashRegister
        {
            Debug.Log($"{context.NpcObject.name}: Notifying CachedCashRegister '{context.RegisterCached.gameObject.name}' of customer departure.", context.NpcObject);
            context.RegisterCached.CustomerDeparted(); // Call using the property

            // The CashRegisterInteractable.CustomerDeparted method *should* handle clearing
            // the Runner's CachedCashRegister field internally. If it doesn't, add it there.
            // For safety, you *could* add `context.Runner.CachedCashRegister = null;` here,
            // but ideally the Register's `CustomerDeparted` is the single source for this.
             context.Runner.CachedCashRegister = null; // Let's keep this here for robustness for now.

        }
        else
        {
             // This warning indicates the register reference was lost before exiting the register flow.
             Debug.LogWarning($"{context.NpcObject.name}: CachedCashRegister reference is null when exiting state from a register flow! Cannot notify register of departure.", context.NpcObject);
             context.Runner.CachedCashRegister = null; // Ensure Runner field is cleared
        }
    }
    else
    {
         // If exiting from a non-register state, ensure the cached register is null anyway
         // (It should already be null, but defensive programming)
         Debug.Log($"{context.NpcObject.name}: Exiting from non-register state ({context.GetPreviousState()?.HandledState.ToString() ?? "NULL"}). Ensuring register reference is null.", context.NpcObject);
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
            context.TransitionToState(GeneralState.ReturningToPool); // Transition via context
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