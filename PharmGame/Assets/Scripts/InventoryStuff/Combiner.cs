// Combiner.cs
using UnityEngine;
using Systems.Inventory; // Make sure this namespace matches your scripts
using System.Collections.Generic;
using System.Linq; // Needed for Any()

namespace Systems.Inventory
{
    /// <summary>
    /// Manages the underlying data array for the inventory,
    /// coordinating between UI structure and item state.
    /// </summary>
    public class Combiner : MonoBehaviour
    {
        [Tooltip("Reference to the FlexibleGridLayout component that defines the visual slots.")]
        [SerializeField]
        private FlexibleGridLayout flexibleGridLayout;

        /// <summary>
        /// Provides access to the FlexibleGridLayout component.
        /// </summary>
        public FlexibleGridLayout FlexibleGridLayoutComponent => flexibleGridLayout;

        private ObservableArray<Item> inventoryState;

        /// <summary>
        /// Provides access to the observable array holding the inventory's item state.
        /// </summary>
        public ObservableArray<Item> InventoryState => inventoryState;

        /// <summary>
        /// Gets the total number of data slots managed by the Combiner (including the ghost slot).
        /// </summary>
        public int TotalDataSlotCount { get; private set; }

        /// <summary>
        /// Gets the number of physical visual slots based on the FlexibleGridLayout.
        /// </summary>
        public int PhysicalSlotCount { get; private set; }

        public Inventory ParentInventory { get; internal set; }

        private void Awake()
        {
            InitializeInventoryData();
        }

        // Optional: You might want to call this publicly if initialization needs to happen later
        public void InitializeInventoryData()
        {
            // Ensure the FlexibleGridLayout reference is set
            if (flexibleGridLayout == null)
            {
                flexibleGridLayout = GetComponent<FlexibleGridLayout>();
                if (flexibleGridLayout == null)
                {
                    Debug.LogError("Combiner requires a FlexibleGridLayout component on the same GameObject or assigned in the inspector.", this);
                    return;
                }
            }

            // Get the number of physical slots from the UI layout
            PhysicalSlotCount = flexibleGridLayout.transform.childCount;
            TotalDataSlotCount = PhysicalSlotCount + 1; // Add 1 for the ghost slot

            // Initialize the ObservableArray with the calculated size
            inventoryState = new ObservableArray<Item>(TotalDataSlotCount);

            Debug.Log($"Combiner ({gameObject.name}): Initialized inventory data with {PhysicalSlotCount} physical slots and {TotalDataSlotCount} total data slots (including ghost). ObservableArray created: {(inventoryState != null ? "NOT NULL" : "NULL")}", this);

            // TODO: Add logic here later for loading saved inventory state if applicable
        }

        /// <summary>
        /// Attempts to add a pre-created item instance to the inventory.
        /// Prioritizes stacking with existing compatible stacks before finding an empty slot
        /// and creating new stacks if the quantity exceeds maxStack.
        /// Only adds to physical slots (excluding the ghost slot).
        /// Handles stackable items by quantity and non-stackable items by placing the instance.
        /// </summary>
        /// <param name="itemToAdd">The Item instance to add (its quantity will be reduced as it's added).</param>
        /// <returns>True if the item was fully added (either stacked or placed in empty slot/new stacks), false otherwise (inventory full).</returns>
        public bool AddItem(Item itemToAdd)
        {
            // Basic validation
            if (itemToAdd == null || itemToAdd.details == null || (itemToAdd.details.maxStack > 1 && itemToAdd.quantity <= 0) || (itemToAdd.details.maxStack == 1 && itemToAdd.health <= 0 && itemToAdd.details.maxHealth > 0))
            {
                 // Check quantity for stackable, or health for durable non-stackable
                Debug.LogWarning($"Combiner ({gameObject.name}): Attempted to add a null, detail-less, or zero/negative quantity/health item. Item: {itemToAdd?.details?.Name ?? "NULL"}, Qty: {itemToAdd?.quantity ?? 0}, Health: {itemToAdd?.health ?? 0}, MaxStack: {itemToAdd?.details?.maxStack ?? 0}, MaxHealth: {itemToAdd?.details?.maxHealth ?? 0}");
                return false;
            }

            if (ParentInventory != null && !ParentInventory.CanAddItem(itemToAdd))
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): Item '{itemToAdd.details?.Name ?? "Unknown"}' with label '{itemToAdd.details?.itemLabel.ToString() ?? "None"}' is not allowed in this inventory.");
                return false; // Item type not allowed, reject addition
            }

