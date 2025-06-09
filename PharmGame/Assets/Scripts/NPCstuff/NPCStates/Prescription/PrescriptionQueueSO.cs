// --- START OF FILE PrescriptionQueueSO.cs (Fixed Redundant Queue Join) ---

using UnityEngine;
using System;
using Game.NPC.States; // Needed for NpcStateSO and NpcStateContext
using Game.NPC; // Needed for CustomerState and GeneralState enums
using Game.Prescriptions; // Needed for PrescriptionManager
using Game.Events; // Needed for new event
using CustomerManagement; // Needed for QueueType

namespace Game.NPC.States // Place alongside other active states
{
    /// <summary>
    /// State for a Prescription Customer waiting in the prescription queue.
    /// Corresponds to CustomerState.PrescriptionQueue.
    /// MODIFIED: Removed redundant queue joining logic in OnEnter.
    /// </summary>
    [CreateAssetMenu(fileName = "CustomerPrescriptionQueueState", menuName = "NPC/Customer States/Prescription Queue", order = 4)] // Order after Waiting For Prescription
    public class PrescriptionQueueSO : NpcStateSO
    {
        public override System.Enum HandledState => CustomerState.PrescriptionQueue;

        // Note: Impatience timer for queue waiting is handled by the BasicState simulation
        // when the NPC is inactive. For active NPCs, impatience might be handled here,
        // but the vision doesn't specify impatience *in the queue* for active prescription NPCs,
        // only when WaitingForPrescription. Let's omit active impatience for now.


        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // --- Get the assigned queue spot information from the NpcQueueHandler --- // <-- MODIFIED LOGIC
            int assignedSpotIndex = context.AssignedQueueSpotIndex; // Get index from context helper
            QueueType assignedQueueType = context.CurrentQueueMoveType; // Get type from context helper

            // Validate that the NPC has a valid prescription queue assignment
            if (assignedSpotIndex == -1 || assignedQueueType != QueueType.Prescription)
            {
                 Debug.LogError($"{context.NpcObject.name}: Entering {name} state but does NOT have a valid prescription queue assignment (Index: {assignedSpotIndex}, Type: {assignedQueueType})! This indicates a flow error. Transitioning to Exiting.", context.NpcObject);
                 context.TransitionToState(CustomerState.Exiting); // Fallback on invalid assignment
                 return; // Exit OnEnter early
            }

            // Get the transform for the assigned queue spot from the PrescriptionManager
            PrescriptionManager prescriptionManager = PrescriptionManager.Instance; // Get manager instance

            if (prescriptionManager == null)
            {
                 Debug.LogError($"{context.NpcObject.name}: PrescriptionManager.Instance is null! Cannot get assigned queue spot transform. Transitioning to Exiting (fallback).", context.NpcObject);
                 context.TransitionToState(CustomerState.Exiting); // Fallback
                 return; // Exit OnEnter early
            }

            Transform assignedSpotTransform = prescriptionManager.GetPrescriptionQueuePoint(assignedSpotIndex); // Use manager method

