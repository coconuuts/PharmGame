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
            // Use context properties now
            Debug.Log($"{context.NpcObject.name}: Entering {name}. Starting impatience timer for {impatientDuration:F2} seconds at spot {context.AssignedQueueSpotIndex} in {context.CurrentQueueMoveType} queue.", context.NpcObject);
            // ------------------------------

            // Note: Play waiting animation
            // context.PlayAnimation("WaitingOutside");

            // --- Phase 3, Substep 1: Initiate movement to the assigned secondary queue spot ---
             // The spot's transform was set as Runner.CurrentTargetLocation by TryJoinSecondaryQueue before the transition.
             // Access CurrentTargetLocation via Runner property on Context
            if (context.Runner.CurrentTargetLocation.HasValue && context.Runner.CurrentTargetLocation.Value.browsePoint != null && context.AssignedQueueSpotIndex != -1)
            {
                Transform assignedSpotTransform = context.Runner.CurrentTargetLocation.Value.browsePoint; // Get transform from Runner's target

                // Use context properties now
                Debug.Log($"{context.NpcObject.name}: Entering {name}. Moving to assigned spot {context.AssignedQueueSpotIndex} at {assignedSpotTransform.position} in {context.CurrentQueueMoveType} queue.", context.NpcObject);
                 // context.MoveToDestination handles setting _hasReachedCurrentDestination = false
                bool moveStarted = context.MoveToDestination(assignedSpotTransform.position);

                 // Use context properties now
                 if (!moveStarted) // Add check for move failure
                 {
                      Debug.LogError($"{context.NpcObject.name}: Failed to start movement to {context.CurrentQueueMoveType} queue spot {context.AssignedQueueSpotIndex}! Is the point on the NavMesh? Exiting.", context.NpcObject);
                      context.TransitionToState(GeneralState.ReturningToPool); // Fallback
                 }
                  // Note: No need to explicitly set _isMovingToQueueSpot here for the initial entry move.
            }
            else
            {
                // Use context properties now
                Debug.LogError($"{context.NpcObject.name}: Entering {name} state without a valid assigned queue spot target or index in context! Index: {context.AssignedQueueSpotIndex}. Target Valid: {context.Runner.CurrentTargetLocation.HasValue}. Exiting.", context.NpcObject);
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
                // Use context properties now
                Debug.Log($"{context.NpcObject.name}: IMPATIENT in {name} state at spot {context.AssignedQueueSpotIndex} in {context.CurrentQueueMoveType} queue after {impatientTimer:F2} seconds. Publishing NpcImpatientEvent.", context.NpcObject);
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

             // Use context properties now
             Debug.Log($"{context.NpcObject.name}: Reached {context.CurrentQueueMoveType} Queue spot {context.AssignedQueueSpotIndex} (detected by Runner). Stopping and Rotating.", context.NpcObject);

             // --- Phase 3, Substep 1: Start Rotation Logic (Migration from old BaseQueueLogic.OnUpdate) ---
             // Get the Transform of the currently assigned secondary queue spot using context helper
             // Use context properties now to get the index
             Transform currentQueueSpotTransform = context.Manager?.GetSecondaryQueuePoint(context.AssignedQueueSpotIndex); // Use Secondary getter

             if (currentQueueSpotTransform != null)
             {
                  Quaternion targetRotation = currentQueueSpotTransform.rotation;
                  Debug.Log($"CustomerAI ({context.NpcObject.name}): Starting rotation towards secondary queue spot rotation {targetRotation.eulerAngles} via MovementHandler.", context.NpcObject);
                  context.RotateTowardsTarget(targetRotation); // Use context helper
             }
             else
             {
                 // Use context properties now to get the index
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
             // Use context properties now
             if (context.AssignedQueueSpotIndex != -1)
             {
                  Debug.Log($"{context.NpcObject.name}: Exiting {name}. Publishing QueueSpotFreedEvent for spot {context.AssignedQueueSpotIndex} in {context.CurrentQueueMoveType} queue.", context.NpcObject);
                  context.PublishEvent(new QueueSpotFreedEvent(context.CurrentQueueMoveType, context.AssignedQueueSpotIndex));
             }
             else
             {
                  Debug.LogWarning($"{context.NpcObject.name}: Queue spot index not set when exiting {name} state! Cannot publish QueueSpotFreedEvent.", context.NpcObject);
             }
            // --- END Phase 3, Substep 1 ---


            // Example: Stop waiting animation
            // context.PlayAnimation("Idle");

            // --- Phase 3, Substep 1: REMOVE AssignedQueueSpotIndex reset from here ---
            // This is handled by NpcQueueHandler.Reset() called from Runner.ResetRunnerTransientData
            // context.Runner.AssignedQueueSpotIndex = -1; // REMOVED
            // --- END REMOVED ---
        }
    }
}
// --- END OF FILE CustomerSecondaryQueueStateSO.cs ---