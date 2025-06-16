using UnityEngine;
using Systems.Inventory; // Needed for Inventory, Item, ItemDetails, Combiner, ObservableArray, InventorySlotUI, CraftingStation
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;
using Systems.GameStates; // Needed to access MenuManager and GameState
using Systems.UI; // Needed for PlayerUIPopups
using System.Linq; // Needed for Linq methods like FirstOrDefault

namespace Systems.Inventory
{
    /// <summary>
    /// Manages the drag and drop interactions for all inventories.
    /// </summary>
    public class DragAndDropManager : MonoBehaviour
    {
        // Singleton instance
        public static DragAndDropManager Instance { get; private set; }

        [Tooltip("The UI Image element used to visualize the item being dragged.")]
        [SerializeField] private Image ghostItemImage; // Assign your ghost visual UI Image here

        [Tooltip("The Text or TextMeshPro component for the quantity on the ghost visual.")]
        [SerializeField] private TextMeshProUGUI ghostQuantityText;

        // --- Flag to indicate if a drag operation is currently in progress ---
        public bool IsDragging { get; private set; } = false;

        // --- Event broadcast when the drag state changes ---
        public event Action<bool> OnDragStateChanged;


        private Item itemBeingDragged; // The actual Item instance being dragged
        private Inventory sourceInventory; // The inventory the item came from
        private int sourceSlotIndex; // The original physical slot index in the source inventory

        public event Action OnDragDropCompleted;

        // List of all active inventories in the scene (inventories register themselves)
        private static List<Inventory> allInventories_static = new List<Inventory>(); // Renamed to avoid conflict if a local variable is named allInventories

        public static void RegisterInventory(Inventory inventory)
        {
            if (inventory != null && !allInventories_static.Contains(inventory))
            {
                allInventories_static.Add(inventory);
                Debug.Log($"DragAndDropManager: Registered inventory '{inventory.Id}' ({inventory.gameObject.name}). Total registered: {allInventories_static.Count}");
            }
             else if (inventory == null)
             {
                  Debug.LogWarning("DragAndDropManager: Attempted to register a null inventory.");
             }
        }

