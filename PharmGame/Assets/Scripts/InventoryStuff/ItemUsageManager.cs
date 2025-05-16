using UnityEngine;
using Systems.Inventory;
using System.Collections.Generic; // Needed for List
using System; // Needed for Action

namespace Systems.Inventory
{
    /// <summary>
    /// Manages the logic for using items, typically from the player's selected inventory slot.
    /// Handles use input and delegates effect execution based on item type.
    /// </summary>
    public class ItemUsageManager : MonoBehaviour
    {
        // Singleton instance
        public static ItemUsageManager Instance { get; private set; }

        [Tooltip("Tag identifying the player's toolbar Inventory GameObject.")]
        [SerializeField] private string playerInventoryTag = "PlayerToolbarInventory"; // Ensure this matches the tag you set

        private InventorySelector playerInventorySelector; // Reference to the player's selector script

        private void Awake()
        {
            // Implement singleton pattern
            if (Instance == null)
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
                     Debug.LogError($"ItemUsageManager: GameObject with tag '{playerInventoryTag}' found, but it does not have an InventorySelector component.", playerInventoryObject);
                 }
             }
             else
             {
                 Debug.LogError($"ItemUsageManager: No GameObject found with tag '{playerInventoryTag}'. Player item usage will not work.", this);
             }

             // Optional: Subscribe to MenuManager state changes if you only want usage in specific states
             // MenuManager.OnStateChanged += HandleGameStateChanged; // Example
        }

         /* // Example if you need state-based usage
         private void HandleGameStateChanged(MenuManager.GameState newState)
         {
             // Enable/disable usage logic based on state
         }
         */


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
                // Check for the "use item" input button press
                if (Input.GetKeyDown(KeyCode.F)) // Using Input.GetButtonDown for configurable input
                {
                     Debug.Log($"ItemUsageManager: input detected. Attempting to use selected item.");
                    // Tell the player inventory selector to attempt to use its selected item
                    playerInventorySelector.UseSelectedItem(); // This calls back to ItemUsageManager.UseItem
                }
            }
            // Optional: Add other states where item usage is allowed
        }

        // --- Item Usage Logic (Called by InventorySelector) ---

        /// <summary>
        /// Executes the usage logic for a specific item instance from a specific slot.
        /// This method is called by the InventorySelector.
        /// </summary>
        /// <param name="itemToUse">The Item instance to use (should be the one from the selected slot).</param>
        /// <param name="userInventory">The Inventory component the item belongs to (should be the player's inventory).</param>
        /// <param name="selectedIndex">The index of the slot the item was selected from.</param> // ADDED PARAMETER
        /// <returns>True if the item usage logic was successfully triggered, false otherwise.</returns>
        public bool UseItem(Item itemToUse, Inventory userInventory, int selectedIndex) // ADDED PARAMETER
        {
            if (itemToUse == null || itemToUse.details == null || userInventory == null || userInventory.InventoryState == null || userInventory.Combiner == null) // Added check for InventoryState
            {
                Debug.LogWarning("ItemUsageManager: UseItem called with invalid parameters.", this);
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


            Debug.Log($"ItemUsageManager: Executing usage logic for item '{itemToUse.details.Name}' (Instance ID: {itemToUse.Id}) from slot {selectedIndex}.", this);

            bool usageHandled = false;

            // --- Implement Item Effect Logic Here ---
            // Based on itemToUse.details, perform the effect.

            // Example: Consumable Logic (assuming any item used is a consumable)
            // You would replace this with more specific logic based on item type/properties
            Debug.Log($"ItemUsageManager: Assuming '{itemToUse.details.Name}' is a consumable. Reducing quantity by 1 in slot {selectedIndex}.");

            itemToUse.quantity--; // Reduce the quantity of the specific item instance

            if (itemToUse.quantity <= 0)
            {
                // If quantity is zero or less, remove the item instance from the inventory
                Debug.Log($"ItemUsageManager: Item quantity depleted in slot {selectedIndex}. Removing item instance.");
                // Use the ObservableArray's method to remove the item at the specific index
                userInventory.InventoryState.RemoveAt(selectedIndex); // Use RemoveAt with the index
                usageHandled = true; // Item was used and removed
            }
            else
            {
                // If quantity is still positive, notify the ObservableArray that the item at this index changed (quantity updated)
                Debug.Log($"ItemUsageManager: Item quantity updated in slot {selectedIndex}. New quantity: {itemToUse.quantity}.");
                // Use SetItemAtIndex to update the item at the specific index.
                // Pass the SAME item instance, but its internal quantity has been modified.
                userInventory.InventoryState.SetItemAtIndex(itemToUse, selectedIndex); // Triggers SlotUpdated for this index
                usageHandled = true; // Item was used and quantity updated
            }


            // TODO: Add more sophisticated logic for different item types (Equip, Throw, Placeable, etc.)
            // Equippables might call methods to equip/unequip and change state, not remove.
            // Other items might spawn prefabs, trigger animations, etc.


            if (!usageHandled)
            {
                 Debug.LogWarning($"ItemUsageManager: No specific usage logic found or handled for item: {itemToUse.details.Name}.", this);
                 // If no specific usage was handled, decide what to do.
                 // For consumables, the logic above already handles removal.
                 // For other unhandled types, maybe do nothing or log a different message.
            }


            return usageHandled; // Return true if usage was handled (item quantity reduced/removed)
        }

        // TODO: Add other usage-related methods (e.g., EquipItem, DropItem - though Drop might be D&D related)
    }
}