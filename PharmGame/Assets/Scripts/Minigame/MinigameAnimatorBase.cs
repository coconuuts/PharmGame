// Systems/Minigame/Animation/MinigameAnimatorBase.cs
using UnityEngine;
using DG.Tweening; // Import the DOTween namespace

namespace Systems.Minigame.Animation // A new sub-namespace for animation helpers
{
    /// <summary>
    /// Abstract base class for minigame-specific animator components.
    /// Provides common methods for performing DOTween animations and cleanup.
    /// </summary>
    public abstract class MinigameAnimatorBase : MonoBehaviour
    {
        /// <summary>
        /// Use this method from derived classes to stop all DOTween animations
        /// associated with this Animator component's GameObject and its children.
        /// This should be called from the derived class's implementation of PerformAnimatorCleanup().
        /// </summary>
        protected void StopAllTweens()
        {
            Debug.Log($"MinigameAnimatorBase ({gameObject.name}): Stopping all tweens associated with this GameObject and children.", this);
            DOTween.Kill(this.gameObject, true); // Kill tweens on this GameObject and its children
        }

        /// <summary>
        /// Creates a new DOTween Sequence. Sequences are useful for chaining multiple tweens.
        /// You are responsible for managing and playing the sequence.
        /// </summary>
        protected Sequence CreateSequence()
        {
            return DOTween.Sequence();
        }

        /// <summary>
        /// Animates the position of a Transform using DOTween.
        /// </summary>
        /// <param name="target">The Transform to animate.</param>
        /// <param name="endValue">The target world position.</param>
        /// <param name="duration">The duration of the animation.</param>
        /// <param name="ease">The ease type for the animation.</param>
        /// <returns>The created Tween object.</returns>
        protected virtual Tween AnimatePosition(Transform target, Vector3 endValue, float duration, Ease ease = Ease.Linear)
        {
            if (target == null) { Debug.LogWarning($"MinigameAnimatorBase ({gameObject.name}): AnimatePosition called with null target Transform.", this); return null; }
            if (duration <= 0) { target.position = endValue; return null; } // Snap to end if duration is zero or less
            // Use SetTarget(this.gameObject) to associate the tween with the animator GameObject for easy killing
            return target.DOMove(endValue, duration).SetEase(ease).SetTarget(this.gameObject);
        }

        /// <summary>
        /// Animates the local position of a Transform using DOTween.
        /// </summary>
        /// <param name="target">The Transform to animate.</param>
        /// <param name="endValue">The target local position.</param>
        /// <param name="duration">The duration of the animation.</param>
        /// <param name="ease">The ease type for the animation.</param>
        /// <returns>The created Tween object.</returns>
        protected virtual Tween AnimateLocalPosition(Transform target, Vector3 endValue, float duration, Ease ease = Ease.Linear)
        {
             if (target == null) { Debug.LogWarning($"MinigameAnimatorBase ({gameObject.name}): AnimateLocalPosition called with null target Transform.", this); return null; }
             if (duration <= 0) { target.localPosition = endValue; return null; }
            return target.DOLocalMove(endValue, duration).SetEase(ease).SetTarget(this.gameObject);
        }

        /// <summary>
        /// Animates the rotation of a Transform using DOTween (towards a target Euler angle).
        /// </summary>
        /// <param name="target">The Transform to animate.</param>
        /// <param name="endValueEuler">The target Euler angles.</param>
        /// <param name="duration">The duration of the animation.</param>
        /// <param name="ease">The ease type for the animation.</param>
         /// <param name="mode">The rotation mode (e.g., FastBeyond360).</param>
        /// <returns>The created Tween object.</returns>
        protected virtual Tween AnimateRotation(Transform target, Vector3 endValueEuler, float duration, Ease ease = Ease.Linear, RotateMode mode = RotateMode.Fast)
        {
            if (target == null) { Debug.LogWarning($"MinigameAnimatorBase ({gameObject.name}): AnimateRotation called with null target Transform.", this); return null; }
             if (duration <= 0) { target.rotation = Quaternion.Euler(endValueEuler); return null; }
            return target.DORotate(endValueEuler, duration, mode).SetEase(ease).SetTarget(this.gameObject);
        }

        /// <summary>
        /// Animates the local rotation of a Transform using DOTween (towards a target Euler angle).
        /// </summary>
        /// <param name="target">The Transform to animate.</param>
        /// <param name="endValueEuler">The target local Euler angles.</param>
        /// <param name="duration">The duration of the animation.</param>
        /// <param name="ease">The ease type for the animation.</param>
        /// <param name="mode">The rotation mode (e.g., FastBeyond360).</param>
        /// <returns>The created Tween object.</returns>
        protected virtual Tween AnimateLocalRotation(Transform target, Vector3 endValueEuler, float duration, Ease ease = Ease.Linear, RotateMode mode = RotateMode.Fast)
        {
             if (target == null) { Debug.LogWarning($"MinigameAnimatorBase ({gameObject.name}): AnimateLocalRotation called with null target Transform.", this); return null; }
             if (duration <= 0) { target.localRotation = Quaternion.Euler(endValueEuler); return null; }
            return target.DOLocalRotate(endValueEuler, duration, mode).SetEase(ease).SetTarget(this.gameObject);
        }


        /// <summary>
        /// Animates the scale of a Transform using DOTween.
        /// </summary>
        /// <param name="target">The Transform to animate.</param>
        /// <param name="endValue">The target local scale.</param>
        /// <param name="duration">The duration of the animation.</param>
        /// <param name="ease">The ease type for the animation.</param>
        /// <returns>The created Tween object.</returns>
        protected virtual Tween AnimateScale(Transform target, Vector3 endValue, float duration, Ease ease = Ease.Linear)
        {
             if (target == null) { Debug.LogWarning($"MinigameAnimatorBase ({gameObject.name}): AnimateScale called with null target Transform.", this); return null; }
             if (duration <= 0) { target.localScale = endValue; return null; }
            return target.DOScale(endValue, duration).SetEase(ease).SetTarget(this.gameObject);
        }

        /// <summary>
        /// Animates the alpha of a CanvasGroup using DOTween.
        /// </summary>
        /// <param name="target">The CanvasGroup to animate.</param>
        /// <param name="endValue">The target alpha value (0 to 1).</param>
        /// <param name="duration">The duration of the animation.</param>
        /// <param name="ease">The ease type for the animation.</param>
        /// <returns>The created Tween object.</returns>
        protected virtual Tween AnimateFade(CanvasGroup target, float endValue, float duration, Ease ease = Ease.Linear)
        {
             if (target == null) { Debug.LogWarning($"MinigameAnimatorBase ({gameObject.name}): AnimateFade called with null target CanvasGroup.", this); return null; }
             if (duration <= 0) { target.alpha = endValue; return null; }
            return target.DOFade(endValue, duration).SetEase(ease).SetTarget(this.gameObject);
        }

        // Add more common animation methods as needed (e.g., color, shake, etc.)


        /// <summary>
        /// Abstract method for performing derived class-specific animator cleanup.
        /// Should include stopping any active tweens via StopAllTweens().
        /// Called by the owning minigame script's PerformCleanup() method.
        /// --- ADDED Abstract method ---
        /// </summary>
        public abstract void PerformAnimatorCleanup(); // Made public to be called by owning minigame script
    }
}