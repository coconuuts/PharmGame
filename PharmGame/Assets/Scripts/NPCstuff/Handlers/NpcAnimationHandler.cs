// --- Updated NpcAnimationHandler.cs ---
// (Content is largely the same as Substep 2's version, confirming it has the logic)
using UnityEngine;

namespace Game.NPC.Handlers
{
    /// <summary>
    /// Handles the animation control for an NPC.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class NpcAnimationHandler : MonoBehaviour
    {
        public Animator Animator { get; private set; }

        [Tooltip("The default speed parameter name used in the Animator Controller.")]
        [SerializeField] private string speedParameterName = "Speed";

        private void Awake()
        {
            Animator = GetComponent<Animator>();
            if (Animator == null)
            {
                Debug.LogError($"NpcAnimationHandler on {gameObject.name}: Animator component not found!", this);
                enabled = false;
            }
        }

        // Public API for Animation
        public void SetSpeed(float speed)
        {
            if (Animator != null)
            {
                Animator.SetFloat(speedParameterName, speed);
            }
        }

        public void SetBool(string paramName, bool value)
        {
             if (Animator != null)
            {
                Animator.SetBool(paramName, value);
            }
        }

         public void SetTrigger(string paramName)
         {
              if (Animator != null)
              {
                   Animator.SetTrigger(paramName);
              }
         }

        public void Play(string stateName, int layer = 0, float normalizedTime = 0f)
        {
             if (Animator != null)
             {
                  Animator.Play(stateName, layer, normalizedTime);
             }
             else
             {
                   Debug.LogWarning($"NpcAnimationHandler on {gameObject.name}: Cannot play animation '{stateName}' - Animator is null.", this);
             }
        }
        // Add other common animator methods as needed (SetInteger, SetFloat, etc.)
    }
}