// --- IdleStateSO.cs ---
using UnityEngine;
using System.Collections; // Needed for IEnumerator
using System;
using Game.NPC; // Needed for CustomerState enum
using Game.NPC.States; // Needed for NpcStateSO and NpcStateContext
using Random = UnityEngine.Random;

namespace Game.NPC.States
{
    /// <summary>
    /// A generic state for an NPC that is simply standing still and not actively doing anything.
    /// Can be a default state or a state transitioned to after interruptions.
    /// Corresponds to CustomerState.Idle.
    /// </summary>
    [CreateAssetMenu(fileName = "IdleState", menuName = "NPC/General States/Idle", order = 1)] // Placed under General States
    public class IdleStateSO : NpcStateSO
    {
        public override System.Enum HandledState => GeneralState.Idle; // <-- Use Idle enum

        [Header("Idle Settings")]
        [Tooltip("The name of the animation clip or trigger for the idle animation.")]
        [SerializeField] private string idleAnimationName = "Idle"; // Common parameter name
        [Tooltip("Optional minimum time to stay in the idle state before seeking a new task.")]
        [SerializeField] private float minIdleDuration = 0f; // 0 means immediately look for task if any
        [Tooltip("Optional maximum time to stay in the idle state.")]
        [SerializeField] private float maxIdleDuration = 5f;


        private float idleTimer;
        private float currentIdleDuration;
        private Coroutine idleRoutine;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // Stop movement and rotation (Base OnEnter handles enabling Agent, Base OnExit stops on exit)
            context.MovementHandler?.StopMoving();
            // Rotation might have finished in the previous state, but explicitly stop any ongoing rotation
            context.MovementHandler?.StopRotation();


            // Play idle animation
            context.PlayAnimation(idleAnimationName);

            // Start timer if duration is > 0
            if (maxIdleDuration > 0)
            {
                currentIdleDuration = Random.Range(minIdleDuration, maxIdleDuration);
                idleTimer = 0f;
                Debug.Log($"{context.NpcObject.name}: Idling for {currentIdleDuration:F2} seconds.", context.NpcObject);

                idleRoutine = context.StartCoroutine(IdleRoutine(context));
            }
            else
            {
                // If maxIdleDuration is 0 or less, immediately try to find next task
                // Or transition to a state that does that (like the Customer's LookToShop)
                Debug.Log($"{context.NpcObject.name}: Idle state has 0 duration, seeking next task.", context.NpcObject);
                // For Phase 3, we'll just transition to LookToShop if Customer type
                // Phase 4/5 needs a generic way to find next task based on type/AI goals.
                // Let's temporarily transition to LookToShop if Shopper component exists.
                if (context.Shopper != null)
                {
                    context.TransitionToState(CustomerState.LookingToShop); // Transition to Customer start state
                }
                else
                {
                    // For non-customer types, maybe transition to a generic "FindTask" state?
                    // For now, if not a customer, just stay in idle (loop back?) or exit.
                    // Let's loop back to idle for non-customers for now if duration is zero.
                    // This prevents errors but isn't useful behavior.
                    idleRoutine = context.StartCoroutine(IdleRoutine(context)); // Start routine even with zero duration to yield one frame
                }
            }
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context); // Call base OnUpdate (empty)
        }

        // OnReachedDestination is not applicable

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)
            // Example: Stop idle animation (if not blended)
            // context.PlayAnimation("Locomotion"); // Blend back to locomotion base state
            idleTimer = 0f; // Reset timer
        }

        // Coroutine method (optional, can be used for timed loops or complex sequences)
        private IEnumerator IdleRoutine(NpcStateContext context) // Correct signature
        {
            Debug.Log($"{context.NpcObject.name}: IdleRoutine started in {name}.", context.NpcObject);

            // Use coroutine for timer if maxIdleDuration > 0
            if (maxIdleDuration > 0)
            {
                 // Wait for the timed duration
                 yield return new WaitForSeconds(currentIdleDuration);

                 // Time is up, seek next task
                 Debug.Log($"{context.NpcObject.name}: Finished idling for {currentIdleDuration:F2} seconds (routine). Seeking next task.", context.NpcObject);
                 // Temporary fallback to LookingToShop if Customer
                 if (context.Shopper != null)
                 {
                      context.TransitionToState(CustomerState.LookingToShop); // <-- Transition using CustomerState enum
                 }
                 else
                 {
                      // For non-customer types, need a different starting point.
                       // For now, if duration is over and not a customer, just stay in idle (loop via routine).
                       // This is a placeholder for Phase 4/5 goal setting.
                       context.TransitionToState(GeneralState.Idle); // <-- Transition using GeneralState enum (looping)
                 }
            }
            else
            {
                 // If maxIdleDuration is 0, just yield one frame and then transition
                 yield return null;
                 Debug.Log($"{context.NpcObject.name}: Idle state has 0 duration (routine), seeking next task.", context.NpcObject);
                 if (context.Shopper != null)
                 {
                      context.TransitionToState(CustomerState.LookingToShop); // <-- Transition using CustomerState enum
                 }
                 else
                 {
                      // For non-customer types with 0 duration, transition to itself?
                       context.TransitionToState(GeneralState.Idle); // <-- Transition using GeneralState enum (looping)
                 }
            }

             // Note: Coroutine will be stopped by Runner when state changes.
            Debug.Log($"{context.NpcObject.name}: IdleRoutine finished.", context.NpcObject); // This line might not be reached
        }
    }
}