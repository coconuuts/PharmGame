// --- START OF FILE UpgradeEffectHandler.cs ---

using UnityEngine;
using System; // Needed for Action
using CustomerManagement; // Needed for CustomerManager
// Make sure UpgradeManager and UpgradeDetailsSO are accessible
// If they are in a specific namespace (e.g., Systems.Upgrades), add:
// using Systems.Upgrades;

// --- NEW: Add using for TimeManager ---
using Game.Utilities; // Assuming TimeManager might be in a utilities namespace, adjust if needed
// If TimeManager is in the global namespace, you don't need a 'using' directive for it,
// but you still need the reference field and checks.
// Let's assume TimeManager is in the global namespace based on the provided script.
// Remove the line above if TimeManager is not in Game.Utilities


/// <summary>
/// Listens to the UpgradeManager's purchase attempt event
/// and applies the actual game effects of the purchased upgrade.
/// MODIFIED: Calls UpgradeManager.MarkUpgradeAsPurchased after applying effect.
/// ADDED: Field to specify the Cashier TI NPC ID for the "Hire Cashier" upgrade.
/// ADDED: Logic to set the 'canStartDay' flag for the Cashier when the upgrade is purchased.
/// ADDED: Logic to delay the "Hire Cashier" effect until the next day.
/// </summary>
public class UpgradeEffectHandler : MonoBehaviour
{
    // References to necessary managers
    private UpgradeManager upgradeManager;
    private CustomerManager customerManager; // For customer-specific effects
    // Add references to other managers for other effect types (e.g., InventoryManager, PlayerStatsManager)

    // --- NEW: Reference to TimeManager ---
    private TimeManager timeManager;
    // --- END NEW ---


    // --- Specific NPC Unlocks ---
    [Header("Specific NPC Unlocks")]
    [Tooltip("The ID of the Cashier TI NPC to unlock with the 'Hire Cashier' upgrade.")]
    [SerializeField] private string cashierTiNpcId;
    // --- END NEW ---

    // --- NEW: Delayed Effect Tracking ---
    // Flag to track if the "Hire Cashier" effect is pending application on the next day start.
    // NOTE: For persistent games, this flag would need to be saved and loaded.
    private bool isHireCashierEffectPending = false;
    // --- END NEW ---


    private void Awake()
    {
        // Get singleton instances
        upgradeManager = UpgradeManager.Instance;
        if (upgradeManager == null)
        {
            Debug.LogError($"UpgradeEffectHandler on {gameObject.name}: UpgradeManager.Instance not found in Awake! Cannot subscribe to purchase events.", this);
            // Consider disabling the component if this is a critical dependency
            // enabled = false;
            return;
        }

        customerManager = CustomerManager.Instance;
        if (customerManager == null)
        {
            Debug.LogWarning($"UpgradeEffectHandler on {gameObject.name}: CustomerManager.Instance not found in Awake! Cannot apply customer-related upgrade effects.", this);
            // This might not need to disable the script if it handles other upgrade types,
            // but it means customer upgrades won't work.
        }

        // --- NEW: Get TimeManager instance ---
        timeManager = TimeManager.Instance;
         if (timeManager == null)
         {
             Debug.LogError($"UpgradeEffectHandler on {gameObject.name}: TimeManager.Instance not found in Awake! Cannot handle delayed time-based effects like 'Hire Cashier'.", this);
              // This is critical for the Hire Cashier upgrade, but not necessarily for others.
              // We won't disable the script entirely, but log the issue.
         }
        // --- END NEW ---


        Debug.Log($"UpgradeEffectHandler on {gameObject.name}: Initialized. UpgradeManager found: {upgradeManager != null}, CustomerManager found: {customerManager != null}, TimeManager found: {timeManager != null}");
    }

