// --- START OF FILE ItemTransferHandler.cs ---

using UnityEngine;
using Systems.Inventory; // Needed for Inventory, Item, ItemDetails, Combiner, ObservableArray, InventorySlotUI, CraftingStation
using Systems.GameStates; // Needed for MenuManager
using Systems.UI; // Needed for PlayerUIPopups
using System.Collections.Generic; // Needed for List (if used later)
using System; // Needed for Action (if events are added later)
using System.Linq; // Needed for Linq methods like FirstOrDefault

namespace Systems.Inventory // Or a more specific namespace if preferred, but Inventory seems appropriate
{
    /// <summary>
    /// Handles the logic for quick item transfers between open inventories (Shift + Click).
    /// Manages finding source/target inventories, filtering, and executing the transfer,
    /// including handling partial transfers and returning leftovers.
    /// </summary>
    public class ItemTransferHandler : MonoBehaviour
    {
        // Singleton instance
        public static ItemTransferHandler Instance { get; private set; }

        private void Awake()
        {
            // Implement singleton pattern
            if (Instance == null)
            {
                Instance = this;
                // Optional: DontDestroyOnLoad(gameObject); // If the manager should persist between scenes
            }
            else
            {
                Debug.LogWarning("ItemTransferHandler: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            Debug.Log("ItemTransferHandler: Initialized.", this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Attempts to perform a quick transfer of an item from the clicked source slot
        /// to another open inventory.
        /// </summary>
        /// <param name="sourceSlotUI">The InventorySlotUI that was Shift + Left Clicked.</param>
        public void AttemptQuickTransfer(InventorySlotUI sourceSlotUI)
        {
            Debug.Log($"ItemTransferHandler: AttemptQuickTransfer called from slot {sourceSlotUI?.SlotIndex ?? -1} in inventory {sourceSlotUI?.ParentInventory?.Id ?? SerializableGuid.Empty}.", this);

            // --- Get Source Information ---
            if (sourceSlotUI == null || sourceSlotUI.ParentInventory == null || sourceSlotUI.ParentInventory.Combiner == null || sourceSlotUI.ParentInventory.InventoryState == null) // Added InventoryState null check
            {
                Debug.LogError("ItemTransferHandler: AttemptQuickTransfer called with invalid sourceSlotUI, parent inventory, combiner, or state.", this);
                PlayerUIPopups.Instance?.ShowPopup("Transfer Failed", "System error: Invalid source.");
                return;
            }

            Inventory sourceInventory = sourceSlotUI.ParentInventory;
            Combiner sourceCombiner = sourceInventory.Combiner;
            ObservableArray<Item> sourceObservableArray = sourceInventory.InventoryState; // Get OA reference
            int sourceSlotIndex = sourceSlotUI.SlotIndex;

            // Ensure the index is within the physical slot range of the source inventory
            if (sourceSlotIndex < 0 || sourceSlotIndex >= sourceCombiner.PhysicalSlotCount)
            {
                 Debug.LogError($"ItemTransferHandler: AttemptQuickTransfer called with invalid source physical slot index ({sourceSlotIndex}) for physical slots ({sourceCombiner.PhysicalSlotCount}).", sourceSlotUI.gameObject);
                 PlayerUIPopups.Instance?.ShowPopup("Transfer Failed", "System error: Invalid source slot index.");
                 return;
            }

            // Retrieve the item instance directly from the source slot *before* any removal attempts
            Item itemToTransfer = sourceObservableArray[sourceSlotIndex];

            if (itemToTransfer == null || itemToTransfer.details == null)
            {
                Debug.Log($"ItemTransferHandler: AttemptQuickTransfer called on empty or detail-less slot {sourceSlotIndex}. No item to transfer.", sourceSlotUI.gameObject);
                // No popup needed for clicking empty slot
                return;
            }

            // --- Prevent quick transfer during drag ---
            if (DragAndDropManager.Instance != null && DragAndDropManager.Instance.IsDragging)
            {
                 Debug.Log("ItemTransferHandler: Cannot quick transfer while a drag operation is active.", this);
                 PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "Cannot quick transfer while dragging.");
                 return;
            }

             // --- Capture original state before potential modifications ---
             // Store original quantity for stackable items to calculate remainder later
             int originalQuantity = itemToTransfer.quantity;
             string itemName = itemToTransfer.details.Name; // Cache name for logs/popups
             bool isStackable = itemToTransfer.details.maxStack > 1;

            // --- Retrieve Target Inventory ---
            Inventory targetInventory = null;
            MenuManager menuManager = MenuManager.Instance;

            if (menuManager == null)
            {
                 Debug.LogError("ItemTransferHandler: MenuManager Instance is null! Cannot determine target inventory for quick transfer.", this);
                 PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "System error: Menu manager not found.");
                 return;
            }

            Inventory playerInventory = menuManager.PlayerToolbarInventorySelector?.parentInventory;

            // Determine the other open inventory based on the current game state
            switch (menuManager.currentState)
            {
                case MenuManager.GameState.InInventory:
                    // In this state, the player toolbar and one other modal inventory are open.
                    Inventory modalInventory = menuManager.CurrentOpenInventoryComponent;
                    if (modalInventory == null)
                    {
                         Debug.LogWarning("ItemTransferHandler: Quick transfer attempted in InInventory state, but CurrentOpenInventoryComponent is null.", this);
                         PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "No target inventory open.");
                         return;
                    }

                    // The target is the *other* inventory that is open
                    if (sourceInventory == playerInventory)
                    {
                        targetInventory = modalInventory;
                    }
                    else if (sourceInventory == modalInventory)
                    {
                        targetInventory = playerInventory;
                    }
                    break;

                case MenuManager.GameState.InCrafting:
                    // In this state, the player toolbar and crafting station inventories are open.
                    CraftingStation craftingStation = menuManager.CurrentCraftingStation;
                    if (craftingStation == null)
                    {
                        Debug.LogWarning("ItemTransferHandler: Quick transfer attempted in InCrafting state, but CurrentCraftingStation is null.", this);
                        PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "Crafting station not active.");
                        return;
                    }

                    // Get references to crafting station inventories, checking for nulls
                    Inventory craftingInputInventory = craftingStation.primaryInputInventory;
                    Inventory craftingSecondaryInputInventory = craftingStation.secondaryInputInventory;
                    Inventory craftingOutputInventory = craftingStation.outputInventory;

                    // Determine target based on source
                    if (sourceInventory == playerInventory)
                    {
                        // Transfer from player to crafting inputs.
                        // Find the FIRST input inventory that *allows* the item.
                        Debug.Log($"ItemTransferHandler: Source is Player Inventory. Searching for compatible input inventory on crafting station '{craftingStation.gameObject.name}'.", craftingStation.gameObject);

                        if (craftingInputInventory != null && craftingInputInventory.CanAddItem(itemToTransfer))
                        {
                            targetInventory = craftingInputInventory;
                            Debug.Log($"ItemTransferHandler: Primary input inventory '{craftingInputInventory.Id}' allows item. Setting as target.", craftingInputInventory.gameObject);
                        }
                        else if (craftingSecondaryInputInventory != null && craftingSecondaryInputInventory.CanAddItem(itemToTransfer))
                        {
                            targetInventory = craftingSecondaryInputInventory;
                            Debug.Log($"ItemTransferHandler: Primary input does not allow or is null. Secondary input inventory '{craftingSecondaryInputInventory.Id}' allows item. Setting as target.", craftingSecondaryInputInventory.gameObject);
                        }
                        else
                        {
                             // Neither input inventory allows the item or they don't exist/are null.
                             // targetInventory remains null. Handled below.
                             Debug.LogWarning($"ItemTransferHandler: Quick transfer from player to crafting attempted. Item '{itemName}' is not allowed in primary input ({craftingInputInventory != null}) or secondary input ({craftingSecondaryInputInventory != null}) on station '{craftingStation.gameObject.name}'.", craftingStation.gameObject);
                        }
                    }
                    // Handle transfer *from* any crafting station inventory *to* the player inventory
                    else if (sourceInventory == craftingInputInventory ||
                             sourceInventory == craftingSecondaryInputInventory ||
                             sourceInventory == craftingOutputInventory)
                    {
                        targetInventory = playerInventory;
                        Debug.Log($"ItemTransferHandler: Source is a Crafting Inventory. Setting Player Inventory '{playerInventory.Id}' as target.", playerInventory.gameObject);
                    }
                    break;

                // Add cases for other states where two inventories might be open (e.g., trading)
                // case MenuManager.GameState.InTrading: ... break;

                default:
                    // If not in a state where two inventories are expected to be open, quick transfer is not possible.
                    Debug.Log($"ItemTransferHandler: Quick transfer attempted in state {menuManager.currentState}. Requires a state with two open inventories.", this);
                    PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "Cannot quick transfer in this state.");
                    return;
            }

