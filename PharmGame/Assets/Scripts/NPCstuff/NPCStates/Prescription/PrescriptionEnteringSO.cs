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


namespace Game.NPC.States // Place alongside other active states
{
    /// <summary>
    /// State for a Prescription Customer moving to the prescription claim spot.
    /// Corresponds to CustomerState.PrescriptionEntering.
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

            // Transition to the WaitingForPrescription state
            Debug.Log($"{context.NpcObject.name}: Reached claim spot. Transitioning to WaitingForPrescription.", context.NpcObject);
            context.TransitionToState(CustomerState.WaitingForPrescription);
        }

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)

            // Note: Stop walking animation
            // context.PlayAnimation("Idle");

            // IMPORTANT: The ClaimPrescriptionSpotEvent is published on ENTER.
            // The PrescriptionManager will need a way to know when the spot is freed,
            // likely when the NPC transitions OUT of WaitingForPrescription or Exiting.
            // This might require a new event (e.g., PrescriptionSpotFreedEvent) or logic in those states' OnExit.
            // Let's plan to handle freeing the claim spot in WaitingForPrescriptionSO.OnExit.
        }
    }
}
// --- END OF FILE PrescriptionEnteringSO.cs ---