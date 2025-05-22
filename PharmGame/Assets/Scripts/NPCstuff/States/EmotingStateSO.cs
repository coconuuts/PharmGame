// --- EmotingStateSO.cs ---
using UnityEngine;
using System.Collections; // Needed for IEnumerator
using Game.NPC; // Needed for CustomerState enum
using Game.NPC.States; // Needed for NpcStateSO and NpcStateContext

namespace Game.NPC.States
{
    /// <summary>
    /// A generic state for an NPC playing an emote animation or effect.
    /// Likely involves stopping movement and waiting for the emote to finish.
    /// Corresponds to CustomerState.Emoting.
    /// </summary>
    [CreateAssetMenu(fileName = "EmotingState", menuName = "NPC/General States/Emoting", order = 4)] // Placed under General States
    public class EmotingStateSO : NpcStateSO
    {
        public override CustomerState HandledState => CustomerState.Emoting; // <-- Use the new enum value

        [Header("Emote Settings")]
        [Tooltip("The name of the animation clip or trigger for this emote.")]
        [SerializeField] private string emoteAnimationName = "Emote";
        [Tooltip("The minimum duration for the emote state, regardless of animation length.")]
        [SerializeField] private float minEmoteDuration = 2.0f;

        // Need a way to get the length of the animation? Or rely on a fixed duration?
        // Getting animation length via Animator is tricky from SO. Fixed duration or a parameter is simpler for now.
        // Let's use a fixed duration for this example.

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)
            Debug.Log($"{context.NpcObject.name}: Entering Emoting state.", context.NpcObject);

            // Stop movement
            context.MovementHandler?.StopMoving();

            // Play the emote animation
            context.PlayAnimation(emoteAnimationName);

            // Start a coroutine to wait for the emote duration
            context.StartCoroutine(EmoteRoutine(context));
        }

        // OnUpdate remains empty
        // OnReachedDestination is not applicable

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)
             Debug.Log($"{context.NpcObject.name}: Exiting Emoting state.", context.NpcObject);
             // Optional: Transition to idle animation? Or is that handled by the next state's OnEnter?
             // context.PlayAnimation("Idle"); // Example
        }

        // Coroutine method to wait for the emote duration
        private IEnumerator EmoteRoutine(NpcStateContext context)
        {
             Debug.Log($"{context.NpcObject.name}: EmoteRoutine started in {name}.", context.NpcObject);

             // Wait for the minimum duration
             yield return new WaitForSeconds(minEmoteDuration);

             Debug.Log($"{context.NpcObject.name}: EmoteRoutine finished. Transitioning back (placeholder).", context.NpcObject);

             // --- DECIDE WHAT HAPPENS AFTER EMOTE ---
             // This state is intended as an *interruption*.
             // The logic to return to the previous state will be part of Phase 5 (State Interruptions).
             // For now, as a placeholder, we can transition to a default idle state or just exit.
             // Transitioning back to the previous state requires Phase 5 stack logic.
             // Let's temporarily transition to Exiting as a safe fallback for Phase 3 testing.
             // Or, if Idle is a known state, transition there. Let's assume Idle is mapped to CustomerState.Browse for now? No, that's bad.
             // Let's add a generic "Idle" state SO in the next step for this purpose.
             // For now, let's make it transition to Exiting as a temporary visual confirmation of state exit.
             context.TransitionToState(CustomerState.Exiting); // TEMPORARY FALLBACK TRANSITION

             // The Phase 5 implementation will instead pop the previous state from the stack and transition to it.
        }
    }
}