        public static void UnregisterInventory(Inventory inventory)
        {
            if (inventory != null && allInventories_static.Contains(inventory))
            {
                allInventories_static.Remove(inventory);
                 Debug.Log($"DragAndDropManager: Unregistered inventory '{inventory.Id}' ({inventory.gameObject.name}). Total registered: {allInventories_static.Count}");
            }
             else if (inventory == null)
             {
                  Debug.LogWarning("DragAndDropManager: Attempted to unregister a null inventory.");
             }
        }


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
                Debug.LogWarning("DragAndDropManager: Duplicate instance found. Destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            // Ensure the ghost item visual is initially hidden
            if (ghostItemImage != null)
            {
                ghostItemImage.gameObject.SetActive(false);
                ghostItemImage.raycastTarget = false; // Disable raycasting so it doesn't block clicks
            }
            else
            {
                Debug.LogError("DragAndDropManager: Ghost Item Image is not assigned!", this);
                enabled = false;
                return;
            }

            // Ensure the ghost quantity text is initially hidden
            if (ghostQuantityText != null)
            {
                ghostQuantityText.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning("DragAndDropManager: Ghost Quantity Text is not assigned. Quantity will not be displayed during drag.", this);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // --- Ensure state is reset and event is potentially invoked on destruction ---
            if (IsDragging)
            {
                // Item is likely stuck in the ghost slot if manager is destroyed mid-drag.
                // Attempt to return it to source.
                Debug.LogError("DragAndDropManager: Manager destroyed while dragging! Attempting to return item to source.", this);
                ReturnItemToSource(); // This will log errors if source is also gone.

                IsDragging = false;
                OnDragStateChanged?.Invoke(false);
            }

            // Clear the static list on shutdown if needed, though typically not necessary
            // allInventories_static.Clear();
        }

        /// <summary>
        /// Called by an InventorySlotUI when a pointer is pressed down.
        /// Initiates a drag if an item is present.
        /// NOTE: This method is only called if Shift is NOT held down (handled in InventorySlotUI.OnPointerDown).
        /// </summary>
        public void StartDrag(InventorySlotUI slotUI, PointerEventData eventData)
        {
            if (IsDragging || itemBeingDragged != null) // Prevent starting a new drag if one is already active
            {
                 Debug.LogWarning("DragAndDropManager: StartDrag called but a drag is already in progress.", this);
                return;
            }

            // Basic validation of source slot
            if (slotUI.ParentInventory == null || slotUI.ParentInventory.InventoryState == null || slotUI.ParentInventory.Combiner == null)
            {
                Debug.LogError($"DragAndDropManager: StartDrag called with invalid slotUI parent inventory, state, or combiner ({slotUI.ParentInventory?.Id ?? SerializableGuid.Empty}).", slotUI.gameObject);
                return;
            }

            Inventory sourceInv = slotUI.ParentInventory;
            ObservableArray<Item> sourceOA = sourceInv.InventoryState;

            if (slotUI.SlotIndex < 0 || slotUI.SlotIndex >= sourceInv.PhysicalSlotCount) // Use PhysicalSlotCount from Inventory
            {
                 Debug.LogError($"DragAndDropManager: StartDrag called with invalid source physical slot index ({slotUI.SlotIndex}) for physical slots ({sourceInv.PhysicalSlotCount}).", slotUI.gameObject);
                 return;
            }


            Item itemInSlot = sourceOA[slotUI.SlotIndex];

            if (itemInSlot != null && itemInSlot.details != null)
            {
                // --- Start the drag ---
                itemBeingDragged = itemInSlot;
                sourceInventory = sourceInv;
                sourceSlotIndex = slotUI.SlotIndex;

                // --- Set dragging flag and broadcast event ---
                IsDragging = true;
                OnDragStateChanged?.Invoke(true);
                Debug.Log("DragAndDropManager: Drag started. Broadcasted OnDragStateChanged(true).");

                // Visually represent the item being dragged
                if (ghostItemImage != null && itemBeingDragged.details.Icon != null)
                {
                    ghostItemImage.sprite = itemBeingDragged.details.Icon;
                    ghostItemImage.transform.position = eventData.position;
                    ghostItemImage.gameObject.SetActive(true);

                    // --- Update Ghost Quantity Text ---
                    if (ghostQuantityText != null)
                    {
                        // Show quantity if maxStack > 1 and quantity > 1, OR if it's a gun showing mag ammo
                        bool showQuantity = false;
                        string quantityText = "";

                        if (itemBeingDragged.details.maxStack > 1 && itemBeingDragged.quantity > 1)
                        {
                            showQuantity = true;
                            quantityText = itemBeingDragged.quantity.ToString();
                        }
                         // Optional: Show gun magazine ammo instead of quantity for guns?
                         // else if (itemBeingDragged.details.maxStack == 1 && itemBeingDragged.details.usageLogic == ItemUsageLogic.GunLogic && itemBeingDragged.details.magazineSize > 0)
                         // {
                         //     showQuantity = true;
                         //     quantityText = itemBeingDragged.currentMagazineHealth.ToString(); // Show current magazine ammo
                         // }


                        if (showQuantity)
                        {
                            ghostQuantityText.text = quantityText;
                            ghostQuantityText.gameObject.SetActive(true);
                        }
                        else
                        {
                            // Hide quantity text if not applicable
                            ghostQuantityText.text = ""; // Clear text
                            ghostQuantityText.gameObject.SetActive(false);
                        }
                    }

                    // --- Move item instance from original slot to source ghost slot ---
                    // Clear the original slot in the source inventory's data
                    sourceOA.SetItemAtIndex(null, sourceSlotIndex);
                    // Move the item instance to the source inventory's ghost data slot
                    sourceOA.SetItemAtIndex(itemBeingDragged, sourceOA.Length - 1); // Length-1 is the ghost slot index

                }
                else
                {
                    Debug.LogError("DragAndDropManager: Ghost item visual setup failed (Image or ItemDetails/Icon missing). Aborting drag.", this);
                    // --- Reset drag state if visual setup fails ---
                    // Return the item immediately as visual setup failed
                    ReturnItemToSource(); // This clears the ghost slot and puts it back
                    // Then reset drag state variables
                    itemBeingDragged = null;
                    sourceInventory = null;
                    sourceSlotIndex = -1;
                    IsDragging = false; // Reset flag immediately
                    OnDragStateChanged?.Invoke(false); // Broadcast state change
                }
            }
            else
            {
                Debug.Log($"DragAndDropManager: Clicked on empty slot {slotUI.SlotIndex} or item has no details. No drag started.");
            }
        }

        /// <summary>
        /// Called by an InventorySlotUI while the pointer is being dragged.
        /// Updates the position of the ghost visual.
        /// </summary>
        public void Drag(PointerEventData eventData)
        {
            if (itemBeingDragged != null && ghostItemImage != null)
            {
                ghostItemImage.transform.position = eventData.position;
                // Keep the quantity text in sync with the image's position
                if (ghostQuantityText != null && ghostQuantityText.gameObject.activeSelf) // Only update position if active
                {
                    ghostQuantityText.transform.position = eventData.position; // Assumes they are siblings or parented correctly
                }
            }
        }

        /// <summary>
        /// Called by an InventorySlotUI when the pointer is released, ending the drag.
        /// Handles the drop logic.
        /// </summary>
        public void EndDrag(PointerEventData eventData)
        {
            if (itemBeingDragged == null || sourceInventory == null || sourceInventory.InventoryState == null)
            {
                 Debug.LogWarning("DragAndDropManager: EndDrag called but no drag was active or source inventory state is missing.", this);
                 // Ensure state is clean just in case
                 ResetDragState();
                 return;
            }

            Debug.Log($"DragAndDropManager: Ending drag for '{itemBeingDragged.details?.Name ?? "Unknown"}' (ID: {itemBeingDragged.Id}) from source slot {sourceSlotIndex} in inventory '{sourceInventory.Id}'.", this);

            // Hide the ghost visual immediately
            if (ghostItemImage != null) ghostItemImage.gameObject.SetActive(false);
            if (ghostQuantityText != null) ghostQuantityText.gameObject.SetActive(false); // Hide quantity too

            InventorySlotUI targetSlotUI = FindTargetSlot(eventData.position);
            Inventory targetInventory = targetSlotUI?.ParentInventory;
            int targetSlotIndex = targetSlotUI?.SlotIndex ?? -1; // -1 if no valid slot UI found

            bool dropSuccessfullyProcessed = false; // Flag to know if item found a valid target or was returned

            // --- Check if dropped over a valid inventory target ---
            if (targetInventory != null && targetInventory.Combiner != null && targetInventory.InventoryState != null)
            {
                Debug.Log($"DragAndDropManager: Dropped over target inventory '{targetInventory.Id}' ({targetInventory.gameObject.name}). Target Slot Index (UI): {targetSlotIndex}.");

                // Ensure the target index is within the physical slot range of the target inventory
                // If dropped over the inventory panel but not a valid physical slot UI (e.g., padding area, ghost area UI)
                if (targetSlotIndex < 0 || targetSlotIndex >= targetInventory.PhysicalSlotCount) // Use PhysicalSlotCount from Inventory
                {
                     Debug.Log($"DragAndDropManager: Drop detected over target inventory '{targetInventory.Id}' but not over a valid physical slot UI. Target Index: {targetSlotIndex}. Returning item to source.");
                     ReturnItemToSource(); // Item returned to its original spot
                     dropSuccessfullyProcessed = true; // Drop process handled (by returning to source)
                }
                else
                {
                    // --- Dropped over a valid physical slot UI ---
                    Debug.Log($"DragAndDropManager: Dropped over valid physical slot {targetSlotIndex} in target inventory '{targetInventory.Id}'.");

                    // --- Check if the target inventory allows this item type ---
                    if (!targetInventory.CanAddItem(itemBeingDragged))
                    {
                        Debug.Log($"DragAndDropManager: Item '{itemBeingDragged.details?.Name ?? "Unknown"}' is not allowed in target inventory '{targetInventory.Id}' due to filtering. Returning to source.");
                        ReturnItemToSource(); // Item returned to its original spot
                        dropSuccessfullyProcessed = true; // Drop process handled (by returning to source)
                        PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "This item cannot go in that inventory.");
                    }
                    else
                    {
                        // --- Filtering check passed, proceed with drop logic on target ---
                        Item targetItemInSlot = targetInventory.InventoryState[targetSlotIndex];

                        // --- Handle Drop Logic based on Target Slot Content and Item Type ---

                        // Case 1: Target slot is empty
                        if (targetItemInSlot == null)
                        {
                            Debug.Log($"DragAndDropManager: Target slot {targetSlotIndex} is empty. Placing item directly.");

                            // Directly place the item instance from the source ghost slot into the target physical slot.
                            // The itemBeingDragged variable *is* the instance currently in the source ghost slot.
                            targetInventory.InventoryState.SetItemAtIndex(itemBeingDragged, targetSlotIndex);

                            // Clear the source ghost slot now that the item has been moved.
                            sourceInventory.InventoryState.SetItemAtIndex(null, sourceInventory.InventoryState.Length - 1);

                            // The item was successfully placed in the target.
                            dropSuccessfullyProcessed = true;
                            Debug.Log($"DragAndDropManager: Successfully placed '{itemBeingDragged.details.Name}' into empty target slot {targetSlotIndex}.");

                            // Note: itemBeingDragged.quantity is NOT modified here. It retains its original quantity,
                            // which is correct as the whole instance was moved.
                            // The return logic below will NOT trigger because dropSuccessfullyProcessed is true.

                        }
                        // Case 2: Target slot is NOT empty
                        else
                        {
                            Debug.Log($"DragAndDropManager: Target slot {targetSlotIndex} is not empty (contains '{targetItemInSlot.details?.Name ?? "Unknown"}').");

                            // --- Check if we can stack onto the target item ---
                            // Only applies if both are stackable and the same type
                            if (itemBeingDragged.details.maxStack > 1 && targetItemInSlot.CanStackWith(itemBeingDragged))
                            {
                                Debug.Log($"DragAndDropManager: Attempting to stack '{itemBeingDragged.details.Name}' onto existing stack at target slot {targetSlotIndex}.");

                                // Attempt to stack the dragged quantity onto the target stack.
                                // TryStackQuantityToSpecificSlot will add to targetItemInSlot.quantity
                                // and reduce itemBeingDragged.quantity with the remainder.
                                int quantityStacked = targetInventory.TryStackQuantityToSpecificSlot(itemBeingDragged, targetSlotIndex);

                                if (quantityStacked > 0)
                                {
                                    // If any quantity was stacked, the drop process for that portion is handled.
                                    // The item instance with the *remaining* quantity is still in the source ghost slot.
                                    // The return logic below will handle adding the remainder back to the source.
                                    // DO NOT set dropSuccessfullyProcessed = true here. Let the return logic handle the final state.
                                     Debug.Log($"DragAndDropManager: Stacked {quantityStacked} of '{itemBeingDragged.details.Name}' onto target stack. Remainder ({itemBeingDragged.quantity}) will be handled by return logic.");
                                }
                                else
                                {
                                    // If stacking failed (e.g., target stack full), the item instance with its full quantity
                                    // is still in the source ghost slot. dropSuccessfullyProcessed remains false,
                                    // and the return logic below will trigger to return the full item to source.
                                    Debug.Log($"DragAndDropManager: Failed to stack onto target slot {targetSlotIndex}. Target stack likely full or other issue. Item will be returned to source.");
                                }
                                // Note: If stacking fails entirely, itemBeingDragged.quantity remains unchanged.
                            }
                            // --- If not stacking (different types, non-stackable, or target stack full/wrong type) ---
                            else
                            {
                                Debug.Log($"DragAndDropManager: Cannot stack. Attempting to swap item '{itemBeingDragged.details.Name}' with item '{targetItemInSlot.details.Name}' at slot {targetSlotIndex}.");

                                // --- Check if the target item is allowed in the source inventory ---
                                // This check is needed for a swap. If the target item can't go back to the source, the swap fails.
                                if (!sourceInventory.CanAddItem(targetItemInSlot))
                                {
                                     Debug.Log($"DragAndDropManager: Cannot swap. Target item '{targetItemInSlot.details?.Name ?? "Unknown"}' is not allowed in source inventory '{sourceInventory.Id}' due to filtering. Returning dragged item to source.");
                                     ReturnItemToSource(); // Dragged item goes back
                                     dropSuccessfullyProcessed = true; // Drop process handled
                                     PlayerUIPopups.Instance?.ShowPopup("Cannot Swap", "Item in target won't fit in source."); // Refined message
                                     goto EndDragCleanup; // Skip further processing for this drop attempt
                                }
                                // --- Filtering check for swap passed ---

                                // Perform the swap using SetItemAtIndex on both arrays
                                // itemBeingDragged is currently in source ghost slot. targetItemInSlot is in target physical slot.

                                Debug.Log($"DragAndDropManager: Performing swap: Moving '{itemBeingDragged.details.Name}' (ID: {itemBeingDragged.Id}) to target {targetSlotIndex}, and '{targetItemInSlot.details.Name}' (ID: {targetItemInSlot.Id}) to source {sourceSlotIndex}.");

                                // Move dragged item from source ghost slot to target physical slot
                                targetInventory.InventoryState.SetItemAtIndex(itemBeingDragged, targetSlotIndex);

                                // Move target item from target physical slot to source original physical slot
                                sourceInventory.InventoryState.SetItemAtIndex(targetItemInSlot, sourceSlotIndex);

                                // Clear the source ghost slot (itemBeingDragged has moved)
                                sourceInventory.InventoryState.SetItemAtIndex(null, sourceInventory.InventoryState.Length - 1);

                                dropSuccessfullyProcessed = true; // Swap handled the drop process fully
                                Debug.Log("DragAndDropManager: Swap successful.");

                                // The dragged item was successfully moved (swapped). Its quantity state is irrelevant for returning.
                                // No need to modify itemBeingDragged.quantity here, as dropSuccessfullyProcessed flag prevents return logic.
                            }
                        } // End Case 2 (Target not empty)

                        // --- Handle Item Return to Source After Attempted Placement/Stacking (If Not Swapped or Placed Directly) ---
                        // This applies to Case 1 (Empty slot, if direct place failed - although it shouldn't)
                        // and Case 2 (Attempted Stack) if not fully added/stacked.
                        // It does NOT apply if a Swap occurred or if placed directly into an empty slot (handled above).

                        if (!dropSuccessfullyProcessed) // Only run return logic if the drop wasn't fully handled by direct place or swap
                        {
                            // itemBeingDragged.quantity now reflects the remainder *after* attempted stacking in target.
                            // If stacking failed entirely, quantity is original. If partially stacked, quantity is remainder.
                            // If itemBeingDragged.quantity is still > 0, it means any quantity/instance remains to be returned.

                            if (itemBeingDragged.quantity > 0) // Check if any quantity/instance remains to be returned
                            {
                                Debug.Log($"DragAndDropManager: Item was partially placed/stacked or placement/stacking failed. Remaining quantity ({itemBeingDragged.quantity}) of '{itemBeingDragged.details.Name}' needs to be returned to source inventory '{sourceInventory.Id}'.");

                                // Attempt to add the remaining quantity/instance back into the source inventory.
                                // Use the general AddItem method on the source inventory.
                                // AddItem handles finding space and updating itemBeingDragged.quantity (sets to 0 if fully returned).
                                bool returnedToSource = sourceInventory.AddItem(itemBeingDragged); // AddItem handles stackable/non-stackable

                                // Clear the ghost slot regardless, as the item instance has now either been placed
                                // by AddItem or failed to be placed and remains in itemBeingDragged var (conceptually lost from data).
                                sourceInventory.InventoryState.SetItemAtIndex(null, sourceInventory.InventoryState.Length - 1);

                                if (returnedToSource)
                                {
                                    // itemBeingDragged.quantity is now 0 if fully added by AddItem (for both stackable and non-stackable).
                                    Debug.Log($"DragAndDropManager: Remaining quantity successfully returned to source using AddItem. Remaining quantity on instance: {itemBeingDragged.quantity}.");
                                }
                                else
                                {
                                    // itemBeingDragged.quantity reflects the remainder that couldn't be added back.
                                    Debug.LogError($"DragAndDropManager: Failed to return remaining quantity ({itemBeingDragged.quantity}) of '{itemBeingDragged.details.Name}' to source inventory '{sourceInventory.Id}'. Source inventory is likely full. Item is LOST!", this);
                                    PlayerUIPopups.Instance?.ShowPopup("Return Failed", "Could not return item to source!");
                                    // The item instance with the remaining quantity is effectively lost from the UI/data.
                                }

                                // Regardless of whether the return succeeded, the drop process is now considered handled.
                                dropSuccessfullyProcessed = true;
                            }
                            else // itemBeingDragged.quantity is <= 0. This means the entire original quantity/instance was successfully placed/stacked in the target.
                            {
                                Debug.Log($"DragAndDropManager: Entire quantity/instance of '{itemBeingDragged.details.Name}' successfully placed/stacked in target.");
                                // The item instance with quantity 0 is still in the source ghost slot. Need to clear it.
                                sourceInventory.InventoryState.SetItemAtIndex(null, sourceInventory.InventoryState.Length - 1);
                                dropSuccessfullyProcessed = true; // Drop process handled (by placing in target)
                            }
                        } // End if (!dropSuccessfullyProcessed) - return logic
                    } // End if (target filtering passed)
                } // End if (dropped over valid physical slot)
            }
            // --- Handle cases where the drop was NOT over a valid inventory target UI ---
            else // targetInventory was null (dropped outside any inventory UI)
            {
                Debug.Log("DragAndDropManager: Drop target not found (e.g., dropped outside any inventory UI). Returning item to source.");
                ReturnItemToSource(); // Return the item to its source slot
                dropSuccessfullyProcessed = true; // Drop process handled (by returning to source)
            }

            // --- End Drag Cleanup ---
            EndDragCleanup:; // Label for goto

            // Resetting these *before* potentially triggering completion events
            // minimizes the window where a new drag could incorrectly start.
            ResetDragState();

            // Now trigger the completion event AFTER state is fully reset
            OnDragDropCompleted?.Invoke();
            Debug.Log($"DragAndDropManager: Drag/Drop transaction completed. Triggering OnDragDropCompleted event.");

        }