            if (assignedSpotTransform != null)
            {
                 // We have a valid assignment and the spot transform. Initiate movement to the assigned spot.
                 Debug.Log($"{context.NpcObject.name}: Entering {name}. Moving to assigned spot {assignedSpotIndex} at {assignedSpotTransform.position} in {assignedQueueType} queue.", context.NpcObject);

                 // context.MoveToDestination handles setting _hasReachedCurrentDestination = false and CurrentDestinationPosition
                 bool moveStarted = context.MoveToDestination(assignedSpotTransform.position);

                 if (!moveStarted) // Add check for move failure
                 {
                      Debug.LogError($"{context.NpcObject.name}: Failed to start movement to Prescription Queue spot {assignedSpotIndex}! Is the point on the NavMesh?", context.NpcObject);
                       Debug.LogWarning($"PrescriptionQueueSO ({context.NpcObject.name}): Movement failed, falling back to Exiting.", context.NpcObject);
                       context.TransitionToState(CustomerState.Exiting); // Fallback on movement failure
                       // The Runner's TransitionToState will handle stopping movement and resetting Runner flags.
                 }
                 // Note: No need to explicitly set _isMovingToQueueSpot here, Runner.MoveToQueueSpot handles that
                 // when called *by the Manager* to move up the line. When *entering* the state,
                 // the initial move is just "move to spot X". The Runner's arrival logic will
                 // call OnReachedDestination, but won't trigger FreePreviousPrescriptionQueueSpotOnArrival
                 // because _isMovingToQueueSpot is false initially.
            }
            else
            {
                 // The assigned spot transform was not found (e.g., invalid index or WaypointManager issue)
                 Debug.LogError($"{context.NpcObject.name}: Assigned Prescription Queue spot transform for index {assignedSpotIndex} is null! Cannot move. Transitioning to Exiting.", context.NpcObject);
                 Debug.LogWarning($"PrescriptionQueueSO ({context.NpcObject.name}): Assigned spot transform not found, falling back to Exiting.", context.NpcObject);
                 context.TransitionToState(CustomerState.Exiting); // Fallback
                 // The Runner's TransitionToState will handle stopping movement and resetting Runner flags.
            }
            // --- END MODIFIED LOGIC ---
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context); // Call base OnUpdate (empty)
            // Movement and arrival are handled by the Runner and OnReachedDestination.
            // Impatience timer is handled by BasicState simulation when inactive.
        }

        public override void OnReachedDestination(NpcStateContext context) // Called by Runner when NavMesh destination is reached
        {
            // This logic happens when the NPC reaches their assigned spot in the prescription queue line.
            // Removed: context.MovementHandler?.StopMoving(); // Redundant, Runner does this

            // Use context properties now
            Debug.Log($"{context.NpcObject.name}: Reached Prescription Queue spot {context.AssignedQueueSpotIndex} (detected by Runner). Stopping and Rotating.", context.NpcObject);

            // Start Rotation Logic: Rotate towards the next spot in the queue or the claim point if at spot 0.
            Transform targetRotationTransform = null;
            PrescriptionManager prescriptionManager = PrescriptionManager.Instance;

            if (prescriptionManager != null)
            {
                 if (context.AssignedQueueSpotIndex == 0)
                 {
                      // If at the front of the queue, rotate towards the claim point
                      targetRotationTransform = prescriptionManager.GetPrescriptionClaimPoint(); // Placeholder method
                 }
                 else if (context.AssignedQueueSpotIndex > 0)
                 {
                      // If not at the front, rotate towards the spot in front of them (index - 1)
                      // Need a method in PrescriptionManager to get a queue point by index
                      // Let's add GetPrescriptionQueuePoint(int index)
                      // For now, use a placeholder or assume the list is accessible (less ideal).
                      // Assuming PrescriptionManager will have GetPrescriptionQueuePoint(int index)
                      targetRotationTransform = prescriptionManager.GetPrescriptionQueuePoint(context.AssignedQueueSpotIndex - 1); // Use manager method
                       // For now, let's just rotate towards the claim point as a simpler default
                       // targetRotationTransform = prescriptionManager.GetPrescriptionClaimPoint(); // Simpler placeholder rotation target
                 }
            }

            if (targetRotationTransform != null)
            {
                Quaternion targetRotation = targetRotationTransform.rotation;
                Debug.Log($"CustomerAI ({context.NpcObject.name}): Starting rotation towards queue target rotation {targetRotation.eulerAngles} via MovementHandler.", context.NpcObject);
                context.RotateTowardsTarget(targetRotation); // Use context helper
            }
            else
            {
                Debug.LogWarning($"CustomerAI ({context.NpcObject.name}): Could not get Prescription Queue rotation target for spot {context.AssignedQueueSpotIndex}! PrescriptionManager or target transform is null.", context.NpcObject); // Improved log
            }

            // No state transition needed here, they just wait in this state.
            // The next transition (to PrescriptionEntering or Exiting) is triggered externally
            // by the PrescriptionManager receiving a FreePrescriptionClaimSpotEvent or NpcImpatientEvent (if implemented).
        }

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)

            // Note: Stop waiting animation
            // context.PlayAnimation("Idle");

            // --- Publish the QueueSpotFreedEvent for the prescription queue ---
             // This event must be published when the NPC EXITS this state, regardless of the reason.
             // The PrescriptionManager will receive this event, free the spot, and start the cascade if needed.
             // Use context properties now
             if (context.AssignedQueueSpotIndex != -1)
             {
                  Debug.Log($"{context.NpcObject.name}: Exiting {name}. Publishing QueueSpotFreedEvent for spot {context.AssignedQueueSpotIndex} in {context.CurrentQueueMoveType} queue.", context.NpcObject);
                  // Ensure the queue type is correct (should be Prescription if they were in this state)
                  context.PublishEvent(new QueueSpotFreedEvent(QueueType.Prescription, context.AssignedQueueSpotIndex)); // Use QueueType.Prescription directly
             }
             else
             {
                  Debug.LogWarning($"{context.NpcObject.name}: Queue spot index not set when exiting {name} state! Cannot publish QueueSpotFreedEvent.", context.NpcObject);
             }

            // --- Clear assigned queue spot index and type on the QueueHandler ---
            // This is handled by NpcQueueHandler.Reset() called from Runner.ResetRunnerTransientData
            // when the NPC is pooled/deactivated. If the NPC transitions to another state *without* pooling,
            // the QueueHandler's state should be reset by the handler itself or the Runner's reset logic.
            context.QueueHandler?.ClearQueueAssignment(); // Use the ClearQueueAssignment method on the handler
        }
    }
}
// --- END OF FILE PrescriptionQueueSO.cs (Fixed Redundant Queue Join) ---