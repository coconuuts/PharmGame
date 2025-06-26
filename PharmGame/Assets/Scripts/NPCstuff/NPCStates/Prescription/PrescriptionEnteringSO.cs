// --- START OF FILE PrescriptionEnteringSO.cs ---

using UnityEngine;
using System;
using Game.NPC.States; // Needed for NpcStateSO and NpcStateContext
using Game.NPC; // Needed for CustomerState and GeneralState enums
using Game.Prescriptions; // Needed for PrescriptionManager // <-- NEW: Added using directive
using Game.Events; // Needed for EventManager and new events // <-- NEW: Added using directive
using Game.NPC.TI; // Needed for TiNpcManager, TiNpcData // <-- NEW: Added using directive
using System.Collections; // Needed for Coroutines // <-- NEW: Added using directive
using CustomerManagement; // Needed for QueueType // <-- NEW: Added using directive
using Systems.Player; // Needed for PlayerPrescriptionTracker // <-- NEW: Added using directive


namespace Game.NPC.States // Place alongside other active states
{
    /// <summary>
    /// State for a Prescription Customer moving to the prescription claim spot.
    /// Corresponds to CustomerState.PrescriptionEntering.
    /// MODIFIED: Checks if the NPC's order is marked ready OR is the player's active task upon arrival and transitions to WaitingForDelivery if it is. // <-- Added note
    /// </summary>
    [CreateAssetMenu(fileName = "CustomerPrescriptionEnteringState", menuName = "NPC/Customer States/Prescription Entering", order = 2)] // Order after Look To Prescription
    public class PrescriptionEnteringSO : NpcStateSO
    {
        public override System.Enum HandledState => CustomerState.PrescriptionEntering;

        [Header("TI Queue Full Suppression")] // <-- NEW HEADER
        [Tooltip("Duration (real-time seconds) to suppress the pendingPrescription flag for TI NPCs if the queue is full upon entering this state.")]
        [SerializeField] private float tiQueueFullSuppressionDuration = 60f; // Example: Suppress for 60 seconds


        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            PrescriptionManager prescriptionManager = context.PrescriptionManager; // Get manager instance from context

            if (prescriptionManager == null)
            {
                 Debug.LogError($"{context.NpcObject.name}: PrescriptionManager.Instance is null! Cannot move to claim spot. Transitioning to Exiting (fallback).", context.NpcObject);
                 context.TransitionToState(CustomerState.Exiting); // Fallback
                 return;
            }

            // --- NEW: Check for TI Queue Full Suppression ---
            // This check happens *upon entering* the state, as per the vision.
            if (context.Runner != null && context.Runner.IsTrueIdentityNpc && context.TiData != null)
            {
                 // Check if the claim spot is occupied OR the queue is full
                 // Note: LookToPrescriptionSO should prevent entering if claim spot is occupied,
                 // but checking queue full here is the specific TI rule.
                 if (prescriptionManager.IsPrescriptionClaimSpotOccupied() || prescriptionManager.IsPrescriptionQueueFull()) // Implemented in Phase 4
                 {
                      Debug.LogWarning($"{context.NpcObject.name}: TI NPC entering PrescriptionEntering, but prescription queue/spot is full. Suppressing pendingPrescription and transitioning to Exiting.", context.NpcObject);

                      // Suppress the pendingPrescription flag on the TiData
                      context.TiData.pendingPrescription = false; // Suppress immediately

                      // Start the suppression coroutine via PrescriptionManager (now accessible via context)
                      if (context.PrescriptionManager != null) // Use context.PrescriptionManager
                      {
                           context.PrescriptionManager.StartPrescriptionSuppressionCoroutine(context.TiData, tiQueueFullSuppressionDuration); // Call on PrescriptionManager
                      } else {
                           Debug.LogError($"{context.NpcObject.name}: PrescriptionManager is null in context! Cannot start prescription suppression coroutine.", context.NpcObject);
                           // Flag will remain suppressed until manually reset or game restart
                      }

                      // Transition immediately to Exiting
                      context.TransitionToState(CustomerState.Exiting);
                      return; // Exit OnEnter early
                 }
            }
            // --- END NEW ---


            // Get the transform for the prescription claim point
            Transform claimPointTransform = prescriptionManager.GetPrescriptionClaimPoint(); // Implemented in Phase 4

            if (claimPointTransform != null)
            {
                // Set the Runner's target position and initiate movement
                // Avoid setting Runner.CurrentTargetLocation as it's tied to BrowseLocation
                Debug.Log($"{context.NpcObject.name}: Moving to prescription claim spot at {claimPointTransform.position}.", context.NpcObject);
                // context.MoveToDestination handles setting _hasReachedCurrentDestination = false and CurrentDestinationPosition
                bool moveStarted = context.MoveToDestination(claimPointTransform.position);

                 if (!moveStarted) // Add check for move failure
                 {
                      Debug.LogError($"{context.NpcObject.name}: Failed to start movement to prescription claim spot! Is the point on the NavMesh?", context.NpcObject);
                       Debug.LogWarning($"PrescriptionEnteringSO ({context.NpcObject.name}): Movement failed, falling back to Exiting.", context.NpcObject);
                       context.TransitionToState(CustomerState.Exiting); // Fallback
                       return; // Exit OnEnter early
                 }

                 // --- NEW: Publish event to claim the spot ---
                 // This event signals that an NPC is moving to/occupying the claim spot.
                 // The PrescriptionManager will listen for this to update its IsPrescriptionClaimSpotOccupied status.
                 Debug.Log($"{context.NpcObject.name}: Publishing ClaimPrescriptionSpotEvent.", context.NpcObject);
                 context.PublishEvent(new ClaimPrescriptionSpotEvent(context.NpcObject)); // Need to define ClaimPrescriptionSpotEvent
                 // --- END NEW ---

            }
            else
            {
                Debug.LogError($"{context.NpcObject.name}: Prescription claim point transform is null in PrescriptionManager! Cannot move.", context.NpcObject);
                Debug.LogWarning($"PrescriptionEnteringSO ({context.NpcObject.name}): Claim point not found, falling back to Exiting.", context.NpcObject);
                context.TransitionToState(CustomerState.Exiting); // Fallback
                return; // Exit OnEnter early
            }

