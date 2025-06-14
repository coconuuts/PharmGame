// --- START OF FILE ItemTransferHandler.cs ---

using UnityEngine;
using Systems.Inventory; // Needed for Inventory, Item, ItemDetails, Combiner, ObservableArray, InventorySlotUI, CraftingStation
using Systems.GameStates; // Needed for MenuManager
using Systems.UI; // Needed for PlayerUIPopups
using System.Collections.Generic; // Needed for List (if used later)
using System; // Needed for Action (if events are added later)

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
            if (sourceSlotUI == null || sourceSlotUI.ParentInventory == null)
            {
                Debug.LogError("ItemTransferHandler: AttemptQuickTransfer called with invalid sourceSlotUI or parent inventory.", this);
                return;
            }

            Inventory sourceInventory = sourceSlotUI.ParentInventory;
            int sourceSlotIndex = sourceSlotUI.SlotIndex;

            // Ensure the index is within the physical slot range of the source inventory
            if (sourceInventory.Combiner == null || sourceSlotIndex < 0 || sourceSlotIndex >= sourceInventory.Combiner.PhysicalSlotCount)
            {
                 Debug.LogError($"ItemTransferHandler: AttemptQuickTransfer called with invalid source slot index ({sourceSlotIndex}) for physical slots ({sourceInventory.Combiner.PhysicalSlotCount}).", sourceSlotUI.gameObject);
                 return;
            }

            ObservableArray<Item> sourceObservableArray = sourceInventory.InventoryState;

            if (sourceObservableArray == null)
            {
                Debug.LogError($"ItemTransferHandler: Source inventory {sourceInventory.Id} has a null ObservableArray.", sourceInventory.gameObject);
                return;
            }

            Item itemToTransfer = sourceObservableArray[sourceSlotIndex];

            if (itemToTransfer == null)
            {
                Debug.Log($"ItemTransferHandler: AttemptQuickTransfer called on empty slot {sourceSlotIndex}. No item to transfer.", sourceSlotUI.gameObject);
                return; 
            }

            // --- Prevent quick transfer during drag ---
            if (DragAndDropManager.Instance != null && DragAndDropManager.Instance.IsDragging)
            {
                 Debug.Log("ItemTransferHandler: Cannot quick transfer while a drag operation is active.", this);
                 PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "Cannot quick transfer while dragging another item.");
                 return;
            }

            Debug.Log($"ItemTransferHandler: Initiating quick transfer for '{itemToTransfer.details?.Name ?? "Unknown"}' (Qty: {itemToTransfer.quantity}) from source slot {sourceSlotIndex} in inventory '{sourceInventory.Id}'.", this);


            // --- Identify Target Inventory with Robust Null Checks and Crafting Input Logic ---
            Inventory targetInventory = null;
            MenuManager menuManager = MenuManager.Instance;

            if (menuManager == null)
            {
                 Debug.LogError("ItemTransferHandler: MenuManager Instance is null! Cannot determine target inventory for quick transfer.", this);
                 PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "System error: Menu manager not found."); // Added user feedback
                 return;
            }

            Inventory playerInventory = menuManager.PlayerToolbarInventorySelector?.parentInventory;
            // Check if player inventory is even available (it should be if any other inventory is open, but defensive check)
            if (playerInventory == null)
            {
                 Debug.LogError("ItemTransferHandler: Player Toolbar Inventory is null! Cannot perform quick transfer.", this);
                 PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "System error: Player inventory not found."); // Added user feedback
                 return;
            }

            // --- Determine the other open inventory based on the current game state ---
            switch (menuManager.currentState)
            {
                case MenuManager.GameState.InInventory:
                    // In this state, the player toolbar and one other modal inventory are open.
                    Inventory modalInventory = menuManager.CurrentOpenInventoryComponent;
                    if (modalInventory == null)
                    {
                         Debug.LogWarning("ItemTransferHandler: Quick transfer attempted in InInventory state, but CurrentOpenInventoryComponent is null.", this);
                         PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "No target inventory open."); // Added user feedback
                         return; // Exit if the modal inventory is missing
                    }

                    if (sourceInventory == playerInventory)
                    {
                        targetInventory = modalInventory;
                    }
                    else if (sourceInventory == modalInventory)
                    {
                        targetInventory = playerInventory;
                    }
                    // If sourceInventory is neither player nor modal, something is wrong, handled by the final check below.
                    break;

                case MenuManager.GameState.InCrafting:
                    // In this state, the player toolbar and crafting station inventories are open.
                    CraftingStation craftingStation = menuManager.CurrentCraftingStation;
                    if (craftingStation == null)
                    {
                        Debug.LogWarning("ItemTransferHandler: Quick transfer attempted in InCrafting state, but CurrentCraftingStation is null.", this);
                        PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "Crafting station not active."); // Added user feedback
                        return; // Exit if the crafting station is missing
                    }

                    // Get references to crafting station inventories, checking for nulls
                    Inventory craftingInputInventory = craftingStation.primaryInputInventory;
                    Inventory craftingSecondaryInputInventory = craftingStation.secondaryInputInventory;
                    Inventory craftingOutputInventory = craftingStation.outputInventory;

                    // --- Determine target based on source ---
                    if (sourceInventory == playerInventory)
                    {
                        // Transfer from player to crafting.
                        // We need to find the FIRST input inventory that *allows* the item.
                        Debug.Log($"ItemTransferHandler: Source is Player Inventory. Searching for compatible input inventory on crafting station '{craftingStation.gameObject.name}'.", craftingStation.gameObject);

                        if (craftingInputInventory != null && craftingInputInventory.CanAddItem(itemToTransfer))
                        {
                            // Primary input exists and allows the item
                            targetInventory = craftingInputInventory;
                            Debug.Log($"ItemTransferHandler: Primary input inventory '{craftingInputInventory.Id}' allows item. Setting as target.", craftingInputInventory.gameObject);
                        }
                        else if (craftingSecondaryInputInventory != null && craftingSecondaryInputInventory.CanAddItem(itemToTransfer))
                        {
                            // Primary did not allow or doesn't exist, check secondary input
                            targetInventory = craftingSecondaryInputInventory;
                            Debug.Log($"ItemTransferHandler: Primary input does not allow or is null. Secondary input inventory '{craftingSecondaryInputInventory.Id}' allows item. Setting as target.", craftingSecondaryInputInventory.gameObject);
                        }
                        else
                        {
                             // Neither input inventory allows the item or they don't exist/are null.
                             // targetInventory remains null. The general failure check below will handle this.
                             Debug.LogWarning($"ItemTransferHandler: Quick transfer from player to crafting attempted. Item '{itemToTransfer.details?.Name ?? "Unknown"}' is not allowed in primary input ({craftingInputInventory != null}) or secondary input ({craftingSecondaryInputInventory != null}) on station '{craftingStation.gameObject.name}'.", craftingStation.gameObject);
                             // A popup will be shown by the final check if targetInventory is still null.
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
                    // If sourceInventory is none of the above, something is wrong, handled by the final check below.
                    break;

                // Add cases for other states where two inventories might be open (e.g., trading)
                // case MenuManager.GameState.InTrading: ... break;

                default:
                    // If not in a state where two inventories are expected to be open, quick transfer is not possible.
                    Debug.Log($"ItemTransferHandler: Quick transfer attempted in state {menuManager.currentState}. Requires a state with two open inventories.", this);
                    PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "Cannot quick transfer in this state."); // Added popup feedback
                    return; // Exit if not in a valid state
            }

            // Final check: Ensure a valid target inventory was found and it's not the source inventory
            // Note: The crafting input logic above handles the case where the item is not allowed
            // in *any* input inventory by leaving targetInventory as null.
            if (targetInventory == null || targetInventory == sourceInventory)
            {
                // This should ideally be caught by the specific state checks above,
                // but this serves as a final safeguard.
                Debug.Log($"ItemTransferHandler: Quick transfer failed. Could not find a valid target inventory different from the source.", this);
                PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "No valid target inventory found."); // Added popup feedback
                return; // Exit if target is invalid
            }

            // Check if the target inventory has a Combiner (it should, but defensive)
            if (targetInventory.Combiner == null)
            {
                 Debug.LogError($"ItemTransferHandler: Target inventory '{targetInventory.Id}' ({targetInventory.gameObject.name}) is missing a Combiner component! Cannot transfer.", targetInventory.gameObject);
                 PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "System error: Target inventory misconfigured."); // Added popup feedback
                 return; // Exit if target Combiner is missing
            }

             // The targetSlotIndex for the ADD operation is relevant if we are attempting to stack onto it.
             // If we are just adding generally, TryAddQuantity handles finding a slot.
             // Use the sourceSlotIndex as the *intended* target index for the click point
             // (representing the slot UI that was clicked).
             int intendedTargetSlotIndex = sourceSlotIndex;

             int intendedTargetSlotIndexForSpecificStacking = sourceSlotIndex; // Use source index as the potential target index for the special case

             // Ensure this intended index is valid for the TARGET inventory's physical slots BEFORE using it for the specific stacking check.
             bool isIntendedTargetIndexValidForTarget = (intendedTargetSlotIndexForSpecificStacking >= 0 && intendedTargetSlotIndexForSpecificStacking < targetInventory.Combiner.PhysicalSlotCount);


            Debug.Log($"ItemTransferHandler: Target inventory identified: '{targetInventory.Id}' ({targetInventory.gameObject.name}). Intended target slot index (based on source click): {intendedTargetSlotIndexForSpecificStacking}. Is valid for target: {isIntendedTargetIndexValidForTarget}.", this);

            // Store original quantity before attempting to add to target
            int originalQuantity = itemToTransfer.quantity;
            int quantityAddedToTarget = 0; // Initialize quantity added

            // Get the item currently in the *intended* target slot in the target inventory
            // Only do this if the intended index is valid for the target inventory
            Item targetItemInSlot = isIntendedTargetIndexValidForTarget ? targetInventory.InventoryState[intendedTargetSlotIndexForSpecificStacking] : null;


            // Determine if we should attempt specific stacking *only* into the clicked slot.
            // This is true if the intended target index is valid, AND the target slot has an item,
            // AND it's the same stackable type as the itemToTransfer, AND it's NOT full.
            bool attemptingToStackOntoExistingNonFull = (isIntendedTargetIndexValidForTarget &&
                                                         targetItemInSlot != null &&
                                                         itemToTransfer.details != null &&
                                                         itemToTransfer.details.maxStack > 1 && // Ensure source is stackable
                                                         targetItemInSlot.CanStackWith(itemToTransfer) && // Ensure target is same type & stackable
                                                         targetItemInSlot.quantity < targetItemInSlot.details.maxStack); // Ensure target is NOT full

            if (attemptingToStackOntoExistingNonFull)
            {
                 Debug.Log($"ItemTransferHandler: Attempting quick transfer onto existing, non-full stack at target slot {intendedTargetSlotIndexForSpecificStacking}. Using TryStackQuantityToSpecificSlot.");

                 // 1. Remove the item from the source slot.
                 // This clears the source slot visually and in data.
                 // The item instance itself is now only held by the 'itemToTransfer' local variable.
                 bool removedFromSource = sourceInventory.Combiner.TryRemoveAt(sourceSlotIndex);

                 if (!removedFromSource)
                 {
                      Debug.LogError($"ItemTransferHandler: Failed to remove item from source slot {sourceSlotIndex} during quick transfer onto stack. Aborting transfer.", this);
                      PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "Could not remove item from source slot.");
                      return; // Exit if removal failed
                 }

                 // 2. Attempt to add the item *only* to the specific target slot stack.
                 // This method will add as much as possible to targetSlotIndex and update itemToTransfer.quantity with the remainder.
                 quantityAddedToTarget = targetInventory.Combiner.TryStackQuantityToSpecificSlot(itemToTransfer, intendedTargetSlotIndexForSpecificStacking); 

                 Debug.Log($"ItemTransferHandler: TryStackQuantityToSpecificSlot to target returned {quantityAddedToTarget}. ItemToTransfer quantity AFTER TryStackQuantityToSpecificSlot: {itemToTransfer.quantity}. Original quantity: {originalQuantity}.");

            }
            else
            {
                 // This branch is taken if:
                 // - The intended target index is invalid for the target inventory.
                 // - The target slot is empty.
                 // - The target slot has a non-stackable item.
                 // - The target slot has a different stackable item type.
                 // - The target slot has a *full* stack of the same item type.
                 Debug.Log($"ItemTransferHandler: Attempting general quick transfer (onto empty slot, non-stackable/different item, full stack, or invalid intended index). Using TryAddQuantity.");

                 // 1. Remove the item from the source slot.
                 bool removedFromSource = sourceInventory.Combiner.TryRemoveAt(sourceSlotIndex);

                 if (!removedFromSource)
                 {
                      Debug.LogError($"ItemTransferHandler: Failed to remove item from source slot {sourceSlotIndex} during general quick transfer. Aborting transfer.", this);
                      PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "Could not remove item from source slot.");
                      return; // Exit if removal failed
                 }

                 // 2. Attempt to add the item to the target inventory using the general TryAddQuantity method.
                 // This method will add as much as possible anywhere in the target (prioritizing existing stacks, then empty slots)
                 // and update itemToTransfer.quantity with the remainder.
                 quantityAddedToTarget = targetInventory.Combiner.TryAddQuantity(itemToTransfer);

                 Debug.Log($"ItemTransferHandler: TryAddQuantity to target returned {quantityAddedToTarget}. ItemToTransfer quantity AFTER TryAddQuantity: {itemToTransfer.quantity}. Original quantity: {originalQuantity}.");
            }


            // --- Implement Return to Source Logic ---
            // This logic remains the same, handling any quantity left on itemToTransfer after the target add attempt.
            bool returnedToSource = false;
            // Check if any quantity remains on the item instance after attempting to add to the target
            if (itemToTransfer.quantity > 0)
            {
                Debug.Log($"ItemTransferHandler: Partial transfer occurred. Remaining quantity ({itemToTransfer.quantity}) of '{itemToTransfer.details?.Name ?? "Unknown"}' needs to be returned to source inventory '{sourceInventory.Id}'.");

                // Attempt to add the remaining quantity back into the source inventory.
                // AddItem will find a suitable slot (existing stack or empty slot) in the source.
                returnedToSource = sourceInventory.Combiner.AddItem(itemToTransfer);

                if (returnedToSource)
                {
                    Debug.Log($"ItemTransferHandler: Remaining quantity ({itemToTransfer.quantity}) successfully returned to source inventory '{sourceInventory.Id}'.");
                }
                else
                {
                    // This case means the source inventory was also full or couldn't accept the remaining quantity.
                    // The item instance with the remaining quantity is effectively lost from the UI/data.
                    Debug.LogError($"ItemTransferHandler: Failed to return remaining quantity ({itemToTransfer.quantity}) of '{itemToTransfer.details?.Name ?? "Unknown"}' to source inventory '{sourceInventory.Id}'. Source inventory is likely full.", this);
                }
            }
            else // itemToTransfer.quantity is 0, meaning the entire original quantity was added to the target (either fully stacked into the specific slot, or fully added via TryAddQuantity)
            {
                Debug.Log($"ItemTransferHandler: Item '{itemToTransfer.details?.Name ?? "Unknown"}' fully transferred to target inventory '{targetInventory.Id}'.");
            }

            // --- Refine UI Feedback (Popups) ---
            // This logic also remains the same, using the final quantityAddedToTarget and returnedToSource status.
            if (quantityAddedToTarget == originalQuantity)
            {
                // Full transfer successful
                // No popup needed for full success, or maybe a subtle one?
                Debug.Log($"ItemTransferHandler: Full transfer successful for '{itemToTransfer.details?.Name ?? "Unknown"}'.");
            }
            else if (quantityAddedToTarget > 0) // Some quantity was added to the target
            {
                if (returnedToSource)
                {
                    // Partial transfer, remainder returned to source
                    PlayerUIPopups.Instance?.ShowPopup("Partial Transfer", $"Transferred {quantityAddedToTarget} of {itemToTransfer.details?.Name ?? "item"}, {itemToTransfer.quantity} remained."); // Include remaining quantity in message
                    Debug.Log($"ItemTransferHandler: Partial transfer successful for '{itemToTransfer.details?.Name ?? "Unknown"}'. {quantityAddedToTarget} added to target, {itemToTransfer.quantity} returned to source.");
                }
                else
                {
                    // Partial transfer, remainder failed to return to source (critical)
                    PlayerUIPopups.Instance?.ShowPopup("Transfer Failed", $"Transferred {quantityAddedToTarget} of {itemToTransfer.details?.Name ?? "item"}, but remaining {itemToTransfer.quantity} could not be returned.");
                     Debug.LogError($"ItemTransferHandler: Partial transfer occurred, but remaining {itemToTransfer.quantity} of '{itemToTransfer.details?.Name ?? "Unknown"}' failed to return to source.");
                }
            }
            else // quantityAddedToTarget == 0 (Nothing was added to the target)
            {
                 if (returnedToSource || originalQuantity == 0) // originalQuantity == 0 check is for safety, though handled earlier
                 {
                     // Item was returned to source (or was empty initially).
                     // This happens if the target couldn't accept it (either due to full space *anywhere* in target, or filtering *at the Combiner level*).
                     PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "Could not transfer item (target full or invalid)."); // Updated message to cover filtering too
                     Debug.Log($"ItemTransferHandler: No quantity of '{itemToTransfer.details?.Name ?? "Unknown"}' could be added to target. Item returned to source.");
                 }
                 else
                 {
                      // Item was NOT returned to source (critical failure)
                      PlayerUIPopups.Instance?.ShowPopup("Transfer Failed", "Could not transfer item, and it could not be returned to source."); // Message for total failure
                      Debug.LogError($"ItemTransferHandler: No quantity of '{itemToTransfer.details?.Name ?? "Unknown"}' could be added to target, and it failed to return to source.");
                 }
            }
        }
    }
}

// --- END OF FILE ItemTransferHandler.cs ---