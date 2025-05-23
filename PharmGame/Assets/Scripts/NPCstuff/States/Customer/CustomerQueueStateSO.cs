// --- Updated CustomerQueueStateSO.cs ---
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

            // *** IMPORTANT RECONCILIATION FOR QUEUE MOVEMENT ***
            // The initial movement to the assigned spot happens BEFORE entering this state
            // in CustomerInitializingStateSO or CustomerBrowseStateSO, which sets the Runner's
            // CurrentTargetLocation and AssignedQueueSpotIndex and calls context.MoveToDestination.
            // This state SO's OnUpdate and OnReachedDestination handle moving *within* the queue line.
            // The old BaseQueueLogic component is now removed.
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
        
        public override void OnReachedDestination(NpcStateContext context) // <-- Implement OnReachedDestination
        {
             // This logic was triggered from the old BaseQueueLogic.OnUpdate after reaching destination.
             Debug.Log($"{context.NpcObject.name}: Reached Main Queue spot {context.AssignedQueueSpotIndex} (detected by Runner). Stopping and Rotating.", context.NpcObject);

             context.MovementHandler?.StopMoving(); // Explicitly stop movement again after Runner detects arrival

             // --- Start Rotation Logic (Migration from old BaseQueueLogic.OnUpdate) ---
             // Get the Transform of the currently assigned queue spot using context helper
             Transform currentQueueSpotTransform = context.Manager?.GetQueuePoint(context.AssignedQueueSpotIndex);

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

             // OnReachedEndOfQueue logic from the old BaseQueueLogic (which was abstract).
             // For main queue, reaching the spot just means waiting for the Manager to call GoToRegisterFromQueue (on the Runner).
             // So no action needed here besides stopping and rotating.
        }
    }
}