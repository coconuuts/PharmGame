// --- START OF FILE UpgradePanelHandler.cs ---

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Systems.UI; // Needed for IPanelActivatable
using Utils.Pooling; // Needed for PoolingManager
using System; // Needed for Action
using System.Linq; // Needed for ToList()

// Assuming UpgradeManager and UpgradeDetailsSO are accessible (e.g., in a global namespace or a shared namespace)
// If they are in a specific namespace (e.g., Systems.Upgrades), add:
// using Systems.Upgrades;

/// <summary>
/// Handles the logic for the Upgrades panel in the computer UI.
/// Displays a list of available upgrades and a detail view for selected upgrades.
/// MODIFIED: Checks UpgradeManager for purchased status to disable Buy button.
/// </summary>
public class UpgradePanelHandler : MonoBehaviour, IPanelActivatable
{
    [Header("UI References")]
    [Tooltip("The Transform parent for the list of upgrade buttons (the 'Content' GameObject under the ScrollRect).")]
    [SerializeField] private Transform upgradeListContentParent;

    [Tooltip("The GameObject container for the upgrade detail view.")]
    [SerializeField] private GameObject upgradeDetailArea;

    [Tooltip("The TextMeshProUGUI component that displays the upgrade description.")]
    [SerializeField] private TextMeshProUGUI upgradeDetailText;

    [Tooltip("The Button component used to purchase the selected upgrade.")]
    [SerializeField] private Button buyUpgradeButton;


    [Header("Prefab References")]
    [Tooltip("The prefab for the individual upgrade buttons in the list.")]
    [SerializeField] private GameObject upgradeButtonPrefab;


    // --- Manager References ---
    private UpgradeManager upgradeManager; // Reference to the UpgradeManager singleton


    // --- Internal Tracking ---
    // List to keep track of INSTANTIATED/POOLED buttons currently active in the scene
    private List<GameObject> activeButtonInstances = new List<GameObject>();

    // Field to store the upgrade currently displayed in the detail area
    private UpgradeDetailsSO currentDetailUpgrade; // Use the ScriptableObject reference


    // --- IPanelActivatable Implementation ---

    /// <summary>
    /// Called by the TabManager when this panel becomes active.
    /// This is where we will populate the list of upgrades using pooling.
    /// </summary>
    public void OnPanelActivated()
    {
        Debug.Log($"UpgradePanelHandler on {gameObject.name}: Panel Activated. Attempting to populate upgrade list.", this);

        // --- Validation: Check essential UI references ---
         if (upgradeDetailArea == null || upgradeListContentParent == null || upgradeButtonPrefab == null || buyUpgradeButton == null)
         {
             Debug.LogError($"UpgradePanelHandler on {gameObject.name}: Essential UI references missing in OnPanelActivated. Cannot proceed.", this);
             if (upgradeDetailArea != null) upgradeDetailArea.SetActive(false);
             return;
         }

        // Ensure detail area is hidden when the panel is first activated
        upgradeDetailArea.SetActive(false);

        // Clear any existing buttons before repopulating (returns them to pool)
        ClearUpgradeList(); // Call the cleanup method

        // Fetch and populate the list
        PopulateUpgradeList(); // Call the method to populate

        // --- Add Listener for Buy Button ---
         if (buyUpgradeButton != null)
         {
              // Remove any old listeners before adding to prevent duplicates
              buyUpgradeButton.onClick.RemoveAllListeners(); // Added RemoveAllListeners defensively
              buyUpgradeButton.onClick.AddListener(OnBuyUpgradeButtonClick);
              Debug.Log($"UpgradePanelHandler on {gameObject.name}: Subscribed Buy button listener.");
         }
         else
         {
              Debug.LogWarning($"UpgradePanelHandler on {gameObject.name}: Buy Upgrade Button reference is null! Cannot subscribe listener.", this);
         }

        // Initialize button states (will disable Buy button as currentDetailUpgrade is null)
        UpdateButtonStates();

        // --- NEW: Subscribe to UpgradeManager's purchased event ---
        // This allows the UI to update instantly if an upgrade is purchased
        // while the panel is active (e.g., via console command, or maybe future multi-purchase logic)
        if (upgradeManager != null)
        {
             upgradeManager.OnUpgradePurchasedSuccessfully += HandleUpgradePurchasedSuccessfully;
             Debug.Log($"UpgradePanelHandler on {gameObject.name}: Subscribed to UpgradeManager.OnUpgradePurchasedSuccessfully.");
        }
        // --- END NEW ---
    }

