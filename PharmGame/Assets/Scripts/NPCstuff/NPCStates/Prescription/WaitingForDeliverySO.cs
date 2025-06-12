// --- START OF FILE WaitingForDeliverySO.cs ---

// --- START OF FILE WaitingForDeliverySO.cs ---

using UnityEngine;
using System;
using Game.NPC.States; // Needed for NpcStateSO and NpcStateContext
using Game.NPC; // Needed for CustomerState and GeneralState enums
using Systems.Interaction; // Needed for MultiInteractableManager and IInteractable
using Game.Interaction; // Needed for ObtainPrescription and DeliverPrescription
using Game.Prescriptions; // Needed for PrescriptionManager, PrescriptionOrder
using Systems.Player; // Needed for PlayerPrescriptionTracker // <-- Added using directive
using System.Collections; // Needed for Coroutines // <-- Added using directive
using Random = UnityEngine.Random; // Specify UnityEngine.Random // <-- Added using directive
using Game.Events;


namespace Game.NPC.States // Place alongside other active states
{
    /// <summary>
    /// State for a Prescription Customer waiting at the prescription claim spot
    /// for the player to deliver the crafted item.
    /// Corresponds to CustomerState.WaitingForDelivery.
    /// --- MODIFIED: Added impatience timer and order clearing on exit. ---
    /// </summary>
    [CreateAssetMenu(fileName = "CustomerWaitingForDeliveryState", menuName = "NPC/Customer States/Waiting For Delivery", order = 5)] // Order after WaitingForPrescription
    public class WaitingForDeliverySO : NpcStateSO
    {
        // Maps to the new enum value
        public override System.Enum HandledState => CustomerState.WaitingForDelivery;

        // --- NEW: Impatience Timer ---
        [Header("Waiting Settings")]
        [Tooltip("Minimum and maximum time (real-time seconds) the NPC will wait before becoming impatient.")]
        [SerializeField] private Vector2 impatientTimeRange = new Vector2(20f, 40f); // Adjust range as needed (maybe slightly longer than initial wait)

        // Timer fields are managed by the coroutine now
        // private float impatientTimer;
        // private float impatientDuration;

        private Coroutine waitingRoutine; // Coroutine for the timer
        // --- END NEW ---


        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // Stop any residual movement from reaching the spot (should already be stopped from previous state)
            context.MovementHandler?.StopMoving();
            context.MovementHandler?.StopRotation();

            // Play waiting/idle animation
            // context.PlayAnimation("WaitingAtCounter"); // Placeholder animation name

            Debug.Log($"{context.NpcObject.name}: Entering {name}. Waiting for player delivery.", context.NpcObject);

            // --- Start impatience timer coroutine --- // <-- NEW
            float impatientDuration = Random.Range(impatientTimeRange.x, impatientTimeRange.y); // Duration is local to OnEnter
            Debug.Log($"{context.NpcObject.name}: Entering {name}. Starting impatience timer for {impatientDuration:F2} seconds.", context.NpcObject);
            waitingRoutine = context.StartCoroutine(WaitingRoutine(context, impatientDuration)); // Start the timer coroutine
            // --- END NEW ---


            // --- Manage Interactables: Deactivate ObtainPrescription, Activate DeliverPrescription ---
            MultiInteractableManager multiManager = context.GetMultiInteractableManager(); // Get from context helper
            ObtainPrescription obtainPrescriptionComponent = context.GetObtainPrescription(); // Get from context helper
            // Need a reference to the DeliverPrescription component (will add next)
            DeliverPrescription deliverPrescriptionComponent = context.NpcObject.GetComponent<DeliverPrescription>(); // Assuming it's on the same GO

