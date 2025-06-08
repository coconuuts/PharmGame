// --- START OF FILE BasicLookToPrescriptionStateSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData, TiNpcManager
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicNpcStateSO, BasicNpcStateManager
using Game.Prescriptions; // Needed for PrescriptionManager // <-- NEW: Added using directive

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    /// <summary>
    /// Basic state for simulating an inactive TI NPC looking to get a prescription.
    /// Simulates movement towards the pharmacy claim spot and then decides based on simulated queue status.
    /// Operates directly on TiNpcData.
    /// Corresponds to BasicState.BasicLookToPrescription.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicLookToPrescriptionState", menuName = "NPC/Basic States/Basic Look To Prescription", order = 2)] // Order near BasicLookToShop
    public class BasicLookToPrescriptionStateSO : BasicNpcStateSO
    {
        public override System.Enum HandledBasicState => BasicState.BasicLookToPrescription;

        // This state uses timeout only if it gets stuck, but primarily progresses by reaching the target.
        // Let's keep timeout enabled as a safety.
        public override bool ShouldUseTimeout => true; // Override base property

        [Header("Simulation Settings")]
        [Tooltip("The speed at which the NPC moves in simulation.")]
        [SerializeField] private float simulationSpeed = 2.0f; // Match BasicPatrol/BasicExiting
        [Tooltip("The distance threshold to consider the target reached in simulation.")]
        [SerializeField] private float arrivalThreshold = 0.75f; // Match BasicPatrol/BasicExiting

        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            // Base OnEnter handles logging, setting ShouldUseTimeout, initializing the timer, and clearing the target position.
            base.OnEnter(data, manager);

            // In this state, the target is the prescription claim point.
            PrescriptionManager prescriptionManager = PrescriptionManager.Instance;

            if (prescriptionManager == null)
            {
                 Debug.LogError($"SIM {data.Id}: PrescriptionManager.Instance is null! Cannot set simulation target for BasicLookToPrescription. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                 manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                 return;
            }

            Transform claimPointTransform = prescriptionManager.GetPrescriptionClaimPoint(); // Placeholder method

            if (claimPointTransform != null)
            {
                 data.simulatedTargetPosition = claimPointTransform.position;
                 Debug.Log($"SIM {data.Id}: BasicLookToPrescriptionState OnEnter. Set simulation target to prescription claim point at {data.simulatedTargetPosition.Value}.", data.NpcGameObject);

                 // Simulate initial rotation towards the target
                 Vector3 direction = (data.simulatedTargetPosition.Value - data.CurrentWorldPosition).normalized;
                 if (direction.sqrMagnitude > 0.001f)
                 {
                      data.CurrentWorldRotation = Quaternion.LookRotation(direction);
                 }

                 // Reset timer while moving towards the target
                 data.simulatedStateTimer = -1f; // Use -1 to indicate timer is not active while moving

            }
            else
            {
                 Debug.LogError($"SIM {data.Id}: Prescription claim point transform is null in PrescriptionManager! Cannot set simulation target. Transitioning to BasicPatrol fallback.", data.NpcGameObject);
                 manager.TransitionToBasicState(data, BasicState.BasicPatrol); // Fallback
                 return;
            }
        }

        public override void SimulateTick(TiNpcData data, float deltaTime, BasicNpcStateManager manager)
        {
            // The base manager handles timeout if simulatedTargetPosition is null.
            // If simulatedTargetPosition has a value, we simulate movement.
            if (data.simulatedTargetPosition.HasValue)
            {
                Vector3 targetPosition = data.simulatedTargetPosition.Value;
                Vector3 currentPosition = data.CurrentWorldPosition;

                // Check for arrival at the target position
                if (Vector3.Distance(currentPosition, targetPosition) <= arrivalThreshold)
                {
                    // Arrived at the prescription claim point
                    Debug.Log($"SIM {data.Id}: Simulated arrival at prescription claim point {targetPosition}.", data.NpcGameObject);

                    // Snap position to the target to avoid overshooting
                    data.CurrentWorldPosition = targetPosition;
                    data.simulatedTargetPosition = null; // Clear target position

                    // Simulate rotation towards the claim point's rotation if available
                     PrescriptionManager prescriptionManager = PrescriptionManager.Instance;
                     if (prescriptionManager != null && prescriptionManager.GetPrescriptionClaimPoint() != null)
                     {
                         data.CurrentWorldRotation = prescriptionManager.GetPrescriptionClaimPoint().rotation;
                     }


                    // --- Simulate Decision Logic ---
                    // Check the simulated status of the prescription queue and claim spot
                    if (prescriptionManager == null)
                    {
                         Debug.LogError($"SIM {data.Id}: PrescriptionManager.Instance is null after arrival! Cannot simulate decision. Transitioning to BasicExitingStore fallback.", data.NpcGameObject);
                         manager.TransitionToBasicState(data, BasicState.BasicExitingStore); // Fallback
                         return; // Exit tick processing
                    }

                    // Need placeholder methods in PrescriptionManager for simulated status
                    bool simulatedClaimSpotOccupied = prescriptionManager.IsPrescriptionClaimSpotOccupied(); // Placeholder
                    bool simulatedQueueFull = prescriptionManager.IsPrescriptionQueueFull(); // Placeholder

                    if (simulatedClaimSpotOccupied && simulatedQueueFull)
                    {
                        // Claim spot occupied AND queue full, give up
                        Debug.Log($"SIM {data.Id}: Simulated decision: Claim spot occupied AND queue full. Transitioning to BasicExitingStore.", data.NpcGameObject);
                        manager.TransitionToBasicState(data, BasicState.BasicExitingStore);
                    }
                    else
                    {
                        // Claim spot free OR queue has space, proceed to wait
                        Debug.Log($"SIM {data.Id}: Simulated decision: Claim spot free OR queue has space. Transitioning to BasicWaitForPrescription.", data.NpcGameObject);
                        manager.TransitionToBasicState(data, BasicState.BasicWaitForPrescription);
                    }
                    // --- End Simulate Decision Logic ---

                    // Note: The new state's OnEnter will handle setting up its timer/target.
                }
                else
                {
                    // Not yet arrived, simulate movement
                    Vector3 direction = (targetPosition - currentPosition).normalized;
                    float moveDistance = simulationSpeed * deltaTime;

                    data.CurrentWorldPosition += direction * moveDistance;

                    // Simulate rotation towards the movement direction
                    if (direction.sqrMagnitude > 0.001f)
                    {
                         data.CurrentWorldRotation = Quaternion.LookRotation(direction);
                    }
                    // Debug.Log($"SIM {data.Id}: Simulating movement towards {targetPosition}. New Pos: {data.CurrentWorldPosition}."); // Too noisy

                    // Ensure timer is reset while moving
                    data.simulatedStateTimer = -1f;
                }
            }
            // If simulatedTargetPosition is null, the NPC is waiting for the timeout managed by BasicNpcStateManager.
        }

         public override void OnExit(TiNpcData data, BasicNpcStateManager manager)
         {
             // Base OnExit handles logging.
             base.OnExit(data, manager);

             // Clear target position on exit just in case
             data.simulatedTargetPosition = null;
             // Timer is reset by base OnExit if ShouldUseTimeout is false, or managed by the next state's OnEnter.
         }
    }
}
// --- END OF FILE BasicLookToPrescriptionStateSO.cs ---