// --- START OF FILE BasicExitingStoreStateSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicNpcStateSO

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    [CreateAssetMenu(fileName = "BasicExitingStoreState", menuName = "NPC/Basic States/Basic Exiting Store", order = 6)] // Order towards the end of customer states
    public class BasicExitingStoreStateSO : BasicNpcStateSO
    {
        public override System.Enum HandledBasicState => BasicState.BasicExitingStore;

        // Basic Exiting uses the standard timeout as a safeguard against getting stuck
        public override bool ShouldUseTimeout => true; // Override base property

        [Header("Basic Exiting Settings")]
        [Tooltip("The simulated destination point for NPCs leaving the store.")]
        [SerializeField] private Vector3 simulatedExitPosition = new Vector3(0f, 0f, 0f); // Example exit point

        [Tooltip("Simulated speed for off-screen movement (units per second).")]
        [SerializeField] private float simulatedMovementSpeed = 2f; // Should match BasicPatrolStateSO

        [Tooltip("The simulated distance threshold to consider the NPC 'arrived' at the exit position.")]
        [SerializeField] private float simulatedArrivalThreshold = 0.75f; // Larger threshold for exit

        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            base.OnEnter(data, manager); // Call base OnEnter (logs entry, sets base timeout timer, clears base target)

            // Set the simulated target position to the predefined exit point
            data.simulatedTargetPosition = simulatedExitPosition;
            Debug.Log($"SIM {data.Id}: BasicExitingStore OnEnter. Setting simulated target to exit point: {data.simulatedTargetPosition.Value}");

            // The base OnEnter already initializes the simulatedStateTimer for the timeout.
            // This timer will ensure they eventually transition out if they never reach the exit.
        }

        public override void SimulateTick(TiNpcData data, float deltaTime, BasicNpcStateManager manager)
        {
            // Check for arrival first
             if (data.simulatedTargetPosition.HasValue)
             {
                if (Vector3.Distance(data.CurrentWorldPosition, data.simulatedTargetPosition.Value) < simulatedArrivalThreshold)
                {
                    // Reached the exit point, transition back to BasicPatrol
                    Debug.Log($"SIM {data.Id}: Reached simulated exit target {data.simulatedTargetPosition.Value}. Transitioning to BasicPatrol.");
                    manager.TransitionToBasicState(data, BasicState.BasicPatrol);
                    return; // Exit tick processing after transition
                }
                else // Not yet at target, simulate movement
                {
                    Vector3 direction = (data.simulatedTargetPosition.Value - data.CurrentWorldPosition).normalized;
                    float moveDistance = simulatedMovementSpeed * deltaTime;
                    data.CurrentWorldPosition += direction * moveDistance;
                     // Simulate rotation
                     if (direction.sqrMagnitude > 0.001f)
                     {
                          data.CurrentWorldRotation = Quaternion.LookRotation(direction);
                     }
                }
             }
             else // Target position is null, unexpected for this state after OnEnter
             {
                  Debug.LogError($"SIM {data.Id}: In BasicExitingStore state but simulatedTargetPosition is null!", data.NpcGameObject);
             }
        }

         public override void OnExit(TiNpcData data, BasicNpcStateManager manager)
         {
             base.OnExit(data, manager); // Call base OnExit (logs exit)

             // Clear the simulated target position on exit
             data.simulatedTargetPosition = null;

             // The base OnExit handles resetting the simulatedStateTimer if using timeout.
         }
    }
}
// --- END OF FILE BasicExitingStoreStateSO.cs ---