            if (multiManager != null)
            {
                 // Deactivate ObtainPrescription (should be handled by WaitingForPrescriptionSO.OnExit, but defensive)
                 if (obtainPrescriptionComponent != null && multiManager.CurrentActiveInteractable == obtainPrescriptionComponent)
                 {
                      Debug.Log($"{context.NpcObject.name}: Deactivating ObtainPrescription on entering WaitingForDelivery.", context.NpcObject);
                      multiManager.DeactivateCurrentInteractable(); // Deactivates the current one
                 }
                 // Activate DeliverPrescription
                 if (deliverPrescriptionComponent != null)
                 {
                      Debug.Log($"{context.NpcObject.name}: Activating DeliverPrescription interactable.", context.NpcObject);
                      multiManager.SetActiveInteractable(deliverPrescriptionComponent); // Set DeliverPrescription as the active interactable
                 }
                 else
                 {
                      Debug.LogError($"{context.NpcObject.name}: DeliverPrescription component not found on NPC! Cannot activate delivery interaction.", context.NpcObject);
                      // Decide fallback: Maybe transition to Exiting if delivery is critical?
                      // For now, just log error and NPC remains in this state without delivery interaction.
                 }
            }
            else
            {
                Debug.LogError($"{context.NpcObject.name}: MultiInteractableManager not found! Cannot manage interactables.", context.NpcObject);
            }
            // --- END Manage Interactables ---

            // Optional: Rotate towards the player interaction point if known (same as claim point)
             PrescriptionManager prescriptionManager = PrescriptionManager.Instance;
             if (prescriptionManager != null && prescriptionManager.GetPrescriptionClaimPoint() != null)
             {
                 Quaternion targetRotation = prescriptionManager.GetPrescriptionClaimPoint().rotation;
                 Debug.Log($"CustomerAI ({context.NpcObject.name}): Starting rotation towards claim point rotation {targetRotation.eulerAngles} in WaitingForDelivery.", context.NpcObject);
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
        public override void OnReachedDestination(NpcStateContext context) { /* Not applicable */ }


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

            // --- Deactivate the DeliverPrescription interactable on exit ---
            MultiInteractableManager multiManager = context.GetMultiInteractableManager(); // Get from context helper
            DeliverPrescription deliverPrescriptionComponent = context.NpcObject.GetComponent<DeliverPrescription>(); // Assuming it's on the same GO

            if (multiManager != null)
            {
                 if (deliverPrescriptionComponent != null && multiManager.CurrentActiveInteractable == deliverPrescriptionComponent)
                 {
                      Debug.Log($"{context.NpcObject.name}: Deactivating DeliverPrescription on exiting WaitingForDelivery.", context.NpcObject);
                      multiManager.DeactivateCurrentInteractable(); // Deactivates the current one
                 }
            }
            else
            {
                 Debug.LogWarning($"{context.NpcObject.name}: MultiInteractableManager not found on exit! Cannot deactivate interactable.", context.NpcObject);
            }
            // --- END Deactivate Interactable ---

            // --- NEW: Clear player's active prescription order ---
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player"); // Assuming player has the "Player" tag
            PlayerPrescriptionTracker playerTracker = null;
            if (playerGO != null)
            {
                playerTracker = playerGO.GetComponent<PlayerPrescriptionTracker>();
            }

            if (playerTracker != null && playerTracker.ActivePrescriptionOrder.HasValue)
            {
                 Debug.Log($"{context.NpcObject.name}: Exiting {name}. Clearing player's active prescription order.", context.NpcObject);
                 playerTracker.ClearActiveOrder();
                 // Also hide the UI popup if it's still showing
                 PlayerUIPopups.Instance?.HidePrescriptionOrder();
            }
            // Note: Clearing the order from the PrescriptionManager's tracking dictionaries
            // happens in DeliverPrescription.Interact() on *successful* delivery.
            // If the NPC becomes impatient, the order remains in the manager's tracking
            // until the end of the day (HandleSunset) or potentially until the NPC is deactivated/pooled.
            // This might be desired behavior (the order is still "assigned" but couldn't be fulfilled).
            // If the order *must* be unassigned on impatience, add that logic here.
            // For now, let's clear it only from the player's tracker and the manager on success.

            context.PublishEvent(new FreePrescriptionClaimSpotEvent(context.NpcObject));
            Debug.Log($"{context.NpcObject.name}: Exiting {name}. Claim spot freeing handled by impatience coroutine or successful delivery.", context.NpcObject);

            Debug.Log($"{context.NpcObject.name}: Exiting {name}.", context.NpcObject);
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
// --- END OF FILE WaitingForDeliverySO.cs ---