            int quantityRemaining = itemToAdd.quantity; // Use a variable to track quantity being added (only relevant for stackables)

            // --- Attempt to stack with existing items ---
            // Only attempt stacking if the item type is stackable (maxStack > 1)
            if (itemToAdd.details.maxStack > 1)
            {
                Debug.Log($"Combiner ({gameObject.name}): Attempting to stack {itemToAdd.details.Name} (Initial Qty: {quantityRemaining}).");

                // Iterate through physical slots to find existing stacks
                for (int i = 0; i < PhysicalSlotCount; i++)
                {
                    Item itemInSlot = inventoryState[i]; // Get item instance in the slot

                    // Check if the slot has an item AND if the item being added can stack with it
                    if (itemInSlot != null && itemInSlot.CanStackWith(itemToAdd)) // Use the CanStackWith helper method
                    {
                        // Calculate how much space is left in this stack
                        int spaceInStack = itemInSlot.details.maxStack - itemInSlot.quantity;

                        // Determine how many items we can take from the remaining quantity to fill this stack
                        int numToStack = Mathf.Min(quantityRemaining, spaceInStack);

                        if (numToStack > 0)
                        {
                            // Add the quantity to the existing stack
                            itemInSlot.quantity += numToStack;

                            // Reduce the quantity remaining to be added
                            quantityRemaining -= numToStack;

                            // Notify the observable array that this slot's item has changed (quantity updated)
                            inventoryState.SetItemAtIndex(itemInSlot, i);

                            Debug.Log($"Combiner ({gameObject.name}): Stacked {numToStack} of {itemToAdd.details.Name} into slot {i}. Remaining to add: {quantityRemaining}.");

                            // If the remaining quantity is now 0 or less, we are done
                            if (quantityRemaining <= 0)
                            {
                                Debug.Log($"Combiner ({gameObject.name}): Item fully added/stacked into existing slots.");
                                itemToAdd.quantity = 0; // Explicitly set the original item's quantity to 0 as it's fully consumed
                                return true; // Item successfully added (fully stacked)
                            }
                            // Else, continue the loop to find other stacks to add the remaining quantity to
                        }
                    }
                }
                // If the loop finishes and quantityRemaining is still > 0, it means the remaining quantity
                // could not be fully stacked into existing stacks.
            }
            // Note: If itemToAdd is non-stackable (maxStack == 1), the above 'if' is false,
            // and quantityRemaining starts at 1. The logic proceeds directly below.


