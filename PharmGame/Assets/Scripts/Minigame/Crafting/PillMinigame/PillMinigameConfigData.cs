// Systems/Minigame/Config/PillMinigameConfigData.cs (Adjust namespace if needed)
using UnityEngine;
using TMPro; // Needed for TextMeshProUGUI reference type if needed later (keeping for now)
using UnityEngine.UI; // Needed for Button reference type if needed later (keeping for now)

namespace Systems.Minigame.Config // Use the same sub-namespace
{
    /// <summary>
    /// ScriptableObject holding configuration specific to the Pill Crafting Minigame.
    /// Inherits from MinigameConfigData.
    /// </summary>
    [CreateAssetMenu(fileName = "PillMinigameConfig", menuName = "Minigames/Pill Minigame Config Data", order = 2)]
    public class PillMinigameConfigData : MinigameConfigData
    {
        [Header("Pill Minigame Prefab References")]
        [Tooltip("Prefab for the large container that pours pills.")]
        public GameObject containerPrefab;
        [Tooltip("Prefab for a single pill asset.")]
        public GameObject pillPrefab;
        [Tooltip("Prefab for the finished prescription container.")]
        public GameObject prescriptionContainerPrefab;
        [Tooltip("Prefab for the lid of the finished prescription container.")]
        public GameObject prescriptionContainerLidPrefab;


        public float pouringDuration;
        
        [Header("Pill Minigame Settings")]
        [Tooltip("Minimum random number of pills to require packaging.")]
        public int minTargetPills = 10;
        [Tooltip("Maximum random number of pills to require packaging.")]
        public int maxTargetPills = 20;

        [Tooltip("Minimum excess pills to pour (beyond target).")]
        public int minExcessPills = 5;
        [Tooltip("Maximum excess pills to pour (beyond target).")]
        public int maxExcessPills = 10;

        [Tooltip("Radius for scattering pills as they enter the container (Used by Movement/Animator).")]
        public float packagingScatterRadius = 0.05f;

        [Tooltip("The LayerMask for detecting clickable pills.")]
        public LayerMask pillLayer;

         [Tooltip("Minimum delay after spawning a pill before enabling physics to prevent phasing.")]
         public float minPhysicsEnableDelay = 0.02f;
         [Tooltip("Maximum delay after spawning a pill before enabling physics to prevent phasing.")]
         public float maxPhysicsEnableDelay = 0.05f;

         [Tooltip("Desired local scale for the instantiated Prescription Container Lid.")]
         public Vector3 prescriptionContainerLidScale = Vector3.one;


        [Header("Animation Settings")]
         [Tooltip("How many degrees the pill stock container tilts forward when pouring (Z-axis usually).")]
         public float pourTiltAngle = 133f; // Moved from Animator
         [Tooltip("Duration of the pill stock container tilting forward.")]
         public float pourTiltDuration = 0.45f; // Moved from Animator

         [Tooltip("Duration of the pill stock container tilting back.")]
         public float pourReturnDuration = 0.25f; // Moved from Animator
         [Tooltip("Duration for the lid to animate open/closed.")]
         public float lidAnimateDuration = 0.75f; // Moved from Animator
         [Tooltip("Local Euler angle for the prescription container lid when open.")]
         public Vector3 lidOpenLocalEuler; // Moved from Animator
         [Tooltip("Local Euler angle for the prescription container lid when closed.")]
         public Vector3 lidClosedLocalEuler; // Moved from Animator

          [Tooltip("Duration for individual pills to animate into the packaging container (Total for the entire path).")]
         public float pillPackageAnimateDuration = 0.75f; // Moved from Animator

         [Tooltip("Duration of the interval where pills drop during packaging animation.")]
         public float pillsDropIntervalDuration = 0.6f; // Moved from Animator
    }
}