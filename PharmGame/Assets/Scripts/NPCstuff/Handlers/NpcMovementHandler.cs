using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Game.Events;

namespace Game.NPC.Handlers
{
    /// <summary>
    /// Handles the movement and rotation of an NPC using a NavMeshAgent.
    /// Now includes explicit methods to enable/disable the agent.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NpcMovementHandler : MonoBehaviour
    {
        public NavMeshAgent Agent { get; private set; }

        [SerializeField] private float destinationReachedThreshold = 0.5f;
        [SerializeField] private float rotationSpeed = 5f;

        private Coroutine currentRotationCoroutine;

        // --- Public property to check if agent is enabled ---
        public bool IsAgentEnabled => Agent != null && Agent.enabled;

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            if (Agent == null)
            {
                Debug.LogError($"NpcMovementHandler on {gameObject.name}: NavMeshAgent component not found!", this);
                enabled = false;
            }

            if (Agent != null) Agent.enabled = false; // Still managed externally, initially off
        }

        private void OnEnable()
        {
             // Future: Subscriptions
        }

        private void OnDisable()
        {
             // Future: Unsubscriptions
             StopMoving();
             StopRotation();
             // --- Ensure agent is disabled on disable ---
             DisableAgent(); // This calls the modified DisableAgent
        }

        private void Update()
        {
            // --- ADD PATH DEBUGGING ---
            if (Agent != null && Agent.isActiveAndEnabled && Agent.hasPath)
            {
                Debug.DrawLine(transform.position, Agent.steeringTarget, Color.red); // Draw line to immediate steering target
                Color pathColor = Color.cyan;
                Vector3 lastCorner = transform.position;
                foreach (var corner in Agent.path.corners)
                {
                    Debug.DrawLine(lastCorner, corner, pathColor); // Draw segments of the path
                    lastCorner = corner;
                }
            }
            if (Agent != null && Agent.isActiveAndEnabled && !Agent.pathPending && !Agent.hasPath && Agent.remainingDistance > Agent.stoppingDistance)
            {
                 // Agent is active, not waiting for a path, has no path, and isn't already at the destination.
                 // This state often indicates a pathfinding failure after SetDestination was called.
                 // Debug.LogWarning($"NpcMovementHandler on {gameObject.name}: Agent has no path! Path Status: {Agent.pathStatus}", this); // Too noisy
            }
            // --- END ADD ---
        }

        // --- Public methods to control Agent enabled state ---
        /// <summary>
        /// Explicitly enables the NavMeshAgent.
        /// </summary>
        public void EnableAgent()
        {
            if (Agent != null && !Agent.enabled)
            {
                Agent.enabled = true;
                // Debug.Log($"NpcMovementHandler on {gameObject.name}: NavMeshAgent enabled.", this); // Too noisy
            }
        }

        /// <summary>
        /// Explicitly disables the NavMeshAgent and resets its current path.
        /// </summary>
        public void DisableAgent()
        {
            if (Agent != null && Agent.enabled)
            {
                Agent.enabled = false;
                // --- REMOVED: Agent.ResetPath() call here ---
                // Agent.ResetPath(); // Clear any active path when disabling - REMOVED
                // Debug.Log($"NpcMovementHandler on {gameObject.name}: NavMeshAgent disabled and path reset.", this); // Too noisy
            }
        }

        // Public API for Movement
        public bool SetDestination(Vector3 position)
        {
             if (Agent != null && Agent.enabled) // Ensure agent is enabled before setting destination
            {
                StopRotation(); // Stop any ongoing rotation before moving
                Agent.isStopped = false;
                Agent.destination = position; // Use .destination directly
                // Debug.Log($"NpcMovementHandler on {gameObject.name}: Set destination to {position}. Agent enabled: {Agent.enabled}", this); // Too noisy
                return true;
            }
             Debug.LogWarning($"NpcMovementHandler on {gameObject.name}: Cannot set destination to {position} - NavMeshAgent is null ({Agent == null}) or disabled ({Agent?.enabled == false}).", this);
            return false;
        }

        public void StopMoving()
        {
             if (Agent != null && Agent.isActiveAndEnabled)
             {
                  Agent.isStopped = true;
                  Agent.ResetPath(); // Keep ResetPath here, as it's called on an *active* agent
                  // Debug.Log($"NpcMovementHandler on {gameObject.name}: Movement stopped."); // Too noisy
             }
        }

        public bool Warp(Vector3 position)
        {
            if (Agent != null)
            {
                 // Warp requires the agent to be enabled to succeed.
                 bool wasEnabled = Agent.enabled;
                 if (!wasEnabled) Agent.enabled = true; // Temporarily enable

                 // --- Perform the Warp ---
                 bool success = Agent.Warp(position);
                 // ------------------------

                 if (success)
                 {
                      Debug.Log($"NpcMovementHandler on {gameObject.name}: Successfully Warped to {position}. Performing post-warp cleanup.", this);
                      // --- ONLY perform cleanup if Warp succeeded ---
                      Agent.ResetPath(); // Keep ResetPath here, as it's called on an *active* agent
                      Agent.isStopped = true;
                      // --------------------------------------------
                 }
                 else
                 {
                      Debug.LogWarning($"NpcMovementHandler on {gameObject.name}: Failed to Warp to {position}. Is the position *exactly* on the NavMesh?", this);
                      // If Warp fails, the agent is NOT on the NavMesh. Its state is unreliable.
                      // It might still be enabled if it was before the call.
                      // We should probably disable it here to be safe, unless the calling code expects to handle failure.
                      // For NPC spawning failure, disabling it is safer.
                      Agent.enabled = false; // Disable agent on failure
                 }

                 // --- Restore previous enabled state ONLY IF it was disabled and Warp succeeded ---
                 // If Warp failed, we disabled it regardless.
                 // If Warp succeeded and it was initially disabled, re-disable.
                 if (!wasEnabled && success)
                 {
                      Agent.enabled = false;
                 }
                 // -----------------------------------------------------------------------------

                 return success;
            }
            Debug.LogWarning($"NpcMovementHandler on {gameObject.name}: Cannot Warp - NavMeshAgent is null.", this);
            return false;
        }

