using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using System;

namespace Systems.UI
{
    /// <summary>
    /// A generic Singleton manager for standardized UI animations using DOTween.
    /// Handles Hover, Press, Open, and Close animations with percentage-based scaling.
    /// Also handles Text-specific offset animations.
    /// </summary>
    public class UIAnimationManager : MonoBehaviour
    {
        public static UIAnimationManager Instance;

        [Header("Global Scale Settings (Percentages)")]
        [Tooltip("Scale multiplier when hovering (e.g., 1.1 = 110% size)")]
        [SerializeField] private float hoverScaleMult = 1.05f; 
        [Tooltip("Scale multiplier when pressed (e.g., 0.95 = 95% size)")]
        [SerializeField] private float pressScaleMult = 0.95f;

        [Header("Scale Timing")]
        [SerializeField] private float scaleDuration = 0.2f;
        [SerializeField] private float fadeDuration = 0.25f;
        [SerializeField] private Ease scaleEase = Ease.OutBack;
        [SerializeField] private Ease fadeEase = Ease.OutQuad;

        [Header("Global Text Move Settings")]
        [Tooltip("Pixels to move text down when pressed (e.g. 5 or 10).")]
        [SerializeField] private float textPressOffsetY = 10f; 
        [SerializeField] private float textMoveDuration = 0.15f;
        [SerializeField] private Ease textMoveEase = Ease.OutQuad;

        // Dictionary to store the original scale of objects so we calculate percentages correctly.
        private Dictionary<int, Vector3> initialScales = new Dictionary<int, Vector3>();
        
        // Dictionary to store original positions of text elements
        private Dictionary<int, Vector2> initialAnchoredPositions = new Dictionary<int, Vector2>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else 
            { 
                Destroy(gameObject); 
                return; 
            }
        }

        // --- Helper: Get or Cache Initial Scale ---
        private Vector3 GetInitialScale(Transform target)
        {
            int id = target.GetInstanceID();
            if (!initialScales.ContainsKey(id))
            {
                initialScales[id] = target.localScale;
            }
            return initialScales[id];
        }

        // --- Helper: Get or Cache Initial Position (RectTransform) ---
        private Vector2 GetInitialPosition(RectTransform target)
        {
            int id = target.GetInstanceID();
            if (!initialAnchoredPositions.ContainsKey(id))
            {
                initialAnchoredPositions[id] = target.anchoredPosition;
            }
            return initialAnchoredPositions[id];
        }

        /// <summary>
        /// Instantly kills any tweens and snaps the target back to its original scale.
        /// </summary>
        public void ResetScaleImmediate(Transform target)
        {
            if (target == null) return;
            target.DOKill(true); 
            target.localScale = GetInitialScale(target);
        }

        /// <summary>
        /// Instantly kills tweens and snaps text back to original position.
        /// </summary>
        public void ResetTextPositionImmediate(RectTransform target)
        {
            if (target == null) return;
            target.DOKill(true);
            target.anchoredPosition = GetInitialPosition(target);
        }

        // --- PUBLIC API: SCALING INTERACTION ---

        /// <summary>
        /// Animates the hover effect.
        /// </summary>
        /// <param name="overrideScaleMult">Optional: If set, overrides the global hover scale multiplier.</param>
        public void AnimateHover(Transform target, bool isHovering, float? overrideScaleMult = null)
        {
            if (target == null) return;
            target.DOKill(true);

            Vector3 baseScale = GetInitialScale(target);

            // Use override if provided, otherwise global
            float effectiveMult = overrideScaleMult ?? hoverScaleMult;

            Vector3 targetScale = isHovering ? baseScale * effectiveMult : baseScale;

            target.DOScale(targetScale, scaleDuration).SetEase(scaleEase).SetUpdate(true);
        }