    /// <summary>
    /// Called by the TabManager when this panel becomes inactive.
    /// This is where we will return the generated buttons to the pool.
    /// </summary>
    public void OnPanelDeactivated()
    {
        Debug.Log($"UpgradePanelHandler on {gameObject.name}: Panel Deactivated. Returning upgrade list buttons to pool.", this);

        // --- NEW: Unsubscribe from UpgradeManager's purchased event ---
        if (upgradeManager != null)
        {
             upgradeManager.OnUpgradePurchasedSuccessfully -= HandleUpgradePurchasedSuccessfully;
             Debug.Log($"UpgradePanelHandler on {gameObject.name}: Unsubscribed from UpgradeManager.OnUpgradePurchasedSuccessfully.");
        }
        // --- END NEW ---

        // Hide the detail area when the panel is deactivated
         if (upgradeDetailArea != null)
         {
              upgradeDetailArea.SetActive(false);
         }
         else
         {
              Debug.LogWarning($"UpgradePanelHandler on {gameObject.name}: Upgrade Detail Area reference is null! Cannot hide detail area.", this);
         }

        // --- Remove Listener for Buy Button ---
        if (buyUpgradeButton != null)
        {
             buyUpgradeButton.onClick.RemoveAllListeners(); // Remove all listeners added to this button
             Debug.Log($"UpgradePanelHandler on {gameObject.name}: Unsubscribed Buy button listeners.");
        }
        // --- END NEW ---


        // Clear the internal displayed upgrade reference
        currentDetailUpgrade = null; // Clear the stored upgrade

        // Return instantiated buttons to the pool
        ClearUpgradeList(); // Call the cleanup method

        Debug.Log($"UpgradePanelHandler on {gameObject.name}: Returned upgrade list buttons to pool. Active instances tracked: {activeButtonInstances.Count}.");
    }

    // --- Panel Logic Methods ---

