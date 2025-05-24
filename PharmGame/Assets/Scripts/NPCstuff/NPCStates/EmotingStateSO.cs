// --- Updated EmotingStateSO.cs (Full Placeholder Logic) ---
using UnityEngine;
using System.Collections;
using System;
using CustomerManagement;
using Game.NPC;
using Game.Events;
using Game.NPC.States;

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "EmotingState", menuName = "NPC/General States/Emoting", order = 4)]
    public class EmotingStateSO : NpcStateSO
    {
        public override System.Enum HandledState => GeneralState.Emoting;

        // Emoting state is typically interruptible by combat, but not other triggers?
        // public override bool IsInterruptible => true; // Default is true


        [Header("Emote Settings")]
        [Tooltip("The name of the animation clip or trigger for this emote.")]
        [SerializeField] private string emoteAnimationName = "Emote";
        [Tooltip("The minimum duration for the emote state, regardless of animation length.")]
        [SerializeField] private float minEmoteDuration = 2.0f;


        private Coroutine emoteRoutine;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context);

            // Stop movement
            context.MovementHandler?.StopMoving();
            context.MovementHandler?.StopRotation(); // Ensure rotation stops


            // Play the emote animation
            context.PlayAnimation(emoteAnimationName);

            // Start a coroutine to handle the emote duration/completion
            emoteRoutine = context.StartCoroutine(EmoteRoutine(context));
        }

        public override void OnUpdate(NpcStateContext context)
        {
            // No continuous logic needed for this placeholder emote
            // base.OnUpdate(context);
        }

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context);

            // Stop the coroutine if it's still running (Runner handles activeStateCoroutine, but safety)
            // context.StopCoroutine(emoteRoutine); // Redundant

            // Transition to idle animation
            // context.PlayAnimation("Idle"); // Example
        }

        // Coroutine method to handle the emote duration/completion
        private IEnumerator EmoteRoutine(NpcStateContext context)
        {
             Debug.Log($"{context.NpcObject.name}: EmoteRoutine started in {name}.", context.NpcObject);

             // Wait for the minimum duration
             yield return new WaitForSeconds(minEmoteDuration);

             // TODO: In a real system, you might wait for the animation to finish here
             // yield return new WaitUntil(() => !context.AnimationHandler.IsPlaying(emoteAnimationName)); // Requires AnimationHandler method

             Debug.Log($"{context.NpcObject.name}: Emote duration finished. Publishing NpcEmoteEndedEvent.", context.NpcObject);

             // --- PUBLISH THE COMPLETION EVENT ---
             context.PublishEvent(new NpcEmoteEndedEvent(context.NpcObject));
             // This coroutine will automatically be stopped when the state changes.
             // ------------------------------------
        }
    }
}