    private void OnEnable()
    {
        // Subscribe to the upgrade purchase attempt event
        if (upgradeManager != null)
        {
            upgradeManager.OnUpgradePurchaseAttempt += HandleUpgradePurchaseAttempt;
            Debug.Log($"UpgradeEffectHandler on {gameObject.name}: Subscribed to UpgradeManager.OnUpgradePurchaseAttempt.");
        }

        // --- NEW: Subscribe to Day Changed event ---
        if (timeManager != null)
        {
            timeManager.OnDayChanged += HandleDayChanged;
            Debug.Log($"UpgradeEffectHandler on {gameObject.name}: Subscribed to TimeManager.OnDayChanged.");
        }
        // --- END NEW ---
    }

    private void OnDisable()
    {
        // Unsubscribe from the event to prevent memory leaks
        if (upgradeManager != null)
        {
            upgradeManager.OnUpgradePurchaseAttempt -= HandleUpgradePurchaseAttempt;
            Debug.Log($"UpgradeEffectHandler on {gameObject.name}: Unsubscribed from UpgradeManager.OnUpgradePurchaseAttempt.");
        }

        // --- NEW: Unsubscribe from Day Changed event ---
        if (timeManager != null)
        {
            timeManager.OnDayChanged -= HandleDayChanged;
            Debug.Log($"UpgradeEffectHandler on {gameObject.name}: Unsubscribed from TimeManager.OnDayChanged.");
        }
        // --- END NEW ---
    }

    /// <summary>
    /// Handles the UpgradeManager's OnUpgradePurchaseAttempt event.
    /// This is where the logic for applying specific upgrade effects goes.
    /// For the "Hire Cashier" upgrade, this now only marks the effect as pending.
    /// </summary>
    /// <param name="upgradeDetails">The details of the upgrade being attempted.</param>
    private void HandleUpgradePurchaseAttempt(UpgradeDetailsSO upgradeDetails)
    {
        if (upgradeDetails == null)
        {
            Debug.LogWarning($"UpgradeEffectHandler on {gameObject.name}: Received UpgradePurchaseAttempt event with null upgradeDetails.", this);
            return;
        }

        Debug.Log($"UpgradeEffectHandler on {gameObject.name}: Handling purchase attempt for upgrade: '{upgradeDetails.upgradeName}' (ID: {upgradeDetails.uniqueID}).");

        // --- IMPORTANT ---
        // Add your actual purchase validation and cost deduction logic here!
        // For example:
        // if (!CanAfford(upgradeDetails.cost))
        // {
        //     Debug.Log($"UpgradeEffectHandler: Cannot afford '{upgradeDetails.upgradeName}'. Cost: {upgradeDetails.cost}");
        //     // Show player feedback (e.g., "Not enough money!")
        //     // PlayerUIPopups.Instance?.ShowPopup("Purchase Failed", "Not enough money!");
        //     return; // Abort purchase
        // }
        // if (upgradeManager.IsUpgradePurchased(upgradeDetails)) // Check if already purchased (for one-time upgrades)
        // {
        //      Debug.Log($"UpgradeEffectHandler: Upgrade '{upgradeDetails.upgradeName}' already purchased.");
        //      // Show player feedback (e.g., "Already owned!")
        //      // PlayerUIPopups.Instance?.ShowPopup("Purchase Failed", "Already owned!");
        //      return; // Abort purchase
        // }
        // if (!DeductCurrency(upgradeDetails.cost)) // Implement your currency deduction logic
        // {
        //      Debug.LogError("UpgradeEffectHandler: Failed to deduct currency after CanAfford check passed. Logic error?");
        //      // Show player feedback (e.g., "An error occurred!")
        //      // PlayerUIPopups.Instance?.ShowPopup("Purchase Failed", "An error occurred!");
        //      return; // Abort purchase
        // }

        // --- Apply Specific Upgrade Effects based on Name or ID ---
        // Using upgradeName for simplicity as requested, but uniqueID is more robust.
        // This method now primarily handles *immediate* effects or marks *delayed* effects as pending.
        bool effectHandledOrPending = false; // Renamed for clarity

        switch (upgradeDetails.upgradeName)
        {
            case "Paid Advertisements": // Ensure this string matches the UpgradeDetailsSO asset name EXACTLY
                ApplyPaidAdvertisementsEffect();
                effectHandledOrPending = true; // Indicate that an effect was handled immediately
                break;

            // Add more cases here for other upgrades:
            // case "Faster Browsing":
            //     ApplyFasterBrowsingEffect();
            //     effectHandledOrPending = true;
            //     break;
            // case "Cashier Training":
            //     ApplyCashierTrainingEffect();
            //     effectHandledOrPending = true;
            //     break;
            // etc.

            // --- MODIFIED CASE FOR HIRING CASHIER --- // <-- MODIFIED CASE
            case "Hire Cashier": // Match the name of the upgrade asset
                 // We don't apply the effect immediately, just mark it as pending.
                 // The actual effect application happens on the next day start.
                 isHireCashierEffectPending = true;
                 effectHandledOrPending = true; // Indicate that the effect handling is set up (pending)
                 Debug.Log($"UpgradeEffectHandler on {gameObject.name}: Marked 'Hire Cashier' effect as pending for the next day.", this);
                 // The actual ApplyHireCashierEffect() method is NOT called here anymore.
                 break;
            // --- END MODIFIED CASE ---

            default:
                Debug.LogWarning($"UpgradeEffectHandler on {gameObject.name}: Received purchase attempt for unknown upgrade: '{upgradeDetails.upgradeName}'. No effect applied or pending setup.", this);
                // effectHandledOrPending remains false
                break;
        }

        // --- Mark as purchased *if* the effect was successfully handled or set up as pending ---
        // This ensures the UI updates and the upgrade is tracked as owned immediately after purchase validation passes.
        // In a real game, this should only happen AFTER cost is deducted and persistence is handled.
        if (effectHandledOrPending)
        {
            // Show success popup (optional, depends on your UI flow)
            // Systems.UI.PlayerUIPopups.Instance?.ShowPopup("Upgrade Purchased!", $"'{upgradeDetails.upgradeName}' purchased!"); // Text might change depending on immediate/delayed effect

            // Call the UpgradeManager to mark this upgrade as purchased
            upgradeManager.MarkUpgradeAsPurchased(upgradeDetails); // This still happens immediately
        }
        else
        {
             // Show failure popup if no effect handler was found (optional)
             // Systems.UI.PlayerUIPopups.Instance?.ShowPopup("Purchase Failed", $"Could not find effect handler for '{upgradeDetails.upgradeName}'.");
        }
    }

