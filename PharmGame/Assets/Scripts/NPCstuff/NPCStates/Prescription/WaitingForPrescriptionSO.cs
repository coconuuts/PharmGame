// --- START OF FILE WaitingForPrescriptionSO.cs ---

using UnityEngine;
using System;
using System.Collections;
using Game.NPC.States; // Needed for NpcStateSO and NpcStateContext
using Game.NPC; // Needed for CustomerState and GeneralState enums
using Game.Prescriptions; // Needed for PrescriptionOrder // <-- NEW: Added using directive
using Random = UnityEngine.Random; // Specify UnityEngine.Random
using Game.Events; // Needed for new event // <-- NEW: Added using directive

namespace Game.NPC.States // Place alongside other active states
{
    /// <summary>
    /// State for a Prescription Customer waiting at the prescription claim spot.
    /// An impatience timer runs, after which the NPC exits.
    /// Corresponds to CustomerState.WaitingForPrescription.
    /// </summary>
    [CreateAssetMenu(fileName = "CustomerWaitingForPrescriptionState", menuName = "NPC/Customer States/Waiting For Prescription", order = 3)] // Order after Prescription Entering
    public class WaitingForPrescriptionSO : NpcStateSO
    {
        public override System.Enum HandledState => CustomerState.WaitingForPrescription;


        [Header("Waiting Settings")]
        [Tooltip("Minimum and maximum time (real-time seconds) the NPC will wait before becoming impatient.")]
        [SerializeField] private Vector2 impatientTimeRange = new Vector2(15f, 30f); // Adjust range as needed

        private float impatientTimer;
        private float impatientDuration;

        private Coroutine waitingRoutine; // Coroutine for the timer

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // Stop any residual movement from reaching the spot
            context.MovementHandler?.StopMoving();
            context.MovementHandler?.StopRotation(); // Stop rotation coroutine if it was still running from arrival

            // Play waiting/idle animation
            // context.PlayAnimation("WaitingAtCounter"); // Placeholder animation name

            // Start impatience timer coroutine
            impatientDuration = Random.Range(impatientTimeRange.x, impatientTimeRange.y);
            impatientTimer = 0f; // Timer is managed by the coroutine now
            Debug.Log($"{context.NpcObject.name}: Entering {name}. Starting impatience timer for {impatientDuration:F2} seconds.", context.NpcObject);

            waitingRoutine = context.StartCoroutine(WaitingRoutine(context)); // Start the timer coroutine

            // --- Log Prescription Order Data --- // <-- NEW LOGIC
            PrescriptionOrder assignedOrder;
            bool hasOrder = false;

            if (context.Runner != null && context.Runner.IsTrueIdentityNpc && context.TiData != null)
            {
                 // TI NPC: Get order from TiData
                 assignedOrder = context.TiData.assignedOrder; // Struct copy
                 hasOrder = context.TiData.pendingPrescription; // Check the flag
                 Debug.Log($"{context.NpcObject.name}: TI NPC. Has Pending Prescription: {hasOrder}. Order Data: {assignedOrder.ToString()}", context.NpcObject);
            }
            else if (context.Runner != null && !context.Runner.IsTrueIdentityNpc)
            {
                 // Transient NPC: Get order from Runner's temporary fields
                 assignedOrder = context.Runner.assignedOrderTransient; // Struct copy
                 hasOrder = context.Runner.hasPendingPrescriptionTransient; // Check the flag
                 Debug.Log($"{context.NpcObject.name}: Transient NPC. Has Pending Prescription: {hasOrder}. Order Data: {assignedOrder.ToString()}", context.NpcObject);
            }
            else
            {
                 Debug.LogError($"{context.NpcObject.name}: WaitingForPrescriptionSO OnEnter: Runner or TiData is null! Cannot access assigned order data.", context.NpcObject);
                 hasOrder = false; // Cannot confirm order
            }

            if (!hasOrder)
            {
                 Debug.LogWarning($"{context.NpcObject.name}: Entered WaitingForPrescription state but does not have a pending prescription flag! Transitioning to Exiting.", context.NpcObject);
                 // Fallback if somehow entered this state without a pending order
                 context.TransitionToState(CustomerState.Exiting);
                 // Note: The coroutine might still start but will be stopped by OnExit.
            }
            // --- END NEW LOGIC ---

