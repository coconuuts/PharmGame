// --- START OF FILE WaitingForPrescriptionSO.cs ---

// --- START OF FILE WaitingForPrescriptionSO.cs (Modified for Caching Interaction Components) ---

using UnityEngine;
using System;
using System.Collections;
using Game.NPC.States; // Needed for NpcStateSO and NpcStateContext
using Game.NPC; // Needed for CustomerState and GeneralState enums
using Game.Prescriptions; // Needed for PrescriptionOrder
using Random = UnityEngine.Random; // Specify UnityEngine.Random
using Game.Events; // Needed for EventManager and new event
using Systems.Interaction; // Needed for MultiInteractableManager
using Game.Interaction; // Needed for ObtainPrescription
using Systems.Player; // Needed for PlayerPrescriptionTracker // <-- Added using directive


namespace Game.NPC.States // Place alongside other active states
{
    /// <summary>
    /// State for a Prescription Customer waiting at the prescription claim spot.
    /// An impatience timer runs, after which the NPC exits.
    /// Corresponds to CustomerState.WaitingForPrescription.
    /// MODIFIED: Activates the ObtainPrescription interactable on Enter and deactivates on Exit.
    /// Interaction components are accessed via context (cached on Runner).
    /// --- MODIFIED: FreePrescriptionClaimSpotEvent is now published by the impatience coroutine on exit to Exiting. ---
    /// </summary>
    [CreateAssetMenu(fileName = "CustomerWaitingForPrescriptionState", menuName = "NPC/Customer States/Waiting For Prescription", order = 3)] // Order after Prescription Entering
    public class WaitingForPrescriptionSO : NpcStateSO
    {
        public override System.Enum HandledState => CustomerState.WaitingForPrescription;


        [Header("Waiting Settings")]
        [Tooltip("Minimum and maximum time (real-time seconds) the NPC will wait before becoming impatient.")]
        [SerializeField] private Vector2 impatientTimeRange = new Vector2(15f, 30f); // Adjust range as needed

        // Timer fields are managed by the coroutine now
        // private float impatientTimer;
        // private float impatientDuration;

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
            float impatientDuration = Random.Range(impatientTimeRange.x, impatientTimeRange.y); // Duration is local to OnEnter
            Debug.Log($"{context.NpcObject.name}: Entering {name}. Starting impatience timer for {impatientDuration:F2} seconds.", context.NpcObject);

            waitingRoutine = context.StartCoroutine(WaitingRoutine(context, impatientDuration)); // Start the timer coroutine
            // impatientTimer = 0f; // Timer is managed by the coroutine now


            // --- Log Prescription Order Data ---
            PrescriptionOrder assignedOrder;
            bool hasOrder = false;

            if (context.Runner != null)
            {
                 if (context.Runner.IsTrueIdentityNpc && context.TiData != null)
                 {
                      // TI NPC: Get order from TiData
                      assignedOrder = context.TiData.assignedOrder; // Struct copy
                      hasOrder = context.TiData.pendingPrescription; // Check the flag
                      Debug.Log($"{context.NpcObject.name}: TI NPC. Has Pending Prescription: {hasOrder}. Order Data: {assignedOrder.ToString()}", context.NpcObject);
                 }
                 else if (!context.Runner.IsTrueIdentityNpc)
                 {
                      // Transient NPC: Get order from Runner's temporary fields
                      assignedOrder = context.Runner.assignedOrderTransient; // Struct copy
                      hasOrder = context.Runner.hasPendingPrescriptionTransient; // Check the flag
                      Debug.Log($"{context.NpcObject.name}: Transient NPC. Has Pending Prescription: {hasOrder}. Order Data: {assignedOrder.ToString()}", context.NpcObject);
                 } else {
                      Debug.LogWarning($"{context.NpcObject.name}: WaitingForPrescriptionSO OnEnter: Runner is TI but TiData is null!", context.NpcObject);
                 }
            } else {
                 Debug.LogError($"{context.NpcObject.name}: WaitingForPrescriptionSO OnEnter: Runner is null! Cannot access assigned order data.", context.NpcObject);
            }