    /// <summary>
    /// Fetches available upgrades and populates the list UI.
    /// </summary>
    private void PopulateUpgradeList()
    {
         // --- Validation: Check essential references ---
         if (upgradeListContentParent == null)
         {
              Debug.LogError($"UpgradePanelHandler on {gameObject.name}: Upgrade List Content Parent Transform is null! Cannot populate list.", this);
              return;
         }
         if (upgradeButtonPrefab == null)
         {
              Debug.LogError($"UpgradePanelHandler on {gameObject.name}: Upgrade Button Prefab is not assigned! Cannot instantiate buttons.", this);
              return;
         }
         if (PoolingManager.Instance == null)
         {
              Debug.LogError($"UpgradePanelHandler on {gameObject.name}: PoolingManager instance is null! Cannot get pooled objects.", this);
              return;
         }
         if (upgradeManager == null || upgradeManager.AllAvailableUpgrades == null)
         {
              Debug.LogError($"UpgradePanelHandler on {gameObject.name}: UpgradeManager or its AllAvailableUpgrades list is null! Cannot get upgrade data.", this);
              return;
         }

         List<UpgradeDetailsSO> availableUpgrades = upgradeManager.AllAvailableUpgrades;

         if (availableUpgrades.Count == 0)
         {
              Debug.Log($"UpgradePanelHandler on {gameObject.name}: No available upgrades to display.", this);
              // Optionally display a "No upgrades" message in the list area
         }
         else
         {
             Debug.Log($"UpgradePanelHandler on {gameObject.name}: Found {availableUpgrades.Count} available upgrades. Getting pooled buttons.");
         }

         // Ensure the tracking list is empty before populating
         // This should be handled by ClearUpgradeList being called first
         // activeButtonInstances.Clear();


         for (int i = 0; i < availableUpgrades.Count; i++)
         {
             var upgrade = availableUpgrades[i]; // Get the upgrade details

             // --- Optional: Skip displaying already purchased one-time upgrades ---
             // If you want purchased upgrades to disappear from the list after buying:
             // if (upgradeManager.IsUpgradePurchased(upgrade)) continue;
             // For this request, we keep them in the list but disable the detail button,
             // so we don't add the 'continue' here.


             GameObject buttonInstance = PoolingManager.Instance.GetPooledObject(upgradeButtonPrefab);

             if (buttonInstance == null)
             {
                  Debug.LogWarning($"UpgradePanelHandler on {gameObject.name}: Failed to get a pooled instance of '{upgradeButtonPrefab.name}'. Skipping upgrade: {upgrade.upgradeName}", this);
                  continue; // Skip to the next upgrade
             }

             // Set the parent of the pooled object AFTER getting it from the pool
             buttonInstance.transform.SetParent(upgradeListContentParent, false); // Use SetParent(parent, worldPositionStays)

             // Explicitly set the sibling index
             buttonInstance.transform.SetSiblingIndex(i); // Set the position in the hierarchy based on the loop index


             // --- Add the instance to our tracking list ---
             activeButtonInstances.Add(buttonInstance);


             // Get the helper script and assign the upgrade data
             UpgradeButtonData buttonData = buttonInstance.GetComponent<UpgradeButtonData>();
             if (buttonData != null)
             {
                 buttonData.upgradeDetails = upgrade; // Assign the ScriptableObject reference
             }
             else
             {
                 Debug.LogError($"UpgradePanelHandler on {gameObject.name}: Pooled button instance '{buttonInstance.name}' is missing UpgradeButtonData component! Cannot assign upgrade data.", buttonInstance);
             }

             // Find the TextMeshProUGUI component and set its text (assuming it's a child)
             TextMeshProUGUI upgradeNameText = buttonInstance.GetComponentInChildren<TextMeshProUGUI>();

             if (upgradeNameText != null)
             {
                 upgradeNameText.text = upgrade.upgradeName;
             }
             else Debug.LogWarning($"UpgradePanelHandler on {gameObject.name}: TextMeshProUGUI component not found on button instance or its children.", buttonInstance);


             // Get the Button component and add the click listener
             Button button = buttonInstance.GetComponent<Button>();
             if (button != null)
             {
                 if (buttonData != null)
                 {
                      // Ensure no previous listeners remain if not using pooling correctly or on scene load
                      button.onClick.RemoveAllListeners(); // Defensive removal

                      // Use the buttonData.upgradeDetails which is unique per instance
                      button.onClick.AddListener(() => ShowUpgradeDetails(buttonData.upgradeDetails));
                 }
                 else
                 {
                      Debug.LogError($"UpgradePanelHandler on {gameObject.name}: Cannot add click listener, buttonData is null for instance!", buttonInstance);
                 }
             }
             else
             {
                 Debug.LogError($"UpgradePanelHandler on {gameObject.name}: Button component not found on button instance! Cannot add click listener.", buttonInstance);
             }

             // --- Optional: Visually update the list button itself if purchased ---
             // You would add logic here to change the appearance of 'button' or 'buttonData.gameObject'
             // based on 'upgradeManager.IsUpgradePurchased(upgrade)'.
             // Example:
             // if (upgradeManager.IsUpgradePurchased(upgrade))
             // {
             //     // Change button color, add a "PURCHASED" text, etc.
             // }
             // --- END Optional ---
         }

         Debug.Log($"UpgradePanelHandler on {gameObject.name}: Finished populating upgrade list using pooling. Active instances tracked: {activeButtonInstances.Count}.");
    }

