// --- START OF FILE BasicWaitForPrescriptionStateSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicNpcStateSO, BasicNpcStateManager

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    /// <summary>
    /// Basic state for simulating an inactive TI NPC waiting in the prescription queue or at the claim spot.
    /// Relies on the BasicNpcStateManager's timeout logic for progression.
    /// Operates directly on TiNpcData.
    /// Corresponds to BasicState.BasicWaitForPrescription.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicWaitForPrescriptionState", menuName = "NPC/Basic States/Basic Wait For Prescription", order = 6)] // Order near BasicWaitForCashier
    public class BasicWaitForPrescriptionStateSO : BasicNpcStateSO
    {
        public override System.Enum HandledBasicState => BasicState.BasicWaitForPrescription;

        // This state uses the standard timeout to force progression if inactive for too long.
        public override bool ShouldUseTimeout => true; // Override base property

        // Optional: Override timeout range if prescription waiting should differ from cash register waiting
        // [Header("Prescription Waiting Timeout")]
        // [SerializeField] private float minPrescriptionInactiveTimeout = 15f; // Example: slightly longer wait
        // [SerializeField] private float maxPrescriptionInactiveTimeout = 30f; // Example: slightly longer wait
        // public override float MinInactiveTimeout => minPrescriptionInactiveTimeout;
        // public override float MaxInactiveTimeout => maxPrescriptionInactiveTimeout;


        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            // Base OnEnter handles logging, setting ShouldUseTimeout, initializing the timer (using overridden fields), and clearing the target position.
            base.OnEnter(data, manager);

            // Ensure target position is null as the NPC is waiting
            data.simulatedTargetPosition = null;

             Debug.Log($"SIM {data.Id}: BasicWaitForPrescriptionState OnEnter. Will remain frozen until timeout ({data.simulatedStateTimer:F2}s initial).");
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
         }
    }
}
// --- END OF FILE BasicWaitForPrescriptionStateSO.cs ---