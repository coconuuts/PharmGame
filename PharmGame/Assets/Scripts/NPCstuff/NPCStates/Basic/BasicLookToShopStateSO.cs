// --- START OF FILE BasicLookToShopStateSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData
using Game.NPC.BasicStates; // Needed for BasicState enum, BasicNpcStateSO
using Random = UnityEngine.Random; // Specify UnityEngine.Random

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    [CreateAssetMenu(fileName = "BasicLookToShopState", menuName = "NPC/Basic States/Basic Look To Shop", order = 2)] // Order according to customer flow
    public class BasicLookToShopStateSO : BasicNpcStateSO
    {
        public override System.Enum HandledBasicState => BasicState.BasicLookToShop;

        // This state uses the standard timeout to force progression (giving up on shopping)
        public override bool ShouldUseTimeout => true; // Override base property

        // Timeout range is inherited from the base class (minInactiveTimeout, maxInactiveTimeout)
        // You could override these fields here if you wanted a specific timeout for this state.

        [Header("Basic Look To Shop Settings")]
        [Tooltip("The simulated destination point for NPCs deciding whether to enter the store.")]
        [SerializeField] private Vector3 simulatedDecisionPosition = new Vector3(0f, 0f, 10f); // Example point near entrance

        [Tooltip("Simulated speed for off-screen movement (units per second).")]
        [SerializeField] private float simulatedMovementSpeed = 3.5f; // Should match BasicPatrolStateSO

        [Tooltip("The simulated distance threshold to consider the NPC 'arrived' at the decision position.")]
        [SerializeField] private float simulatedArrivalThreshold = 0.5f;


        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            // Call base OnEnter for standard logging and setting ShouldUseTimeout.
            // BUT override the timer initialization and target position clearing.
            base.OnEnter(data, manager); // This will call Debug.Log entry, set ShouldUseTimeout

            // Set the simulated target position to the predefined decision point
            data.simulatedTargetPosition = simulatedDecisionPosition;
            Debug.Log($"SIM {data.Id}: BasicLookToShopState OnEnter. Setting simulated target to decision point: {data.simulatedTargetPosition.Value}");

            // Do NOT initialize data.simulatedStateTimer here.
            // The timer will start when the NPC reaches this point in SimulateTick.
            data.simulatedStateTimer = 0f; // Explicitly reset timer on entry
        }

        public override void SimulateTick(TiNpcData data, float deltaTime, BasicNpcStateManager manager)
    {
        if (data.simulatedTargetPosition.HasValue)
        {
             // Check if simulated target is reached
            bool reachedTarget = Vector3.Distance(data.CurrentWorldPosition, data.simulatedTargetPosition.Value) < simulatedArrivalThreshold;

            if (!reachedTarget)
            {
                 // Still moving towards the target
                 Vector3 direction = (data.simulatedTargetPosition.Value - data.CurrentWorldPosition).normalized;
                 float moveDistance = simulatedMovementSpeed * deltaTime;
                 data.CurrentWorldPosition += direction * moveDistance;
                 // Simulate rotation
                  if (direction.sqrMagnitude > 0.001f)
                  {
                       data.CurrentWorldRotation = Quaternion.LookRotation(direction);
                  }
                // Timer does NOT *start* counting down the actual timeout duration while moving.
                // The timer is decremented by the manager, but we want it to be >= 0 *only* when waiting.
                // A value like float.MaxValue or -1 can indicate 'not yet started'. Let's use -1.
                data.simulatedStateTimer = -1f; // Use -1 to indicate timer is paused/not yet started
             }
             else // Reached the target point
             {
                  // Clear the target position once arrived
                  data.simulatedTargetPosition = null; // Target reached, clear it

                  // If the timer is -1 (was moving or just arrived), start it now.
                  // Otherwise, it's already running (arrived previously), and we let the manager's
                  // universal decrement/check logic handle it.
                   if (data.simulatedStateTimer < 0) // Check for -1 or less
                   {
                        // Timer hasn't started counting down the actual timeout yet
                        data.simulatedStateTimer = Random.Range(MinInactiveTimeout, MaxInactiveTimeout); // Use base properties for range
                        Debug.Log($"SIM {data.Id}: Reached simulated decision point. Starting timeout timer for {data.simulatedStateTimer:F2}s (BasicLookToShop).");
                   }
                   // Else: timer is already running (was set in a previous tick), just let it continue.
                   // The manager's loop will handle the decrement and timeout transition to BasicExitingStore.
             }
        }
        else // Target position is null (already arrived or was set null elsewhere incorrectly)
        {
             // If target is null, the NPC should be waiting, and the timer should be running.
             // If the timer is <= 0 or still -1, it needs to be started/reset because target is null (indicating arrived) but timer is not counting down.
             if (data.simulatedStateTimer < 0 || data.simulatedStateTimer <= 0.001f) // Check for -1 or near zero
             {
                  // Timer needs to be started/reset because target is null (indicating arrived) but timer is not counting down
                  data.simulatedStateTimer = Random.Range(MinInactiveTimeout, MaxInactiveTimeout); // Use base properties for range
                  Debug.LogWarning($"SIM {data.Id}: Found in BasicLookToShop state with null target and non-counting timer. Assuming arrival happened and starting timer for {data.simulatedStateTimer:F2}s.");
             }
             // Else: timer is running correctly, manager will decrement and check.
        }
         // The BasicNpcStateManager.SimulateTickForNpc handles the actual timer decrement and timeout transition check based on ShouldUseTimeout.
         // We just manage *when* the timer starts counting down from its initial random value by setting it >= 0.
    }

         public override void OnExit(TiNpcData data, BasicNpcStateManager manager)
         {
             base.OnExit(data, manager); // Call base OnExit (logs exit)

             // Clear the simulated target position and timer on exit
             data.simulatedTargetPosition = null;
             data.simulatedStateTimer = 0f;
         }
    }
}
// --- END OF FILE BasicLookToShopStateSO.cs ---