        public bool IsAtDestination()
          {
          if (Agent == null || !Agent.isActiveAndEnabled)
          {
               return false; // Cannot be at destination if agent isn't active/enabled
          }

          // If agent is calculating path, we haven't arrived yet
          if (Agent.pathPending)
          {
               return false;
          }

          // --- Primary Arrival Check ---
          // Check if remaining distance is within stopping distance plus a small threshold
          // This is the most reliable check after SetDestination.
          bool isCloseEnough = Agent.remainingDistance <= Agent.stoppingDistance + destinationReachedThreshold;

          if (isCloseEnough)
          {
               // We are close. Now, are we stopped or just arrived?
               // Check if velocity is low OR if remaining distance is negligible (handles precision issues)
               // A small tolerance for velocity is needed because agents rarely hit exactly 0.
               // A very small tolerance for remainingDistance handles cases where remainingDistance might hover slightly above 0.
               if (Agent.velocity.sqrMagnitude < 0.1f * 0.1f || Agent.remainingDistance < 0.01f)
               {
                    return true; // We are close AND stopped or effectively there
               }
               // If we are close but still moving fast, we are not yet "at" the destination point.
          }

          // If not close enough, we are definitely not at the destination.
          // This also implicitly covers cases where hasPath is false and remainingDistance is large,
          // as remainingDistance > stoppingDistance will be true.

          // What about the !Agent.hasPath case?
          // If SetDestination was called and succeeded, hasPath *should* be true until near the end.
          // If hasPath is false *before* reaching the destination, it implies SetDestination failed
          // or the path became invalid. In such error cases, we are NOT at the destination.
          // The check above handles the successful path case.
          // If pathPending is false and hasPath is false, remainingDistance might be unreliable,
          // but the check `Agent.velocity.sqrMagnitude < 0.1f * 0.1f` when `isCloseEnough` is true
          // should still catch the "stopped near the point" condition.
          // Let's trust the remainingDistance check combined with velocity/negligible distance.

          return false;
          }

        // Public API for Rotation
        public void StartRotatingTowards(Quaternion targetRotation)
        {
             StopRotation();
             // Ensure the rotation routine receives a reference to this handler
             currentRotationCoroutine = StartCoroutine(RotateTowardsRoutine(targetRotation));
        }

        public void StopRotation()
        {
             if (currentRotationCoroutine != null)
             {
                 StopCoroutine(currentRotationCoroutine);
                 currentRotationCoroutine = null;
             }
        }

        // Coroutine for Rotation (runs within this MonoBehaviour)
        private IEnumerator RotateTowardsRoutine(Quaternion targetRotation)
        {
             // Add isActiveAndEnabled check at the start of the coroutine for robustness
             // Rotation can happen even if Agent is disabled (e.g., during path following)
             // So, check MonoBehaviour.isActiveAndEnabled instead of Agent.isActiveAndEnabled
             if (!isActiveAndEnabled) // Check MonoBehaviour state
             {
                  // Debug.LogWarning($"NpcMovementHandler on {gameObject.name}: Handler not active/enabled at start of rotation routine.", this); // Too noisy
                  yield break; // Cannot rotate if handler is not active
             }

             // Rotation should ideally happen regardless of Agent.enabled state,
             // but the coroutine is started by the MovementHandler.
             // If rotation is needed while Agent is disabled (e.g., during path following),
             // the path following handler will need its own rotation logic or call this.
             // For now, keep it tied to the MovementHandler's activity.

             // Only stop the agent if it's enabled and not already stopped
             bool wasAgentStopped = true;
             if (Agent != null && Agent.isActiveAndEnabled)
             {
                 wasAgentStopped = Agent.isStopped;
                 if (!wasAgentStopped)
                 {
                     Agent.isStopped = true; // Stop movement while rotating
                 }
             }


             Quaternion startRotation = transform.rotation;
             float angleDifference = Quaternion.Angle(startRotation, targetRotation);

             if (angleDifference < 0.1f)
             {
                  // Only restore agent state if it was active and not stopped initially
                  if (Agent != null && Agent.isActiveAndEnabled && !wasAgentStopped)
                  {
                       Agent.isStopped = false;
                  }
                  yield break;
             }

             // Adjust duration calculation for potentially faster rotation
             float duration = angleDifference / (rotationSpeed * 90f); // Rotate 90 degrees in ~1 second at speed 5
             if (duration < 0.05f) duration = 0.05f; // Minimum duration

             float timeElapsed = 0f;

             while (timeElapsed < duration)
             {
                  // Add isActiveAndEnabled check inside loop
                  if (!isActiveAndEnabled) yield break; // Check MonoBehaviour state

                  transform.rotation = Quaternion.Slerp(startRotation, targetRotation, timeElapsed / duration);
                  timeElapsed += Time.deltaTime;
                  yield return null;
             }

             transform.rotation = targetRotation;

             // Only restore agent state if it was active and not stopped initially
             if (Agent != null && Agent.isActiveAndEnabled && !wasAgentStopped)
             {
                  Agent.isStopped = false;
             }
        }
    }
}