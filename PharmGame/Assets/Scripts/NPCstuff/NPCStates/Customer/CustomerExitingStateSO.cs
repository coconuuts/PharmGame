// --- START OF FILE CustomerExitingStateSO.cs ---

// --- Updated CustomerExitingStateSO.cs (Phase 1, Substep 5) ---

using UnityEngine;
using System.Collections;
using System;
using CustomerManagement; // Ensure this is present
using Game.NPC; // Ensure this is present for CustomerState and GeneralState
using Game.Events; // Needed for NpcExitedStoreEvent
using Game.NPC.States; // Ensure this is present
using Game.NPC.TI; // Needed for TiNpcData // <-- NEW: Added using directive

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "CustomerExitingState", menuName = "NPC/Customer States/Exiting", order = 9)]
    public class CustomerExitingStateSO : NpcStateSO
    {
        public override System.Enum HandledState => CustomerState.Exiting;

        public override void OnEnter(NpcStateContext context)
{
    base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

    // Signal departure to the CashRegisterInteractable and signal Manager (via Register)
    // This logic only runs if coming from *any* register-related state where the spot was claimed.
    if (context.GetPreviousState() != null &&
        (context.GetPreviousState().HandledState.Equals(CustomerState.MovingToRegister) ||
         context.GetPreviousState().HandledState.Equals(CustomerState.WaitingAtRegister) ||
         context.GetPreviousState().HandledState.Equals(CustomerState.TransactionActive)))
    {
        if (context.RegisterCached != null) // Use the property reading from Runner.CachedCashRegister
        {
            Debug.Log($"{context.NpcObject.name}: Notifying CachedCashRegister '{context.RegisterCached.gameObject.name}' of customer departure.", context.NpcObject); // <-- Use the property
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
         Debug.Log($"{context.NpcObject.name}: Exiting from non-register state ({context.GetPreviousState()?.HandledState.ToString() ?? "NULL"}). Ensuring register reference is null.", context.NpcObject);
         context.Runner.CachedCashRegister = null;
    }


    // --- NEW: Publish event to signal exit from the store ---
    // This event is listened to by the CustomerManager to update the activeCustomers count.
    // Published in OnEnter, as soon as they commit to leaving the store area.
    Debug.Log($"{context.NpcObject.name}: Publishing NpcExitedStoreEvent.", context.NpcObject);
    context.PublishEvent(new NpcExitedStoreEvent(context.NpcObject));
    // --- END NEW ---


            Transform exitTarget = context.GetRandomExitPoint();

            if (exitTarget != null)
            {
                context.Runner.CurrentTargetLocation = new BrowseLocation { browsePoint = exitTarget, inventory = null };
                Debug.Log($"{context.NpcObject.name}: Setting exit destination to {exitTarget.position}.", context.NpcObject);

                // Initiate movement using context helper (resets _hasReachedCurrentDestination flag)
                bool moveStarted = context.MoveToDestination(exitTarget.position);

                 if (!moveStarted) // Add check for move failure
                 {
                      Debug.LogError($"{context.NpcObject.name}: Failed to start movement to exit point! Is the point on the NavMesh?", context.NpcObject);
                       Debug.LogWarning($"CustomerExitingStateSO ({context.NpcObject.name}): Movement failed, falling back to ReturningToPool.", context.NpcObject);
                       context.TransitionToState(GeneralState.ReturningToPool); // Fallback
                       return; // Exit OnEnter early
                 }
            }
            else
            {
                Debug.LogWarning($"{context.NpcObject.name}: No exit points available for Exiting state! Transitioning to ReturningToPool.", context.NpcObject);
                // If no exit point, transition directly to ReturningToPool.
                Debug.LogWarning($"CustomerExitingStateSO ({context.NpcObject.name}): No exit point found, falling back to ReturningToPool.", context.NpcObject);
                context.TransitionToState(GeneralState.ReturningToPool); // Fallback
                return; // Exit OnEnter early
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
            Debug.Log($"{context.NpcObject.name}: Reached exit destination (detected by Runner).", context.NpcObject);

            // Ensure movement is stopped before transitioning (Runner does this before calling OnReachedDestination, but defensive)
            context.MovementHandler?.StopMoving();

            // --- PHASE 1, SUBSTEP 5: Differentiate TI NPCs and check endDay flag ---
             if (context.Runner != null && context.Runner.IsTrueIdentityNpc)
             {
                  // Check if the NPC is currently in their endDay schedule
                  if (context.TiData != null && context.TiData.isEndingDay) // Check the flag on TiData
                  {
                      Debug.Log($"{context.NpcObject.name}: TI NPC reached exit and is in endDay schedule. Transitioning to ReturningToPool.", context.NpcObject);
                      // Transition to the ReturningToPool state
                      context.TransitionToState(GeneralState.ReturningToPool);
                  }
                  else
                  {
                      Debug.Log($"{context.NpcObject.name}: TI NPC reached exit and is NOT in endDay schedule. Transitioning to Patrol.", context.NpcObject);
                      // Transition to the Patrol state (normal behavior after exiting store)
                      context.TransitionToState(TestState.Patrol);
                  }
             }
             else
             {
                  Debug.Log($"{context.NpcObject.name}: This is a Transient NPC. Transitioning to ReturningToPool.", context.NpcObject);
                  // Transient NPCs always return to pool after exiting
                  context.TransitionToState(GeneralState.ReturningToPool);
             }
            // --- END PHASE 1, SUBSTEP 5 ---
        }

        public override void OnExit(NpcStateContext context)
        {
            // NOTE: The NpcExitedStoreEvent is now published in OnEnter.

            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)

            // Logic from CustomerExitingLogic.OnExit (currently empty)
            // Example: Stop walking animation
            // context.PlayAnimation("Idle");
        }
    }
}
// --- END OF FILE CustomerExitingStateSO.cs ---