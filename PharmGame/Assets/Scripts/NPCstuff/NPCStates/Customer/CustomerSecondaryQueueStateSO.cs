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

            if (context.Manager != null)
            {
                context.Manager.CheckStoreCapacityAndReleaseSecondaryCustomer();
            }

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

            // Note: Play waiting animation
            // context.PlayAnimation("WaitingOutside");

            // --- Initiate movement to the assigned secondary queue spot ---
             // The spot's transform was set as Runner.CurrentTargetLocation by TryJoinSecondaryQueue before the transition.
             // Access CurrentTargetLocation via Runner property on Context
            if (context.Runner.CurrentTargetLocation.HasValue && context.Runner.CurrentTargetLocation.Value.browsePoint != null && context.AssignedQueueSpotIndex != -1)
            {
                Transform assignedSpotTransform = context.Runner.CurrentTargetLocation.Value.browsePoint; // Get transform from Runner's target

                Debug.Log($"{context.NpcObject.name}: Entering {name}. Moving to assigned spot {context.AssignedQueueSpotIndex} at {assignedSpotTransform.position} in {context.CurrentQueueMoveType} queue.", context.NpcObject);
                 // context.MoveToDestination handles setting _hasReachedCurrentDestination = false
                bool moveStarted = context.MoveToDestination(assignedSpotTransform.position);

                 if (!moveStarted) 
                 {
                      Debug.LogError($"{context.NpcObject.name}: Failed to start movement to {context.CurrentQueueMoveType} queue spot {context.AssignedQueueSpotIndex}! Is the point on the NavMesh? Exiting.", context.NpcObject);
                      context.TransitionToState(GeneralState.ReturningToPool);
                 }
            }
            else
            {
                Debug.LogError($"{context.NpcObject.name}: Entering {name} state without a valid assigned queue spot target or index in context! Index: {context.AssignedQueueSpotIndex}. Target Valid: {context.Runner.CurrentTargetLocation.HasValue}. Exiting.", context.NpcObject);
                context.TransitionToState(GeneralState.ReturningToPool); 
            }
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context);

            // --- Impatience Timer Update and Check ---
            impatientTimer += Time.deltaTime; 

            if (impatientTimer >= impatientDuration)
            {
                Debug.Log($"{context.NpcObject.name}: IMPATIENT in {name} state at spot {context.AssignedQueueSpotIndex} in {context.CurrentQueueMoveType} queue after {impatientTimer:F2} seconds. Publishing NpcImpatientEvent.", context.NpcObject);
                context.PublishEvent(new NpcImpatientEvent(context.NpcObject, CustomerState.SecondaryQueue));
            }
        }

        public override void OnReachedDestination(NpcStateContext context) 
        {
             Debug.Log($"{context.NpcObject.name}: Reached {context.CurrentQueueMoveType} Queue spot {context.AssignedQueueSpotIndex} (detected by Runner). Stopping and Rotating.", context.NpcObject);

             // --- Start Rotation Logic (Migration from old BaseQueueLogic.OnUpdate) ---
             Transform currentQueueSpotTransform = context.Manager?.GetSecondaryQueuePoint(context.AssignedQueueSpotIndex);

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
        }


        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context);

            impatientTimer = 0f;

            context.Runner.CurrentTargetLocation = null;

            // --- Publish the QueueSpotFreedEvent ---
             if (context.AssignedQueueSpotIndex != -1)
             {
                  Debug.Log($"{context.NpcObject.name}: Exiting {name}. Publishing QueueSpotFreedEvent for spot {context.AssignedQueueSpotIndex} in {context.CurrentQueueMoveType} queue.", context.NpcObject);
                  context.PublishEvent(new QueueSpotFreedEvent(context.CurrentQueueMoveType, context.AssignedQueueSpotIndex));
             }
             else
             {
                  Debug.LogWarning($"{context.NpcObject.name}: Queue spot index not set when exiting {name} state! Cannot publish QueueSpotFreedEvent.", context.NpcObject);
             }
            // Example: Stop waiting animation
            // context.PlayAnimation("Idle");
        }
    }
}
// --- END OF FILE CustomerSecondaryQueueStateSO.cs ---