    /// <summary>
    /// Returns all instantiated upgrade button GameObjects managed by this handler to the pool.
    /// </summary>
    private void ClearUpgradeList()
    {
         // --- Validation: Check PoolingManager instance ---
         if (PoolingManager.Instance == null)
         {
              Debug.LogError($"UpgradePanelHandler on {gameObject.name}: PoolingManager instance is null! Cannot return pooled objects. Destroying tracked objects instead.", this);
              // Fallback to destroying if pooling manager is missing (less efficient)
              DestroyAllTrackedUpgradeListChildren(); // Call a new helper method for destruction fallback
              return; // Stop here
         }

         Debug.Log($"UpgradePanelHandler on {gameObject.name}: Returning {activeButtonInstances.Count} tracked buttons to pool.", this);

         // Return all tracked instances to the pool
         // Iterate over a copy (ToList()) is safer if the Return call might somehow affect the original list
         foreach (GameObject child in activeButtonInstances.ToList()) // Requires System.Linq
         {
             if (child != null)
             {
                 // The PoolingManager.ReturnPooledObject handles setting inactive and reparenting
                 PoolingManager.Instance.ReturnPooledObject(child);
             }
             else
             {
                 Debug.LogWarning($"UpgradePanelHandler on {gameObject.name}: Found a null entry in activeButtonInstances list.", this);
             }
         }

         // --- Clear the tracking list AFTER returning all objects ---
         activeButtonInstances.Clear();

         // Clear the stored detail upgrade reference
         currentDetailUpgrade = null; // Clear the stored upgrade

         Debug.Log($"UpgradePanelHandler on {gameObject.name}: Returned upgrade list buttons to pool. Active instances tracked: {activeButtonInstances.Count}.");
    }

    /// <summary>
    /// Fallback method to destroy all children currently tracked by this handler.
    /// Used if pooling is not available.
    /// </summary>
    private void DestroyAllTrackedUpgradeListChildren() // Renamed for clarity
    {
         Debug.LogWarning($"UpgradePanelHandler on {gameObject.name}: Destroying {activeButtonInstances.Count} tracked children as fallback.", this);

         // Iterate over a copy (ToList()) to safely destroy objects
         foreach (GameObject child in activeButtonInstances.ToList()) // Requires System.Linq
         {
             if (child != null)
             {
                 Destroy(child);
             }
         }

         // Clear the tracking list after destroying
         activeButtonInstances.Clear();

         // Clear the stored detail upgrade reference
         currentDetailUpgrade = null; // Clear the stored upgrade

         Debug.LogWarning($"UpgradePanelHandler on {gameObject.name}: Destroyed tracked children. Active instances tracked: {activeButtonInstances.Count}.", this);
    }


    /// <summary>
    /// Displays the details of a selected upgrade in the detail area.
    /// </summary>
    /// <param name="upgrade">The UpgradeDetailsSO to display.</param>
    public void ShowUpgradeDetails(UpgradeDetailsSO upgrade)
    {
         if (upgrade == null)
         {
             Debug.LogWarning($"UpgradePanelHandler on {gameObject.name}: Attempted to show details for a null upgrade.", this);
             // Optionally hide the detail area if a null upgrade is passed
             if (upgradeDetailArea != null) upgradeDetailArea.SetActive(false);
             currentDetailUpgrade = null; // Ensure internal reference is null
             UpdateButtonStates(); // Update button states based on null selection
             return;
         }

         Debug.Log($"UpgradePanelHandler on {gameObject.name}: Showing details for upgrade: {upgrade.upgradeName}", this);

         // --- Store the upgrade being displayed ---
         currentDetailUpgrade = upgrade; // Store the ScriptableObject reference
         // --- END NEW ---

         // --- Validation: Check essential UI references ---
         if (upgradeDetailArea == null)
         {
              Debug.LogError($"UpgradePanelHandler on {gameObject.name}: Upgrade Detail Area reference is null! Cannot show details.", this);
              return;
         }
         if (upgradeDetailText == null)
         {
              Debug.LogError($"UpgradePanelHandler on {gameObject.name}: Upgrade Detail TextMeshProUGUI reference is null! Cannot show details.", this);
              return;
         }

         // Activate the detail area GameObject
         upgradeDetailArea.SetActive(true);

         // Set the text component's text (can add more details like cost later)
         upgradeDetailText.text = upgrade.upgradeDescription; // + $"\nCost: {upgrade.cost}"; // Example adding cost

         Debug.Log($"UpgradePanelHandler on {gameObject.name}: Upgrade details displayed.");

         // Update button states whenever a new upgrade is shown in the detail area
         UpdateButtonStates(); // IMPORTANT: Call this after setting currentDetailUpgrade
    }

