// --- START OF FILE CustomerSecondaryQueueStateSO.cs ---

// --- Updated CustomerSecondaryQueueStateSO.cs ---
using UnityEngine;
using System.Collections;
using CustomerManagement;
using System;
using Game.NPC;
using Game.Events;
using Game.NPC.States;
using Random = UnityEngine.Random; // Specify UnityEngine.Random

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "CustomerSecondaryQueueState", menuName = "NPC/Customer States/Secondary Queue", order = 8)]
    public class CustomerSecondaryQueueStateSO : NpcStateSO
    {
        public override System.Enum HandledState => CustomerState.SecondaryQueue;

        [Header("Queue Settings")]
        [SerializeField] private Vector2 impatientTimeRange = new Vector2(10f, 15f);

        private float impatientTimer;
        private float impatientDuration;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // --- Impatience Timer Setup (Migration) ---
            impatientDuration = Random.Range(impatientTimeRange.x, impatientTimeRange.y); // Use SO field
            impatientTimer = 0f;
            Debug.Log($"{context.NpcObject.name}: Entering {name}. Starting impatience timer for {impatientDuration:F2} seconds at spot {context.AssignedQueueSpotIndex}.", context.NpcObject);
            // ------------------------------

            // Note: Play waiting animation
            // context.PlayAnimation("WaitingOutside");

            // --- Phase 3, Substep 1: Initiate movement to the assigned secondary queue spot ---
             // The spot's transform was set as Runner.CurrentTargetLocation by TryJoinSecondaryQueue before the transition.
            if (context.Runner.CurrentTargetLocation.HasValue && context.Runner.CurrentTargetLocation.Value.browsePoint != null && context.AssignedQueueSpotIndex != -1)
            {
                Transform assignedSpotTransform = context.Runner.CurrentTargetLocation.Value.browsePoint; // Get transform from Runner's target

                Debug.Log($"{context.NpcObject.name}: Entering {name}. Moving to assigned spot {context.AssignedQueueSpotIndex} at {assignedSpotTransform.position}.", context.NpcObject);
                 // context.MoveToDestination handles setting _hasReachedCurrentDestination = false
                bool moveStarted = context.MoveToDestination(assignedSpotTransform.position);

                 if (!moveStarted) // Add check for move failure
                 {
                      Debug.LogError($"{context.NpcObject.name}: Failed to start movement to secondary queue spot {context.AssignedQueueSpotIndex}! Is the point on the NavMesh? Exiting.", context.NpcObject);
                      context.TransitionToState(GeneralState.ReturningToPool); // Fallback
                 }
                  // Note: No need to explicitly set _isMovingToQueueSpot here for the initial entry move.
            }
            else
            {
                Debug.LogError($"{context.NpcObject.name}: Entering Secondary Queue state without a valid assigned queue spot target in context! Exiting.", context.NpcObject);
                context.TransitionToState(GeneralState.ReturningToPool); // Fallback
            }
            // --- END Phase 3, Substep 1 ---
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context);

            // --- Impatience Timer Update and Check (Migration) ---
            impatientTimer += Time.deltaTime;

            if (impatientTimer >= impatientDuration)
            {
                Debug.Log($"{context.NpcObject.name}: IMPATIENT in Secondary Queue state at spot {context.AssignedQueueSpotIndex} after {impatientTimer:F2} seconds. Publishing NpcImpatientEvent.", context.NpcObject);
                context.PublishEvent(new NpcImpatientEvent(context.NpcObject, CustomerState.SecondaryQueue));
                // The Runner's handler for this event will transition the state.
            }
            // -------------------------------------------

             // Check IsAtDestination logic is now in the Runner's Update.
             // The Runner calls OnReachedDestination when true.
        }

        public override void OnReachedDestination(NpcStateContext context) // <-- Implement OnReachedDestination if CheckMovementArrival is true
        {
             // This logic was triggered from the old BaseQueueLogic.OnUpdate after reaching destination.
             // Removed: context.MovementHandler?.StopMoving(); // Redundant, Runner does this

             Debug.Log($"{context.NpcObject.name}: Reached Secondary Queue spot {context.AssignedQueueSpotIndex} (detected by Runner). Stopping and Rotating.", context.NpcObject);

             // --- Phase 3, Substep 1: Start Rotation Logic (Migration from old BaseQueueLogic.OnUpdate) ---
             // Get the Transform of the currently assigned secondary queue spot using context helper
             Transform currentQueueSpotTransform = context.Manager?.GetSecondaryQueuePoint(context.AssignedQueueSpotIndex); // Use Secondary getter

             if (currentQueueSpotTransform != null)
             {
                  Quaternion targetRotation = currentQueueSpotTransform.rotation;
                  Debug.Log($"CustomerAI ({context.NpcObject.name}): Starting rotation towards secondary queue spot rotation {targetRotation.eulerAngles} via MovementHandler.", context.NpcObject);
                  context.RotateTowardsTarget(targetRotation); // Use context helper
             }
             else
             {
                 Debug.LogWarning($"CustomerAI ({context.NpcObject.name}): Could not get Secondary Queue spot Transform {context.AssignedQueueSpotIndex} for rotation!", context.NpcObject);
             }
             // --- End Rotation Logic ---

             // OnReachedEndOfQueue logic from the old BaseQueueLogic.
             // For secondary queue, reaching the spot means waiting for the Manager to publish ReleaseNpcFromSecondaryQueueEvent.
             // So no action needed here besides stopping and rotating.
        }


        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context);

            impatientTimer = 0f;

            // --- Phase 3, Substep 1: Publish the QueueSpotFreedEvent ---
             // This event must be published when the NPC EXITS this state, regardless of the reason.
             // The CustomerManager will receive this event, free the spot, and start the cascade if needed.
             if (context.AssignedQueueSpotIndex != -1)
             {
                  Debug.Log($"{context.NpcObject.name}: Exiting {name}. Publishing QueueSpotFreedEvent for spot {context.AssignedQueueSpotIndex} in Secondary queue.", context.NpcObject);
                  context.PublishEvent(new QueueSpotFreedEvent(QueueType.Secondary, context.AssignedQueueSpotIndex));
             }
             else
             {
                  Debug.LogWarning($"{context.NpcObject.name}: Queue spot index not set when exiting Secondary Queue state! Cannot publish QueueSpotFreedEvent.", context.NpcObject);
             }
            // --- END Phase 3, Substep 1 ---


            // Example: Stop waiting animation
            // context.PlayAnimation("Idle");

            // --- Phase 3, Substep 1: REMOVE AssignedQueueSpotIndex reset from here ---
            // context.Runner.AssignedQueueSpotIndex = -1; // REMOVED - Handled by Runner.TransitionToState/ResetNPCData
            // --- END REMOVED ---
        }
    }
}