    /// <summary>
    /// Applies the effect for the "Paid Advertisements" upgrade.
    /// Sets the CustomerManager's bus arrival interval.
    /// </summary>
    private void ApplyPaidAdvertisementsEffect()
    {
        if (customerManager != null)
        {
            // Set the new bus arrival interval value
            float newInterval = 60f; // The desired interval for this upgrade
            customerManager.BusArrivalInterval = newInterval; // Use the public property

            Debug.Log($"UpgradeEffectHandler on {gameObject.name}: Applied 'Paid Advertisements' effect. CustomerManager Bus Arrival Interval set to {newInterval}s.");
        }
        else
        {
            Debug.LogError($"UpgradeEffectHandler on {gameObject.name}: Cannot apply 'Paid Advertisements' effect. CustomerManager instance is null.", this);
        }
    }

    // --- NEW METHOD TO HANDLE DAY CHANGED EVENT --- // <-- ADDED METHOD
    /// <summary>
    /// Called by TimeManager when the game day increments.
    /// Checks for and applies any pending delayed upgrade effects.
    /// </summary>
    /// <param name="newDay">The number of the new day.</param>
    private void HandleDayChanged(int newDay)
    {
        Debug.Log($"UpgradeEffectHandler on {gameObject.name}: Received OnDayChanged event for Day {newDay}. Checking for pending effects.");

        // Check if the "Hire Cashier" effect is pending
        if (isHireCashierEffectPending)
        {
            Debug.Log($"UpgradeEffectHandler on {gameObject.name}: 'Hire Cashier' effect is pending. Applying now for Day {newDay}.");
            ApplyHireCashierEffect(); // Apply the actual effect

            // Reset the pending flag AFTER applying the effect
            isHireCashierEffectPending = false;
            Debug.Log($"UpgradeEffectHandler on {gameObject.name}: 'Hire Cashier' effect applied and pending flag reset.");

            // Optional: Show player feedback that the hired cashier is now available
             // Systems.UI.PlayerUIPopups.Instance?.ShowPopup("New Employee!", "Your new cashier is ready to start work!");
        }

        // Add checks for other potential delayed effects here if needed in the future
        // if (isAnotherEffectPending) { ApplyAnotherDelayedEffect(); isAnotherEffectPending = false; }
    }
    // --- END NEW METHOD ---