            // --- If remaining quantity > 0 (for stackables) OR it's a non-stackable item (quantityRemaining is 1) ---
            // This condition correctly covers both cases.
            if (quantityRemaining > 0)
            {
                 Debug.Log($"Combiner ({gameObject.name}): Remaining quantity ({quantityRemaining}) or non-stackable item. Searching for empty slot(s).");
                // Iterate through physical slots to find an empty one
                for (int i = 0; i < PhysicalSlotCount; i++)
                {
                    // Check if the slot is empty
                    if (inventoryState[i] == null)
                    {
                         // Calculate how much of the remaining quantity can fit into a new stack (for stackables)
                         // or confirm it's just 1 for non-stackables.
                         int quantityForThisSlot = itemToAdd.details.maxStack > 1 ?
                                                   Mathf.Min(quantityRemaining, itemToAdd.details.maxStack) : // If stackable, add up to maxStack
                                                   quantityRemaining; // If not stackable, this will be 1

                         if (quantityForThisSlot > 0) // Should always be true here if quantityRemaining > 0
                         {
                             Item itemToPlace;
                             if (itemToAdd.details.maxStack > 1)
                             {
                                 // For stackable items, create a NEW instance with the partial quantity.
                                 // The original itemToAdd instance's quantity is reduced below.
                                 itemToPlace = itemToAdd.details.Create(quantityForThisSlot);
                             }
                             else // itemToAdd.details.maxStack == 1 (non-stackable)
                             {
                                 // For non-stackable items, place the ORIGINAL itemToAdd instance.
                                 // Its quantity is already 1 (set in the constructor), and its health is set.
                                 // We are placing the specific instance that was passed in.
                                 itemToPlace = itemToAdd;
                                 // quantityForThisSlot will be 1 in this case.
                             }

                             // Add this item instance to the empty slot
                             inventoryState.SetItemAtIndex(itemToPlace, i); // Triggers SlotUpdated

                             // Reduce the quantity remaining to be added from the *original* itemToAdd instance
                             quantityRemaining -= quantityForThisSlot; // This correctly reduces by quantityForThisSlot (1 for non-stackable)

                             Debug.Log($"Combiner ({gameObject.name}): Added {(itemToAdd.details.maxStack > 1 ? quantityForThisSlot.ToString() + " of " : "")}{itemToAdd.details.Name} to empty physical slot {i}. Remaining to add: {quantityRemaining}.");

                             // If the remaining quantity is now 0 or less, we are done adding the item
                             if (quantityRemaining <= 0)
                             {
                                 Debug.Log($"Combiner ({gameObject.name}): Item fully added into new slot(s).");
                                 itemToAdd.quantity = 0; // Explicitly set original item's quantity to 0 (it was 1, now 0 for non-stackable)
                                 return true; // Item successfully added
                             }
                             // Else, continue the loop to find the next empty slot for the remaining quantity (only relevant for stackable items adding multiple stacks)
                         }
                    }
                }

                // --- If no empty slot found and quantity still remains ---
                // If we reach here, it means no empty physical slot was found for the remaining quantity.
                Debug.LogWarning($"Combiner ({gameObject.name}): Failed to add item: {itemToAdd.details.Name}. All {PhysicalSlotCount} physical slots are full. Remaining quantity: {quantityRemaining}.");
                 // Note: At this point, the original 'itemToAdd' still has 'quantityRemaining' left (which is 1 for non-stackable if it failed to add).
                 // Depending on your Drag and Drop system, you might need to handle this remaining item (e.g., return it to the player's cursor).
                itemToAdd.quantity = quantityRemaining; // Update the original item's quantity to reflect what wasn't added.
                return false; // No empty slot found or item could not be fully added
            }
            else
            {
                // This case is reached if the item was fully consumed by stacking in (only possible for stackables).
                Debug.Log($"Combiner ({gameObject.name}): AddItem finished, quantity remaining is 0 after stacking.");
                itemToAdd.quantity = 0; // Ensure original item quantity is 0
                return true; // Already fully stacked
            }
        }

        /// <summary>
        /// Attempts to remove the item from a specific physical slot index.
        /// </summary>
        /// <param name="index">The index of the physical slot to remove from (0-based, excludes ghost slot).</param>
        /// <returns>True if an item was removed from the specified index, false otherwise (e.g., index out of bounds or slot was empty).</returns>
        public bool TryRemoveAt(int index)
        {
            // Ensure the index is within the physical slot range
            if (index < 0 || index >= PhysicalSlotCount)
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): TryRemoveAt - Index {index} is out of physical bounds (0-{PhysicalSlotCount-1}).");
                return false;
            }

            // Check if the slot actually contains an item
            // We can access directly via the indexer which uses the internal array
            if (inventoryState[index] == null)
            {
                Debug.Log($"Combiner ({gameObject.name}): TryRemoveAt - Slot {index} is already empty.");
                return false; // Slot is already empty
            }

            // Use the ObservableArray's RemoveAt method to clear the slot
            inventoryState.RemoveAt(index);

            Debug.Log($"Combiner ({gameObject.name}): Successfully removed item from slot {index}.");
            return true;
        }

        /// <summary>
        /// Attempts to remove a specific quantity of an item type from the inventory.
        /// Searches physical slots first, removing from stacks as needed.
        /// This method is primarily for stackable items. Removing non-stackable items by quantity is less common.
        /// </summary>
        /// <param name="itemType">The ItemDetails of the type to remove.</param>
        /// <param name="quantityToRemove">The desired quantity to remove.</param>
        /// <returns>The actual quantity of the item type successfully removed.</returns>
        public int TryRemoveQuantity(ItemDetails itemType, int quantityToRemove)
        {
            if (itemType == null)
            {
                Debug.LogWarning("Combiner ({gameObject.name}): TryRemoveQuantity - Cannot remove quantity of a null item type.");
                return 0;
            }
            if (quantityToRemove <= 0)
            {
                 Debug.LogWarning($"Combiner ({gameObject.name}): TryRemoveQuantity - Quantity to remove must be positive ({quantityToRemove}).");
                 return 0;
            }

            int quantityRemoved = 0;

            // Iterate through physical slots to find matching item types
            for (int i = 0; i < PhysicalSlotCount; i++)
            {
                Item itemInSlot = inventoryState[i]; // Get item instance

                // Check if the slot has an item and it's the same type
                // This method is primarily designed for stackable items.
                // If it finds a non-stackable item of the same type, it will remove it if quantityToRemove is 1.
                if (itemInSlot != null && itemInSlot.details != null && itemInSlot.details == itemType) // Use ItemDetails == operator
                {
                    // Determine how many to take from this stack/slot
                    int canTake = itemInSlot.details.maxStack > 1 ? itemInSlot.quantity : 1; // Take quantity for stackable, 1 for non-stackable
                    int numToTake = Mathf.Min(quantityToRemove - quantityRemoved, canTake);

                    if (numToTake > 0)
                    {
                        // For stackable items, reduce quantity
                        if (itemInSlot.details.maxStack > 1)
                        {
                             itemInSlot.quantity -= numToTake; // Reduce quantity
                        }
                        // For non-stackable items, numToTake will be 1, and we'll remove the whole item instance below.
                        // No quantity reduction needed on the instance itself as quantity is fixed at 1.

                        quantityRemoved += numToTake; // Track total removed

                        // If stackable quantity <= 0 OR it's a non-stackable item (numToTake was 1)
                        if ((itemInSlot.details.maxStack > 1 && itemInSlot.quantity <= 0) || itemInSlot.details.maxStack == 1)
                        {
                            // If stack is depleted, remove the item instance completely from the slot
                            inventoryState.RemoveAt(i); // Use RemoveAt which triggers event
                             Debug.Log($"Combiner ({gameObject.name}): Removed {(itemInSlot.details.maxStack > 1 ? "stack" : "item instance")} of {itemType.Name} from slot {i} (depleted/removed).");
                        }
                        else if (itemInSlot.details.maxStack > 1) // Only update slot if stackable quantity was reduced but not depleted
                        {
                            // If stackable quantity is just reduced, update the slot to trigger UI update
                            inventoryState.SetItemAtIndex(itemInSlot, i);
                             Debug.Log($"Combiner ({gameObject.name}): Reduced stack of {itemType.Name} in slot {i} by {numToTake}. Remaining: {itemInSlot.quantity}.");
                        }
                    }

                    // Stop if we've removed the desired quantity
                    if (quantityRemoved >= quantityToRemove)
                    {
                        break;
                    }
                }
            }

            if (quantityRemoved > 0)
            {
                 Debug.Log($"Combiner ({gameObject.name}): Finished removing {quantityRemoved} of {itemType.Name}.");
            }
            else
            {
                 Debug.Log($"Combiner ({gameObject.name}): Could not find any {itemType.Name} to remove.");
            }


            return quantityRemoved; // Return how many were actually removed
        }

        /// <summary>
        /// Attempts to remove a specific item instance from the inventory by matching its unique instance Id.
        /// Searches all slots (including potentially ghost), but typically used with physical slots.
        /// </summary>
        /// <param name="itemInstance">The exact Item instance to remove.</param>
        /// <returns>True if the specific item instance was found and removed, false otherwise.</returns>
        public bool TryRemoveInstance(Item itemInstance)
        {
             if (itemInstance == null)
            {
                Debug.LogWarning("Combiner ({gameObject.name}): TryRemoveInstance - Cannot remove a null item instance.");
                return false;
            }

            // Use the ObservableArray's TryRemove method.
            // This method internally iterates and uses EqualityComparer<Item>.Default.Equals,
            // which, because we implemented IEquatable<Item> on Item based on its Id,
            // will find the exact instance with the matching Item.Id.
            bool removed = inventoryState.TryRemove(itemInstance);

            if (removed)
            {
                 Debug.Log($"Combiner ({gameObject.name}): Successfully removed specific item instance with ID {itemInstance.Id}.");
            }
            else
            {
                 Debug.LogWarning($"Combiner ({gameObject.name}): Specific item instance with ID {itemInstance.Id} not found in inventory.");
            }

            return removed;
        }

        // TODO: Add methods here later for initial item population triggered by game events
        // e.g., public void AddInitialItem(Item item, int targetSlotIndex = -1) { ... }
    }
}