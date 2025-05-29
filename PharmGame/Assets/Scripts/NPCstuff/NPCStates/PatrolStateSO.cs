// --- START OF FILE TestState.cs --- // Assuming this is your active PatrolStateSO

// --- START OF FILE TestState.cs ---

using UnityEngine;
using System;
using System.Collections;
using Game.NPC.States; // Base class NpcStateSO
using Random = UnityEngine.Random; // Specify UnityEngine.Random
using Game.NPC.TI; // Needed for TiNpcData

namespace Game.NPC.States // Assuming this is where your active states are
{
    // Assuming TestState.Patrol is the enum for your active patrol
    // And assuming you have a Scriptable Object class like PatrolStateSO inheriting from NpcStateSO
    // Let's assume the file is named PatrolStateSO.cs for clarity

    // [CreateAssetMenu(fileName = "PatrolState", menuName = "NPC/States/Patrol", order = 1)]
    public class PatrolStateSO : NpcStateSO // Assuming this is the name of your active Patrol State SO
    {
        public override System.Enum HandledState => TestState.Patrol; // Assuming TestState.Patrol is the enum

        [Header("Patrol Settings")]
        [Tooltip("The minimum simulated bounds (X, Z) of the rectangular patrol area.")]
        [SerializeField] private Vector2 patrolAreaMin = new Vector2(-10f, -10f); // Match BasicPatrol
        [Tooltip("The maximum simulated bounds (X, Z) of the rectangular patrol area.")]
        [SerializeField] private Vector2 patrolAreaMax = new Vector2(10f, 10f); // Match BasicPatrol
        [Tooltip("Minimum time to wait at a patrol point.")]
        [SerializeField] private float minWaitTimeAtPoint = 1f;
        [Tooltip("Maximum time to wait at a patrol point.")]
        [SerializeField] private float maxWaitTimeAtPoint = 3f;
        [Tooltip("Probability (0-1) of a TI NPC transitioning to the LookToShop state after waiting at a patrol point.")]
        [Range(0f, 1f)][SerializeField] private float chanceToShop = 0.2f;


        // Internal state needed by this SO per NPC instance
        // This would typically be managed by the state itself, but for SOs this is tricky.
        // We'll manage the state *data* on the TiNpcData or the Runner itself.
        // The wait timer logic for active states can live on the Runner or a handler.
        // Let's assume the Runner manages a simple state timer for the active states,
        // distinct from the basic state timer. Or perhaps we only manage wait time *here*
        // and rely on the Runner's _hasReachedCurrentDestination for flow.
        // For now, let's keep wait time logic within the state SO itself, managed via coroutine.


        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logging)

            // Reset arrival flag managed by the Runner
            context.Runner._hasReachedCurrentDestination = false;

            Vector3? targetPosition = null;

            // --- Check for carried-over destination from BasicPatrol ---
            if (context.Runner.IsTrueIdentityNpc && context.Runner.TiData != null && context.Runner.TiData.simulatedTargetPosition.HasValue)
            {
                targetPosition = context.Runner.TiData.simulatedTargetPosition.Value;
                Debug.Log($"{context.NpcObject.name}: PatrolState OnEnter - Using carried-over simulated target from TiData: {targetPosition.Value}.");
                // Clear the target from TiData now that the active state is handling it
                context.Runner.TiData.simulatedTargetPosition = null; // <-- CLEAR SAVED TARGET *AFTER READING IT*
                context.Runner.TiData.simulatedStateTimer = 0f;
            }
            // --- End Check ---

            // If no carried-over target, pick a new one
            if (!targetPosition.HasValue)
            {
                targetPosition = GetRandomPointInPatrolArea(context);
                Debug.Log($"{context.NpcObject.name}: PatrolState OnEnter - No carried-over target, picking new target: {targetPosition.Value}.");
                 if (context.Runner.IsTrueIdentityNpc && context.Runner.TiData != null)
                 {
                     context.Runner.TiData.simulatedStateTimer = 0f;
                 }
            }


            // Move to the target
            if (targetPosition.HasValue)
            {
                 context.MoveToDestination(targetPosition.Value); // This sets Runner.CurrentDestinationPosition
            } else
            {
                 Debug.LogError($"{context.NpcObject.name}: PatrolState OnEnter - Failed to get a valid target position! Cannot move.", context.NpcObject);
                 // Fallback: Maybe transition to Idle or ReturningToPool?
                 context.TransitionToState(GeneralState.Idle); // Example fallback
            }

