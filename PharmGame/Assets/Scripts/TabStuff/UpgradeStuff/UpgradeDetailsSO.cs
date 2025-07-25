// --- START OF FILE UpgradeDetailsSO.cs ---

using UnityEngine;

// This attribute allows you to create instances of this ScriptableObject
// directly from the Unity Editor's Assets > Create menu.
[CreateAssetMenu(fileName = "NewUpgradeDetails", menuName = "ScriptableObjects/Upgrade Details")]
public class UpgradeDetailsSO : ScriptableObject
{
    [Header("Upgrade Information")]
    [Tooltip("The unique name of the upgrade.")]
    public string upgradeName = "New Upgrade";

    [Tooltip("A detailed description of what the upgrade does.")]
    [TextArea(3, 10)] // Provides a multi-line text area in the Inspector
    public string upgradeDescription = "This is a description of the upgrade.";

    [Header("Purchase Requirements (Optional for now)")]
    [Tooltip("The cost of the upgrade.")]
    public float cost = 0; // Placeholder for future cost implementation

    [Tooltip("The player level required to unlock this upgrade.")]
    public int requiredLevel = 0; // Placeholder for future level requirements

    [Header("Internal Tracking (Optional for future)")]
    [Tooltip("A unique identifier for this upgrade (e.g., for saving/loading).")]
    public string uniqueID = System.Guid.NewGuid().ToString(); // Generates a unique ID by default

    // You can add more fields here later as needed for specific upgrade types,
    // icons, prerequisites, etc.
}

// --- END OF FILE UpgradeDetailsSO.cs ---