        /// <summary>
        /// Animates the press effect.
        /// </summary>
        /// <param name="overridePressMult">Optional: Overrides global press multiplier.</param>
        /// <param name="overrideHoverMult">Optional: Overrides global hover multiplier (needed for release state).</param>
        public void AnimatePress(Transform target, bool isPressed, float? overridePressMult = null, float? overrideHoverMult = null)
        {
            if (target == null) return;
            target.DOKill(true);

            Vector3 baseScale = GetInitialScale(target);

            // Determine effective multipliers
            float effectivePressMult = overridePressMult ?? pressScaleMult;
            float effectiveHoverMult = overrideHoverMult ?? hoverScaleMult;

            // If pressed, shrink. If released, return to Hover scale (assuming mouse is still over).
            Vector3 targetScale = isPressed ? baseScale * effectivePressMult : baseScale * effectiveHoverMult;

            target.DOScale(targetScale, scaleDuration).SetEase(scaleEase).SetUpdate(true);
        }

        // --- PUBLIC API: TEXT OFFSET INTERACTION ---

        /// <summary>
        /// Moves a list of Text RectTransforms down when pressed, and back to original when released.
        /// No hover effect is applied here.
        /// </summary>
        /// <param name="overrideOffsetY">Optional: Overrides the global pixel offset amount.</param>
        public void AnimateTextPress(List<RectTransform> targets, bool isPressed, float? overrideOffsetY = null)
        {
            if (targets == null || targets.Count == 0) return;

            float effectiveOffset = overrideOffsetY ?? textPressOffsetY;

            foreach (var t in targets)
            {
                if (t == null) continue;
                
                t.DOKill(true);

                Vector2 basePos = GetInitialPosition(t);
                
                // If Pressed: Move down by offset (Y minus offset). 
                // If Released: Return to basePos.
                Vector2 targetPos = isPressed ? 
                    new Vector2(basePos.x, basePos.y - effectiveOffset) : 
                    basePos;

                t.DOAnchorPos(targetPos, textMoveDuration).SetEase(textMoveEase).SetUpdate(true);
            }
        }

        // --- PUBLIC API: WINDOWS / PANELS ---

        public void OpenPanel(GameObject panel, bool useScale = true, bool useFade = true)
        {
            if (panel == null) return;

            panel.SetActive(true);
            Transform t = panel.transform;
            t.DOKill(); 

            Vector3 baseScale = GetInitialScale(t);

            if (useScale)
            {
                t.localScale = Vector3.zero;
                t.DOScale(baseScale, scaleDuration).SetEase(scaleEase).SetUpdate(true);
            }
            else
            {
                t.localScale = baseScale;
            }

            if (useFade)
            {
                CanvasGroup cg = panel.GetComponent<CanvasGroup>();
                if (cg == null) cg = panel.AddComponent<CanvasGroup>();
                
                cg.DOKill();
                cg.alpha = 0f;
                cg.DOFade(1f, fadeDuration).SetEase(fadeEase).SetUpdate(true);
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }

        public void ClosePanel(GameObject panel, bool useScale = true, bool useFade = true, Action onComplete = null)
        {
            if (panel == null || !panel.activeSelf) 
            {
                onComplete?.Invoke();
                return;
            }

            Transform t = panel.transform;
            t.DOKill();

            TweenCallback onFinish = () => 
            {
                panel.SetActive(false);
                t.localScale = GetInitialScale(t); 
                onComplete?.Invoke();
            };

            Tween mainTween = null;

            if (useScale)
            {
                mainTween = t.DOScale(Vector3.zero, scaleDuration).SetEase(scaleEase).SetUpdate(true);
            }

            if (useFade)
            {
                CanvasGroup cg = panel.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.DOKill();
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                    Tween fadeTween = cg.DOFade(0f, fadeDuration).SetEase(fadeEase).SetUpdate(true);
                    
                    if (!useScale || fadeDuration > scaleDuration)
                    {
                        mainTween = fadeTween;
                    }
                }
            }

            if (mainTween != null) mainTween.OnComplete(onFinish);
            else onFinish.Invoke();
        }
    }
}