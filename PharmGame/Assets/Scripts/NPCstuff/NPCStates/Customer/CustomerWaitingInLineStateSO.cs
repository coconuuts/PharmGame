// --- START OF FILE CustomerWaitingInLineStateSO.cs ---

// --- Updated CustomerWaitingInLineStateSO.cs ---
using UnityEngine;
using System.Collections;
using System;
using CustomerManagement;
using Game.NPC;
using Game.Events;
using Game.NPC.States;
using Random = UnityEngine.Random; // Specify UnityEngine.Random

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "CustomerWaitingInLineState", menuName = "NPC/Customer States/Waiting In Line", order = 7)] // Adjust order as needed
    public class CustomerWaitingInLineStateSO : NpcStateSO
    {
        public override System.Enum HandledState => CustomerState.WaitingInLine;

        // Impatience logic now lives here
        [Header("Waiting In Line Settings")]
        [SerializeField] private Vector2 impatientTimeRange = new Vector2(10f, 15f);

        private float impatientTimer;
        private float impatientDuration;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // Stop any residual movement from reaching the spot
            context.MovementHandler?.StopMoving();
            context.MovementHandler?.StopRotation(); // Stop rotation coroutine if it was still running from arrival

            // Play waiting/idle animation
            // context.PlayAnimation("WaitingInLine"); // Placeholder animation name

            // Start impatience timer
            impatientDuration = Random.Range(impatientTimeRange.x, impatientTimeRange.y);
            impatientTimer = 0f;
            Debug.Log($"{context.NpcObject.name}: Entering {name}. Starting impatience timer for {impatientDuration:F2} seconds at spot {context.AssignedQueueSpotIndex}.", context.NpcObject);

            // No coroutine needed unless there's complex waiting logic beyond the timer

            // Note: Rotation towards a target (like the register) could happen here if desired,
            // but `OnReachedDestination` on the *previous* state (Queue) is probably a better place for the final rotation.
            // Let's add the rotation from QueueStateSO OnReachedDestination back into this OnEnter
             if (context.CurrentTargetLocation.HasValue && context.CurrentTargetLocation.Value.browsePoint != null)
             {
                 Quaternion targetRotation = context.CurrentTargetLocation.Value.browsePoint.rotation; // Should be the rotation of the assigned queue spot
                 Debug.Log($"CustomerAI ({context.NpcObject.name}): Starting rotation towards assigned spot rotation {targetRotation.eulerAngles} in WaitingInLine.", context.NpcObject);
                 context.RotateTowardsTarget(targetRotation); // Use context helper
             }
             else
             {
                 Debug.LogWarning($"CustomerAI ({context.NpcObject.name}): No valid target location stored for WaitingInLine rotation or MovementHandler missing!", context.NpcObject);
             }
        }

        public override void OnUpdate(NpcStateContext context)
        {
            // Only update impatience timer
            impatientTimer += Time.deltaTime; 

            if (impatientTimer >= impatientDuration)
            {
                Debug.Log($"{context.NpcObject.name}: IMPATIENT in WaitingInLine state at spot {context.AssignedQueueSpotIndex} after {impatientTimer:F2} seconds. Publishing NpcImpatientEvent.", context.NpcObject);
                // Publish NpcImpatientEvent (CustomerManager handles transitioning to Exiting)
                context.PublishEvent(new NpcImpatientEvent(context.NpcObject, CustomerState.WaitingInLine));
            }
        }

        // OnReachedDestination is not applicable here, they are already AT their spot.
        public override void OnReachedDestination(NpcStateContext context) { /* Not applicable */ } // <-- Explicitly empty


        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)

            impatientTimer = 0f; // Reset timer

            // Play idle/locomotion base animation
            // context.PlayAnimation("Idle");

            // IMPORTANT: Do NOT publish QueueSpotFreedEvent here.
            // That must happen when exiting the *moving* queue state (`CustomerState.Queue` or `CustomerState.SecondaryQueue`).
        }
    }
}