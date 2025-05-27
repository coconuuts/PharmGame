// --- START OF FILE PatrolStateSO.cs ---

// --- Updated PatrolStateSO.cs (Phase 3, Substep 2 + Bug Fix) ---

using UnityEngine;
using System.Collections; // Needed for IEnumerator and Coroutine
using System;
using Game.NPC; // Needed for GeneralState enum and CustomerState enum
using Game.NPC.States; // Needed for NpcStateSO and NpcStateContext
using UnityEngine.AI; // Needed for NavMesh

namespace Game.NPC.States // States namespace
{
    /// <summary>
    /// A generic state for an NPC that wanders around a defined area.
    /// Corresponds to GeneralState.Patrol.
    /// </summary>
    [CreateAssetMenu(fileName = "PatrolState", menuName = "NPC/General States/Patrol", order = 2)] // Placed under General States, adjust order as needed
    public class PatrolStateSO : NpcStateSO
    {
        // Maps to the generic Patrol enum value
        public override System.Enum HandledState => TestState.Patrol; // <-- Use Patrol enum

        [Header("Patrol Settings")]
        [Tooltip("The minimum bounds (X, Z) of the rectangular patrol area.")]
        [SerializeField] private Vector2 patrolAreaMin = new Vector2(-10f, -10f);
        [Tooltip("The maximum bounds (X, Z) of the rectangular patrol area.")]
        [SerializeField] private Vector2 patrolAreaMax = new Vector2(10f, 10f);
        [Tooltip("The radius around a randomly picked point to sample the NavMesh.")]
        [SerializeField] private float navMeshSampleRadius = 5f;
        [Tooltip("Minimum time to wait at a patrol point.")]
        [SerializeField] private float minWaitTimeAtPoint = 1f;
        [Tooltip("Maximum time to wait at a patrol point.")]
        [SerializeField] private float maxWaitTimeAtPoint = 3f;
        [Tooltip("Probability (0-1) of a TI NPC transitioning to the LookingToShop state after waiting at a patrol point.")]
        [Range(0f, 1f)][SerializeField] private float chanceToShop = 0.2f; // 20% chance


        private Coroutine patrolWaitRoutine;


        public override void OnEnter(NpcStateContext context)
        {
            base.OnEnter(context); // Call base OnEnter (logs entry, enables Agent)

            // Initiate the first patrol move
            InitiatePatrolMove(context); // <-- Call the new method here
        }

        public override void OnUpdate(NpcStateContext context)
        {
            base.OnUpdate(context); // Call base OnUpdate (empty)
        }

        public override void OnReachedDestination(NpcStateContext context) // <-- Implement OnReachedDestination
        {
             Debug.Log($"{context.NpcObject.name}: Reached Patrol destination. Starting wait routine.", context.NpcObject);
              // Note: Runner.StopMoving() is called before this method by the Runner's Update loop.

             // Start the waiting routine via context
             patrolWaitRoutine = context.StartCoroutine(PatrolWaitRoutine(context));
        }