    /// <summary>
    /// Applies the actual game effect for the "Hire Cashier" upgrade.
    /// Sets the 'canStartDay' flag on the Cashier TI NPC's data.
    /// This method is now called by HandleDayChanged when the next day begins.
    /// </summary>
    private void ApplyHireCashierEffect()
    {
        // Check if the Cashier ID is configured
        if (string.IsNullOrWhiteSpace(cashierTiNpcId))
        {
            Debug.LogError($"UpgradeEffectHandler on {gameObject.name}: Attempting to apply 'Hire Cashier' effect, but Cashier TI NPC ID is not configured in the inspector! Cannot apply effect.", this);
            return;
        }

        // Get the TiNpcManager instance
        // Assuming Game.NPC.TI.TiNpcManager is a singleton with an Instance property
        Game.NPC.TI.TiNpcManager tiNpcManager = Game.NPC.TI.TiNpcManager.Instance;
        if (tiNpcManager == null)
        {
            Debug.LogError($"UpgradeEffectHandler on {gameObject.name}: Attempting to apply 'Hire Cashier' effect, but TiNpcManager instance not found! Cannot apply effect.", this);
            return;
        }

        // Get the Cashier's TiNpcData using the configured ID
        Game.NPC.TI.TiNpcData cashierData = tiNpcManager.GetTiNpcData(cashierTiNpcId);

        if (cashierData != null)
        {
            // Set the canStartDay flag to true
            cashierData.canStartDay = true;
            Debug.Log($"UpgradeEffectHandler on {gameObject.name}: Applied 'Hire Cashier' effect. Set 'canStartDay' to true for TI NPC '{cashierTiNpcId}'.", this);

            // The TimeManager or SimulationManager should now pick this up
            // at the beginning of the next day cycle to activate the NPC's schedule.
        }
        else
        {
            Debug.LogError($"UpgradeEffectHandler on {gameObject.name}: Attempting to apply 'Hire Cashier' effect, but TI NPC data with ID '{cashierTiNpcId}' not found in TiNpcManager! Cannot apply effect. Is the ID correct?", this);
        }
    }
    // --- END MODIFIED METHOD ---


    // Add private methods for other specific upgrade effects here
    // private void ApplyFasterBrowsingEffect() { /* ... logic ... */ }
    // private void ApplyCashierTrainingEffect() { /* ... logic ... */ }


    // --- Placeholder for purchase validation and currency deduction ---
    // private bool CanAfford(float cost)
    // {
    //     // Implement check against player's currency
    //     // Example: return PlayerStatsManager.Instance.Currency >= cost;
    //     Debug.Log($"UpgradeEffectHandler: (Placeholder) Checking if player can afford {cost}.");
    //     return true; // Always true for now
    // }

    // private bool DeductCurrency(float cost)
    // {
    //      // Implement currency deduction
    //      // Example: return PlayerStatsManager.Instance.DeductCurrency(cost);
    //      Debug.Log($"UpgradeEffectHandler: (Placeholder) Deducting {cost} currency.");
    //      return true; // Always true for now
    // }
    // --- End Placeholder ---


    // Optional: OnDestroy for final cleanup if needed, though OnDisable should cover event unsubscribing.
    // private void OnDestroy()
    // {
    //     // OnDisable should handle event cleanup, but calling it here is a safe measure
    //     OnDisable();
    // }
}
// --- END OF FILE UpgradeEffectHandler.cs ---