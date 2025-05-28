// --- START OF FILE BasicPatrolStateSO.cs ---

// --- START OF FILE BasicPatrolStateSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicNpcStateSO
using Random = UnityEngine.Random; // Specify UnityEngine.Random

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    [CreateAssetMenu(fileName = "BasicPatrolState", menuName = "NPC/Basic States/Basic Patrol", order = 1)]
    public class BasicPatrolStateSO : BasicNpcStateSO
    {
        public override System.Enum HandledBasicState => BasicState.BasicPatrol;

        // Basic Patrol does not use the standard timeout; its 'timeout' is the wait time
        // at a point, leading to a transition OUT of Patrol (or another move within Patrol).
        // The base class's timeout mechanism (transitioning to BasicExitingStore) is NOT applicable here.
        public override bool ShouldUseTimeout => false; // Override base property

        [Header("Basic Patrol Settings")]
        [Tooltip("The minimum simulated bounds (X, Z) of the rectangular patrol area.")]
        [SerializeField] private Vector2 simulatedPatrolAreaMin = new Vector2(-10f, -10f);
        [Tooltip("The maximum simulated bounds (X, Z) of the rectangular patrol area.")]
        [SerializeField] private Vector2 simulatedPatrolAreaMax = new Vector2(10f, 10f);
        [Tooltip("Simulated speed for off-screen movement (units per second).")]
        [SerializeField] private float simulatedMovementSpeed = 3.5f; // Match NavMeshAgent speed roughly
        [Tooltip("The simulated distance threshold to consider the NPC 'arrived' at the target position.")]
        [SerializeField] private float simulatedArrivalThreshold = 0.75f; // Slightly larger threshold for simulation
        [Tooltip("Minimum simulated time to wait at a patrol point.")]
        [SerializeField] private float simulatedMinWaitTimeAtPoint = 1f; // Match PatrolStateSO
        [Tooltip("Maximum simulated time to wait at a patrol point.")]
        [SerializeField] private float simulatedMaxWaitTimeAtPoint = 3f;
        [Tooltip("Simulated probability (0-1) of a TI NPC transitioning to the BasicLookToShop state after waiting at a simulated patrol point.")]
        [Range(0f, 1f)][SerializeField] private float simulatedChanceToShop = 0.2f; // Match PatrolStateSO


        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            base.OnEnter(data, manager); // Call base OnEnter (logs entry, resets base timer/target)

            // The base OnEnter clears simulatedTargetPosition if ShouldUseTimeout is true.
            // BasicPatrol overrides ShouldUseTimeout to false, so the base doesn't clear it.
            // This is good - it preserves a target carried over from the active state.

            // In BasicPatrol's simulation, the timer is only used for waiting at points.
            // Initialize or reset it to 0 on entry. The simulation tick will manage starting/stopping it.
            data.simulatedStateTimer = 0f;
            Debug.Log($"SIM {data.Id}: BasicPatrolState OnEnter. Timer reset. Target: {data.simulatedTargetPosition?.ToString() ?? "NULL"}.");

            // Note: We do NOT pick a new target here.
            // If a target was carried over from active, SimulateTick will handle moving towards it.
            // If no target was carried over, SimulateTick will see target is null and start the wait timer.
        }

        public override void SimulateTick(TiNpcData data, float deltaTime, BasicNpcStateManager manager)
        {
            // Check if currently moving towards a target
            if (data.simulatedTargetPosition.HasValue)
            {
                // Simulate movement towards target...
                if (Vector3.Distance(data.CurrentWorldPosition, data.simulatedTargetPosition.Value) < simulatedArrivalThreshold)
                {
                    // Reached the target point
                    Debug.Log($"SIM {data.Id}: Reached simulated patrol target {data.simulatedTargetPosition.Value}.");
                    // Now they are at a point, they should wait.
                    data.simulatedTargetPosition = null; // Clear target as they are no longer moving to it.
                    // Start the wait timer.
                    data.simulatedStateTimer = Random.Range(simulatedMinWaitTimeAtPoint, simulatedMaxWaitTimeAtPoint);
                    Debug.Log($"SIM {data.Id}: Starting simulated wait timer for {data.simulatedStateTimer:F2}s.");
                }
                else // Not yet at target, simulate movement
                {
                    Vector3 direction = (data.simulatedTargetPosition.Value - data.CurrentWorldPosition).normalized;
                    float moveDistance = simulatedMovementSpeed * deltaTime;
                    data.CurrentWorldPosition += direction * moveDistance;
                    // Simulate rotation to face direction
                    if (direction.sqrMagnitude > 0.001f) // Avoid LookRotation with zero vector
                    {
                        data.CurrentWorldRotation = Quaternion.LookRotation(direction);
                    }
                    // Debug.Log($"SIM {data.Id}: Moving towards simulated target {data.simulatedTargetPosition.Value}. New Pos: {data.CurrentWorldPosition}."); // Too noisy
                    // Timer is NOT active while moving. Ensure it's zero or negative.
                    data.simulatedStateTimer = 0f; // Ensure timer is not positive while moving
                }
            }
            else // No target position currently set (means they are at a point, either waiting or deciding/transitioning)
            {
                // Timer manages wait time at a point. If target is null, timer should be > 0 (waiting) or <= 0 (wait finished/initial).
                // If timer <= 0, it means the wait is over or hasn't started. Need to start waiting if just arrived,
                // or make a decision if the wait timer just finished.
                 if (data.simulatedStateTimer > 0)
                 {
                    // Waiting... decrement timer
                    data.simulatedStateTimer -= deltaTime;

                    if (data.simulatedStateTimer <= 0)
                    {
                        // Waiting finished, make decision
                        data.simulatedStateTimer = 0; // Ensure timer is zeroed

                        bool decidedToShop = Random.value <= simulatedChanceToShop;
                        Debug.Log($"SIM {data.Id}: Finished simulated wait. Decided to shop: {decidedToShop} (Chance: {simulatedChanceToShop * 100}%).");

                        if (decidedToShop)
                        {
                            // Transition to BasicLookToShop
                            Debug.Log($"SIM {data.Id}: Transitioning to BasicLookToShop.");
                            manager.TransitionToBasicState(data, BasicState.BasicLookToShop);
                            // The OnEnter of BasicLookToShop will set its target position.
                            // No need to set a target here as they are leaving patrol.
                        }
                        else
                        {
                            // Stay in BasicPatrol, pick a new target and start moving again
                            data.simulatedTargetPosition = GetRandomPointInPatrolAreaSimulated(); // <-- PICK NEW TARGET HERE
                            Debug.Log($"SIM {data.Id}: Decided to continue patrolling. Setting new simulated target: {data.simulatedTargetPosition.Value}.");
                            // No state transition needed, remain in BasicPatrol.
                            // The next tick will see data.simulatedTargetPosition has a value and simulate movement.
                            // Timer remains 0 while moving.
                        }
                    }
                    // else { Debug.Log($"SIM {data.Id}: Waiting at patrol point... {data.simulatedStateTimer:F2}s remaining."); } // Too noisy
                 }
                 else // Timer is <= 0 and target is null (already finished waiting and made decision, or initial state with no carried target)
                 {
                     // This means they just entered the state without a carried target, or finished a wait and are deciding/transitioning.
                     // If target is null here, it means they finished waiting OR just entered the state without a carried target.
                     // If they just entered without a carried target, we want them to START waiting immediately.
                     // If they finished waiting, they already made a decision (shop or new patrol target) and the new state/target is set.
                     // So, if we are here with target null and timer <= 0, and we *didn't* just transition to LookToShop,
                     // it implies they just *entered* this state without a carried target. Start the wait timer.
                     // A simple check is just to see if the target is null and the timer is non-positive.
                     Debug.LogWarning($"SIM {data.Id}: Found in BasicPatrol with null target and non-positive timer. Assuming initial state or decision point reached. Starting initial wait timer.");
                     data.simulatedStateTimer = Random.Range(simulatedMinWaitTimeAtPoint, simulatedMaxWaitTimeAtPoint); // Start the wait timer
                     data.simulatedTargetPosition = null; // Ensure target is null while waiting
                 }
            }
        }

         public override void OnExit(TiNpcData data, BasicNpcStateManager manager)
         {
             base.OnExit(data, manager); // Call base OnExit (logs exit)

             // Clear the specific patrol timer on exit
             data.simulatedStateTimer = 0f;
             // Do NOT clear data.simulatedTargetPosition here, as it might be carried over to the next state (e.g., BasicLookToShop)
             // or it might have been set BY this state just before exiting (if deciding to continue patrol).
             // The OnEnter of the next state will handle interpreting/clearing its target needs.
         }


        /// <summary>
        /// Picks a random point within the defined XZ patrol area bounds (for simulation).
        /// Uses a fixed Y height (e.g., 0) for simplicity as NavMesh sampling is not available.
        /// Matches logic previously in TiNpcManager.
        /// </summary>
        private Vector3 GetRandomPointInPatrolAreaSimulated()
        {
             float randomX = Random.Range(simulatedPatrolAreaMin.x, simulatedPatrolAreaMax.x);
             float randomZ = Random.Range(simulatedPatrolAreaMin.y, simulatedPatrolAreaMax.y); // Note: using y for Z axis in Vector2
             return new Vector3(randomX, 0f, randomZ); // Assume ground is at Y=0 for simulation
        }
    }
}
// --- END OF FILE BasicPatrolStateSO.cs ---    