        public override void OnExit(NpcStateContext context)
        {
            base.OnExit(context); // Call base OnExit (logs exit, stops movement/rotation)
            // The Runner's TransitionToState handles stopping the active state coroutine (patrolWaitRoutine)
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

        /// <summary>
        /// Picks a new patrol point and initiates movement towards it.
        /// </summary>
        private void InitiatePatrolMove(NpcStateContext context) // <-- New method
        {
             Debug.Log($"{context.NpcObject.name}: Initiating new Patrol move.", context.NpcObject);

             Vector3 randomPatrolPoint = GetRandomPointInPatrolArea(context); // Pass context to potentially get current Y

             // Try to find a valid point on the NavMesh near the random point
             if (NavMesh.SamplePosition(randomPatrolPoint, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas)) // Check if hit.position is a valid point
             {
                 Vector3 navMeshPoint = hit.position;
                 Debug.Log($"{context.NpcObject.name}: Moving to random patrol point: {navMeshPoint}.", context.NpcObject);

                 // Initiate movement using the context helper (resets _hasReachedCurrentDestination flag)
                 bool moveStarted = context.MoveToDestination(navMeshPoint);

                 if (!moveStarted) // Add check for move failure
                 {
                     Debug.LogError($"{context.NpcObject.name}: Failed to start movement to patrol point {navMeshPoint}! Is the point on the NavMesh? Trying again.", context.NpcObject);
                     // Fallback: If movement fails, try again by re-calling InitiatePatrolMove.
                     // Use a short coroutine to prevent infinite loop if NavMesh is permanently bad
                     context.StartCoroutine(DelayedInitiatePatrolMove(context, 1f)); // <-- Start a coroutine for retry
                 }
             }
             else
             {
                 Debug.LogWarning($"{context.NpcObject.name}: Could not sample NavMesh near random point {randomPatrolPoint} within radius {navMeshSampleRadius}! Is the patrol area valid? Trying again.", context.NpcObject);
                 // Fallback: If no valid NavMesh point found, try again.
                 context.StartCoroutine(DelayedInitiatePatrolMove(context, 1f)); // <-- Start a coroutine for retry
             }
        }

         // Coroutine for delayed retry of InitiatePatrolMove on failure
         private IEnumerator DelayedInitiatePatrolMove(NpcStateContext context, float delay) // <-- New coroutine
         {
              Debug.Log($"{context.NpcObject.name}: Delaying next patrol move attempt for {delay} seconds.", context.NpcObject);
              yield return new WaitForSeconds(delay);
              // Check if still in Patrol state before retrying
              if (context.Runner.GetCurrentState() == this)
              {
                   InitiatePatrolMove(context);
              } else {
                  Debug.Log($"{context.NpcObject.name}: State changed while waiting for delayed patrol move retry. Aborting retry.", context.NpcObject);
              }
         }


        // Coroutine method to handle waiting and the decision
        private IEnumerator PatrolWaitRoutine(NpcStateContext context) // Correct signature
        {
            Debug.Log($"{context.NpcObject.name}: PatrolWaitRoutine started in {name}.", context.NpcObject);

            // Determine random wait time
            float waitTime = UnityEngine.Random.Range(minWaitTimeAtPoint, maxWaitTimeAtPoint);
            Debug.Log($"{context.NpcObject.name}: Waiting at patrol point for {waitTime:F2} seconds.", context.NpcObject);

            // Wait for the specified duration
            yield return new WaitForSeconds(waitTime);

            // --- Decision Logic ---
            // This logic ONLY applies to TI NPCs currently.
            // How do we know if this is a TI NPC? The Runner instance knows (IsTrueIdentityNpc).
            // We can access this via the context: context.Runner.IsTrueIdentityNpc.

            // NpcStateSO nextState = null; // No longer needed here if patrolling again

            if (context.Runner != null && context.Runner.IsTrueIdentityNpc)
            {
                 Debug.Log($"{context.NpcObject.name}: Finished waiting at patrol point. This is a TI NPC, deciding next state.", context.NpcObject);

                 // Implement the chance to transition to CustomerState.LookingToShop
                 if (UnityEngine.Random.value <= chanceToShop)
                 {
                     // Check if they are eligible/willing to shop (e.g., don't have items yet, or finished previous shopping trip)
                     // For now, a simple check: only shop if they have 0 items.
                     if (context.Shopper != null && context.Shopper.TotalQuantityToBuy == 0)
                     {
                          Debug.Log($"{context.NpcObject.name}: Decided to look to shop (Chance: {chanceToShop * 100}%). Transitioning to LookingToShop.", context.NpcObject);
                          // --- BUG FIX: Call GetStateSO via the Runner reference on the context ---
                          // nextState = context.Runner.GetStateSO(CustomerState.LookingToShop); // Get the LookingToShop state SO
                           context.TransitionToState(CustomerState.LookingToShop); // Transition using Enum
                          // --- END BUG FIX ---
                     }
                     else
                     {
                         // Decided to shop, but not eligible (already has items). Stay patrolling or go home?
                         // For now, just stay patrolling if already has items.
                         Debug.Log($"{context.NpcObject.name}: Decided to look to shop, but already has items ({context.Shopper?.TotalQuantityToBuy ?? -1}). Patrolling again.", context.NpcObject);
                         // Instead of transitioning to self, initiate a new move sequence
                         InitiatePatrolMove(context); // <-- Initiate new move within the same state
                     }
                 }
                 else
                 {
                     Debug.Log($"{context.NpcObject.name}: Decided to continue patrolling (Chance: {chanceToShop * 100}% failed). Patrolling again.", context.NpcObject);
                     // Instead of transitioning to self, initiate a new move sequence
                     InitiatePatrolMove(context); // <-- Initiate new move within the same state
                 }

                 // If for some reason GetStateSO returned null (state not found, fallbacks failed),
                 // context.TransitionToState(null) will handle finding a safe fallback like ReturningToPool.
                 // This call is correct as context *does* have a TransitionToState(NpcStateSO) method.
                 // context.TransitionToState(nextState); // REMOVED - only transition if going *out* of Patrol state
            }
            else
            {
                 // This logic is for non-TI NPCs or unexpected cases reaching Patrol state.
                 Debug.LogWarning($"{context.NpcObject.name}: Finished waiting in Patrol state, but is NOT a TI NPC. Transitioning to ReturningToPool (fallback).", context.NpcObject);
                 // This call is correct as context *does* have a TransitionToState(Enum) method.
                 context.TransitionToState(GeneralState.ReturningToPool);
            }


            Debug.Log($"{context.NpcObject.name}: PatrolWaitRoutine finished.", context.NpcObject); // This line might not be reached
        }
    }
}