            if (!hasOrder)
            {
                 Debug.LogWarning($"{context.NpcObject.name}: Entered WaitingForPrescription state but does not have a pending prescription flag! Transitioning to Exiting.", context.NpcObject);
                 // Fallback if somehow entered this state without a pending order
                 context.TransitionToState(CustomerState.Exiting);
                 // Note: The coroutine might still start but will be stopped by OnExit.
                 return; // Exit OnEnter early
            }
            // --- END Log Prescription Order Data ---


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

            // --- Activate the ObtainPrescription interactable using cached components ---
            MultiInteractableManager multiManager = context.GetMultiInteractableManager(); // Get from context helper
            ObtainPrescription obtainPrescriptionComponent = context.GetObtainPrescription(); // Get from context helper

            if (multiManager != null && obtainPrescriptionComponent != null)
            {
                Debug.Log($"{context.NpcObject.name}: Activating ObtainPrescription interactable.", context.NpcObject);
                multiManager.SetActiveInteractable(obtainPrescriptionComponent); // Set ObtainPrescription as the active interactable
            }
            else
            {
                Debug.LogError($"{context.NpcObject.name}: MultiInteractableManager ({multiManager != null}) or ObtainPrescription component ({obtainPrescriptionComponent != null}) not found! Cannot activate prescription interaction.", context.NpcObject);
                // Decide fallback: maybe transition to Exiting if interaction is critical?
                // For now, just log error and continue the waiting timer.
            }
            // --- END MODIFIED LOGIC ---
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
            // impatientTimer = 0f; // Timer is managed by the coroutine now

            // Note: Stop waiting animation
            // context.PlayAnimation("Idle");

            // --- Deactivate the ObtainPrescription interactable and reset its state using cached components ---
            MultiInteractableManager multiManager = context.GetMultiInteractableManager(); // Get from context helper
            ObtainPrescription obtainPrescriptionComponent = context.GetObtainPrescription(); // Get from context helper


            if (multiManager != null)
            {
                 // Deactivate the current interactable (which should be ObtainPrescription if we entered correctly)
                 Debug.Log($"{context.NpcObject.name}: Deactivating current interactable (expected ObtainPrescription).", context.NpcObject);
                 multiManager.DeactivateCurrentInteractable(); // This also disables the component
            }
            else
            {
                 Debug.LogWarning($"{context.NpcObject.name}: MultiInteractableManager not found on exit! Cannot deactivate interactable.", context.NpcObject);
            }

            if (obtainPrescriptionComponent != null)
            {
                 // Reset the state of the ObtainPrescription component (clears flag)
                 Debug.Log($"{context.NpcObject.name}: Resetting ObtainPrescription component state.", context.NpcObject);
                 obtainPrescriptionComponent.ResetInteraction(); // <-- This now only resets the flag
            }
            else
            {
                 Debug.LogWarning($"{context.NpcObject.name}: ObtainPrescription component not found on exit! Cannot reset its state.", context.NpcObject);
            }

            Debug.Log($"{context.NpcObject.name}: Exiting {name}. Claim spot freeing handled by impatience coroutine or next state.", context.NpcObject);
            // --- END MODIFIED ---
        }

        // Coroutine method for the impatience timer
        private IEnumerator WaitingRoutine(NpcStateContext context, float duration) // Pass duration as parameter
        {
            Debug.Log($"{context.NpcObject.name}: WaitingRoutine started in {name}. Waiting for {duration:F2} seconds.", context.NpcObject);

            float timer = 0f;
            while (timer < duration)
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
            Debug.Log($"{context.NpcObject.name}: IMPATIENT in {name} state after {duration:F2} seconds. Transitioning to Exiting.", context.NpcObject);

            // --- NEW: Publish event to free the claim spot just before exiting due to impatience ---
            Debug.Log($"{context.NpcObject.name}: Impatience timer finished. Publishing FreePrescriptionClaimSpotEvent.", context.NpcObject);
            context.PublishEvent(new FreePrescriptionClaimSpotEvent(context.NpcObject));
            // --- END NEW ---

            // No need to publish NpcImpatientEvent here, just transition directly as per the vision.
            context.TransitionToState(CustomerState.Exiting);
        }
    }
}
// --- END OF FILE WaitingForPrescriptionSO.cs (Modified for Caching Interaction Components) ---