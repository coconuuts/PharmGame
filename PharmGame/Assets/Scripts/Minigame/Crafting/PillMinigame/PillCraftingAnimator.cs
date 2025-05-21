// Systems/Minigame/Animation/PillCraftingAnimator.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening; // Needed for DOTween animations
using Systems.Minigame.Config; // <--- ADDED This using directive

namespace Systems.Minigame.Animation // A new sub-namespace for animation helpers
{
    /// <summary>
    /// Handles the DOTween animations specifically for the Pill Crafting Minigame visuals.
    /// Should be attached to the same GameObject as the PillCraftingMinigame script.
    /// --- MODIFIED: Configuration data moved to ScriptableObject. ---
    /// </summary>
    public class PillCraftingAnimator : MinigameAnimatorBase
    {
        [Header("Configuration Data")]
        [Tooltip("Reference to the Pill Minigame ScriptableObject configuration asset.")]
        [SerializeField] private PillMinigameConfigData pillConfig; // <--- ADDED This field


        [Header("Animated Object References (Set at Runtime)")] // <--- Updated Header
        [Tooltip("The Transform of the instantiated Pill Stock Container.")]
        [SerializeField] private Transform pillStockContainerTransform; // This needs to be set at runtime after instantiation

        [Tooltip("The Transform of the instantiated Prescription Container.")]
        [SerializeField] private Transform prescriptionContainerTransform; // This needs to be set at runtime after instantiation

        [Tooltip("The Transform of the instantiated Prescription Container Lid.")]
        private Transform prescriptionContainerLidTransform; // This needs to be set at runtime after instantiation


        [Header("Prefab/Fixed Position References (Set on Animator Prefab)")] // <--- Updated Header
        [Tooltip("The Transform representing the point where pills should appear to pour from on the Stock Container prefab.")]
        [SerializeField] public Transform pillStockPourPoint; // Link to the pour point *on the prefab*

        [Tooltip("The Transform representing a midpoint in the animation path for pills entering the prescription container.")]
        [SerializeField] private Transform pillPackageMidpoint;

        [Tooltip("The Transform representing the point pills drop into the Prescription Container prefab.")]
        [SerializeField] public Transform prescriptionPillDropPoint; // Link to the drop point *on the prefab*

        // --- Public methods for PillCraftingMinigame to call ---

        /// <summary>
        /// Sets the main runtime references for the animated container root objects after they are instantiated.
        /// Specific child references (PourPoint, DropPoint, Midpoint) are expected to be linked via the Inspector
        /// on the animator's prefab. The Lid transform is passed directly as a runtime reference.
        /// Called by PillCraftingMinigame.PouringSequence and PackagingSequence.
        /// --- MODIFIED: Added lidContainer parameter ---
        /// </summary>
        public void SetRuntimeReferences(Transform stockContainer, Transform prescriptionContainer, Transform lidContainer)
        {
            pillStockContainerTransform = stockContainer;
            prescriptionContainerTransform = prescriptionContainer;
            prescriptionContainerLidTransform = lidContainer;

             Debug.Log($"PillCraftingAnimator: Runtime references set. Stock: {pillStockContainerTransform?.name ?? "null"}, Prescription: {prescriptionContainerTransform?.name ?? "null"}, Lid: {prescriptionContainerLidTransform?.name ?? "null"}.");
             // Optional: Log the linked prefab references to confirm they are set (These are not from the SO)
             Debug.Log($"PillCraftingAnimator: Prefab References - Pour Point: {pillStockPourPoint?.name ?? "null"}, Drop Point: {prescriptionPillDropPoint?.name ?? "null"}, Midpoint: {pillPackageMidpoint?.name ?? "null"}.");
        }