    /// <summary>
    /// Method called when the "Buy" button is clicked.
    /// Triggers the purchase attempt event via the UpgradeManager.
    /// </summary>
    private void OnBuyUpgradeButtonClick()
    {
        Debug.Log($"UpgradePanelHandler on {gameObject.name}: 'Buy' button clicked.", this);

        // Check if there is an upgrade currently displayed in the detail area
        if (currentDetailUpgrade == null)
        {
            Debug.LogWarning($"UpgradePanelHandler on {gameObject.name}: 'Buy' clicked, but no upgrade is currently displayed in the detail area.", this);
            // Optionally provide player feedback here (e.g., a popup)
             // Systems.UI.PlayerUIPopups.Instance?.ShowPopup("Purchase Failed", "No upgrade selected!");
            return;
        }

        // --- Trigger the purchase attempt event via the UpgradeManager ---
        if (upgradeManager != null)
        {
            Debug.Log($"UpgradePanelHandler on {gameObject.name}: Triggering purchase attempt for '{currentDetailUpgrade.upgradeName}'.", this);
            upgradeManager.TriggerUpgradePurchaseAttempt(currentDetailUpgrade); // Call the method on the manager
            // The actual purchase logic (cost, effects) will be handled by subscribers to the event (like UpgradeEffectHandler).
            // We don't mark it as purchased *here* because this only signals an *attempt*.
        }
        else
        {
            Debug.LogError($"UpgradePanelHandler on {gameObject.name}: UpgradeManager reference is null! Cannot trigger purchase event.", this);
            // Optionally provide player feedback here (e.g., a popup)
             // Systems.UI.PlayerUIPopups.Instance?.ShowPopup("Purchase Failed", "Upgrade system error!");
        }

        // IMPORTANT: Update button states immediately after the attempt.
        // If the purchase handler is synchronous, the IsUpgradePurchased check
        // in UpdateButtonStates will already reflect the new state.
        // If asynchronous, you might need a separate event (like OnUpgradePurchasedSuccessfully)
        // to trigger a state update. We've added a subscription for that below.
        // UpdateButtonStates(); // We now handle state updates via the subscribed event instead.
    }

    /// <summary>
    /// NEW: Handles the event from UpgradeManager when an upgrade is successfully purchased.
    /// </summary>
    /// <param name="purchasedUpgrade">The UpgradeDetailsSO that was just purchased.</param>
    private void HandleUpgradePurchasedSuccessfully(UpgradeDetailsSO purchasedUpgrade)
    {
        Debug.Log($"UpgradePanelHandler on {gameObject.name}: Received OnUpgradePurchasedSuccessfully event for '{purchasedUpgrade.upgradeName}'.");

        // If the purchased upgrade is the one currently displayed in the detail area,
        // update the buy button state.
        if (currentDetailUpgrade != null && currentDetailUpgrade == purchasedUpgrade)
        {
            Debug.Log($"UpgradePanelHandler on {gameObject.name}: Currently displaying purchased upgrade '{purchasedUpgrade.upgradeName}'. Updating button states.");
            UpdateButtonStates(); // Update button states (will disable the Buy button)
        }

        // Optional: Visually update the corresponding button in the *list* as well.
        // You would iterate through activeButtonInstances and find the one linked
        // to purchasedUpgrade (via UpgradeButtonData), then update its appearance.
        // Example:
        // foreach (var buttonGO in activeButtonInstances)
        // {
        //     UpgradeButtonData buttonData = buttonGO.GetComponent<UpgradeButtonData>();
        //     if (buttonData != null && buttonData.upgradeDetails == purchasedUpgrade)
        //     {
        //         // Code to visually indicate the button is purchased (e.g., change color)
        //         Debug.Log($"UpgradePanelHandler on {gameObject.name}: Visually updating list button for '{purchasedUpgrade.upgradeName}'.");
        //         break; // Found the button, no need to continue
        //     }
        // }
    }

