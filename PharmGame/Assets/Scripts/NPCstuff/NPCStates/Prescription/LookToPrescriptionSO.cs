// --- START OF FILE LookToPrescriptionSO.cs (Modified for Debugging Queue Join) ---

using UnityEngine;
using System;
using System.Collections;
using Game.NPC.States; // Needed for NpcStateSO and NpcStateContext
using Game.NPC; // Needed for CustomerState and GeneralState enums
using Game.Prescriptions; // Needed for PrescriptionManager // <-- NEW: Added using directive
using CustomerManagement; // Needed for QueueType // <-- NEW: Added using directive

namespace Game.NPC.States // Place alongside other active states
{
    /// <summary>
    /// State for a Customer NPC immediately after universal initialization or arriving at the pharmacy area.
    /// Handles the customer-specific decision to check the prescription queue or enter the pharmacy claim spot.
    /// This is the effective 'starting state' for a Prescription Customer's behavior flow.
    /// Corresponds to CustomerState.LookToPrescription.
    /// MODIFIED: Added debug logging before attempting to join the queue.
    /// </summary>
    [CreateAssetMenu(fileName = "CustomerLookToPrescriptionState", menuName = "NPC/Customer States/Look To Prescription", order = 1)] // Order near Look To Shop
    public class LookToPrescriptionSO : NpcStateSO // Renamed from CustomerLookToShopStateSO for clarity in this flow
    {
        // Maps to the new enum value
        public override System.Enum HandledState => CustomerState.LookToPrescription;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // Logic: Immediately decide the *real* starting state for the prescription flow
            context.StartCoroutine(LookToPrescriptionRoutine(context)); // <-- Coroutine for decision logic
        }

        // OnUpdate remains empty or base call
        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context);
        }

        // OnReachedDestination is not applicable for this state
        public override void OnReachedDestination(NpcStateContext context) { /* Not applicable */ }


        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)
            // No specific cleanup needed
        }

        // Coroutine method for decision logic
        private IEnumerator LookToPrescriptionRoutine(NpcStateContext context)
        {
            Debug.Log($"{context.NpcObject.name}: LookToPrescriptionRoutine started in {name}.", context.NpcObject);

            // Wait one frame to ensure managers are fully initialized after activation/spawn
            yield return null;
            Debug.Log($"{context.NpcObject.name}: LookToPrescriptionRoutine finished processing wait.", context.NpcObject);

            // --- Decision logic based on PrescriptionManager state ---
            PrescriptionManager prescriptionManager = PrescriptionManager.Instance; // Get manager instance

            if (prescriptionManager == null)
            {
                 Debug.LogError($"{context.NpcObject.name}: PrescriptionManager.Instance is null! Cannot determine prescription flow. Transitioning to Exiting (fallback).", context.NpcObject);
                 context.TransitionToState(CustomerState.Exiting); // Fallback
                 yield break; // Stop coroutine
            }

            // Check if the prescription claim spot is occupied
            if (prescriptionManager.IsPrescriptionClaimSpotOccupied()) // Placeholder method, implemented in Phase 4
            {
                Debug.Log($"{context.NpcObject.name}: Prescription claim spot is occupied. Checking prescription queue status.", context.NpcObject);

                // If claim spot is occupied, check the prescription queue
                bool isQueueFull = prescriptionManager.IsPrescriptionQueueFull(); // Get the status

                if (isQueueFull)
                {
                    Debug.LogWarning($"{context.NpcObject.name}: Prescription claim spot is occupied AND prescription queue is full! Cannot proceed with prescription. Transitioning to Exiting.", context.NpcObject);
                    context.TransitionToState(CustomerState.Exiting); // Give up and exit
                }
                else
                {
                    // Claim spot is occupied, but queue has space. Attempt to join the queue.
                    Debug.Log($"{context.NpcObject.name}: Prescription claim spot is occupied, but queue has space (IsPrescriptionQueueFull reported {isQueueFull}). Attempting to join prescription queue.", context.NpcObject); // <-- Added log here
                    Transform assignedSpot;
                    int spotIndex;

                    // Attempt to join the prescription queue via the manager
                    // context.Runner is passed implicitly by context helper
                    if (context.TryJoinPrescriptionQueue(out assignedSpot, out spotIndex)) // Placeholder method, implemented in Phase 4
                    {
                        Debug.Log($"{context.NpcObject.name}: Successfully joined prescription queue at spot {spotIndex}. Transitioning to PrescriptionQueue.", context.NpcObject);
                        // The target position/index/type will be set on the QueueHandler by TryJoinPrescriptionQueue
                        // The movement will be initiated in PrescriptionQueueSO.OnEnter
                        context.TransitionToState(CustomerState.PrescriptionQueue); // Transition via context helper
                    }
                    else
                    {
                        // Should theoretically not happen if IsPrescriptionQueueFull is false, but defensive.
                        // The logs added to TryJoinPrescriptionQueue should now explain *why* it failed.
                        Debug.LogError($"{context.NpcObject.name}: PrescriptionManager.TryJoinPrescriptionQueue failed unexpectedly! Transitioning to Exiting.", context.NpcObject);
                        context.TransitionToState(CustomerState.Exiting); // Fallback
                    }
                }
            }
            else
            {
                // Prescription claim spot is free, proceed directly to it
                Debug.Log($"{context.NpcObject.name}: Prescription claim spot is free. Transitioning to PrescriptionEntering.", context.NpcObject);
                context.TransitionToState(CustomerState.PrescriptionEntering); // Transition via context helper
            }
            // ---------------------------------------------------------------

            Debug.Log($"{context.NpcObject.name}: LookToPrescriptionRoutine finished.", context.NpcObject);
        }
    }
}
// --- END OF FILE LookToPrescriptionSO.cs (Modified for Debugging Queue Join) ---