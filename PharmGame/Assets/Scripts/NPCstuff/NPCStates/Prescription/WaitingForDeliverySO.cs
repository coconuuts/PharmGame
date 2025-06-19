// --- START OF FILE WaitingForDeliverySO.cs ---

// --- START OF FILE WaitingForDeliverySO.cs ---

using UnityEngine;
using System;
using Game.NPC.States; // Needed for NpcStateSO and NpcStateContext
using Game.NPC; // Needed for CustomerState and GeneralState enums
using Systems.Interaction; // Needed for IInteractable, InteractionManager // <-- MODIFIED using directive
using Game.Interaction; // Needed for ObtainPrescription and DeliverPrescription
using Game.Prescriptions; // Needed for PrescriptionManager, PrescriptionOrder
using Systems.Player; // Needed for PlayerPrescriptionTracker
using System.Collections; // Needed for Coroutines
using Random = UnityEngine.Random; // Specify UnityEngine.Random
using Game.Events;
using Systems.GameStates; // Needed for PlayerUIPopups


namespace Game.NPC.States // Place alongside other active states
{
    /// <summary>
    /// State for a Prescription Customer waiting at the prescription claim spot
    /// for the player to deliver the crafted item.
    /// Corresponds to CustomerState.WaitingForDelivery.
    /// Uses the InteractionManager singleton directly.
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

        private Coroutine waitingRoutine; // Coroutine for the timer

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // Stop any residual movement from reaching the spot (should already be stopped from previous state)
            context.MovementHandler?.StopMoving();
            context.MovementHandler?.StopRotation();

            // Play waiting/idle animation
            // context.PlayAnimation("WaitingAtCounter"); // Placeholder animation name

            Debug.Log($"{context.NpcObject.name}: Entering {name}. Waiting for player delivery.", context.NpcObject);

            // --- Start impatience timer coroutine ---
            float impatientDuration = Random.Range(impatientTimeRange.x, impatientTimeRange.y); // Duration is local to OnEnter
            Debug.Log($"{context.NpcObject.name}: Entering {name}. Starting impatience timer for {impatientDuration:F2} seconds.", context.NpcObject);
            waitingRoutine = context.StartCoroutine(WaitingRoutine(context, impatientDuration)); // Start the timer coroutine


            // --- Manage Interactables using the InteractionManager singleton ---
            if (InteractionManager.Instance != null)
            {
                Debug.Log($"{context.NpcObject.name}: Activating DeliverPrescription interactable via singleton.", context.NpcObject);
                // Enable *only* the DeliverPrescription component on this NPC's GameObject
                InteractionManager.Instance.EnableOnlyInteractableComponent<DeliverPrescription>(context.NpcObject);
            }
            else
            {
                Debug.LogError($"{context.NpcObject.name}: InteractionManager.Instance is null! Cannot manage interactables.", context.NpcObject);
                // Decide fallback: Maybe transition to Exiting if delivery is critical?
                // For now, just log error and NPC remains in this state without guaranteed interaction.
            }

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

            // --- Disable interactables on exit using the InteractionManager singleton ---
            if (InteractionManager.Instance != null)
            {
                // Disable the DeliverPrescription component on this NPC's GameObject
                // This will also deactivate its prompt via the component's OnDisable/OnDestroy if needed.
                InteractionManager.Instance.DisableInteractableComponent<DeliverPrescription>(context.NpcObject);

                InteractionManager.Instance.EnableOnlyInteractableComponent<OpenNPCInventory>(context.NpcObject);
            }
            else
            {
                Debug.LogWarning($"{context.NpcObject.name}: InteractionManager.Instance is null on exit! Cannot disable interactables.", context.NpcObject);
            }

            // --- Clear player's active prescription order and UI popup ---
            context.PublishEvent(new FreePrescriptionClaimSpotEvent(context.NpcObject));
            PlayerUIPopups.Instance?.HidePopup("Prescription Order"); // Ensure UI is hidden on exit
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
            PlayerPrescriptionTracker playerTracker = null;
            if (playerGO != null)
            {
                playerTracker = playerGO.GetComponent<PlayerPrescriptionTracker>();
            }

            if (playerTracker != null && playerTracker.ActivePrescriptionOrder.HasValue)
            {
                Debug.Log($"{context.NpcObject.name}: Exiting {name}. Clearing player's active prescription order.", context.NpcObject);
                playerTracker.ClearActiveOrder();
            }


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

            // --- Publish event to free the claim spot just before exiting due to impatience ---
            Debug.Log($"{context.NpcObject.name}: Impatience timer finished. Publishing FreePrescriptionClaimSpotEvent.", context.NpcObject);
            context.PublishEvent(new FreePrescriptionClaimSpotEvent(context.NpcObject));

            PrescriptionManager prescriptionManager = PrescriptionManager.Instance; // Get the instance
            if (prescriptionManager != null && context.Runner != null)
            {
                 if (context.Runner.IsTrueIdentityNpc && context.Runner.TiData != null)
                 {
                      prescriptionManager.RemoveAssignedTiOrder(context.Runner.TiData.Id);
                      Debug.Log($"{context.NpcObject.name}: Impatience exit: Notified PrescriptionManager to remove assigned TI order for '{context.Runner.TiData.Id}'.", context.NpcObject);
                 }
                 else if (!context.Runner.IsTrueIdentityNpc)
                 {
                      // Use context.NpcObject to reference the NPC's GameObject
                      prescriptionManager.RemoveAssignedTransientOrder(context.NpcObject);
                      Debug.Log($"{context.NpcObject.name}: Impatience exit: Notified PrescriptionManager to remove assigned Transient order for '{context.NpcObject.name}'.", context.NpcObject);
                 }
                 else
                 {
                      Debug.LogError($"{context.NpcObject.name}: Impatience exit: Runner is TI but TiData is null, or Runner is null. Cannot notify PrescriptionManager to remove assigned order.", context.NpcObject);
                 }
            }
            else
            {
                 Debug.LogError($"{context.NpcObject.name}: Impatience exit: PrescriptionManager or Runner is null! Cannot notify manager to remove assigned order.", context.NpcObject);
            }

            // No need to publish NpcImpatientEvent here, just transition directly as per the vision.
            context.TransitionToState(CustomerState.Exiting);
        }
    }
}

// --- END OF FILE WaitingForDeliverySO.cs ---