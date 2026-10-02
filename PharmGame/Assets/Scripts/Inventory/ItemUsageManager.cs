using UnityEngine;
using Systems.Inventory;
using System.Collections.Generic;
using System;
using Systems.GameStates;

namespace Systems.Inventory
{
    /// <summary>
    /// Manages the logic for using items, typically from the player's selected inventory slot.
    /// Handles use input and delegates effect execution based on item type,
    /// including new health/durability logic for non-stackable items and checking allowed triggers.
    /// Also manages gun-specific mechanics like firing and reloading state.
    /// </summary>
    public class ItemUsageManager : MonoBehaviour
    {
        public static ItemUsageManager Instance { get; private set; }

        [Tooltip("Tag identifying the player's toolbar Inventory GameObject.")]
        [SerializeField] private string playerInventoryTag = "PlayerToolbarInventory"; // Ensure this matches the tag you set

        private InventorySelector playerInventorySelector; // Reference to the player's selector script

        private void Awake()
        {
            // Implement singleton pattern
            if (Instance == null) // Corrected singleton check
            {
                Instance = this;
                // Optional: DontDestroyOnLoad(gameObject); // If the manager should persist
            }
            else
            {
                Debug.LogWarning("ItemUsageManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

             // Find the player inventory selector by tag
             GameObject playerInventoryObject = GameObject.FindGameObjectWithTag(playerInventoryTag);
             if (playerInventoryObject != null)
             {
                 playerInventorySelector = playerInventoryObject.GetComponent<InventorySelector>();
                 if (playerInventorySelector == null)
                 {
                     Debug.LogError($"ItemUsageManager: GameObject with tag '{playerInventoryObject.tag}' found, but it does not have an InventorySelector component.", playerInventoryObject);
                 }
             }
             else
             {
                 Debug.LogError($"ItemUsageManager: No GameObject found with tag '{playerInventoryTag}'. Player item usage will not work.", this);
             }

             // Optional: Subscribe to MenuManager state changes if you only want usage in specific states
             // MenuManager.OnStateChanged += HandleGameStateChanged; // Example
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            // Optional: Unsubscribe from MenuManager event
            // if (MenuManager.Instance != null) MenuManager.OnStateChanged -= HandleGameStateChanged;
        }

        // --- Input Handling for Using Item ---
        private void Update()
        {
            // Only process item usage input if the game is in the Playing state
            // And if we have a player inventory selector assigned
            if (MenuManager.Instance != null && MenuManager.Instance.currentState == MenuManager.GameState.Playing && playerInventorySelector != null)
            {
                // Get the currently selected item instance
                Item selectedItem = playerInventorySelector.SelectedItem;

                // Only proceed if an item is selected and has details and allowed triggers list
                if (selectedItem != null && selectedItem.details != null && selectedItem.details.allowedUsageTriggers != null)
                {
                    // --- Check for the "use item" input button press (e.g., F key) ---
                    if (Input.GetKeyDown(KeyCode.F))
                    {
                        // Check if the item allows usage via the FKey trigger
                        if (selectedItem.details.allowedUsageTriggers.Contains(UsageTriggerType.FKey))
                        {
                             Debug.Log($"ItemUsageManager: 'F' input detected. Attempting to use selected item '{selectedItem.details.Name}' via FKey trigger.");
                            // Tell the player inventory selector to attempt to use its selected item, passing the trigger type
                            // Pass -1 for variableHealthLoss as it's not applicable to FKey trigger by default
                            playerInventorySelector.UseSelectedItem(UsageTriggerType.FKey);
                        }
                        // else { Debug.Log($"ItemUsageManager: 'F' input detected for '{selectedItem.details.Name}', but FKey trigger is not allowed for this item."); } // Commented out to reduce spam
                    }

                    // --- Check for Left Mouse Button input (e.g., for weapons) ---
                     if (Input.GetMouseButtonDown(0))
                     {
                         // Check if the item allows usage via the LeftClick trigger
                         if (selectedItem.details.allowedUsageTriggers.Contains(UsageTriggerType.LeftClick))
                         {
                             Debug.Log($"ItemUsageManager: Left Click input detected. Attempting to use selected item '{selectedItem.details.Name}' via LeftClick trigger.");
                             // Tell the player inventory selector to attempt to use its selected item, passing the trigger type
                             // Pass -1 for variableHealthLoss as it's not applicable to LeftClick trigger by default
                             playerInventorySelector.UseSelectedItem(UsageTriggerType.LeftClick);
                         }
                         // else { Debug.Log($"ItemUsageManager: Left Click input detected for '{selectedItem.details.Name}', but LeftClick trigger is not allowed for this item."); } // Commented out to reduce spam
                     }

                    // --- Check for Reload input (e.g., R key) ---
                     if (Input.GetKeyDown(KeyCode.R))
                     {
                         // Check if the selected item is a gun and can be reloaded
                         if (selectedItem.details.usageLogic == ItemUsageLogic.GunLogic && selectedItem.details.magazineSize > 0)
                         {
                             Debug.Log($"ItemUsageManager: 'R' input detected. Attempting to reload '{selectedItem.details.Name}'.");
                             TryStartReload(selectedItem, playerInventorySelector.parentInventory, playerInventorySelector.selectedIndex);
                         }
                         // else { Debug.Log($"ItemUsageManager: 'R' input detected, but selected item '{selectedItem.details.Name}' is not a reloadable gun."); } // Commented out to reduce spam
                     }

                    // --- Check for Add Ammo input (e.g., T key) - TEMPORARY FOR TESTING ---
                     if (Input.GetKeyDown(KeyCode.T))
                     {
                         // Check if the selected item is a gun
                         if (selectedItem.details.usageLogic == ItemUsageLogic.GunLogic && selectedItem.details.magazineSize > 0)
                         {
                             Debug.Log($"ItemUsageManager: 'T' input detected. Simulating ammo acquisition for '{selectedItem.details.Name}'.");
                             SimulateAddAmmo(selectedItem, playerInventorySelector.parentInventory, playerInventorySelector.selectedIndex);
                         }
                         // else { Debug.Log($"ItemUsageManager: 'T' input detected, but selected item '{selectedItem.details.Name}' is not a gun."); } // Commented out to reduce spam
                     }

                    // TODO: Add other input checks here for other trigger types if needed
                }

                // --- Handle Reloading Progress ---
                // Check the currently selected item if it's a gun and is reloading
                if (selectedItem != null && selectedItem.details != null &&
                    selectedItem.details.usageLogic == ItemUsageLogic.GunLogic && selectedItem.isReloading)
                {
                    CheckReloadProgress(selectedItem, playerInventorySelector.parentInventory, playerInventorySelector.selectedIndex);
                }
            }
            // Optional: Add other states where item usage is allowed
        }

        // --- Item Usage Logic (Called by InventorySelector or other systems) ---

        /// <summary>
        /// Executes the usage logic for a specific item instance from a specific slot.
        /// This method is called by the InventorySelector or other systems (e.g., Crafting).
        /// Handles quantity reduction for stackable items and health reduction for non-stackable items,
        /// varying logic based on the item's usageLogic and potentially the trigger type.
        /// </summary>
        /// <param name="itemToUse">The Item instance to use (should be the one from the selected slot).</param>
        /// <param name="userInventory">The Inventory component the item belongs to.</param>
        /// <param name="selectedIndex">The index of the slot the item was selected from.</param>
        /// <param name="trigger">The type of action that triggered this usage.</param>
        /// <param name="variableHealthLoss">Optional: The specific amount of health to reduce for VariableHealthReduction logic. Use -1 or omit for default.</param>
        /// <returns>True if the item usage logic was successfully triggered, false otherwise.</returns>
        public bool UseItem(Item itemToUse, Inventory userInventory, int selectedIndex, UsageTriggerType trigger, int variableHealthLoss = -1)
        {
            if (itemToUse == null || itemToUse.details == null || userInventory == null || userInventory.InventoryState == null || userInventory.Combiner == null)
            {
                Debug.LogWarning("ItemUsageManager: UseItem called with invalid parameters (null item, details, inventory, state, or combiner).", this);
                return false;
            }

             // --- IMPORTANT VALIDATION ---
             // Double-check that the item instance provided is actually the one currently at the selected index
             // This prevents issues if the inventory state changed unexpectedly between selection and usage.
             Item itemInSlot = userInventory.InventoryState[selectedIndex];
             if (itemInSlot == null || itemInSlot.Id != itemToUse.Id)
             {
                 Debug.LogWarning($"ItemUsageManager: Item in selected slot {selectedIndex} ({itemInSlot?.details?.Name ?? "Empty"}) does not match the item provided for usage ({itemToUse.details?.Name ?? "Null"}). Inventory state may have changed. Aborting usage.", this);
                 return false; // Abort if the item in the slot is not what we expected
             }
             // --- END VALIDATION ---

            // --- Check if the item is currently usable ---
            // This uses the IsUsable() method from the Item class, which includes gun-specific checks.
            if (!itemToUse.IsUsable())
            {
                // Log specific reason if it's a gun
                if (itemToUse.details.usageLogic == ItemUsageLogic.GunLogic && itemToUse.details.magazineSize > 0)
                {
                    if (itemToUse.isReloading) Debug.Log($"ItemUsageManager: Attempted to use '{itemToUse.details.Name}' from slot {selectedIndex}, but it is reloading. Aborting usage.");
                    else if (itemToUse.currentMagazineHealth <= 0 && itemToUse.totalReserveHealth <= 0) Debug.Log($"ItemUsageManager: Attempted to use '{itemToUse.details.Name}' from slot {selectedIndex}, but it is completely out of ammo. Aborting usage.");
                    else if (itemToUse.currentMagazineHealth <= 0) Debug.Log($"ItemUsageManager: Attempted to use '{itemToUse.details.Name}' from slot {selectedIndex}, but the magazine is empty. Needs reload. Aborting usage.");
                    // Note: If it has mag ammo, IsUsable() would be true, so we don't need an else here for that case.
                }
                else // Other non-usable items (e.g., quantity 0 stackable, health 0 durable non-gun)
                {
                     Debug.Log($"ItemUsageManager: Attempted to use depleted item '{itemToUse.details.Name}' from slot {selectedIndex}. Aborting usage.");
                }
                return false; // Item is not usable in its current state
            }

            // --- Check if the trigger type is allowed for this item ---
            // This check is primarily for external calls (like crafting systems) or robustness.
            // Input handling in Update already checks this for player input.
            if (itemToUse.details.allowedUsageTriggers == null || !itemToUse.details.allowedUsageTriggers.Contains(trigger))
            {
                 Debug.LogWarning($"ItemUsageManager: UseItem called for '{itemToUse.details.Name}' with disallowed trigger type '{trigger}'. Aborting usage.", this);
                 return false;
            }

            Debug.Log($"ItemUsageManager: Executing usage logic for item '{itemToUse.details.Name}' (Instance ID: {itemToUse.Id}) from slot {selectedIndex}, triggered by: {trigger}.");

            bool usageHandled = false;

            // --- Implement Item Effect Logic Here ---
            // Based on itemToUse.details, perform the effect.
            // This is where you'd add specific effects (e.g., apply buff, spawn projectile, etc.)
            // For now, we focus on the quantity/health reduction side effects of usage.
            Debug.Log($"ItemUsageManager: Applying effects for '{itemToUse.details.Name}' (Effect logic TBD).");


            // --- Handle Stackable vs Non-Stackable Usage ---

            if (itemToUse.details.maxStack > 1)
            {
                // --- Stackable Item Usage (Existing Logic) ---
                Debug.Log($"ItemUsageManager: Item '{itemToUse.details.Name}' is stackable. Reducing quantity by 1 in slot {selectedIndex}.");

                itemToUse.quantity--; // Reduce the quantity of the specific item instance

                if (itemToUse.quantity <= 0)
                {
                    // If quantity is zero or less, remove the item instance from the inventory
                    Debug.Log($"ItemUsageManager: Stackable item quantity depleted in slot {selectedIndex}. Removing item instance.");
                    // Use the ObservableArray's method to remove the item at the specific index
                    userInventory.InventoryState.RemoveAt(selectedIndex); // Use RemoveAt with the index
                    usageHandled = true; // Item was used and removed
                }
                else
                {
                    // If quantity is still positive, notify the ObservableArray that the item at this index changed (quantity updated)
                    Debug.Log($"ItemUsageManager: Stackable item quantity updated in slot {selectedIndex}. New quantity: {itemToUse.quantity}.");
                    // Use SetItemAtIndex to update the item at the specific index.
                    // Pass the SAME item instance, but its internal quantity has been modified.
                    userInventory.InventoryState.SetItemAtIndex(itemToUse, selectedIndex); // Triggers SlotUpdated for this index
                    usageHandled = true; // Item was used and quantity updated
                }
            }
            else // itemToUse.details.maxStack == 1 (Non-Stackable Item)
            {
                // --- Non-Stackable Item Usage (Health/Durability Logic) ---

                if (itemToUse.details.maxHealth > 0)
                {
                    // --- Durable Non-Stackable Item ---
                    // Check if it's a gun before applying general health logic
                    if (itemToUse.details.usageLogic == ItemUsageLogic.GunLogic && itemToUse.details.magazineSize > 0)
                    {
                        // --- Gun Logic (Firing) ---
                        Debug.Log($"ItemUsageManager: Item '{itemToUse.details.Name}' is a gun. Applying GunLogic.");

                        // We already checked IsUsable() at the start, which includes checking if mag > 0 and not reloading.
                        // So if we reach here for a gun, it means it *can* fire.
                        itemToUse.currentMagazineHealth--; // Decrement magazine ammo

                        Debug.Log($"ItemUsageManager: Fired '{itemToUse.details.Name}'. Magazine ammo: {itemToUse.currentMagazineHealth}/{itemToUse.details.magazineSize}. Total ammo: {itemToUse.health}.");

                        // Notify the ObservableArray that the item state changed (mag health updated)
                        userInventory.InventoryState.SetItemAtIndex(itemToUse, selectedIndex); // Triggers SlotUpdated

                        usageHandled = true; // Usage was handled (fired)

                        // Check if the magazine is now empty
                        if (itemToUse.currentMagazineHealth <= 0)
                        {
                            Debug.Log($"ItemUsageManager: Magazine of '{itemToUse.details.Name}' is empty. Auto-reloading...");
                            // --- Trigger Auto-Reload --- 
                            // For now, just log the intention. The actual reload start logic goes here later.
                            TryStartReload(itemToUse, userInventory, selectedIndex);
                        }
                         // Note: Gun items are NOT removed when total health reaches 0.
                         // The IsUsable() check at the start prevents using them when total health is 0.
                    }
                    else
                    {
                        // --- Other Durable Non-Stackable Item (Basic/Variable/Delayed) ---
                        Debug.Log($"ItemUsageManager: Item '{itemToUse.details.Name}' is non-stackable with health ({itemToUse.health}/{itemToUse.details.maxHealth}). Applying usage logic: {itemToUse.details.usageLogic}.");

                        bool healthChanged = false; // Flag to know if health was actually reduced this use

                        // --- APPLY HEALTH REDUCTION BASED ON USAGE LOGIC ---
                        switch (itemToUse.details.usageLogic)
                        {
                            case ItemUsageLogic.BasicHealthReduction:
                                itemToUse.health--;
                                healthChanged = true;
                                Debug.Log($"ItemUsageManager: BasicHealthReduction applied. Health reduced by 1.");
                                // Reset usage counter for clarity, though not strictly needed for this logic type
                                itemToUse.usageEventsSinceLastLoss = 0;
                                break;

                            case ItemUsageLogic.VariableHealthReduction:
                                int actualLoss = (variableHealthLoss >= 0) ? variableHealthLoss : itemToUse.details.defaultVariableHealthLoss;
                                itemToUse.health -= actualLoss;
                                healthChanged = true;
                                Debug.Log($"ItemUsageManager: VariableHealthReduction applied. Health reduced by {actualLoss}.");
                                // Reset usage counter for clarity
                                itemToUse.usageEventsSinceLastLoss = 0;
                                break;

                            case ItemUsageLogic.DelayedHealthReduction:
                                itemToUse.usageEventsSinceLastLoss++;
                                Debug.Log($"ItemUsageManager: DelayedHealthReduction logic. Usage events since last loss: {itemToUse.usageEventsSinceLastLoss}/{itemToUse.details.usageEventsPerHealthLoss}.");

                                if (itemToUse.usageEventsSinceLastLoss >= itemToUse.details.usageEventsPerHealthLoss)
                                {
                                    itemToUse.health -= itemToUse.details.healthLossPerEventThreshold;
                                    healthChanged = true;
                                    Debug.Log($"ItemUsageManager: DelayedHealthReduction threshold reached ({itemToUse.details.usageEventsPerHealthLoss} events). Health reduced by {itemToUse.details.healthLossPerEventThreshold}.");
                                    itemToUse.usageEventsSinceLastLoss = 0; // Reset counter after health loss
                                }
                                // Note: Health doesn't change every use, only when threshold is met.
                                // We still call SetItemAtIndex below to update the usage counter visual (if implemented later).
                                break;

                            case ItemUsageLogic.QuantityBased:
                                 // This case should ideally not be reached for maxStack == 1 && maxHealth > 0
                                 // If it is, treat it as basic reduction? Or log error?
                                 Debug.LogWarning($"ItemUsageManager: Durable non-stackable item '{itemToUse.details.Name}' has usageLogic set to QuantityBased. This is unexpected. Applying BasicHealthReduction as fallback.");
                                 itemToUse.health--;
                                 healthChanged = true;
                                 itemToUse.usageEventsSinceLastLoss = 0;
                                 break;

                            case ItemUsageLogic.GunLogic:
                                // This case is handled above. Should not reach here.
                                Debug.LogError($"ItemUsageManager: Durable non-stackable item '{itemToUse.details.Name}' with GunLogic reached the general durable logic switch. This is a logic error.");
                                break;

                            default:
                                Debug.LogWarning($"ItemUsageManager: Item '{itemToUse.details.Name}' has unhandled usageLogic: {itemToUse.details.usageLogic}. Applying BasicHealthReduction as fallback.");
                                itemToUse.health--;
                                healthChanged = true;
                                itemToUse.usageEventsSinceLastLoss = 0;
                                break;
                        }
                        // --- END APPLY HEALTH REDUCTION ---

                        // Debug log the health change (if it happened this use) and current state
                        if (healthChanged)
                        {
                             Debug.Log($"ItemUsageManager: Item '{itemToUse.details.Name}' health is now {itemToUse.health}.");
                        }
                         Debug.Log($"ItemUsageManager: Item '{itemToUse.details.Name}' usageEventsSinceLastLoss is now {itemToUse.usageEventsSinceLastLoss}.");


                        // Notify the ObservableArray that the item at this index changed (health or usage counter updated)
                        userInventory.InventoryState.SetItemAtIndex(itemToUse, selectedIndex); // Triggers SlotUpdated for this index
                        usageHandled = true; // Item was used and health/counter updated

                        // Check if health reached zero AFTER applying reduction
                        if (itemToUse.health <= 0)
                        {
                            Debug.Log($"ItemUsageManager: Durable non-stackable item '{itemToUse.details.Name}' in slot {selectedIndex} health reached zero. Removing item instance.");
                            // Remove the item instance from the inventory
                            userInventory.InventoryState.RemoveAt(selectedIndex); // Use RemoveAt with the index
                            // usageHandled is already true
                        }
                    }
                }
                else
                {
                    // --- Non-Stackable Item with No Health (Simple Consumable) ---
                    // This case covers items like a single piece of fruit (maxStack=1, maxHealth=0).
                    // They are consumed entirely on one use.
                    Debug.Log($"ItemUsageManager: Item '{itemToUse.details.Name}' is non-stackable with no health. Consuming entirely.");

                    // Treat it like a quantity-1 stackable that is fully consumed.
                    // The existing quantity logic handles this if quantity starts at 1.
                    itemToUse.quantity = 0; // Set quantity to 0 to trigger removal

                    Debug.Log($"ItemUsageManager: Non-durable non-stackable item depleted in slot {selectedIndex}. Removing item instance.");
                    userInventory.InventoryState.RemoveAt(selectedIndex); // Use RemoveAt with the index
                    usageHandled = true; // Item was used and removed
                }
            }

            // TODO: Add more sophisticated logic for different item types (Equip, Throw, Placeable, etc.)
            // This current logic only covers "consumable" type usage (quantity/health reduction) and gun firing.

            if (!usageHandled)
            {
                 Debug.LogWarning($"ItemUsageManager: No specific usage logic found or handled for item: {itemToUse.details.Name}.", this);
                 // If no specific usage was handled, decide what to do.
                 // For consumables, the logic above already handles removal/reduction.
                 // For other unhandled types, maybe do nothing or log a different message.
            }


            return usageHandled; // Return true if usage was handled (item quantity/health reduced/removed, or gun fired)
        }


        // --- RELOAD LOGIC ---

        /// <summary>
        /// Attempts to start the reload process for a gun item.
        /// Called by player input (R key) or automatically when magazine is empty after firing.
        /// </summary>
        public bool TryStartReload(Item gunItem, Inventory userInventory, int selectedIndex)
        {
            if (gunItem == null || gunItem.details == null || userInventory == null || userInventory.InventoryState == null || userInventory.Combiner == null)
            {
                Debug.LogWarning("ItemUsageManager: TryStartReload called with invalid parameters.", this);
                return false;
            }

            // Ensure it's actually a gun item
            if (gunItem.details.usageLogic != ItemUsageLogic.GunLogic || gunItem.details.magazineSize <= 0)
            {
                Debug.LogWarning($"ItemUsageManager: Attempted to reload non-gun item '{gunItem.details.Name}'.", this);
                return false;
            }

            // Check if it's already reloading
            if (gunItem.isReloading)
            {
                Debug.Log($"ItemUsageManager: '{gunItem.details.Name}' is already reloading.", this);
                return false;
            }

            // Check if the magazine is already full (no need to reload manually)
            // Auto-reload logic after firing will bypass this check if mag is 0.
            // Manual reload (R key) should respect this.
            if (gunItem.currentMagazineHealth >= gunItem.details.magazineSize)
            {
                Debug.Log($"ItemUsageManager: Magazine of '{gunItem.details.Name}' is already full. No reload needed.", this);
                return false;
            }

            // Check if there is any reserve ammo to reload from
            if (gunItem.totalReserveHealth <= 0)
            {
                Debug.Log($"ItemUsageManager: '{gunItem.details.Name}' is out of reserve ammo. Cannot reload.", this);
                return false;
            }

            // --- Start the reload process ---
            gunItem.isReloading = true;
            gunItem.reloadStartTime = Time.time;

            Debug.Log($"ItemUsageManager: Starting reload for '{gunItem.details.Name}'. Reload time: {gunItem.details.reloadTime}s.", this);

            // Notify the ObservableArray that the item state changed (isReloading flag updated)
            userInventory.InventoryState.SetItemAtIndex(gunItem, selectedIndex); // Triggers SlotUpdated

            return true; // Reload successfully started
        }

        /// <summary>
        /// Checks if a reloading gun has finished its reload time and completes the process.
        /// Called from Update for the currently selected gun.
        /// </summary>
        private void CheckReloadProgress(Item gunItem, Inventory userInventory, int selectedIndex)
        {
             if (gunItem == null || gunItem.details == null || userInventory == null || userInventory.InventoryState == null || userInventory.Combiner == null)
            {
                Debug.LogWarning("ItemUsageManager: CheckReloadProgress called with invalid parameters.", this);
                // If parameters are invalid, stop reloading state?
                 if (gunItem != null) gunItem.isReloading = false; // Attempt to reset state
                return;
            }

             // Ensure it's a gun item and is actually marked as reloading
             if (gunItem.details.usageLogic != ItemUsageLogic.GunLogic || !gunItem.isReloading)
             {
                  // This shouldn't happen if called correctly from Update
                  Debug.LogError($"ItemUsageManager: CheckReloadProgress called for non-reloading or non-gun item '{gunItem.details.Name}'.", this);
                  return;
             }

            // Check if enough time has passed
            if (Time.time - gunItem.reloadStartTime >= gunItem.details.reloadTime)
            {
                // --- Reload Complete ---
                Debug.Log($"ItemUsageManager: Reload complete for '{gunItem.details.Name}'.");

                // Calculate how much ammo to transfer from reserve to magazine
                int ammoToTransfer = Mathf.Min(gunItem.details.magazineSize - gunItem.currentMagazineHealth, gunItem.totalReserveHealth);

                gunItem.currentMagazineHealth += ammoToTransfer;
                gunItem.totalReserveHealth -= ammoToTransfer;
                gunItem.health = gunItem.currentMagazineHealth + gunItem.totalReserveHealth; // Keep total health updated

                // Reset reloading state
                gunItem.isReloading = false;
                gunItem.reloadStartTime = 0.0f;

                Debug.Log($"ItemUsageManager: Reloaded {ammoToTransfer} rounds. Magazine: {gunItem.currentMagazineHealth}/{gunItem.details.magazineSize}. Reserve: {gunItem.totalReserveHealth}. Total: {gunItem.health}.");

                // Notify the ObservableArray that the item state changed (mag/reserve/reloading updated)
                userInventory.InventoryState.SetItemAtIndex(gunItem, selectedIndex); // Triggers SlotUpdated

                // Optional: Trigger a reload complete event here
            }
            // else: Reload is still in progress, do nothing this frame.
        }

        // --- TEMPORARY AMMO ACQUISITION LOGIC ---
        /// <summary>
        /// Simulates adding ammo to a gun's total reserve health (for testing).
        /// Called by player input (T key).
        /// </summary>
        public void SimulateAddAmmo(Item gunItem, Inventory userInventory, int selectedIndex)
        {
             if (gunItem == null || gunItem.details == null || userInventory == null || userInventory.InventoryState == null || userInventory.Combiner == null)
            {
                Debug.LogWarning("ItemUsageManager: SimulateAddAmmo called with invalid parameters.", this);
                return;
            }

            // Ensure it's actually a gun item
            if (gunItem.details.usageLogic != ItemUsageLogic.GunLogic || gunItem.details.magazineSize <= 0)
            {
                Debug.LogWarning($"ItemUsageManager: Attempted to add ammo to non-gun item '{gunItem.details.Name}'.", this);
                return;
            }

            // Add a magazine's worth of ammo to the total health, capped by maxHealth
            int ammoToAdd = gunItem.details.magazineSize; // Add a full magazine to reserve
            int newTotalHealth = Mathf.Min(gunItem.health + ammoToAdd, gunItem.details.maxHealth);
            int actualAmmoAdded = newTotalHealth - gunItem.health;

            if (actualAmmoAdded > 0)
            {
                gunItem.health = newTotalHealth;
                // Update reserve based on the new total health and current magazine
                gunItem.totalReserveHealth = gunItem.health - gunItem.currentMagazineHealth;

                Debug.Log($"ItemUsageManager: Simulated adding {actualAmmoAdded} ammo to '{gunItem.details.Name}'. Total ammo is now {gunItem.health}. Reserve: {gunItem.totalReserveHealth}.");

                // Notify the ObservableArray that the item state changed (total health/reserve updated)
                userInventory.InventoryState.SetItemAtIndex(gunItem, selectedIndex); // Triggers SlotUpdated

                // Note: Adding ammo doesn't automatically reload. Player still needs to press R or empty the mag.
            }
            else
            {
                 Debug.Log($"ItemUsageManager: Could not add ammo to '{gunItem.details.Name}'. Already at max total ammo ({gunItem.details.maxHealth}).");
            }
        }


        // TODO: Add other usage-related methods (e.g., EquipItem, DropItem - though Drop might be D&D related)

        /// <summary>
        /// Public method for other systems (like Crafting) to trigger item usage with variable health loss.
        /// </summary>
        /// <param name="itemToUse">The Item instance to use.</param>
        /// <param name="userInventory">The Inventory component the item belongs to.</param>
        /// <param name="selectedIndex">The index of the slot the item is in.</param>
        /// <param name="variableLossAmount">The specific amount of health to reduce.</param>
        /// <returns>True if usage was successful.</returns>
        public bool UseItemVariableLoss(Item itemToUse, Inventory userInventory, int selectedIndex, int variableLossAmount)
        {
             // This method assumes the caller knows this item uses VariableHealthReduction logic
             // and provides a valid loss amount.
             // The UseItem method will still validate the trigger type (Crafting, etc.)
             // and the item's usageLogic.
             return UseItem(itemToUse, userInventory, selectedIndex, UsageTriggerType.Crafting, variableLossAmount); // Assuming Crafting uses this
        }

        // Example public method for Delayed usage (e.g., melee hit)
         public bool UseItemDelayedLoss(Item itemToUse, Inventory userInventory, int selectedIndex)
         {
             // This method assumes the caller knows this item uses DelayedHealthReduction logic.
             // The UseItem method will still validate the trigger type (LeftClick, WorldInteraction, etc.)
             // and the item's usageLogic.
             return UseItem(itemToUse, userInventory, selectedIndex, UsageTriggerType.LeftClick); // Assuming LeftClick triggers melee
         }
    }
}