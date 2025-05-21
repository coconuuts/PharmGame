// Systems/Minigame/Movement/MinigameMovementManager.cs (Adjust namespace if needed)
using UnityEngine;
using System; // Needed for Action event
using System.Collections; // Needed for Coroutines

namespace Systems.Minigame.Movement // Using a sub-namespace for clarity
{
    /// <summary>
    /// General component to handle programmatic movement and physics state changes for GameObjects within a minigame.
    /// Uses Rigidbody.MovePosition for physics objects or Transform.position for non-physics objects.
    /// Manages temporary physics state changes during moves and physics activation delays.
    /// </summary>
    public class MinigameMovementManager : MonoBehaviour
    {
        /// <summary>
        /// Starts a coroutine to smoothly move a GameObject.
        /// Manages physics and collider state during the move.
        /// </summary>
        /// <param name="go">The GameObject to move.</param>
        /// <param name="targetPos">The target world position.</param>
        /// <param name="duration">The duration of the move.</param>
        /// <param name="useRigidbody">If true, uses Rigidbody.MovePosition and manipulates RB state. If false, uses Transform.position.</param>
        /// <param name="disablePhysicsDuringMove">If true and using Rigidbody, sets kinematic=true, gravity=false, collider=false during move. If not using Rigidbody, only affects collider.</param>
        /// <param name="onComplete">An optional callback Action to execute after the move finishes.</param>
        /// <returns>The started Coroutine, or null if the input GameObject is null or Rigidbody is missing when useRigidbody is true.</returns>
        public Coroutine StartSmoothMove(GameObject go, Vector3 targetPos, float duration, bool useRigidbody, bool disablePhysicsDuringMove, Action onComplete = null)
        {
            if (go == null)
            {
                Debug.LogWarning("MinigameMovementManager: StartSmoothMove called with null GameObject.", this);
                return null;
            }

            Rigidbody rb = useRigidbody ? go.GetComponent<Rigidbody>() : null;
            Collider col = go.GetComponent<Collider>();

            if (useRigidbody && rb == null)
            {
                Debug.LogError($"MinigameMovementManager: Cannot use Rigidbody movement for GameObject '{go.name}', Rigidbody component missing!", go);
                 return null;
            }

            // Capture initial state before modifying for the move
            bool initialKinematicState = useRigidbody && rb != null ? rb.isKinematic : false;
            bool initialGravityState = useRigidbody && rb != null ? rb.useGravity : false;
            bool initialColliderState = col != null ? col.enabled : false;
            Transform originalParent = go.transform.parent;

            return StartCoroutine(MoveGameObjectSmoothlyCoroutine(
                go,
                rb,
                col,
                originalParent,
                initialKinematicState,
                initialGravityState,
                initialColliderState,
                targetPos,
                duration,
                useRigidbody,
                disablePhysicsDuringMove,
                onComplete
            ));
        }

        private IEnumerator MoveGameObjectSmoothlyCoroutine(
            GameObject go,
            Rigidbody rb, // Pass Rigidbody explicitly
            Collider col,   // Pass Collider explicitly
            Transform originalParent, // Pass original parent
            bool initialKinematicState, // Pass initial states
            bool initialGravityState,
            bool initialColliderState,
            Vector3 targetPos,
            float duration,
            bool useRigidbody,
            bool disablePhysicsDuringMove,
            Action onComplete)
        {
             if (go == null) { Debug.LogWarning("MinigameMovementManager: GameObject became null at start of MoveCoroutine.", this); yield break; } // Defensive check

            Vector3 startPos = useRigidbody ? rb.position : go.transform.position;
            float timer = 0f;

            // --- Set State for Move ---
            if (useRigidbody && rb != null)
            {
                 if(disablePhysicsDuringMove) // Only modify if we intend to disable
                 {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                 }
            }

            if (col != null && disablePhysicsDuringMove) // Only modify if we intend to disable
            {
                 col.enabled = false;
            }

             // Unparent during physics move to avoid parent scale/rotation issues influencing position
             // This should happen regardless of useRigidbody if we expect world position
             // If animating local position, we would keep parent. This method is for world position.
             if (go.transform.parent != null) go.transform.SetParent(null);


            // --- The Move Loop ---
            while (timer < duration)
            {
                 if (go == null) { Debug.LogWarning("MinigameMovementManager: GameObject became null during smooth move loop.", this); yield break; }

                timer += Time.unscaledDeltaTime; // Use unscaledDeltaTime for time-independent movement
                float t = Mathf.Clamp01(timer / duration);
                Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);

                if (useRigidbody && rb != null)
                {
                    rb.MovePosition(currentPos);
                }
                else
                {
                    go.transform.position = currentPos;
                }
                yield return null; // Wait until next frame
            }

