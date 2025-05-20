// Systems/Minigame/Animation/PillCraftingAnimator.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening; // Needed for DOTween animations

namespace Systems.Minigame.Animation
{
    /// <summary>
    /// Handles the DOTween animations specifically for the Pill Crafting Minigame visuals.
    /// Should be attached to the same GameObject as the PillCraftingMinigame script.
    /// </summary>
    public class PillCraftingAnimator : MinigameAnimatorBase
    {
        [Header("Animated Object References (Set at Runtime)")]
        [Tooltip("The Transform of the instantiated Pill Stock Container.")]
        [SerializeField] private Transform pillStockContainerTransform; // This needs to be set at runtime after instantiation

        [Tooltip("The Transform of the instantiated Prescription Container.")]
        [SerializeField] private Transform prescriptionContainerTransform; // This needs to be set at runtime after instantiation

        [Tooltip("The Transform of the instantiated Prescription Container Lid.")]
        private Transform prescriptionContainerLidTransform; // This needs to be set at runtime after instantiation


        [Header("Prefab/Fixed Position References (Set on Animator Prefab)")]
        [Tooltip("The Transform representing the point where pills should appear to pour from on the Stock Container prefab.")]
        [SerializeField] public Transform pillStockPourPoint; // Link to the pour point *on the prefab*

        // --- ADDED: Midpoint for Pill Packaging Animation ---
        [Header("Packaging Path References")]
        [Tooltip("The Transform representing a midpoint in the animation path for pills entering the prescription container.")]
        [SerializeField] private Transform pillPackageMidpoint;
        // ---------------------------------------------------

        [Tooltip("The Transform representing the point pills drop into the Prescription Container prefab.")]
        [SerializeField] public Transform prescriptionPillDropPoint; // Link to the drop point *on the prefab*


         [Header("Animation Settings")]
         [Tooltip("How many degrees the pill stock container tilts forward when pouring.")]
         [SerializeField] private float pourTiltAngle = 45f;
         [Tooltip("Duration of the pill stock container tilting forward.")]
         [SerializeField] public float pourTiltDuration = 1.0f; // Make public for PillCraftingMinigame to read

         [Tooltip("Duration of the pill stock container tilting back.")]
         [SerializeField] private float pourReturnDuration = 0.5f;
         [Tooltip("Duration for the lid to animate open/closed.")]
         [SerializeField] public float lidAnimateDuration = 0.3f; // Make public for PillCraftingMinigame to read
         [Tooltip("Local Euler angle for the prescription container lid when open.")]
         [SerializeField] private Vector3 lidOpenLocalEuler;
         [Tooltip("Local Euler angle for the prescription container lid when closed.")]
         [SerializeField] private Vector3 lidClosedLocalEuler;

          [Tooltip("Duration for individual pills to animate into the packaging container (Total for the entire path).")]
         [SerializeField] public float pillPackageAnimateDuration = 0.5f; // Duration for the entire path

         [Tooltip("Duration of the interval where pills drop during packaging animation.")]
         [SerializeField] public float pillsDropIntervalDuration = 2.0f; // Make public for PillCraftingMinigame to read


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
             // Optional: Log the linked prefab references to confirm they are set
             Debug.Log($"PillCraftingAnimator: Prefab References - Pour Point: {pillStockPourPoint?.name ?? "null"}, Drop Point: {prescriptionPillDropPoint?.name ?? "null"}, Midpoint: {pillPackageMidpoint?.name ?? "null"}.");
        }


        /// <summary>
        /// Animates the pill stock container tilting for pouring.
        /// --- Returns a Sequence to yield on in the calling script ---
        /// </summary>
        public Sequence AnimatePouring()
        {
            if (pillStockContainerTransform == null)
            {
                Debug.LogWarning("PillCraftingAnimator: Pill Stock Container Transform is null. Cannot animate pouring.", this);
                return CreateSequence(); // Return an empty sequence
            }
             if (pillStockPourPoint == null)
             {
                 Debug.LogWarning("PillCraftingAnimator: Pill Stock Pour Point Transform is null. Pouring animation may not look correct.", this);
                 // Animation can still proceed, but visual sync might be off.
             }


            Sequence sequence = CreateSequence();

            // Get the current rotation as the start point relative to its current orientation
            Vector3 startLocalEuler = pillStockContainerTransform.localEulerAngles;
            // Calculate the tilted rotation (assuming tilting forward around the X axis)
            Vector3 tiltedLocalEuler = startLocalEuler;
            tiltedLocalEuler.z += pourTiltAngle; // Adjust based on your container's local pivot and desired tilt direction


            // Add tweens to the sequence
            // Using AnimateRotation assumes world rotation or the object has no rotating parent.
            // If the container has a rotating parent, use AnimateLocalRotation.
            sequence.Append(AnimateRotation(pillStockContainerTransform, tiltedLocalEuler, pourTiltDuration, Ease.OutSine)); // Tilt forward
            sequence.AppendInterval(pillsDropIntervalDuration); // Wait for defined interval while pills drop (pills should spawn during this)
            sequence.Append(AnimateRotation(pillStockContainerTransform, startLocalEuler, pourReturnDuration, Ease.InSine)); // Tilt back

            Debug.Log($"PillCraftingAnimator: Created Pouring sequence. Total Duration: {sequence.Duration(false)}s (Tilt: {pourTiltDuration} + Interval: {pillsDropIntervalDuration} + Return: {pourReturnDuration}).");

            return sequence;
        }


