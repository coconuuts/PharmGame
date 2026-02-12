using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic; // Required for List

namespace Systems.UI
{
    /// <summary>
    /// Attaches to a UI element to automatically trigger Hover and Press animations 
    /// via the UIAnimationManager singleton.
    /// Supports optional text movement (offset) on click.
    /// </summary>
    public class UIElementAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Main Target Animation")]
        [Tooltip("The transform to actually animate (scale). If null, uses this object's transform.")]
        [SerializeField] private Transform animationTarget;

        [Tooltip("If true, this element will pulse/scale up when hovered.")]
        [SerializeField] private bool useHoverEffect = true;

        [Tooltip("If true, this element will shrink when clicked.")]
        [SerializeField] private bool usePressEffect = true;

        [Header("Override Global Settings")]
        [Tooltip("If true, uses the values below instead of UIAnimationManager defaults.")]
        [SerializeField] private bool overrideGlobalSettings = false;
        
        [Tooltip("Custom hover multiplier (e.g. 1.2 for bigger).")]
        [SerializeField] private float customHoverScale = 1.03f;
        
        [Tooltip("Custom press multiplier (e.g. 0.9 for smaller).")]
        [SerializeField] private float customPressScale = 0.95f;

        [Header("Text Animation")]
        [Tooltip("Optional: List of text elements (RectTransforms) to move down when clicked. They will NOT scale or react to hover.")]
        [SerializeField] private List<RectTransform> textMoveTargets;
        
        [Tooltip("Custom pixel offset for text movement (if overriding globals).")]
        [SerializeField] private float customTextOffset = 10f;

        // Cache the target on Awake so we don't check for null every frame
        private Transform Target => animationTarget != null ? animationTarget : transform;

        private void OnEnable()
        {
            if (UIAnimationManager.Instance != null)
            {
                // Reset Scale
                UIAnimationManager.Instance.ResetScaleImmediate(Target);
                
                // Reset Text Positions
                if (textMoveTargets != null)
                {
                    foreach (var t in textMoveTargets)
                        UIAnimationManager.Instance.ResetTextPositionImmediate(t);
                }
            }
        }
        
        private void OnDisable()
        {
            if (UIAnimationManager.Instance != null)
            {
                UIAnimationManager.Instance.ResetScaleImmediate(Target);

                if (textMoveTargets != null)
                {
                    foreach (var t in textMoveTargets)
                        UIAnimationManager.Instance.ResetTextPositionImmediate(t);
                }
            }
        }

        // --- HELPER FOR VALUES ---
        // These return null if we are not overriding, letting the Manager use its defaults.
        private float? GetHoverMult() => overrideGlobalSettings ? customHoverScale : (float?)null;
        private float? GetPressMult() => overrideGlobalSettings ? customPressScale : (float?)null;
        private float? GetTextOffset() => overrideGlobalSettings ? customTextOffset : (float?)null;

        // --- INTERFACE IMPLEMENTATIONS ---

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Hover only affects the main target (scaling), not the text
            if (!useHoverEffect) return;
            UIAnimationManager.Instance.AnimateHover(Target, true, GetHoverMult());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!useHoverEffect) return;
            UIAnimationManager.Instance.AnimateHover(Target, false, GetHoverMult());
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // 1. Scale Effect
            if (usePressEffect)
            {
                // We pass both Press and Hover multipliers, because when you release press (but stay hovered),
                // it needs to return to the correct Hover scale.
                UIAnimationManager.Instance.AnimatePress(Target, true, GetPressMult(), GetHoverMult());
            }

            // 2. Text Offset Effect
            UIAnimationManager.Instance.AnimateTextPress(textMoveTargets, true, GetTextOffset());
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // 1. Scale Effect
            if (usePressEffect)
            {
                UIAnimationManager.Instance.AnimatePress(Target, false, GetPressMult(), GetHoverMult());
            }

            // 2. Text Offset Effect
            UIAnimationManager.Instance.AnimateTextPress(textMoveTargets, false, GetTextOffset());
        }
    }
}