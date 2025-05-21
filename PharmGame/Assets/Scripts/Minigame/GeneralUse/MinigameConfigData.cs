// Systems/Minigame/Config/MinigameConfigData.cs (Adjust namespace if needed)
using UnityEngine;

namespace Systems.Minigame.Config // Created a new sub-namespace for clarity
{
    /// <summary>
    /// Abstract base ScriptableObject to hold configuration data common
    /// across different crafting minigames. Specific minigame configs
    /// will inherit from this class.
    /// </summary>
    // Removed [CreateAssetMenu] as abstract classes cannot be instantiated directly
    public abstract class MinigameConfigData : ScriptableObject
    {
        [Header("General Settings (Optional common fields)")]
        [Tooltip("The duration for camera movements within this minigame states.")]
        public float cameraMoveDuration = 0.3f;

        // Add other fields here that are truly common across ALL minigames if any arise later.
        // For now, just cameraMoveDuration as a potential example.
    }
}