            // Final check: Ensure a valid target inventory was found and it's not the source inventory
            if (targetInventory == null || targetInventory == sourceInventory || targetInventory.Combiner == null || targetInventory.InventoryState == null) // Added InventoryState null check
            {
                Debug.Log($"ItemTransferHandler: Quick transfer failed. Could not find a valid target inventory different from the source, or target is missing Combiner/State.", this);
                PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "No valid target inventory found.");
                return;
            }

            Combiner targetCombiner = targetInventory.Combiner;
            ObservableArray<Item> targetObservableArray = targetInventory.InventoryState; // Get target OA

            // --- Check if the target inventory allows this item type ---
             // We do this check BEFORE removing from source, so the item stays put if disallowed.
            if (!targetInventory.CanAddItem(itemToTransfer))
            {
                Debug.Log($"DragAndDropManager: Item '{itemName}' is not allowed in target inventory '{targetInventory.Id}' due to filtering. Quick transfer aborted.");
                PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "This item cannot go in that inventory.");
                return; // Exit without modifying source inventory
            }


            // --- Perform Transfer Logic based on Item Type ---

            bool transferAddedToTarget = false; // Did *any* quantity/instance get added to the target?
            bool returnAttempted = false;
            bool returnSuccessful = false;
            int quantityAddedToTarget = 0; // Track quantity added for stackable
            bool instanceAddedToTarget = false; // Track instance added for non-stackable


            // --- Remove item from source slot FIRST ---
            // For quick transfer, it's cleaner to remove the item from its original spot
            // and then try to add it to the target. If it doesn't fit anywhere, it's returned.
            bool removedFromSource = sourceCombiner.TryRemoveAt(sourceSlotIndex);

            if (!removedFromSource)
            {
                 Debug.LogError($"ItemTransferHandler: Failed to remove item '{itemName}' from source slot {sourceSlotIndex} during quick transfer initial removal. Aborting transfer.", this);
                 PlayerUIPopups.Instance?.ShowPopup("Transfer Failed", "Could not remove item from source slot.");
                 return; // Exit if removal failed
            }
             // itemToTransfer instance is now only held by the local variable


            // --- Attempt to add the item to the target inventory based on specific quick transfer rules ---
            if (isStackable) // Stackable Item Quick Transfer Logic
            {
                Debug.Log($"ItemTransferHandler: Attempting quick transfer of stackable item '{itemName}' (Initial Qty: {itemToTransfer.quantity}) to target '{targetInventory.Id}' with specific rules.");

                int targetStackSlotIndex = -1;
                int targetEmptySlotIndex = -1;

                // 1. Find the first non-full stack of the same type in the target physical slots
                for (int i = 0; i < targetCombiner.PhysicalSlotCount; i++)
                {
                    Item itemInTargetSlot = targetObservableArray[i];
                    if (itemInTargetSlot != null && itemInTargetSlot.CanStackWith(itemToTransfer) && itemInTargetSlot.quantity < itemInTargetSlot.details.maxStack)
                    {
                        targetStackSlotIndex = i; // Found the first non-full stack target
                        break; // Prioritize stacking, stop searching
                    }
                }

                // 2. If no non-full stack was found, find the first empty physical slot
                if (targetStackSlotIndex == -1)
                {
                     for (int i = 0; i < targetCombiner.PhysicalSlotCount; i++)
                     {
                         if (targetObservableArray[i] == null)
                         {
                             targetEmptySlotIndex = i; // Found the first empty target
                             break; // Stop searching for empty slots
                         }
                     }
                }

                // --- Execute the transfer based on the found target slot ---
                if (targetStackSlotIndex != -1)
                {
                    // Target is a non-full stack: Attempt to stack onto it.
                    Debug.Log($"ItemTransferHandler: Found non-full stack at target slot {targetStackSlotIndex}. Attempting to stack.");
                    // TryStackQuantityToSpecificSlot will add as much as fits and update itemToTransfer.quantity with the remainder.
                    quantityAddedToTarget = targetCombiner.TryStackQuantityToSpecificSlot(itemToTransfer, targetStackSlotIndex);
                    if (quantityAddedToTarget > 0)
                    {
                        transferAddedToTarget = true; // Some quantity was added
                    }
                    Debug.Log($"ItemTransferHandler: Stacked {quantityAddedToTarget}. Remaining on instance: {itemToTransfer.quantity}.");

                }
                else if (targetEmptySlotIndex != -1)
                {
                    // Target is the first empty slot: Attempt to add the entire stackable item instance.
                    Debug.Log($"ItemTransferHandler: No non-full stack found. Found first empty slot at {targetEmptySlotIndex}. Attempting to add item instance.");
                    // AddStackableItems will add the item (splitting if needed) and update itemToTransfer.quantity with the remainder.
                    // We pass the current itemToTransfer instance, which holds the quantity removed from source.
                    bool addedEntirelyOrPartially = targetInventory.AddItem(itemToTransfer); // AddItem calls AddStackableItems internally

                    if (addedEntirelyOrPartially) // AddItem returns true if *all* quantity was added
                    {
                         // AddItem returns true only if itemToTransfer.quantity became 0.
                         // So if it returns true, the whole stack was added.
                         quantityAddedToTarget = originalQuantity; // Whole original quantity added
                         transferAddedToTarget = true; // Entire quantity added
                         Debug.Log($"ItemTransferHandler: Added entire stackable item to empty slot {targetEmptySlotIndex}. Remaining on instance: {itemToTransfer.quantity}.");
                    }
                    else // AddItem returned false, meaning some quantity remains on itemToTransfer
                    {
                         // AddItem failed to add the *entire* original quantity.
                         // The quantityAddedToTarget is not directly returned by AddItem.
                         // We know how much was added by comparing originalQuantity to itemToTransfer.quantity.
                         quantityAddedToTarget = originalQuantity - itemToTransfer.quantity;
                         if (quantityAddedToTarget > 0)
                         {
                             transferAddedToTarget = true; // Some quantity was added (partial add)
                             Debug.Log($"ItemTransferHandler: Partially added stackable item to empty slot {targetEmptySlotIndex}. Added {quantityAddedToTarget}, {itemToTransfer.quantity} remaining on instance.");
                         }
                         else
                         {
                              // Should not happen if AddItem returned false and quantity > 0 initially, but defensive.
                              Debug.LogWarning($"ItemTransferHandler: AddItem returned false for stackable, but quantity added was 0.");
                         }
                    }
                }
                else
                {
                    // No suitable target slot found in the target inventory.
                    Debug.Log($"ItemTransferHandler: No non-full stack or empty slot found for stackable item '{itemName}' in target inventory '{targetInventory.Id}'. Item will be returned to source.");
                    // itemToTransfer.quantity remains its original quantity.
                    transferAddedToTarget = false; // Nothing added to target
                    quantityAddedToTarget = 0;
                }

            }
            else // Non-Stackable Item Quick Transfer Logic (remains unchanged)
            {
                 Debug.Log($"ItemTransferHandler: Attempting quick transfer of non-stackable item '{itemName}' (ID: {itemToTransfer.Id}) to target '{targetInventory.Id}'.");

                 // Use AddSingleInstance on the target. It returns true if added, false if target is full/filtered.
                 // AddSingleInstance does NOT modify itemToTransfer.quantity.
                 instanceAddedToTarget = targetCombiner.AddSingleInstance(itemToTransfer); // Use Combiner directly for instance placement

                 if (instanceAddedToTarget)
                 {
                      // If successfully added, the instance is now in the target inventory's data.
                      // We need to signal that this instance is "fully transferred" from the perspective
                      // of the quick transfer operation, so it doesn't get "returned" below.
                      // Setting quantity to 0 serves this purpose for the return logic.
                      itemToTransfer.quantity = 0; // Indicate instance was transferred
                      transferAddedToTarget = true; // An instance was added
                      Debug.Log($"ItemTransferHandler: Non-stackable item '{itemName}' successfully added to target.");
                 }
                 else
                 {
                      // If not added, itemToTransfer.quantity remains 1 (as it was originally),
                      // correctly indicating the instance still needs to be placed/returned.
                      Debug.Log($"ItemTransferHandler: Non-stackable item '{itemName}' could not be added to target (target full or filter mismatch).");
                      transferAddedToTarget = false; // No instance added
                 }
            }


            // --- Handle Item Return to Source (If Not Fully Added/Transferred) ---
            // itemToTransfer.quantity now reflects the remainder that couldn't be added to the target.
            // For stackable, it's > 0 if partially added or failed to add.
            // For non-stackable, it's 1 if failed to add, 0 if successfully added.

            if (itemToTransfer.quantity > 0) // Check if any quantity/instance remains to be returned
            {
                returnAttempted = true;
                Debug.Log($"ItemTransferHandler: Item was partially transferred or failed to transfer to target. Remaining quantity ({itemToTransfer.quantity}) of '{itemName}' needs to be returned to source inventory '{sourceInventory.Id}'.");

                // Attempt to add the remaining quantity/instance back into the source inventory.
                // Use the general AddItem method on the source inventory.
                // AddItem handles finding space and updating itemToTransfer.quantity (sets to 0 if fully returned).
                returnSuccessful = sourceInventory.AddItem(itemToTransfer); // AddItem handles stackable/non-stackable

                if (returnSuccessful)
                {
                    Debug.Log($"ItemTransferHandler: Remaining quantity successfully returned to source using AddItem. Remaining quantity on instance: {itemToTransfer.quantity}.");
                }
                else
                {
                    // This means the source inventory was also full or couldn't accept the remaining quantity/instance.
                    Debug.LogError($"ItemTransferHandler: Failed to return remaining quantity ({itemToTransfer.quantity}) of '{itemName}' to source inventory '{sourceInventory.Id}'. Source inventory is likely full. Item is LOST!", this);
                    PlayerUIPopups.Instance?.ShowPopup("Return Failed", "Could not return item to source!");
                    // The item instance with the remaining quantity is effectively lost from the UI/data.
                }
            }
            else // itemToTransfer.quantity is <= 0. This means the entire original quantity/instance was successfully added/transferred to the target.
            {
                Debug.Log($"ItemTransferHandler: Entire quantity/instance of '{itemName}' successfully transferred to target.");
                // No return necessary. The item instance is now fully managed by the target inventory's data.
            }


            // --- UI Feedback (Popups) ---

            if (!transferAddedToTarget) // If *any* quantity/instance made it to the target
            {
                 // This happens if the target couldn't accept it (due to full space *anywhere* in target, or filter check passed but no slot found).
                 // The item was already removed from source, and the logic above attempted to return it.
                 // If returnAttempted is true and returnSuccessful is false, the "Return Failed" popup was shown.
                 // If returnAttempted is true and returnSuccessful is true, it was fully returned, so the transfer failed but item wasn't lost.
                 // If returnAttempted is false, it means itemToTransfer.quantity was 0 initially (shouldn't happen with fixes) or became 0 unexpectedly.
                 // A general "Cannot Transfer" popup is appropriate here if no other popup was shown.

                 if (!returnAttempted || !returnSuccessful) // If no return was attempted OR return failed
                 {
                     // This covers the case where target add failed and return either wasn't needed (unexpected state) or failed.
                      PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", $"Could not transfer '{itemName}' (target full or invalid).");
                      Debug.Log($"ItemTransferHandler: No quantity/instance of '{itemName}' could be added to target.");
                 }
                 // else: target add failed, but it was fully returned. No extra popup needed besides the return success log.
            }
        }
    }
}