            // Start the coroutine for waiting at points, but it only acts AFTER arrival
            context.StartCoroutine(WaitForPointCoroutine(context));
        }

        public override void OnUpdate(NpcStateContext context)
        {
            // Movement is handled by NpcMovementHandler and arrival check by Runner
            // The coroutine handles waiting after arrival.
        }

        public override void OnReachedDestination(NpcStateContext context)
        {
            base.OnReachedDestination(context); // Call base OnReachedDestination (logging)

            Debug.Log($"{context.NpcObject.name}: PatrolState reached destination. Starting wait timer (managed by coroutine).");

            // The WaitForPointCoroutine is already running and waiting for _hasReachedCurrentDestination to become true.
            // Now that it's true, the coroutine will proceed with the wait and decision logic.
            // No need to start another coroutine or timer here directly.
        }


        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logging)

            // Stop the wait coroutine if it's running
            // This is handled by the Runner in its TransitionToState -> StopManagedStateCoroutine

            // --- Save current destination if it's a TI NPC AND they were moving ---
            if (context.Runner.IsTrueIdentityNpc && context.Runner.TiData != null)
            {
                 // Check if the Runner currently has a valid destination *that it's moving towards*
                 // _hasReachedCurrentDestination being false indicates it was moving.
                 // CurrentDestinationPosition holds the target position.
                 if (!context.Runner._hasReachedCurrentDestination && context.Runner.CurrentDestinationPosition.HasValue)
                 {
                      // Save the active state's destination to the basic state's target field
                      context.Runner.TiData.simulatedTargetPosition = context.Runner.CurrentDestinationPosition.Value; // <-- SAVE ACTIVE TARGET
                      Debug.Log($"{context.NpcObject.name}: PatrolState OnExit - Saving current destination {context.Runner.CurrentDestinationPosition.Value} to TiData.simulatedTargetPosition.");
                 } else {
                      // If they reached the destination or had no target, ensure the simulated target is clear
                      context.Runner.TiData.simulatedTargetPosition = null;
                      // Debug.Log($"{context.NpcObject.name}: PatrolState OnExit - NPC was stationary or had no target. Clearing TiData.simulatedTargetPosition.");
                 }

                 // Also ensure the basic state's wait timer is reset when exiting active Patrol,
                 // unless the target was just saved (in which case the next Basic tick will start simulation movement).
                 // If a target was saved, basic patrol will move. If no target was saved, basic patrol will start waiting immediately.
                 // BasicPatrolStateSO.OnEnter handles the timer initialization logic based on target position.
            }
            // --- End Save ---
        }


        private IEnumerator WaitForPointCoroutine(NpcStateContext context)
        {
            // Wait until the NPC reaches the destination (flag set by Runner)
            yield return new WaitUntil(() => context.Runner._hasReachedCurrentDestination);

            // Wait at the point
            float waitTime = Random.Range(minWaitTimeAtPoint, maxWaitTimeAtPoint);
            Debug.Log($"{context.NpcObject.name}: Waiting at point for {waitTime:F2}s.");
            yield return new WaitForSeconds(waitTime);
            Debug.Log($"{context.NpcObject.name}: Finished waiting.");


            // Decision logic (only applies to TI NPCs likely)
            if (context.Runner.IsTrueIdentityNpc && context.Runner.TiData != null)
            {
                bool decidedToShop = Random.value <= chanceToShop;
                Debug.Log($"{context.NpcObject.name}: Decided to shop: {decidedToShop} (Chance: {chanceToShop * 100}%).");

                if (decidedToShop)
                {
                    // Transition to LookToShop
                    context.TransitionToState(CustomerState.LookingToShop); // Assuming CustomerState.LookingToShop is the correct enum
                    yield break; // End coroutine
                }
            }

            // If not transitioning to shop (either not TI or decided not to shop), pick a new patrol target
            Vector3 newTarget = GetRandomPointInPatrolArea(context);
            Debug.Log($"{context.NpcObject.name}: Decided to continue patrolling. Setting new target: {newTarget}.");
            context.MoveToDestination(newTarget); // This sets Runner.CurrentDestinationPosition and clears _hasReachedCurrentDestination
            context.StartCoroutine(WaitForPointCoroutine(context)); // Start new wait cycle after moving
        }


        /// <summary>
        /// Picks a random point within the defined XZ patrol area bounds.
        /// Uses the NPC's current Y position as a base for the random point.
        /// </summary>
        private Vector3 GetRandomPointInPatrolArea(NpcStateContext context)
        {
            float randomX = UnityEngine.Random.Range(patrolAreaMin.x, patrolAreaMax.x);
            float randomZ = UnityEngine.Random.Range(patrolAreaMin.y, patrolAreaMax.y); // Note: using y for Z axis in Vector2
            // Use the NPC's current Y position as the Y for the random point.
            // NavMesh sampling will correct it to the ground height.
            return new Vector3(randomX, context.NpcObject.transform.position.y, randomZ);
        }
    }
}
// --- END OF FILE TestState.cs ---