        /// <summary>
        /// Animates the pill stock container tilting for pouring.
        /// --- Returns a Sequence to yield on in the calling script ---
        /// --- Reads animation settings from config SO ---
        /// </summary>
        public Sequence AnimatePouring()
        {
             // --- UPDATED: Check config SO reference ---
            if (pillConfig == null)
            {
                 Debug.LogError("PillCraftingAnimator: Pill Config Data ScriptableObject is not assigned! Cannot animate pouring.", this);
                 return CreateSequence(); // Return an empty sequence
            }
             // -----------------------------------------

            if (pillStockContainerTransform == null)
            {
                Debug.LogWarning("PillCraftingAnimator: Pill Stock Container Transform is null (runtime ref missing). Cannot animate pouring.", this);
                return CreateSequence(); // Return an empty sequence
            }
             if (pillStockPourPoint == null) // This is a prefab reference, checked once during config setup
             {
                 Debug.LogWarning("PillCraftingAnimator: Pill Stock Pour Point Transform is null (prefab ref missing). Pouring animation may not look correct.", this);
             }


            Sequence sequence = CreateSequence();

            // Get the current rotation as the start point relative to its current orientation
            Vector3 startLocalEuler = pillStockContainerTransform.localEulerAngles;
            // Calculate the tilted rotation (assuming tilting forward around the X axis)
            Vector3 tiltedLocalEuler = startLocalEuler;
            // --- UPDATED: Read tilt angle from config SO ---
            tiltedLocalEuler.z += pillConfig.pourTiltAngle; // Adjust based on your container's local pivot and desired tilt direction
            // ----------------------------------------------


            // Add tweens to the sequence
            // Using AnimateRotation assumes world rotation or the object has no rotating parent.
            // If the container has a rotating parent, use AnimateLocalRotation.
            // --- UPDATED: Read durations and interval from config SO ---
            sequence.Append(AnimateRotation(pillStockContainerTransform, tiltedLocalEuler, pillConfig.pourTiltDuration, Ease.OutSine)); // Tilt forward
            sequence.AppendInterval(pillConfig.pillsDropIntervalDuration); // Wait for defined interval while pills drop (pills should spawn during this)
            sequence.Append(AnimateRotation(pillStockContainerTransform, startLocalEuler, pillConfig.pourReturnDuration, Ease.InSine)); // Tilt back
            // ----------------------------------------------------------

            // --- UPDATED: Include SO values in debug log ---
            Debug.Log($"PillCraftingAnimator: Created Pouring sequence. Total Duration: {sequence.Duration(false)}s (Tilt: {pillConfig.pourTiltDuration} + Interval: {pillConfig.pillsDropIntervalDuration} + Return: {pillConfig.pourReturnDuration}).");
            // -------------------------------------------------

            return sequence;
        }


         /// <summary>
         /// Animates the prescription container lid opening, waits for pills, and closes the lid.
         /// --- Returns a Sequence to yield on ---
         /// --- Uses defined interval duration ---
         /// --- Reads animation settings from config SO ---
         /// </summary>
        public Sequence AnimatePackaging()
        {
             // --- UPDATED: Check config SO reference ---
            if (pillConfig == null)
            {
                 Debug.LogError("PillCraftingAnimator: Pill Config Data ScriptableObject is not assigned! Cannot animate packaging sequence.", this);
                 return CreateSequence();
            }
            // -----------------------------------------

             if (prescriptionContainerTransform == null)
             {
                 Debug.LogWarning("PillCraftingAnimator: Prescription Container Transform is null (runtime ref missing). Cannot animate packaging sequence.", this);
                 return CreateSequence();
             }
             // Lid transform is now a runtime reference
             if (prescriptionContainerLidTransform == null)
             {
                 Debug.LogWarning("PillCraftingAnimator: Prescription Container Lid Transform is null (runtime reference not set?). Lid animations will be skipped.", this);
             }
             if (prescriptionPillDropPoint == null) // This is a prefab reference
             {
                 Debug.LogWarning("PillCraftingAnimator: Prescription Pill Drop Point Transform is null (prefab ref missing). Pill packaging animations will use container center.", this);
             }
             if (pillPackageMidpoint == null) // This is a prefab reference
             {
                 Debug.LogError("PillCraftingAnimator: Pill Package Midpoint Transform is null (prefab ref missing)! Pill packaging animation will not run correctly.", this);
                 // Returning empty sequence or null here might break the calling coroutine's timing.
                 // Just log error and proceed; the individual pill animations will fail if midpoint is needed.
             }


             Sequence sequence = CreateSequence();

             // 1. Animate Lid Open (only if lid transform exists - now checking runtime ref)
             if (prescriptionContainerLidTransform != null)
             {
                  // --- UPDATED: Read lid animation settings from config SO ---
                  sequence.Append(AnimateLocalRotation(prescriptionContainerLidTransform, pillConfig.lidOpenLocalEuler, pillConfig.lidAnimateDuration, Ease.OutSine));
                  Debug.Log("PillCraftingAnimator: Appended Lid Open animation.");
                  // ---------------------------------------------------------
             }


             // 2. Wait for pills to animate into the container (this will be handled by the calling script running a coroutine in parallel)
             // The calling script *starts* the individual pill animations during this interval.
             // --- UPDATED: Read interval duration from config SO ---
             sequence.AppendInterval(pillConfig.pillsDropIntervalDuration); // Use the defined interval duration.
             Debug.Log($"PillCraftingAnimator: Appended interval for pills to drop ({pillConfig.pillsDropIntervalDuration}s).");
             // ----------------------------------------------------


             // 3. Animate Lid Close (only if lid transform exists - now checking runtime ref)
             if (prescriptionContainerLidTransform != null)
             {
                 // --- UPDATED: Read lid animation settings from config SO ---
                 sequence.Append(AnimateLocalRotation(prescriptionContainerLidTransform, pillConfig.lidClosedLocalEuler, pillConfig.lidAnimateDuration, Ease.InSine));
                 Debug.Log("PillCraftingAnimator: Appended Lid Close animation.");
                 // ---------------------------------------------------------
             }

             // 4. (Optional) Animate container visual confirmation (e.g., pulse scale slightly)
             // sequence.Append(AnimateScale(prescriptionContainerTransform, Vector3.one * 1.1f, 0.2f, Ease.OutSine));
             // sequence.Append(AnimateScale(prescriptionContainerTransform, Vector3.one, 0.2f, Ease.InSine));

             Debug.Log($"PillCraftingAnimator: Created Packaging sequence. Total Duration: {sequence.Duration(false)}s.");

             return sequence;
        }