        // Helper method to find the InventorySlotUI the pointer is currently over.
        private InventorySlotUI FindTargetSlot(Vector3 screenPosition)
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = screenPosition;
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (RaycastResult result in results)
            {
                InventorySlotUI slotUI = result.gameObject.GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    // Ensure the hit slot belongs to a valid inventory
                    if (slotUI.ParentInventory != null)
                    {
                        // --- IMPORTANT: Ignore raycasts hitting the ghost item itself ---
                        // The ghost image has raycastTarget = false, but defensive check is good.
                        if (result.gameObject == ghostItemImage.gameObject) continue;

                         // Return the SlotUI if found. EndDrag will handle index validation.
                         return slotUI; // Found a slot UI associated with a valid inventory
                    }
                    else
                    {
                        Debug.LogWarning($"DragAndDropManager: Raycast hit InventorySlotUI on {result.gameObject.name} but it has no ParentInventory assigned.", result.gameObject);
                    }
                }
                 else
                 {
                     // Did not hit an InventorySlotUI. Check if we hit the main Inventory panel itself.
                     // This is useful if dropping into empty space within an inventory window.
                     Inventory inventory = result.gameObject.GetComponent<Inventory>();
                     if (inventory != null)
                     {
                          // If we hit the Inventory panel directly, *and* no specific slot UI was hit first,
                          // we could potentially return a special indicator or the Inventory itself.
                          // For now, we rely on hitting a SlotUI. If no SlotUI is hit, targetSlotUI will be null.
                     }
                 }
            }

