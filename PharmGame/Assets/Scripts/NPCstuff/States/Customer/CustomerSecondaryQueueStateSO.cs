// --- Updated CustomerSecondaryQueueStateSO.cs ---
using UnityEngine;
using System.Collections;
using CustomerManagement;
using System;
using Game.NPC;
using Game.Events;
using Game.NPC.States;
using Random = UnityEngine.Random;

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

            // --- Logic from CustomerSecondaryQueueLogic.OnEnter (Migration) ---
            impatientDuration = Random.Range(impatientTimeRange.x, impatientTimeRange.y); // Use SO field
            impatientTimer = 0f;
            Debug.Log($"{context.NpcObject.name}: Starting impatience timer for {impatientDuration:F2} seconds.", context.NpcObject);
            // ------------------------------

            // Note: Play waiting animation
            // context.PlayAnimation("WaitingOutside");

             // *** IMPORTANT RECONCILIATION ***
             // The logic for moving to the assigned secondary queue spot was in BaseQueueLogic component's OnEnter.
             // That component is still on the GO for now. When it's removed in a later phase,
             // this logic MUST be here or triggered by the Runner based on the context.AssignedQueueSpotIndex.
             // Same as Main Queue state, for THIS step, leaving initial movement to old component's Update.
             /*
             if (context.CurrentTargetLocation.HasValue && context.CurrentTargetLocation.Value.browsePoint != null && context.AssignedQueueSpotIndex != -1)
             {
                 context.MoveToDestination(context.CurrentTargetLocation.Value.browsePoint.position);
             }
             else
             {
                 Debug.LogError($"{context.NpcObject.name}: Entering Secondary Queue state without a valid assigned queue spot in context! Exiting.", context.NpcObject);
                 context.TransitionToState(CustomerState.ReturningToPool);
             }
             */
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context);

            // --- Impatience Timer Update and Check (Migration) ---
            impatientTimer += Time.deltaTime;

            if (impatientTimer >= impatientDuration)
            {
                Debug.Log($"{context.NpcObject.name}: IMPATIENT in Secondary Queue state after {impatientTimer:F2} seconds. Publishing NpcImpatientEvent.", context.NpcObject);
                context.PublishEvent(new NpcImpatientEvent(context.NpcObject, CustomerState.SecondaryQueue));
            }
            // -------------------------------------------

             // Check IsAtDestination logic is now in the Runner's Update.
             // The Runner calls OnReachedDestination when true.
        }
        
        public override void OnReachedDestination(NpcStateContext context) // <-- Implement OnReachedDestination
        {
             // This logic was triggered from the old BaseQueueLogic.OnUpdate after reaching destination.
             Debug.Log($"{context.NpcObject.name}: Reached Secondary Queue spot {context.AssignedQueueSpotIndex} (detected by Runner). Stopping and Rotating.", context.NpcObject);

             context.MovementHandler?.StopMoving(); // Explicitly stop movement again after Runner detects arrival

             // --- Start Rotation Logic (Migration from old BaseQueueLogic.OnUpdate) ---
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

            // Publish the QueueSpotFreedEvent using context helper
             if (context.AssignedQueueSpotIndex != -1)
             {
                  context.PublishEvent(new QueueSpotFreedEvent(QueueType.Secondary, context.AssignedQueueSpotIndex));
             }
             else
             {
                  Debug.LogWarning($"{context.NpcObject.name}: Queue spot index not set when exiting Secondary Queue state!", context.NpcObject);
             }

            // Example: Stop waiting animation
            // context.PlayAnimation("Idle");

             context.Runner.AssignedQueueSpotIndex = -1; // Reset index on Runner/Context
        }
    }
}