// --- START OF FILE CustomerQueueStateSO.cs ---

using UnityEngine;
using System.Collections;
using System;
using CustomerManagement;
using Game.NPC;
using Game.Events;
using Game.NPC.States;
using Random = UnityEngine.Random;

namespace Game.NPC.States
{
    [CreateAssetMenu(fileName = "CustomerQueueState", menuName = "NPC/Customer States/Main Queue", order = 7)]
    public class CustomerQueueStateSO : NpcStateSO
    {
        public override System.Enum HandledState => CustomerState.Queue;


        [Header("Queue Settings")]
        [SerializeField] private Vector2 impatientTimeRange = new Vector2(10f, 15f);

        private float impatientTimer;
        private float impatientDuration;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context);

            // --- Impatience Timer Setup (Migration & MODIFIED for Music License) ---
            Vector2 localTimeRange = impatientTimeRange;
            if (context.UpgradeManager != null && context.UpgradeManager.IsMusicLicensePurchased())
            {
                localTimeRange.x *= UpgradeManager.MusicLicensePatienceModifier;
                localTimeRange.y *= UpgradeManager.MusicLicensePatienceModifier;
            }
            impatientDuration = Random.Range(localTimeRange.x, localTimeRange.y);
            impatientTimer = 0f;
            
            Debug.Log($"{context.NpcObject.name}: Entering {name}. Starting impatience timer for {impatientDuration:F2} seconds at spot {context.AssignedQueueSpotIndex} in {context.CurrentQueueMoveType} queue.", context.NpcObject);

            // Note: Play waiting/idle animation
            // context.PlayAnimation("WaitingInLine");

