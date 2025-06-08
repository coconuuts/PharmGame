// --- START OF FILE InitializingSO.cs ---

// --- Updated InitializingSO.cs (Phase 2, Substep 4) ---

using UnityEngine;
using System;
using System.Collections;
using Game.NPC; // Needed for GeneralState and CustomerState enums
using Game.NPC.States; // Needed for NpcStateSO and NpcStateContext
using Game.Prescriptions; // Needed for PrescriptionManager // <-- NEW: Added using directive

namespace Game.NPC.States
{
    /// <summary>
    /// A generic state representing the moment after an NPC is activated/initialized and warped.
    /// Its primary job is to immediately transition to the NPC's determined starting state.
    /// For Transient NPCs, this state now checks for pending prescription assignments.
    /// This is intended to be a very short-lived state.
    /// Corresponds to GeneralState.Initializing.
    /// </summary>
    [CreateAssetMenu(fileName = "InitializingState", menuName = "NPC/General States/Initializing", order = 0)] // Placed under General States menu
    public class InitializingSO : NpcStateSO
    {
        // Maps to the generic Initializing enum value
        public override System.Enum HandledState => GeneralState.Initializing;

        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // Logic: Immediately decide the *real* starting state based on NPC type configuration,
            // loaded TI data, OR a pending transient prescription.

            if (context.Runner == null)
            {
                 Debug.LogError($"InitializingSO ({context.NpcObject?.name}): Runner reference is null in context! Cannot determine starting state. Transitioning to ReturningToPool.", context.NpcObject);
                 // Attempt fallback directly if Runner is null (critical error)
                 context.TransitionToState(GeneralState.ReturningToPool); // This transition helper should handle null Runner gracefully
                 return; // Exit OnEnter early
            }

            // --- NEW LOGIC: Check for Transient Prescription Assignment ---
            // This check only applies to Transient NPCs.
            if (!context.Runner.IsTrueIdentityNpc)
            {
                 PrescriptionManager prescriptionManager = PrescriptionManager.Instance; // Get manager instance

                 if (prescriptionManager != null)
                 {
                      // Attempt to assign a transient prescription order
                      // This method will set the hasPendingPrescriptionTransient flag and assignedOrderTransient on the Runner if successful.
                      if (prescriptionManager.TryAssignTransientPrescription(context.Runner)) // Placeholder method, implemented in Phase 1
                      {
                           // Successfully assigned a transient prescription.
                           // Transition to the prescription flow starting state.
                           Debug.Log($"{context.NpcObject.name}: Assigned transient prescription. Transitioning to LookToPrescription.", context.NpcObject);
                           context.TransitionToState(CustomerState.LookToPrescription); // Transition to the new prescription state
                           return; // Exit OnEnter early, transition is handled
                      }
                      // If TryAssignTransientPrescription returns false, it means no order was assigned (either none available, limit reached, or chance failed).
                      // The NPC should proceed with the normal flow.
                 }
                 else
                 {
                      Debug.LogWarning($"{context.NpcObject.name}: PrescriptionManager.Instance is null! Cannot check for transient prescription assignment. Proceeding with normal flow.", context.NpcObject);
                      // Proceed with normal flow if manager is missing
                 }
            }
            // --- END NEW LOGIC ---


            // --- Existing Logic: Determine the primary starting state (for TI or Transient without prescription) ---
            // This logic runs if the NPC is TI OR if it's Transient and TryAssignTransientPrescription returned false.
            NpcStateSO primaryStartingState = context.Runner.GetPrimaryStartingStateSO(); // This method handles fallbacks internally if needed
            // --- END Existing Logic ---

            if (primaryStartingState != null)
            {
                 Debug.Log($"{context.NpcObject.name}: Initializing state transitioning to determined primary starting state: {primaryStartingState.name}.", context.NpcObject);
                 context.TransitionToState(primaryStartingState); // Transition to the determined start state
            }
            else
            {
                 // If GetPrimaryStartingStateSO returned null, it means even its internal fallbacks failed.
                 Debug.LogError($"{context.NpcObject.name}: Runner.GetPrimaryStartingStateSO returned null! Cannot find a primary starting state for NPC type and fallbacks failed. Transitioning to ReturningToPool.", context.NpcObject);
                 // Transition to the absolute fallback state (ReturningToPool) via context
                 context.TransitionToState(GeneralState.ReturningToPool); // This should always be a valid state or handled gracefully
            }

            // This state is designed to transition immediately, so no significant OnUpdate/Coroutine needed.
        }

        // OnUpdate remains empty
        // OnReachedDestination is not applicable
        // StateCoroutine is not typically needed (transition is immediate)

        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation - though should already be stopped)
        }
    }
}
// --- END OF FILE InitializingSO.cs ---