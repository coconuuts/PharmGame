// --- START OF FILE InitializingSO.cs ---

// --- Updated InitializingSO.cs (Phase 3, Substep 4 - Refinement) ---

using UnityEngine;
using System;
using System.Collections;
using Game.NPC; // Needed for GeneralState and CustomerState enums
using Game.NPC.States; // Needed for NpcStateSO and NpcStateContext

namespace Game.NPC.States
{
    /// <summary>
    /// A generic state representing the moment after an NPC is activated/initialized and warped.
    /// Its primary job is to immediately transition to the NPC's determined starting state.
    /// This is intended to be a very short-lived state.
    /// Corresponds to GeneralState.Initializing.
    /// </summary>
    [CreateAssetMenu(fileName = "InitializingState", menuName = "NPC/General States/Initializing", order = 0)] // Placed under General States menu
    public class InitializingSO : NpcStateSO
    {
        // Maps to the generic Initializing enum value
        public override System.Enum HandledState => GeneralState.Initializing;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // Logic: Immediately decide the *real* starting state based on NPC type configuration or loaded TI data
            // The Runner's GetPrimaryStartingStateSO method is responsible for this determination,
            // potentially considering loaded TI data state first, then the TypeDefinition's primary state,
            // and finally falling back to a safe state like ReturningToPool or Idle if none are valid/found.

            if (context.Runner == null)
            {
                 Debug.LogError($"InitializingSO ({context.NpcObject?.name}): Runner reference is null in context! Cannot determine starting state. Transitioning to ReturningToPool.", context.NpcObject);
                 // Attempt fallback directly if Runner is null (critical error)
                 context.TransitionToState(GeneralState.ReturningToPool); // This transition helper should handle null Runner gracefully
                 return; // Exit OnEnter early
            }

            // --- Use the Runner's method to get the determined primary starting state ---
            NpcStateSO primaryStartingState = context.Runner.GetPrimaryStartingStateSO(); // This method handles fallbacks internally if needed
            // --- END ---

            if (primaryStartingState != null)
            {
                 Debug.Log($"{context.NpcObject.name}: Initializing state transitioning to determined primary starting state: {primaryStartingState.name}.", context.NpcObject);
                 context.TransitionToState(primaryStartingState); // Transition to the determined start state
            }
            else
            {
                 // If GetPrimaryStartingStateSO returned null, it means even its internal fallbacks failed.
                 Debug.LogError($"{context.NpcObject.name}: Runner.GetPrimaryStartingStateSO returned null! Cannot find a primary starting state for NPC type and fallbacks failed. Transitioning to ReturningToPool.", context.NpcObject);
                 // Transition to the absolute fallback state (ReturningToPool) via context
                 context.TransitionToState(GeneralState.ReturningToPool); // This should always be a valid state or handled gracefully
            }

            // This state is designed to transition immediately, so no significant OnUpdate/Coroutine needed.
        }

        // OnUpdate remains empty
        // OnReachedDestination is not applicable
        // StateCoroutine is not typically needed (transition is immediate)

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation - though should already be stopped)
        }
    }
}