         /// <summary>
         /// Animates the prescription container lid opening, waits for pills, and closes the lid.
         /// --- Returns a Sequence to yield on ---
         /// --- Uses defined interval duration ---
         /// </summary>
        public Sequence AnimatePackaging()
        {
             if (prescriptionContainerTransform == null)
             {
                 Debug.LogWarning("PillCraftingAnimator: Prescription Container Transform is null. Cannot animate packaging sequence.", this);
                 return CreateSequence();
             }
             // Lid transform is now a runtime reference
             if (prescriptionContainerLidTransform == null)
             {
                 Debug.LogWarning("PillCraftingAnimator: Prescription Container Lid Transform is null (runtime reference not set?). Lid animations will be skipped.", this);
             }
             if (prescriptionPillDropPoint == null)
             {
                 Debug.LogWarning("PillCraftingAnimator: Prescription Pill Drop Point Transform is null. Pill packaging animations will use container center.", this);
             }
             if (pillPackageMidpoint == null) // Check for the new midpoint reference
             {
                 Debug.LogError("PillCraftingAnimator: Pill Package Midpoint Transform is null! Pill packaging animation will not run correctly.", this);
                 // Decide how critical the midpoint is. If it's essential, return empty sequence.
                 // If pills can just go straight to the end, perhaps log error but proceed?
                 // Let's assume the midpoint is desired for the specific effect.
                 // Returning an empty sequence means the PackagingSequence in PillCraftingMinigame
                 // will finish immediately, potentially messing up timing.
                 // Let's just log the error and proceed, but the pill animations will likely fail/skip.
                 // For now, I'll let it proceed, but the AnimatePillIntoContainer method *will* return null
                 // if the midpoint is missing, causing those tweens to be skipped.
             }


             Sequence sequence = CreateSequence();

             // 1. Animate Lid Open (only if lid transform exists - now checking runtime ref)
             if (prescriptionContainerLidTransform != null)
             {
                  sequence.Append(AnimateLocalRotation(prescriptionContainerLidTransform, lidOpenLocalEuler, lidAnimateDuration, Ease.OutSine));
                  Debug.Log("PillCraftingAnimator: Appended Lid Open animation.");
             }


             // 2. Wait for pills to animate into the container (this will be handled by the calling script running a coroutine in parallel)
             // The calling script *starts* the individual pill animations during this interval.
             sequence.AppendInterval(pillsDropIntervalDuration); // Use the defined interval duration.
             Debug.Log($"PillCraftingAnimator: Appended interval for pills to drop ({pillsDropIntervalDuration}s).");


             // 3. Animate Lid Close (only if lid transform exists - now checking runtime ref)
             if (prescriptionContainerLidTransform != null)
             {
                 sequence.Append(AnimateLocalRotation(prescriptionContainerLidTransform, lidClosedLocalEuler, lidAnimateDuration, Ease.InSine));
                 Debug.Log("PillCraftingAnimator: Appended Lid Close animation.");
             }

             // 4. (Optional) Animate container visual confirmation (e.g., pulse scale slightly)
             // sequence.Append(AnimateScale(prescriptionContainerTransform, Vector3.one * 1.1f, 0.2f, Ease.OutSine));
             // sequence.Append(AnimateScale(prescriptionContainerTransform, Vector3.one, 0.2f, Ease.InSine));

             Debug.Log($"PillCraftingAnimator: Created Packaging sequence. Total Duration: {sequence.Duration(false)}s.");

             return sequence;
        }

         // Need a way for the animator to know the number of counted pills to estimate interval
         private int countedPillsCount = 0;
         public void SetCountedPillsCount(int count)
         {
              countedPillsCount = count;
         }


        /// <summary>
        /// Animates a single pill moving into the prescription container via a midpoint using a DOPath.
        /// This creates a smooth curve or linear path through the waypoints. Physics is temporarily disabled.
        /// --- MODIFIED: Now uses DOPath for a single continuous tween ---
        /// </summary>
        /// <param name="pillGO">The pill GameObject to animate.</param>
         /// <param name="finalTargetPosition">The final target position for the pill inside the container (scattered position).</param>
        public Tween AnimatePillIntoContainer(GameObject pillGO, Vector3 finalTargetPosition)
        {
             if (pillGO == null || !pillGO.activeInHierarchy)
             {
                 Debug.LogWarning("PillCraftingAnimator: AnimatePillIntoContainer called with null or inactive pill GameObject.", this);
                 return null;
             }

             if (pillPackageMidpoint == null)
             {
                  Debug.LogError($"PillCraftingAnimator: Pill Package Midpoint Transform is null! Cannot animate pill '{pillGO.name}' packaging using a path.", this);
                  // Optionally fall back to a single tween if no midpoint is provided?
                  // For now, let's just return null to indicate failure for THIS pill animation.
                  return null;
             }

             Transform pillTransform = pillGO.transform;
             Rigidbody pillRB = pillGO.GetComponent<Rigidbody>();
             Collider pillCol = pillGO.GetComponent<Collider>();

             // Store the pill's starting position
             Vector3 startPosition = pillTransform.position;
             Vector3 midpointPosition = pillPackageMidpoint.position;

             // Ensure physics is off during animation
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
             Tween pathTween = pillTransform.DOPath(waypoints, pillPackageAnimateDuration, PathType.Linear, PathMode.Ignore)
                                          .SetEase(Ease.Linear); // Use Linear ease for constant speed along the path

             // Optional: Add rotation animation alongside the path
             // pillTransform.DOLocalRotate(new Vector3(0, 360, 0), pillPackageAnimateDuration, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1, LoopType.Incremental); // Example continuous spin


             // Note: Re-enabling physics/collider after this animation finishes is not needed
             // as packaged pills should stay kinematic and non-colliding. Cleanup handles reset.

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
            StopAllTweens(); // Call the base class method to kill tweens (including DOPath tweens)
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