            // Note: Play walking animation
            // context.PlayAnimation("Walking");
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context); // Call base OnUpdate (empty)
        }

        public override void OnReachedDestination(NpcStateContext context) // Called by Runner when NavMesh destination is reached
        {
            Debug.Log($"{context.NpcObject.name}: Reached prescription claim spot (detected by Runner).", context.NpcObject);

            // Ensure movement is stopped before transitioning (Runner does this before calling OnReachedDestination, but defensive)
            context.MovementHandler?.StopMoving();

            // --- NEW: Check if the order is already marked ready OR is the player's active task ---
            PrescriptionOrder assignedOrder;
            bool hasOrder = false;

            if (context.Runner != null)
            {
                 if (context.Runner.IsTrueIdentityNpc && context.TiData != null)
                 {
                      assignedOrder = context.TiData.assignedOrder; // Struct copy
                      hasOrder = context.TiData.pendingPrescription; // Check the flag
                 }
                 else if (!context.Runner.IsTrueIdentityNpc)
                 {
                      assignedOrder = context.Runner.assignedOrderTransient; // Struct copy
                      hasOrder = context.Runner.hasPendingPrescriptionTransient; // Check the flag
                 } else {
                     // Should not happen if entered this state correctly, but defensive
                     Debug.LogWarning($"{context.NpcObject.name}: OnReachedDestination: Runner is TI but TiData is null or Runner is null! Cannot access assigned order data.", context.NpcObject);
                     // Fallback to standard flow or exiting? Let's fallback to standard wait for now.
                     hasOrder = false; // Treat as no order found for the check below
                     assignedOrder = new PrescriptionOrder(); // Default struct
                 }
            } else {
                 Debug.LogError($"{context.NpcObject.name}: OnReachedDestination: Runner is null! Cannot access assigned order data.", context.NpcObject);
                 // Fallback to standard flow or exiting? Let's fallback to standard wait for now.
                 hasOrder = false; // Treat as no order found for the check below
                 assignedOrder = new PrescriptionOrder(); // Default struct
            }

            // Get the PlayerPrescriptionTracker instance
            // Assuming PlayerPrescriptionTracker is a singleton or findable via FindObjectOfType
            PlayerPrescriptionTracker playerTracker = FindObjectOfType<PlayerPrescriptionTracker>();
            PrescriptionOrder? currentPlayerActiveOrder = playerTracker?.ActivePrescriptionOrder;

            // Determine if the order is ready OR is the player's active task
            bool isOrderReadyOrActive = false;
            if (hasOrder) // Only perform checks if the NPC actually has an assigned order
            {
                bool isMarkedReady = context.PrescriptionManager != null && context.PrescriptionManager.IsOrderReady(assignedOrder.patientName);
                bool isActiveTask = currentPlayerActiveOrder.HasValue && currentPlayerActiveOrder.Value.Equals(assignedOrder);

                isOrderReadyOrActive = isMarkedReady || isActiveTask;

                Debug.Log($"{context.NpcObject.name}: Order for '{assignedOrder.patientName}' arrival check: IsMarkedReady={isMarkedReady}, IsActiveTask={isActiveTask}. Result: IsOrderReadyOrActive={isOrderReadyOrActive}.", context.NpcObject);
            } else {
                 Debug.LogWarning($"{context.NpcObject.name}: Arrived at claim spot but hasNoOrder. Skipping ready/active check.", context.NpcObject);
            }


            if (isOrderReadyOrActive)
            {
                // Order is ready OR is the player's active task, transition directly to WaitingForDelivery
                Debug.Log($"{context.NpcObject.name}: Order for '{assignedOrder.patientName}' is ready or active task. Transitioning to WaitingForDelivery.", context.NpcObject);
                context.TransitionToState(CustomerState.WaitingForDelivery);
            }
            else
            {
                // Order is not ready AND not the player's active task, transition to WaitingForPrescription
                Debug.Log($"{context.NpcObject.name}: Order for '{assignedOrder.patientName}' is NOT ready and NOT active task. Transitioning to WaitingForPrescription.", context.NpcObject);
                context.TransitionToState(CustomerState.WaitingForPrescription);
            }
            // --- END NEW ---
        }

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)

            // Note: Stop walking animation
            // context.PlayAnimation("Idle");

            // IMPORTANT: The ClaimPrescriptionSpotEvent is published on ENTER.
            // Freeing the spot is now handled ONLY by the impatience coroutine in WaitingForPrescriptionSO/WaitingForDeliverySO
            // or by the successful delivery in DeliverPrescription.
            Debug.Log($"{context.NpcObject.name}: Exiting {name}. Claim spot freeing handled by subsequent states.", context.NpcObject);
        }
    }
}