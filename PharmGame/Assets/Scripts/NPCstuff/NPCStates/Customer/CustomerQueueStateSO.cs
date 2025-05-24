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

            // --- Logic from CustomerQueueLogic.OnEnter (Migration) ---
            impatientDuration = Random.Range(impatientTimeRange.x, impatientTimeRange.y);
            impatientTimer = 0f;
            Debug.Log($"{context.NpcObject.name}: Starting impatience timer for {impatientDuration:F2} seconds.", context.NpcObject);
            // ------------------------------

            // Note: Play waiting/idle animation
            // context.PlayAnimation("WaitingInLine");

            // --- NEW: Initiate movement to the assigned queue spot ---
            if (context.CurrentTargetLocation.HasValue && context.CurrentTargetLocation.Value.browsePoint != null && context.AssignedQueueSpotIndex != -1)
            {
                Debug.Log($"{context.NpcObject.name}: Entering {name}. Moving to assigned spot {context.AssignedQueueSpotIndex} at {context.CurrentTargetLocation.Value.browsePoint.position}.", context.NpcObject);
                bool moveStarted = context.MoveToDestination(context.CurrentTargetLocation.Value.browsePoint.position);
                Debug.Log($"{context.NpcObject.name}: Called MoveToDestination from OnEnter. Result: {moveStarted}", context.NpcObject);
            }
            else
            {
                Debug.LogError($"{context.NpcObject.name}: Entering Main Queue state without a valid assigned queue spot in context! Exiting.", context.NpcObject);
                context.TransitionToState(CustomerState.Exiting); // Or ReturningToPool
            }
            // -------------------------------------------------------
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context);

            // --- Impatience Timer Update and Check (Migration) ---
            impatientTimer += Time.deltaTime;

            if (impatientTimer >= impatientDuration)
            {
                Debug.Log($"{context.NpcObject.name}: IMPATIENT in Main Queue state after {impatientTimer:F2} seconds. Publishing NpcImpatientEvent.", context.NpcObject);
                context.PublishEvent(new NpcImpatientEvent(context.NpcObject, CustomerState.Queue));
            }
            // -------------------------------------------

             // Check IsAtDestination logic is now in the Runner's Update.
             // The Runner calls OnReachedDestination when true.
        }

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context);

            impatientTimer = 0f;

            // Publish the QueueSpotFreedEvent using context helper
             if (context.AssignedQueueSpotIndex != -1)
             {
                  context.PublishEvent(new QueueSpotFreedEvent(QueueType.Main, context.AssignedQueueSpotIndex));
             }
             else
             {
                  Debug.LogWarning($"{context.NpcObject.name}: Queue spot index not set when exiting Main Queue state!", context.NpcObject);
             }

            // Example: Stop waiting animation
            // context.PlayAnimation("Idle");

             context.Runner.AssignedQueueSpotIndex = -1; // Reset index on Runner/Context
        }
        
        public override void OnReachedDestination(NpcStateContext context)
        {
            // Removed: context.MovementHandler?.StopMoving(); // Redundant, Runner does this

            Debug.Log($"{context.NpcObject.name}: Reached Queue spot {context.AssignedQueueSpotIndex} (detected by Runner). Stopping and Rotating.", context.NpcObject);


            // --- Start Rotation Logic ---
            Transform currentQueueSpotTransform;
            if (HandledState.Equals(CustomerState.Queue))
            {
                currentQueueSpotTransform = context.Manager?.GetQueuePoint(context.AssignedQueueSpotIndex);
            }
            else // Must be SecondaryQueue
            {
                currentQueueSpotTransform = context.Manager?.GetSecondaryQueuePoint(context.AssignedQueueSpotIndex);
            }


            if (currentQueueSpotTransform != null)
            {
                Quaternion targetRotation = currentQueueSpotTransform.rotation;
                Debug.Log($"CustomerAI ({context.NpcObject.name}): Starting rotation towards queue spot rotation {targetRotation.eulerAngles} via MovementHandler.", context.NpcObject);
                context.RotateTowardsTarget(targetRotation); // Use context helper
            }
            else
            {
                Debug.LogWarning($"CustomerAI ({context.NpcObject.name}): Could not get Queue spot Transform {context.AssignedQueueSpotIndex} for rotation!", context.NpcObject);
            }
            // --- End Rotation Logic ---

            // No state transition needed here, they just wait in this state.
            // The next transition (to MovingToRegister or Exiting) is triggered externally
            // by the CustomerManager receiving a CashRegisterFreeEvent or NpcImpatientEvent.
        }
    }
}