         // Need a way for the animator to know the number of counted pills to estimate interval (kept)
         private int countedPillsCount = 0;
         public void SetCountedPillsCount(int count)
         {
              countedPillsCount = count;
         }


        /// <summary>
        /// Animates a single pill moving into the prescription container via a midpoint using a DOPath.
        /// This creates a smooth curve or linear path through the waypoints. Physics is temporarily disabled.
        /// --- MODIFIED: Now uses DOPath for a single continuous tween ---
        /// --- Reads animation settings (like duration) from config SO ---
        /// </summary>
        /// <param name="pillGO">The pill GameObject to animate.</param>
         /// <param name="finalTargetPosition">The final target position for the pill inside the container (scattered position).</param>
        public Tween AnimatePillIntoContainer(GameObject pillGO, Vector3 finalTargetPosition)
        {
             // --- UPDATED: Check config SO reference ---
            if (pillConfig == null)
            {
                 Debug.LogError("PillCraftingAnimator: Pill Config Data ScriptableObject is not assigned! Cannot animate pill packaging.", this);
                 return null; // Indicate failure
            }
            // -----------------------------------------

             if (pillGO == null || !pillGO.activeInHierarchy)
             {
                 Debug.LogWarning("PillCraftingAnimator: AnimatePillIntoContainer called with null or inactive pill GameObject.", this);
                 return null;
             }

             if (pillPackageMidpoint == null) // Prefab reference
             {
                  Debug.LogError($"PillCraftingAnimator: Pill Package Midpoint Transform is null (prefab ref missing)! Cannot animate pill '{pillGO.name}' packaging using a path.", this);
                  return null; // Indicate failure
             }

             Transform pillTransform = pillGO.transform;
             Rigidbody pillRB = pillGO.GetComponent<Rigidbody>();
             Collider pillCol = pillGO.GetComponent<Collider>();

             // Store the pill's starting position
             Vector3 startPosition = pillTransform.position;
             Vector3 midpointPosition = pillPackageMidpoint.position; // Prefab reference

             // Ensure physics is off during animation (Animator's responsibility for its animation)
             if (pillRB != null)
             {
                 pillRB.isKinematic = true;
                 pillRB.useGravity = false; // Disable gravity
                 pillRB.linearVelocity = Vector3.zero;
                 pillRB.angularVelocity = Vector3.zero;
             }
             if (pillCol != null) pillCol.enabled = false;

             // Define the waypoints for the path
             Vector3[] waypoints = { startPosition, midpointPosition, finalTargetPosition };

             // Create the DOPath tween
              // --- UPDATED: Read animation duration from config SO ---
             Tween pathTween = pillTransform.DOPath(waypoints, pillConfig.pillPackageAnimateDuration, PathType.Linear, PathMode.Ignore) // <--- Read duration from SO
                                          .SetEase(Ease.Linear); // Use Linear ease for constant speed along the path
              // ----------------------------------------------------

             // Use SetTarget(this.gameObject) so this tween is killed when the Animator's cleanup runs
             pathTween.SetTarget(this.gameObject);

             // Optional: Add rotation animation alongside the path (also needs SetTarget)
             // pillTransform.DOLocalRotate(new Vector3(0, 360, 0), pillConfig.pillPackageAnimateDuration, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1, LoopType.Incremental).SetTarget(this.gameObject); // Example continuous spin


             // Note: Re-enabling physics/collider after this animation finishes is not needed
             // as packaged pills should stay kinematic and non-colliding. Cleanup handles reset for pooling.

             Debug.Log($"PillCraftingAnimator: Created DOPath packaging tween for pill '{pillGO.name}'. Total Duration: {pathTween.Duration(false)}s.");

             return pathTween; // Return the created path tween
        }


        // --- Abstract method implementation ---

        /// <summary>
        /// Implements the abstract method from MinigameAnimatorBase.
        /// Stops all DOTween animations managed by this animator and cleans up animator references.
        /// </summary>
        public override void PerformAnimatorCleanup()
        {
            StopAllTweens(); // Call the base class method to kill tweens (including DOPath tweens) associated with this GO
            Debug.Log("PillCraftingAnimator: Performed specific animator cleanup (killed tweens).", this);

            // Null out runtime references here as they might be pooled/destroyed
            pillStockContainerTransform = null;
            prescriptionContainerTransform = null;
            prescriptionContainerLidTransform = null;

            // Prefab references (PourPoint, DropPoint, Midpoint) linked in Inspector persist.
            countedPillsCount = 0; // Reset internal count
        }
    }
}