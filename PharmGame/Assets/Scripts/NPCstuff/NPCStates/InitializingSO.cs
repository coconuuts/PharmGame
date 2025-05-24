// --- InitializingSO.cs ---
using UnityEngine;
using System;
using System.Collections;
using Game.NPC; // Needed for CustomerState enum (used by base class HandledState)
using Game.NPC.States; // Needed for NpcStateSO and NpcStateContext

namespace Game.NPC.States
{
    /// <summary>
    /// A generic state representing the moment after an NPC is activated and warped.
    /// Its primary job is to immediately transition to the NPC's type-specific starting state.
    /// This is intended to be a very short-lived state.
    /// Corresponds to CustomerState.Initializing.
    /// </summary>
    [CreateAssetMenu(fileName = "InitializingState", menuName = "NPC/General States/Initializing", order = 0)] // Placed under General States menu
    public class InitializingSO : NpcStateSO
    {
        // Maps to the generic Initializing enum value
        public override System.Enum HandledState => GeneralState.Initializing;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // Logic: Immediately decide the *real* starting state based on NPC type
            // This requires the Runner to know the NPC's configured types and their starting states.
            // Let's assume the Runner has a way to get the primary starting state SO for its types.
            // We'll add a method to the Runner like GetPrimaryStartingStateSO().

            NpcStateSO primaryStartingState = context.Runner.GetPrimaryStartingStateSO(); // Need this method on Runner

            if (primaryStartingState != null)
            {
                 Debug.Log($"{context.NpcObject.name}: Initializing state transitioning to primary starting state: {primaryStartingState.name}.", context.NpcObject);
                 context.TransitionToState(primaryStartingState); // Transition to the type-specific start
            }
            else
            {
                 Debug.LogError($"{context.NpcObject.name}: Initializing state cannot find a primary starting state for NPC type! Transitioning to ReturningToPool.", context.NpcObject);
                 context.TransitionToState(GeneralState.ReturningToPool); // Fallback
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