// --- START OF FILE UpgradeButtonData.cs ---

using UnityEngine;
using UnityEngine.UI; // Needed for Button component (implicitly)

// Assuming UpgradeDetailsSO is accessible
// If it's in a specific namespace (e.g., Systems.Upgrades), add:
// using Systems.Upgrades;

/// <summary>
/// Simple data holder script attached to an Upgrade Button prefab instance
/// to store the UpgradeDetailsSO it represents.
/// </summary>
[RequireComponent(typeof(Button))] // Ensure there's a Button component
public class UpgradeButtonData : MonoBehaviour
{
    [Tooltip("The UpgradeDetailsSO this button represents.")]
    public UpgradeDetailsSO upgradeDetails; // Public field to hold the upgrade data
}

// --- END OF FILE UpgradeButtonData.cs ---