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

            // --- Impatience Timer Setup (Migration) ---
            impatientDuration = Random.Range(impatientTimeRange.x, impatientTimeRange.y);
            impatientTimer = 0f;
            Debug.Log($"{context.NpcObject.name}: Entering {name}. Starting impatience timer for {impatientDuration:F2} seconds at spot {context.AssignedQueueSpotIndex}.", context.NpcObject);
            // ------------------------------

            // Note: Play waiting/idle animation
            // context.PlayAnimation("WaitingInLine");

            // --- Phase 3, Substep 1: Initiate movement to the assigned queue spot ---
            // The spot's transform was set as Runner.CurrentTargetLocation by TryJoinQueue before the transition.
            if (context.Runner.CurrentTargetLocation.HasValue && context.Runner.CurrentTargetLocation.Value.browsePoint != null && context.AssignedQueueSpotIndex != -1)
            {
                Transform assignedSpotTransform = context.Runner.CurrentTargetLocation.Value.browsePoint; // Get transform from Runner's target

                Debug.Log($"{context.NpcObject.name}: Entering {name}. Moving to assigned spot {context.AssignedQueueSpotIndex} at {assignedSpotTransform.position}.", context.NpcObject);
                // context.MoveToDestination handles setting _hasReachedCurrentDestination = false
                bool moveStarted = context.MoveToDestination(assignedSpotTransform.position);

                if (!moveStarted) // Add check for move failure from SetDestination
                {
                     Debug.LogError($"{context.NpcObject.name}: Failed to start movement to main queue spot {context.AssignedQueueSpotIndex}! Is the point on the NavMesh? Exiting.", context.NpcObject);
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
                Debug.LogError($"{context.NpcObject.name}: Entering Main Queue state without a valid assigned queue spot target in context! Exiting.", context.NpcObject);
                context.TransitionToState(CustomerState.Exiting); // Fallback
                // The Runner's TransitionToState will handle stopping movement and resetting Runner flags.
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
                Debug.Log($"{context.NpcObject.name}: IMPATIENT in Main Queue state at spot {context.AssignedQueueSpotIndex} after {impatientTimer:F2} seconds. Publishing NpcImpatientEvent.", context.NpcObject);
                context.PublishEvent(new NpcImpatientEvent(context.NpcObject, CustomerState.Queue));
                // The Runner's handler for this event will transition the state.
            }
            // -------------------------------------------

             // Check IsAtDestination logic is now in the Runner's Update.
             // The Runner calls OnReachedDestination when true.
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
                  Debug.Log($"{context.NpcObject.name}: Exiting {name}. Publishing QueueSpotFreedEvent for spot {context.AssignedQueueSpotIndex} in Main queue.", context.NpcObject);
                  context.PublishEvent(new QueueSpotFreedEvent(QueueType.Main, context.AssignedQueueSpotIndex));
             }
             else
             {
                  Debug.LogWarning($"{context.NpcObject.name}: Queue spot index not set when exiting Main Queue state! Cannot publish QueueSpotFreedEvent.", context.NpcObject);
             }
            // --- END Phase 3, Substep 1 ---

            // Example: Stop waiting animation
            // context.PlayAnimation("Idle");

            // --- Phase 3, Substep 1: REMOVE AssigedQueueSpotIndex reset from here ---
            // context.Runner.AssignedQueueSpotIndex = -1; // REMOVED - Handled by Runner.TransitionToState/ResetNPCData
            // --- END REMOVED ---
        }

        public override void OnReachedDestination(NpcStateContext context) // <-- Implement OnReachedDestination if CheckMovementArrival is true
        {
            // This logic happens when the NPC reaches their assigned spot in the queue line.
            // Removed: context.MovementHandler?.StopMoving(); // Redundant, Runner does this

            Debug.Log($"{context.NpcObject.name}: Reached Queue spot {context.AssignedQueueSpotIndex} (detected by Runner). Stopping and Rotating.", context.NpcObject);

            // --- Phase 3, Substep 1: Start Rotation Logic ---
            // Get the Transform of the currently assigned main queue spot using context helper
            Transform currentQueueSpotTransform = context.Manager?.GetQueuePoint(context.AssignedQueueSpotIndex); // Use correct getter

            if (currentQueueSpotTransform != null)
            {
                Quaternion targetRotation = currentQueueSpotTransform.rotation;
                Debug.Log($"CustomerAI ({context.NpcObject.name}): Starting rotation towards queue spot rotation {targetRotation.eulerAngles} via MovementHandler.", context.NpcObject);
                context.RotateTowardsTarget(targetRotation); // Use context helper
            }
            else
            {
                Debug.LogWarning($"CustomerAI ({context.NpcObject.name}): Could not get Main Queue spot Transform {context.AssignedQueueSpotIndex} for rotation!", context.NpcObject);
            }
            // --- End Rotation Logic ---

            // No state transition needed here, they just wait in this state.
            // The next transition (to MovingToRegister or Exiting) is triggered externally
            // by the CustomerManager receiving a CashRegisterFreeEvent or NpcImpatientEvent.
        }
    }
}