             // Ensure final position is exact
            if (go != null)
            {
                if (useRigidbody && rb != null) rb.MovePosition(targetPos);
                else go.transform.position = targetPos;

                 // Restore original parent
                 go.transform.SetParent(originalParent);

                 // --- Restore/Set State AFTER move ---
                 // We restore the *initial* state unless disablePhysicsDuringMove was true,
                 // in which case we leave it kinematic/collider off.
                 // The caller's `onComplete` callback is responsible for setting the *final desired* state
                 // (e.g., re-enabling physics after reclaiming).

                if (useRigidbody && rb != null)
                {
                     // If physics was disabled for the move, leave it off. Otherwise, restore original state.
                     if(disablePhysicsDuringMove)
                     {
                         rb.isKinematic = true;
                         rb.useGravity = false;
                         rb.linearVelocity = Vector3.zero; // Ensure static
                         rb.angularVelocity = Vector3.zero;
                     }
                     else
                     {
                         rb.isKinematic = initialKinematicState;
                         rb.useGravity = initialGravityState;
                     }
                }

                if (col != null)
                {
                     // If collider was disabled for the move, leave it off. Otherwise, restore original state.
                     if(disablePhysicsDuringMove)
                     {
                        col.enabled = false;
                     }
                     else
                     {
                        col.enabled = initialColliderState;
                     }
                }

                 // --- Invoke Callback ---
                 onComplete?.Invoke();
            }
             else { Debug.LogWarning("MinigameMovementManager: GameObject became null after move loop.", this); yield break; } // Final check if GameObject is null

            // Coroutine finishes here
        }


        /// <summary>
        /// Starts a coroutine to activate physics (set Rigidbody to non-kinematic and enable Collider)
        /// after a specified delay, starting from a disabled state.
        /// </summary>
        /// <param name="go">The GameObject with Rigidbody and Collider.</param>
        /// <param name="delay">The delay in seconds (using unscaled time).</param>
        /// <returns>The started Coroutine, or null if the input GameObject is null or Rigidbody is missing.</returns>
        public Coroutine StartPhysicsActivationDelay(GameObject go, float delay)
        {
             if (go == null)
             {
                 Debug.LogWarning("MinigameMovementManager: StartPhysicsActivationDelay called with null GameObject.", this);
                 return null;
             }
             Rigidbody rb = go.GetComponent<Rigidbody>();
             Collider col = go.GetComponent<Collider>();
             if (rb == null)
             {
                 Debug.LogWarning($"MinigameMovementManager: Cannot activate physics for GameObject '{go.name}', Rigidbody component missing!", go);
                 return null;
             }
             // Collider can be null, that's fine

             // Ensure they are off/kinematic before the delay using the helper
             SetPhysicsState(go, false); // Set to kinematic, gravity off, collider off

             return StartCoroutine(ActivatePhysicsAfterDelayCoroutine(go, rb, col, delay));
        }


        private IEnumerator ActivatePhysicsAfterDelayCoroutine(GameObject go, Rigidbody rb, Collider col, float delay)
        {
            // Wait using unscaled time
            float timer = 0f;
            while(timer < delay)
            {
                if (go == null || !go.activeInHierarchy || rb == null) // Also check RB in case component was removed
                {
                     // Debug.LogWarning($"MinigameMovementManager: GameObject '{go?.name ?? "null"}' or its Rigidbody became null/inactive during physics activation delay.", this);
                     yield break; // Stop if object is gone, inactive, or RB is missing
                }
                timer += Time.unscaledDeltaTime;
                yield return null;
            }
             // yield return new WaitForSecondsRealtime(delay); // Alternative using real-time

            // Re-enable if the GameObject is still active, RB exists, and it hasn't been discarded/reclaimed/pooled already
            if (go != null && go.activeInHierarchy && rb != null)
            {
                 // Use the helper method to set the state to active physics
                 SetPhysicsState(go, true); // Set to non-kinematic, gravity on, collider on
                 // Debug.Log($"MinigameMovementManager: Physics activated for '{go.name}' after delay.", go); // Too spammy
            }
             // else { Debug.Log($"MinigameMovementManager: GameObject '{go?.name ?? "null"}' not active or RB missing after delay. Skipping physics activation.", this); }
        }


         /// <summary>
         /// Sets the kinematic, gravity, and collider state of a GameObject.
         /// Assumes Rigidbody and Collider components might be present.
         /// </summary>
         /// <param name="go">The GameObject.</param>
         /// <param name="activatePhysics">If true, sets kinematic=false, useGravity=true, and enables collider. If false, sets kinematic=true, useGravity=false, and disables collider.</param>
         public void SetPhysicsState(GameObject go, bool activatePhysics)
         {
             if (go == null)
             {
                 Debug.LogWarning("MinigameMovementManager: SetPhysicsState called with null GameObject.", this);
                 return;
             }

             Rigidbody rb = go.GetComponent<Rigidbody>();
             Collider col = go.GetComponent<Collider>();

             if (rb != null)
             {
                 rb.isKinematic = !activatePhysics;
                 rb.useGravity = activatePhysics; // Assume gravity is on when physics is active

                 // Optional: clear velocities when disabling physics
                 if (!activatePhysics)
                 {
                     rb.linearVelocity = Vector3.zero;
                     rb.angularVelocity = Vector3.zero;
                 }
             }

             if (col != null)
             {
                 col.enabled = activatePhysics;
             }
             // Debug.Log($"MinigameMovementManager: Physics state set for '{go.name}': {(activatePhysics ? "Active" : "Disabled")}.", go);
         }

        /// <summary>
        /// Stops all coroutines started by this MinigameMovementManager instance.
        /// Should be called by the owning minigame script during its cleanup.
        /// </summary>
        public void PerformCleanup()
        {
            StopAllCoroutines();
            Debug.Log("MinigameMovementManager: Performed cleanup (stopped all coroutines).", this);
            // Note: This does NOT reset the physics state of any objects that were mid-move
            // or waiting for activation. The owning minigame's CleanupMinigameAssets()
            // handles resetting pooled objects' states when returning them.
        }
    }
}