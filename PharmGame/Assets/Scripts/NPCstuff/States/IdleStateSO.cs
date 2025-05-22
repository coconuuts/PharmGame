// --- IdleStateSO.cs ---
using UnityEngine;
using System.Collections; // Needed for IEnumerator
using Game.NPC; // Needed for CustomerState enum
using Game.NPC.States; // Needed for NpcStateSO and NpcStateContext

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
        public override CustomerState HandledState => CustomerState.Idle; // <-- Use Idle enum

        [Header("Idle Settings")]
        [Tooltip("The name of the animation clip or trigger for the idle animation.")]
        [SerializeField] private string idleAnimationName = "Idle"; // Common parameter name
        [Tooltip("Optional minimum time to stay in the idle state before seeking a new task.")]
        [SerializeField] private float minIdleDuration = 0f; // 0 means immediately look for task if any
        [Tooltip("Optional maximum time to stay in the idle state.")]
        [SerializeField] private float maxIdleDuration = 5f;


        private float idleTimer;
        private float currentIdleDuration; // Actual duration for this idle instance

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)
            Debug.Log($"{context.NpcObject.name}: Entering generic Idle state.", context.NpcObject);

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
                       context.StartCoroutine(IdleRoutine(context)); // Start routine even with zero duration to yield one frame
                 }
            }
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context); // Call base OnUpdate (empty)

            // Update timer if duration is > 0
            if (maxIdleDuration > 0)
            {
                idleTimer += Time.deltaTime;

                // Check if timer is finished
                if (idleTimer >= currentIdleDuration)
                {
                    Debug.Log($"{context.NpcObject.name}: Finished idling for {currentIdleDuration:F2} seconds. Seeking next task.", context.NpcObject);
                    // Transition to state that seeks next task
                    // For Phase 3, transition to Customer start state if Shopper exists.
                    if (context.Shopper != null)
                    {
                         context.TransitionToState(CustomerState.LookingToShop); // Transition to Customer start state
                    }
                     else
                    {
                         // Non-customer types need a different "FindTask" or default behavior
                         // For now, just loop back to idle or exit. Let's loop back to idle.
                         // This is a placeholder for Phase 4/5 goal setting.
                         context.TransitionToState(CustomerState.Idle); // Loop back to idle (not useful)
                    }
                }
            }
        }

        // OnReachedDestination is not applicable

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)
            Debug.Log($"{context.NpcObject.name}: Exiting generic Idle state.", context.NpcObject);
            // Example: Stop idle animation (if not blended)
            // context.PlayAnimation("Locomotion"); // Blend back to locomotion base state
            idleTimer = 0f; // Reset timer
        }

        // Coroutine method (optional, can be used for timed loops or complex sequences)
        private IEnumerator IdleRoutine(NpcStateContext context) // Correct signature
        {
            Debug.Log($"{context.NpcObject.name}: IdleRoutine started in {name}.", context.NpcObject);

            // This coroutine could be used for randomized idle animations, looking around, etc.
            // If maxIdleDuration is 0, we use this to simply yield one frame before deciding next state.
            if (maxIdleDuration <= 0)
            {
                 yield return null; // Wait one frame
                 if (context.Shopper != null)
                 {
                      context.TransitionToState(CustomerState.LookingToShop); // Transition to Customer start state
                 }
                 else
                 {
                      // For non-customer types with 0 idle duration, transition to itself?
                      // This highlights the need for a generic "FindTask" state or default SO.
                      // For now, loop back or exit.
                      context.TransitionToState(CustomerState.Idle); // Loop back to idle (still not useful)
                 }
            }
            else
            {
                 // For timed idle, the OnUpdate handles the transition.
                 // This coroutine could do other things *during* the idle time.
                 while (context.Runner.GetCurrentState() == this && idleTimer < currentIdleDuration) // Loop while still in idle and time is left
                 {
                      // Example: Trigger a random look around animation periodically
                      // if (Random.value < 0.1f) context.PlayAnimation("LookAround");
                      yield return new WaitForSeconds(Random.Range(1f, 3f)); // Wait between random actions
                 }
                 // Coroutine will stop when state changes or timer finishes (handled by OnUpdate)
            }

            Debug.Log($"{context.NpcObject.name}: IdleRoutine finished.", context.NpcObject);
        }
    }
}