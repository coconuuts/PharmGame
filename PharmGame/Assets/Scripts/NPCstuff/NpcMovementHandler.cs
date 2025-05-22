// --- Updated NpcMovementHandler.cs ---
// (Content is largely the same as Substep 2's version, but confirming it has the logic)
using UnityEngine;
using UnityEngine.AI;
using System.Collections; // Added for Coroutine
using Game.Events; // Needed if you decide to publish events from here

namespace Game.NPC.Handlers
{
    /// <summary>
    /// Handles the movement and rotation of an NPC using a NavMeshAgent.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NpcMovementHandler : MonoBehaviour
    {
        public NavMeshAgent Agent { get; private set; }

        [SerializeField] private float destinationReachedThreshold = 0.5f;
        [SerializeField] private float rotationSpeed = 5f; // Keep this here

        private Coroutine currentRotationCoroutine;

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            if (Agent == null)
            {
                Debug.LogError($"NpcMovementHandler on {gameObject.name}: NavMeshAgent component not found!", this);
                enabled = false;
            }

            if (Agent != null) Agent.enabled = false; // Still managed externally
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
        }

        private void Update()
        {
            // Logic that might need to run every frame for movement or checking status
            // The state logic components will typically call IsAtDestination() directly,
            // or we could add logic here to publish NpcReachedDestinationEvent when IsAtDestination() becomes true.
            // For now, let the state logic check.
        }

        // Public API for Movement
        public bool SetDestination(Vector3 position)
        {
            // ... (Implementation from Substep 2 remains) ...
             if (Agent != null && Agent.enabled)
            {
                StopRotation(); // Stop any ongoing rotation before moving
                Agent.isStopped = false;
                Agent.destination = position; // Use .destination directly
                // SetDestination returns bool only if the path is *valid* immediately.
                // Setting .destination is generally safer and the pathPending/completion check happens later.
                // Debug.Log($"NpcMovementHandler on {gameObject.name}: Set destination to {position}."); // Optional log
                return true; // Assume setting destination always 'succeeds' unless agent is null/disabled
            }
             Debug.LogWarning($"NpcMovementHandler on {gameObject.name}: Cannot set destination - NavMeshAgent is null or disabled.", this);
            return false;
        }

        public void StopMoving()
        {
             // ... (Implementation from Substep 2 remains) ...
             if (Agent != null && Agent.isActiveAndEnabled)
             {
                  // Agent is active and on NavMesh, safe to stop and reset path
                  Agent.isStopped = true;
                  Agent.ResetPath();
                  // Debug.Log($"NpcMovementHandler on {gameObject.name}: Movement stopped.");
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
                      Agent.ResetPath();
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
            if (Agent == null || !Agent.isActiveAndEnabled || Agent.pathPending)
             {
                 return false;
             }

             // Check if remaining distance is within stopping distance plus a small threshold
             // Need to check if a path is even active or completed before checking remainingDistance reliably
             if (!Agent.hasPath) return Agent.velocity.sqrMagnitude == 0f; // If no path, check if already stopped (e.g. after Warp)

             // If there is a path, check remaining distance and speed
             bool isCloseEnough = Agent.remainingDistance <= Agent.stoppingDistance + destinationReachedThreshold;

             if (isCloseEnough)
             {
                  // Check if velocity is low OR if the path is effectively finished (remainingDistance very small AND path calculation is done)
                  if (Agent.velocity.sqrMagnitude < 0.1f * 0.1f || Agent.remainingDistance < 0.1f) // Added small remainingDistance check for edge cases
                  {
                       return true;
                  }
             }

             return false;
        }

        // Public API for Rotation
        public void StartRotatingTowards(Quaternion targetRotation)
        {
             // ... (Implementation from Substep 2 remains) ...
             StopRotation();
             // Ensure the rotation routine receives a reference to this handler
             currentRotationCoroutine = StartCoroutine(RotateTowardsRoutine(targetRotation));
        }

        public void StopRotation()
        {
             // ... (Implementation from Substep 2 remains) ...
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
             if (Agent == null || !Agent.isActiveAndEnabled)
             {
                  Debug.LogWarning($"NpcMovementHandler on {gameObject.name}: Agent not active/enabled at start of rotation routine.", this);
                  yield break; // Cannot rotate if agent is not active
             }

             bool wasAgentStopped = Agent.isStopped;
             if (!wasAgentStopped)
             {
                 Agent.isStopped = true;
             }

             Quaternion startRotation = transform.rotation;
             float angleDifference = Quaternion.Angle(startRotation, targetRotation);

             if (angleDifference < 0.1f)
             {
                  if (!wasAgentStopped)
                  {
                       Agent.isStopped = false;
                  }
                  yield break;
             }

             float duration = angleDifference / (rotationSpeed * 100f);
             if (duration < 0.1f) duration = 0.1f;

             float timeElapsed = 0f;

             while (timeElapsed < duration)
             {
                  // Add isActiveAndEnabled check inside loop, although stopping coroutine on disable is better
                  if (Agent == null || !Agent.isActiveAndEnabled) yield break;

                  transform.rotation = Quaternion.Slerp(startRotation, targetRotation, timeElapsed / duration);
                  timeElapsed += Time.deltaTime;
                  yield return null;
             }

             transform.rotation = targetRotation;

             if (!wasAgentStopped)
             {
                  Agent.isStopped = false;
             }
        }
    }
}