            // Optional: Rotate towards the player interaction point if known
            // This might be the same as the claim point, or a specific interaction trigger point.
            // Assuming the claim point transform has the correct rotation.
             PrescriptionManager prescriptionManager = PrescriptionManager.Instance;
             if (prescriptionManager != null && prescriptionManager.GetPrescriptionClaimPoint() != null)
             {
                 Quaternion targetRotation = prescriptionManager.GetPrescriptionClaimPoint().rotation;
                 Debug.Log($"CustomerAI ({context.NpcObject.name}): Starting rotation towards claim point rotation {targetRotation.eulerAngles} in WaitingForPrescription.", context.NpcObject);
                 context.RotateTowardsTarget(targetRotation); // Use context helper
             }
             else
             {
                 Debug.LogWarning($"CustomerAI ({context.NpcObject.name}): PrescriptionManager or claim point is null! Cannot rotate.", context.NpcObject);
             }
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context); // Call base OnUpdate (empty)
            // Timer is handled by the coroutine now, not in Update
        }

        // OnReachedDestination is not applicable here, they are already AT their spot.
        public override void OnReachedDestination(NpcStateContext context) { /* Not applicable */ } // <-- Explicitly empty


        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)

            // Stop the timer coroutine
            if (waitingRoutine != null)
            {
                context.StopCoroutine(waitingRoutine);
                waitingRoutine = null;
            }
            impatientTimer = 0f; // Reset timer

            // Note: Stop waiting animation
            // context.PlayAnimation("Idle");

            // --- NEW: Publish event to free the claim spot --- // <-- NEW LOGIC
            // This event signals that the NPC is leaving the claim spot.
            // The PrescriptionManager will listen for this to update its IsPrescriptionClaimSpotOccupied status.
            // This should happen regardless of the reason for exiting (impatience, successful interaction - future).
            Debug.Log($"{context.NpcObject.name}: Exiting {name}. Publishing FreePrescriptionClaimSpotEvent.", context.NpcObject);
            context.PublishEvent(new FreePrescriptionClaimSpotEvent(context.NpcObject)); // Need to define FreePrescriptionClaimSpotEvent
            // --- END NEW LOGIC ---

            // --- NEW: Clear pending prescription flag and order data --- // <-- NEW LOGIC
            // This should happen when the NPC *completes* the prescription flow (by exiting this state).
            // This ensures they don't try to get the same prescription again immediately.
            if (context.Runner != null)
            {
                 if (context.Runner.IsTrueIdentityNpc && context.TiData != null)
                 {
                      Debug.Log($"{context.NpcObject.name}: TI NPC exiting prescription flow. Clearing pendingPrescription flag and assigned order on TiData.", context.NpcObject);
                      context.TiData.pendingPrescription = false;
                      // Decide if assignedOrder should be cleared or kept for history. Let's clear it for now.
                      context.TiData.assignedOrder = new PrescriptionOrder(); // Reset struct
                      // Need to notify PrescriptionManager to remove from assignedTiOrders dictionary
                      PrescriptionManager.Instance?.RemoveAssignedTiOrder(context.TiData.Id); // Need new PM method
                 }
                 else if (!context.Runner.IsTrueIdentityNpc)
                 {
                      Debug.Log($"{context.NpcObject.name}: Transient NPC exiting prescription flow. Clearing pendingPrescription flag and assigned order on Runner.", context.NpcObject);
                      context.Runner.hasPendingPrescriptionTransient = false;
                      context.Runner.assignedOrderTransient = new PrescriptionOrder(); // Reset struct
                      // Need to notify PrescriptionManager to remove from assignedTransientOrders dictionary
                      PrescriptionManager.Instance?.RemoveAssignedTransientOrder(context.NpcObject); // Need new PM method
                 }
            } else {
                 Debug.LogError($"{context.NpcObject.name}: WaitingForPrescriptionSO OnExit: Runner is null! Cannot clear pending prescription data.", context.NpcObject);
            }
            // --- END NEW LOGIC ---
        }

        // Coroutine method for the impatience timer
        private IEnumerator WaitingRoutine(NpcStateContext context)
        {
            Debug.Log($"{context.NpcObject.name}: WaitingRoutine started in {name}. Waiting for {impatientDuration:F2} seconds.", context.NpcObject);

            float timer = 0f;
            while (timer < impatientDuration)
            {
                 // Check if the state has changed externally (e.g., interruption, successful interaction)
                 if (context.Runner.GetCurrentState() != this)
                 {
                      Debug.Log($"{context.NpcObject.name}: WaitingRoutine interrupted due to state change.", context.NpcObject);
                      yield break; // Exit coroutine if state changes
                 }
                 timer += context.DeltaTime; // Use context.DeltaTime for frame-rate independent timing
                 yield return null; // Wait for the next frame
            }

            // Timer finished, NPC becomes impatient
            Debug.Log($"{context.NpcObject.name}: IMPATIENT in {name} state after {impatientDuration:F2} seconds. Transitioning to Exiting.", context.NpcObject);
            // No need to publish NpcImpatientEvent here, just transition directly as per the vision.
            context.TransitionToState(CustomerState.Exiting);
        }
    }
}
// --- END OF FILE WaitingForPrescriptionSO.cs ---