            // --- Initiate movement to the assigned queue spot ---
            // The spot's transform was set as Runner.CurrentTargetLocation by TryJoinQueue before the transition.
            // Access CurrentTargetLocation via Runner property on Context
            if (context.Runner.CurrentTargetLocation.HasValue && context.Runner.CurrentTargetLocation.Value.browsePoint != null && context.AssignedQueueSpotIndex != -1)
            {
                // Access CurrentTargetLocation via Runner property on Context
                Transform assignedSpotTransform = context.Runner.CurrentTargetLocation.Value.browsePoint; // Get transform from Runner's target

                // Use context properties now
                Debug.Log($"{context.NpcObject.name}: Entering {name}. Moving to assigned spot {context.AssignedQueueSpotIndex} at {assignedSpotTransform.position} in {context.CurrentQueueMoveType} queue.", context.NpcObject);
                // context.MoveToDestination handles setting _hasReachedCurrentDestination = false
                bool moveStarted = context.MoveToDestination(assignedSpotTransform.position);

                // Use context properties now
                if (!moveStarted) // Add check for move failure from SetDestination
                {
                     Debug.LogError($"{context.NpcObject.name}: Failed to start movement to {context.CurrentQueueMoveType} queue spot {context.AssignedQueueSpotIndex}! Is the point on the NavMesh? Exiting.", context.NpcObject);
                     context.TransitionToState(CustomerState.Exiting); // Fallback on movement failure
                     // The Runner's TransitionToState will handle stopping movement and resetting Runner flags.
                }
                 // Note: No need to explicitly set _isMovingToQueueSpot here, Runner.MoveToQueueSpot handles that
                 // when called *by the Manager* to move up the line. When *entering* the state,
                 // the initial move is just "move to spot X". The Runner's arrival logic will
                 // call OnReachedDestination, but won't trigger FreePreviousQueueSpotOnArrival
                 // because _isMovingToQueueSpot is false initially.
            }
            else
            {
                // Use context properties now
                Debug.LogError($"{context.NpcObject.name}: Entering {name} state without a valid assigned queue spot target or index in context! Index: {context.AssignedQueueSpotIndex}. Target Valid: {context.Runner.CurrentTargetLocation.HasValue}. Exiting.", context.NpcObject);
                context.TransitionToState(CustomerState.Exiting); // Fallback
                // The Runner's TransitionToState will handle stopping movement and resetting Runner flags.
            }
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
                context.PublishEvent(new NpcImpatientEvent(context.NpcObject, CustomerState.Queue));
                // The Runner's handler for this event will transition the state.
            }

             // Check IsAtDestination logic is now in the Runner's Update.
             // The Runner calls OnReachedDestination when true.
        }
        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context);

            impatientTimer = 0f;

            // --- Publish the QueueSpotFreedEvent & Clean Up ---
            if (context.AssignedQueueSpotIndex != -1)
            {
                // Manually free the spot in the appropriate Manager BEFORE publishing the event.          
                if (context.CurrentQueueMoveType == QueueType.Main || context.CurrentQueueMoveType == QueueType.Secondary)
                {
                    if (context.Manager != null)
                    {
                        Debug.Log($"{context.NpcObject.name}: Exiting {name}. Manually freeing spot {context.AssignedQueueSpotIndex} in {context.CurrentQueueMoveType} queue via CustomerManager.", context.NpcObject);
                        context.Manager.FreePreviousQueueSpotOnArrival(context.CurrentQueueMoveType, context.AssignedQueueSpotIndex);
                    }
                }
                else if (context.CurrentQueueMoveType == QueueType.Prescription)
                {
                    // Use the property on context to access PrescriptionManager
                    if (context.PrescriptionManager != null)
                    {
                        Debug.Log($"{context.NpcObject.name}: Exiting {name}. Manually freeing spot {context.AssignedQueueSpotIndex} in {context.CurrentQueueMoveType} queue via PrescriptionManager.", context.NpcObject);
                        context.PrescriptionManager.FreePreviousPrescriptionQueueSpotOnArrival(context.CurrentQueueMoveType, context.AssignedQueueSpotIndex);
                    }
                }

                // Now safe to publish the event to trigger the cascade (moving other NPCs up)
                Debug.Log($"{context.NpcObject.name}: Exiting {name}. Publishing QueueSpotFreedEvent for spot {context.AssignedQueueSpotIndex} in {context.CurrentQueueMoveType} queue.", context.NpcObject);
                context.PublishEvent(new QueueSpotFreedEvent(context.CurrentQueueMoveType, context.AssignedQueueSpotIndex));

                // Clear the internal assignment on the QueueHandler so the NPC knows it's no longer queued.
                if (context.QueueHandler != null)
                {
                    context.QueueHandler.ClearQueueAssignment();
                }
            }
            else
            {
                Debug.LogWarning($"{context.NpcObject.name}: Queue spot index not set when exiting {name} state! Cannot publish QueueSpotFreedEvent.", context.NpcObject);
            }

            // Example: Stop waiting animation
            // context.PlayAnimation("Idle");
        }

        public override void OnReachedDestination(NpcStateContext context) // <-- Implement OnReachedDestination if CheckMovementArrival is true
        {
            // This logic happens when the NPC reaches their assigned spot in the queue line.
            // Removed: context.MovementHandler?.StopMoving(); // Redundant, Runner does this

            // Use context properties now
            Debug.Log($"{context.NpcObject.name}: Reached {context.CurrentQueueMoveType} Queue spot {context.AssignedQueueSpotIndex} (detected by Runner). Stopping and Rotating.", context.NpcObject);

            // --- Phase 3, Substep 1: Start Rotation Logic ---
            // Get the Transform of the currently assigned main queue spot using context helper
            // Use context properties now to get the index
            Transform currentQueueSpotTransform = context.Manager?.GetQueuePoint(context.AssignedQueueSpotIndex); // Use correct getter

            if (currentQueueSpotTransform != null)
            {
                Quaternion targetRotation = currentQueueSpotTransform.rotation;
                Debug.Log($"CustomerAI ({context.NpcObject.name}): Starting rotation towards queue spot rotation {targetRotation.eulerAngles} via MovementHandler.", context.NpcObject);
                context.RotateTowardsTarget(targetRotation); // Use context helper
            }
            else
            {
                // Use context properties now to get the index
                Debug.LogWarning($"CustomerAI ({context.NpcObject.name}): Could not get Main Queue spot Transform {context.AssignedQueueSpotIndex} for rotation!", context.NpcObject);
            }
            // --- End Rotation Logic ---

            // No state transition needed here, they just wait in this state.
            // The next transition (to MovingToRegister or Exiting) is triggered externally
            // by the CustomerManager receiving a CashRegisterFreeEvent or NpcImpatientEvent.
        }
    }
}
// --- END OF FILE CustomerQueueStateSO.cs ---