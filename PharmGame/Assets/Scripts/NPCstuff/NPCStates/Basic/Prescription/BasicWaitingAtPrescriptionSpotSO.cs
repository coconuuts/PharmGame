// --- START OF FILE BasicWaitingAtPrescriptionSpotSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicNpcStateSO, BasicNpcStateManager

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    /// <summary>
    /// Basic state for simulating an inactive TI NPC waiting at the prescription claim spot
    /// after looking for a prescription (corresponds to active WaitingForPrescription).
    /// Relies on the BasicNpcStateManager's timeout logic for progression.
    /// Operates directly on TiNpcData.
    /// Corresponds to BasicState.BasicWaitingAtPrescriptionSpot.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicWaitingAtPrescriptionSpotState", menuName = "NPC/Basic States/Basic Waiting At Prescription Spot", order = 7)] // Order after BasicWaitForPrescription
    public class BasicWaitingAtPrescriptionSpotSO : BasicNpcStateSO
    {
        // Set the HandledBasicState to the new enum value
        public override System.Enum HandledBasicState => BasicState.BasicWaitingAtPrescriptionSpot;

        // This state uses the standard timeout to force progression if inactive for too long.
        public override bool ShouldUseTimeout => true; // Override base property

        // Optional: Override timeout range if this waiting state should differ
        // [Header("Prescription Spot Waiting Timeout")]
        // [SerializeField] private Vector2 prescriptionSpotInactiveTimeout = new Vector2(15f, 30f); // Example range
        // public override float MinInactiveTimeout => prescriptionSpotInactiveTimeout.x;
        // public override float MaxInactiveTimeout => prescriptionSpotInactiveTimeout.y;


        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            // Base OnEnter handles logging, setting ShouldUseTimeout, initializing the timer, and clearing the target position.
            base.OnEnter(data, manager);

            // Ensure target position is null as the NPC is waiting
            data.simulatedTargetPosition = null;

            Debug.Log($"SIM {data.Id}: BasicWaitingAtPrescriptionSpotState OnEnter. Will remain frozen at claim spot until timeout ({data.simulatedStateTimer:F2}s initial).", data.NpcGameObject);

            // Note: Position and rotation should already be set to the claim spot's position/rotation
            // by the previous state (BasicLookToPrescriptionStateSO) upon arrival.
        }

        public override void SimulateTick(TiNpcData data, float deltaTime, BasicNpcStateManager manager)
        {
            // The core simulation logic for this state (decrementing timer and checking for timeout)
            // is handled by the BasicNpcStateManager.SimulateTickForNpc method because ShouldUseTimeout is true
            // and simulatedTargetPosition is null.

            // No additional simulation logic is needed within this state's tick method itself.
        }

         public override void OnExit(TiNpcData data, BasicNpcStateManager manager)
         {
             // Base OnExit handles logging.
             base.OnExit(data, manager);

             // No specific cleanup needed for TiNpcData in this state's exit.
             // The timeout in BasicNpcStateManager will transition to BasicExitingStore,
             // which will handle any necessary cleanup for that flow.
         }
    }
}
// --- END OF FILE BasicWaitingAtPrescriptionSpotSO.cs ---