            // Debug.Log("DragAndDropManager: Raycast did not hit any valid InventorySlotUI with a parent inventory."); // Commented out to reduce spam
            return null; // No valid InventorySlotUI found at the drop position
        }


        /// <summary>
        /// Returns the item being dragged back to its original source slot.
        /// Called when the drop is invalid (e.g., outside UI, filtering fail) or explicitly by AbortDrag.
        /// Assumes the itemBeingDragged is currently in the source inventory's ghost slot.
        /// </summary>
        private void ReturnItemToSource()
        {
            if (itemBeingDragged == null || sourceInventory == null || sourceSlotIndex == -1 || sourceInventory.InventoryState == null || sourceInventory.Combiner == null)
            {
                Debug.LogError("DragAndDropManager: Attempted to return item to source, but drag state or source inventory/combiner was invalid.", this);
                // Item might be lost if in ghost slot and source is gone.
                // Cannot recover reliably without valid source/index.
                // Try to clear the ghost slot anyway as a cleanup attempt.
                if (sourceInventory?.InventoryState != null)
                {
                     int ghostIndex = sourceInventory.InventoryState.Length - 1;
                     if (ghostIndex >= 0 && ghostIndex < sourceInventory.InventoryState.Length)
                     {
                          Item itemInGhost = sourceInventory.InventoryState[ghostIndex];
                          if (itemInGhost != null && itemInGhost.Id == itemBeingDragged?.Id)
                          {
                               Debug.LogWarning($"DragAndDropManager: Clearing item '{itemInGhost.details?.Name ?? "Unknown"}' from source ghost slot {ghostIndex} during invalid return attempt.", this);
                               sourceInventory.InventoryState.SetItemAtIndex(null, ghostIndex);
                          }
                     }
                }
                return; // Cannot proceed with return
            }

            ObservableArray<Item> sourceOA = sourceInventory.InventoryState;

            // Ensure itemBeingDragged is actually the one in the ghost slot before proceeding.
            int sourceGhostSlotIndex = sourceOA.Length - 1;
            Item itemInGhostSlot = sourceOA[sourceGhostSlotIndex];

            if (itemInGhostSlot == null || itemInGhostSlot.Id != itemBeingDragged.Id)
            {
                Debug.LogError($"DragAndDropManager: Attempted to return item '{itemBeingDragged.details?.Name ?? "Unknown"}' (ID: {itemBeingDragged.Id}) to source {sourceSlotIndex}, but the expected item was NOT found in the source ghost slot {sourceGhostSlotIndex}! Ghost contains: {(itemInGhostSlot != null ? itemInGhostSlot.details?.Name ?? "Unknown" : "NOTHING")}. Item is likely lost or state corrupted.", this);
                 // Clear the ghost slot anyway as it's in an unexpected state
                 if (itemInGhostSlot != null) sourceOA.SetItemAtIndex(null, sourceGhostSlotIndex);
                return; // Cannot proceed safely
            }

            Debug.Log($"DragAndDropManager: Returning item '{itemBeingDragged.details?.Name ?? "Unknown"}' (ID: {itemBeingDragged.Id}, Qty: {itemBeingDragged.quantity}) from ghost slot {sourceGhostSlotIndex} back to source physical slot {sourceSlotIndex} in inventory '{sourceInventory.Id}'.");

            // Attempt to place the item back into its original slot.
            // If the original slot is now occupied (e.g., by a swap), AddItem will find another spot.
            // AddItem handles both stackable and non-stackable correctly.
            // The itemBeingDragged instance still holds the quantity/state from before the drag started (or after partial stacking).
            // AddItem will modify itemBeingDragged.quantity based on what could be added back.

             // Before calling AddItem, check if the original slot is empty.
             // If it is, we can directly place it there using SetItemAtIndex, which is slightly cleaner.
             // If it's not empty, we must use AddItem to find a new spot.
             Item itemInOriginalSlotNow = sourceOA[sourceSlotIndex];

             if (itemInOriginalSlotNow == null)
             {
                  Debug.Log($"DragAndDropManager: Original slot {sourceSlotIndex} is empty. Directly placing item back.");
                   // Place the item instance directly back
                  sourceOA.SetItemAtIndex(itemBeingDragged, sourceSlotIndex);
                   // Clear the ghost slot
                  sourceOA.SetItemAtIndex(null, sourceGhostSlotIndex);
                  // itemBeingDragged.quantity remains unchanged, correctly reflecting its full quantity or remainder after partial stack.
             }
             else
             {
                  Debug.Log($"DragAndDropManager: Original slot {sourceSlotIndex} is occupied (contains '{itemInOriginalSlotNow.details?.Name ?? "Unknown"}'). Using AddItem to return.");
                  // Attempt to add the item back using the general AddItem method.
                  // AddItem handles finding space and updating itemBeingDragged.quantity.
                  bool returnedUsingAddItem = sourceInventory.AddItem(itemBeingDragged);

                  // Clear the ghost slot regardless, as the item instance has now either been placed
                  // by AddItem or failed to be placed and remains in itemBeingDragged var (conceptually lost from data).
                   sourceOA.SetItemAtIndex(null, sourceGhostSlotIndex);


                  if (returnedUsingAddItem)
                  {
                      // itemBeingDragged.quantity is now 0 if fully added by AddItem (for both stackable and non-stackable).
                      Debug.Log($"DragAndDropManager: Item successfully returned to source using AddItem. Remaining quantity (should be 0 if fully returned): {itemBeingDragged.quantity}.");
                  }
                  else
                  {
                      // itemBeingDragged.quantity reflects the remainder that couldn't be added back.
                      Debug.LogError($"DragAndDropManager: Failed to return remaining quantity ({itemBeingDragged.quantity}) of '{itemBeingDragged.details.Name}' to source inventory '{sourceInventory.Id}'. Source inventory is likely full. Item is LOST!", this);
                      PlayerUIPopups.Instance?.ShowPopup("Return Failed", "Could not return item to source!");
                      // The item instance with the remaining quantity is effectively lost from the UI/data.
                  }
             }

            // Note: Drag state (itemBeingDragged, etc.) is cleared in ResetDragState after this method potentially returns.
        }

        /// <summary>
        /// Resets the internal drag state variables and broadcasts the state change event.
        /// </summary>
        private void ResetDragState()
        {
             bool wasDragging = IsDragging; // Capture current state

             // Reset internal variables first
             itemBeingDragged = null;
             sourceInventory = null;
             sourceSlotIndex = -1;

             // Reset the flag and broadcast the event *only if* the state actually changed
             if (wasDragging)
             {
                 IsDragging = false;
                 OnDragStateChanged?.Invoke(false);
                 Debug.Log("DragAndDropManager: ResetDragState completed. Broadcasted OnDragStateChanged(false).");
             }
             else
             {
                 Debug.Log("DragAndDropManager: ResetDragState called but was not dragging.");
             }
        }


        public void AbortDrag()
        {
            // Only abort if a drag is actually in progress
            if (!IsDragging || itemBeingDragged == null)
            {
                Debug.Log("DragAndDropManager: AbortDrag called, but no drag was active.", this);
                return;
            }

            Debug.Log("DragAndDropManager: Aborting active drag operation.", this);

            // --- Hide the ghost visual ---
            if (ghostItemImage != null) ghostItemImage.gameObject.SetActive(false);
            if (ghostQuantityText != null) ghostQuantityText.gameObject.SetActive(false);

            // --- Return the item to its source slot ---
            // Use the existing helper. This will move the item from the ghost slot
            // back to its original physical slot via SetItemAtIndex or AddItem.
            ReturnItemToSource();

            // --- Reset dragging state and broadcast event ---
            ResetDragState();

            // --- Optionally trigger a completion event, or a specific abort event ---
            // Using completion event for simplicity, indicates the drag process is finished.
            OnDragDropCompleted?.Invoke();
            Debug.Log("DragAndDropManager: Triggering OnDragDropCompleted after abort.");
        }
    }
}