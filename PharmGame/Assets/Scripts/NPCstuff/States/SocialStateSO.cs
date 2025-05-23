// --- Updated SocialStateSO.cs (Full Placeholder Logic) ---
using UnityEngine;
using System.Collections;
using System;
using Game.NPC;
using Game.NPC.States;
using Game.Events;
using Random = UnityEngine.Random;

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "SocialState", menuName = "NPC/General States/Social", order = 3)]
    public class SocialStateSO : NpcStateSO
    {
        public override System.Enum HandledState => GeneralState.Social;

        // Social state is often interruptible by combat, but not usually by other social triggers or emotes.
        // Keep IsInterruptible as true by default, or override if specific rules apply.
        // public override bool IsInterruptible => true; // Explicitly state if needed


        [Header("Social Simulation Settings")]
        [Tooltip("Minimum duration for the simulated social interaction state.")]
        [SerializeField] private float minSocialDuration = 2.0f;
        [Tooltip("Maximum duration for the simulated social interaction state.")]
        [SerializeField] private float maxSocialDuration = 5.0f;

        // InteractorObject is available via context.InteractorObject

        private Coroutine socialRoutine;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context);

            // Stop movement and rotation
            context.MovementHandler?.StopMoving();
            context.MovementHandler?.StopRotation(); // Ensure rotation stops

            // Optional: Rotate towards the interactor (using context.InteractorObject)
             if (context.InteractorObject != null)
             {
                 Vector3 directionToInteractor = (context.InteractorObject.transform.position - context.NpcObject.transform.position).normalized;
                  // Check if the direction is meaningful (not zero vector)
                  if (directionToInteractor.sqrMagnitude > 0.001f)
                  {
                       Quaternion targetRotation = Quaternion.LookRotation(directionToInteractor);
                       // Start rotation using MovementHandler
                       context.RotateTowardsTarget(targetRotation);
                  }
             }

            // Play social idle/talking animation (placeholder)
            // context.PlayAnimation("SocialIdle");

            // Start a coroutine for social simulation logic and completion
            socialRoutine = context.StartCoroutine(SocialRoutine(context));
        }

        public override void OnUpdate(NpcStateContext context)
        {
            // Continuous social logic
             // Example: Maybe keep looking at the interactor if they move?
             // if (context.InteractorObject != null) { ... update rotation target ... }
        }

        // OnReachedDestination is not applicable

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context);

            // Stop social routine (Runner handles stopping activeStateCoroutine, but safety)
            // context.StopCoroutine(socialRoutine); // Redundant

            // Cleanup logic for exiting social
            // - Transition to idle animation or locomotion base state
            // context.PlayAnimation("Idle"); // Return to idle

             // Clear the InteractorObject reference in context/runner (if managed there)
             // Note: We set this field directly on _stateContext in the Runner handler.
             // Clearing it should also happen in the Runner handler (HandleInteractionEnded).
        }

        // Coroutine method to simulate social duration and signal completion
        private IEnumerator SocialRoutine(NpcStateContext context)
        {
            Debug.Log($"{context.NpcObject.name}: SocialRoutine started in {name}.", context.NpcObject);

            // Wait for rotation to finish before starting timer? Or just wait for duration.
            // Assuming rotation happens quickly, just wait for duration.

            float socialDuration = Random.Range(minSocialDuration, maxSocialDuration);
            Debug.Log($"{context.NpcObject.name}: Simulating social interaction for {socialDuration:F2} seconds.", context.NpcObject);

            yield return new WaitForSeconds(socialDuration);

            Debug.Log($"{context.NpcObject.name}: Social simulation finished. Publishing NpcInteractionEndedEvent.", context.NpcObject);

            // --- PUBLISH THE COMPLETION EVENT ---
            context.PublishEvent(new NpcInteractionEndedEvent(context.NpcObject));
            // ------------------------------------
        }
    }
}