    /// <summary>
    /// Updates the interactable state of the "Buy" button.
    /// </summary>
    private void UpdateButtonStates()
    {
        // The "Buy" button should be enabled only if:
        // 1. An upgrade is currently selected/displayed (`currentDetailUpgrade` is not null).
        // 2. The selected upgrade has NOT already been purchased (checked via UpgradeManager).
        // 3. (Future) Player can afford it, meets level requirements, etc.

        bool isUpgradeSelected = (currentDetailUpgrade != null);
        bool isUpgradePurchased = false;
        if (isUpgradeSelected && upgradeManager != null)
        {
            isUpgradePurchased = upgradeManager.IsUpgradePurchased(currentDetailUpgrade); // Check purchased status
        }

        bool canInteract = isUpgradeSelected && !isUpgradePurchased;
        // Future: Add affordability/level checks here
        // bool canAfford = isUpgradeSelected ? CanAfford(currentDetailUpgrade.cost) : false; // Need access to cost and player currency
        // bool meetsRequirements = isUpgradeSelected ? MeetsRequirements(currentDetailUpgrade) : false; // Need access to requirements

        // canInteract = isUpgradeSelected && !isUpgradePurchased && canAfford && meetsRequirements;


        if (buyUpgradeButton != null)
        {
            buyUpgradeButton.interactable = canInteract;

            // Optional: Update button text based on state
            // if (isUpgradeSelected)
            // {
            //     if (isUpgradePurchased) buyUpgradeButton.GetComponentInChildren<TextMeshProUGUI>()?.SetText("PURCHASED");
            //     else if (!canAfford) buyUpgradeButton.GetComponentInChildren<TextMeshProUGUI>()?.SetText("Can't Afford"); // If implementing affordability
            //     else buyUpgradeButton.GetComponentInChildren<TextMeshProUGUI>()?.SetText("BUY");
            // }
            // else
            // {
            //      buyUpgradeButton.GetComponentInChildren<TextMeshProUGUI>()?.SetText("Select Upgrade");
            // }


            // Debug.Log($"BuyUpgradeButton interactable: {buyUpgradeButton.interactable} (Selected: {isUpgradeSelected}, Purchased: {isUpgradePurchased})"); // Too noisy
        }
    }


    private void Awake()
    {
         // --- Initial UI reference validation in Awake ---
         if (upgradeListContentParent == null) Debug.LogError($"UpgradePanelHandler on {gameObject.name}: Upgrade List Content Parent Transform is not assigned in Awake!", this);
         if (upgradeDetailArea == null) Debug.LogError($"UpgradePanelHandler on {gameObject.name}: Upgrade Detail Area GameObject is not assigned in Awake!", this);
         if (upgradeDetailText == null) Debug.LogError($"UpgradePanelHandler on {gameObject.name}: Upgrade Detail TextMeshProUGUI is not assigned in Awake!", this);
         if (buyUpgradeButton == null) Debug.LogError($"UpgradePanelHandler on {gameObject.name}: Buy Upgrade Button is not assigned in Awake!", this);
         if (upgradeButtonPrefab == null) Debug.LogError($"UpgradePanelHandler on {gameObject.name}: Upgrade Button Prefab is not assigned in Awake!", this);


         // Get Manager singleton instance
         upgradeManager = UpgradeManager.Instance;
         if (upgradeManager == null)
         {
             Debug.LogError($"UpgradePanelHandler on {gameObject.name}: UpgradeManager.Instance not found in Awake! Cannot get upgrade data or trigger purchase events.", this);
             // Consider disabling the component if this is a critical dependency
             // enabled = false;
         }

         // Initialize the tracking list
         activeButtonInstances = new List<GameObject>();

         // Initialize the stored detail upgrade reference
         currentDetailUpgrade = null; // Ensure it starts as null
    }

    // --- Placeholder for any other necessary methods ---
}

// --- END OF FILE UpgradePanelHandler.cs ---