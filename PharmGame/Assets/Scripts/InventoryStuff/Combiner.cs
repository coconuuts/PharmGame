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
        [SerializeField] private SerializableGuid id = SerializableGuid.Empty; // Added missing id field
        [SerializeField] private Visualizer visualizer; // Added missing visualizer field

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

        // This reference is set by the Parent Inventory script.
        public Inventory ParentInventory { get; internal set; }

        // REMOVED: allowedLabels and allowAllIfListEmpty fields. These belong on the Inventory script.


        private void OnValidate()
        {
            if (id == SerializableGuid.Empty) // Uses the overloaded == operator if SerializableGuid supports it
            {
                id = SerializableGuid.NewGuid();
#if UNITY_EDITOR
                Debug.Log($"Combiner ({gameObject.name}): Assigned new unique ID in OnValidate: {id}", this);
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }

        private void Awake()
        {
            if (flexibleGridLayout == null) flexibleGridLayout = GetComponent<FlexibleGridLayout>();
            if (visualizer == null) visualizer = GetComponent<Visualizer>(); // Get Visualizer

            if (flexibleGridLayout == null || visualizer == null) // Check required components
            {
                Debug.LogError($"Combiner on {gameObject.name} is missing required components (FlexibleGridLayout or Visualizer). Inventory functionality may be limited.", this);
                // Don't disable immediately in Awake, let Start potentially catch it
            }
            else
            {
                // Data initialization is now public and called by Inventory.Awake
                // InitializeInventoryData(); // Called by Inventory
                // Find and assign slot UIs is now public and called by Inventory.Awake
                // FindAndAssignSlotUIs(); // Called by Inventory

                 // Registration with DragAndDropManager is handled by Inventory
            }
        }

        // Moved data initialization here from Awake to ensure FlexibleGridLayout is found first
        // Made public so Inventory can call it after setting ParentInventory
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

            // --- IMPORTANT ---
            // Assign the ParentInventory reference to the ObservableArray as soon as it's created
            // This allows the ObservableArray to access the Inventory's filtering rules via the Combiner
            if (inventoryState != null)
            {
                 inventoryState.ParentInventory = this.ParentInventory;
                 Debug.Log($"Combiner ({gameObject.name}): Initialized inventory data with {PhysicalSlotCount} physical slots and {TotalDataSlotCount} total data slots (including ghost). ObservableArray created: {(inventoryState != null ? "NOT NULL" : "NULL")}, Parent Inventory set on OA.", this);
            }
            else
            {
                 Debug.LogError($"Combiner ({gameObject.name}): Failed to create ObservableArray during InitializeInventoryData.", this);
            }


            // TODO: Add logic here later for loading saved inventory state if applicable
        }


         // This method is now public and called by Inventory.Awake
         public void FindAndAssignSlotUIs()
        {
             if (flexibleGridLayout == null)
             {
                 Debug.LogError($"Combiner ({gameObject.name}): Cannot find and assign SlotUIs, FlexibleGridLayout is null.", this);
                 return;
             }

             List<InventorySlotUI> foundSlots = new List<InventorySlotUI>();

             for (int i = 0; i < flexibleGridLayout.transform.childCount; i++)
             {
                GameObject slotGameObject = flexibleGridLayout.transform.GetChild(i).gameObject;
                InventorySlotUI slotUI = slotGameObject.GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    slotUI.SlotIndex = i; // Assign index
                    // ParentInventory is assigned by the Inventory script after Combiner is found
                    slotUI.ParentInventory = this.ParentInventory; // Assign Parent Inventory Reference on the SlotUI as well
                    foundSlots.Add(slotUI);
                     // Debug.Log($"Combiner: Found and assigned InventorySlotUI at index {i} on {slotGameObject.name}.", this); // Optional: very verbose
                }
                else
                {
                    Debug.LogWarning($"Combiner ({gameObject.name}): Child {i} ({slotGameObject.name}) of FlexibleGridLayout is missing InventorySlotUI component.", slotGameObject);
                }
             }

             // Pass the found list to the Visualizer (Visualizer needs a method to accept this list)
             if (visualizer != null)
             {
                 visualizer.SetSlotUIComponents(foundSlots);
             }
             else
             {
                  Debug.LogError($"Combiner ({gameObject.name}): Visualizer is null, cannot pass slot UI list.", this);
             }
        }


        private void Start()
        {
            // Only attempt to link if we have InventoryState and Visualizer
            if (inventoryState != null && visualizer != null) // Check inventoryState instead of combiner
            {
                 visualizer.SetInventoryState(inventoryState); // This also triggers InitialLoad

                 Debug.Log($"Combiner '{id}' fully initialized and linked components.", this);
            }
            else
            {
                // This error is likely already logged by Inventory or in InitializeInventoryData
                // Debug.LogError($"Combiner ({gameObject.name}): Cannot fully initialize in Start because InventoryState or Visualizer is null. Check previous Awake/InitializeInventoryData logs.", this);
            }
        }

        private void OnDestroy()
        {
             // Visualizer's OnDestroy handles its own unsubscription.
        }

        /// <summary>
        /// Checks if an item is allowed to be placed in this inventory based on its ItemLabel.
        /// This method accesses the filtering rules from the ParentInventory.
        /// It is called by ParentInventory.CanAddItem, which is the public interface,
        /// and potentially by other internal Combiner methods if needed (though removed redundant calls).
        /// </summary>
        internal bool CheckFiltering(Item item) // Changed to internal as ParentInventory is the public access
        {
             // --- ACCESS FILTERING RULES FROM PARENT INVENTORY ---
             if (ParentInventory == null)
             {
                 Debug.LogError($"Combiner ({gameObject.name}): Cannot check filtering, ParentInventory reference is null.", this);
                 return false;
             }

             // Null checks for safety
            if (item == null || item.details == null)
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): Attempted to check null or detail-less item for filtering.");
                return false; // Cannot add a null or detail-less item
            }

            // Get filtering rules from ParentInventory
            List<ItemLabel> parentAllowedLabels = ParentInventory.AllowedLabels; // Use the public property
            bool parentAllowAllIfListEmpty = ParentInventory.AllowAllIfListEmpty; // Use the public property


            // If the allowed list is null or empty and allowAllIfListEmpty is true, bypass filtering
            if (parentAllowedLabels == null || parentAllowedLabels.Count == 0)
            {
                return parentAllowAllIfListEmpty;
            }

            // Check if the item's label is in the allowed list
            return parentAllowedLabels.Contains(item.details.itemLabel);
        }


        /// <summary>
        /// Attempts to add a quantity of a stackable item to the inventory's physical slots.
        /// Prioritizes stacking, then empty slots.
        /// Modifies the quantity of the input 'itemToAdd' instance to reflect any quantity
        /// that could *not* be added.
        /// This method is for STACKABLE items ONLY (maxStack > 1).
        /// Filtering check should be done by the caller (e.g., Inventory.AddItem).
        /// </summary>
        /// <param name="itemToAdd">The stackable Item instance containing the quantity to add. Its quantity will be reduced by the amount successfully added.</param>
        /// <param name="maxQuantityToAttempt">Optional: The maximum quantity to attempt to add from the itemToAdd instance. If -1, attempts to add itemToAdd.quantity.</param>
        /// <returns>The actual quantity of the item successfully added to the inventory.</returns>
        internal int TryAddQuantity(Item itemToAdd, int maxQuantityToAttempt = -1) // Changed to internal
        {
            // --- RESTRICT TO STACKABLE ---
            if (itemToAdd == null || itemToAdd.details == null)
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): TryAddQuantity - Attempted to add a null or detail-less item.");
                return 0;
            }
             if (itemToAdd.details.maxStack <= 1)
             {
                  Debug.LogError($"Combiner ({gameObject.name}): TryAddQuantity called with non-stackable item '{itemToAdd.details.Name}'. This method is for stackable items only.", this);
                  return 0;
             }
            // --- END RESTRICT ---

            // REMOVED: Redundant filtering check here. Assume caller (Inventory.AddItem) has checked.


            int quantityToAttempt = (maxQuantityToAttempt == -1 || maxQuantityToAttempt > itemToAdd.quantity) ? itemToAdd.quantity : maxQuantityToAttempt;
            int quantityAdded = 0; // Track how much was successfully added
            int quantityRemainingToAttempt = quantityToAttempt; // Stackable remaining starts at quantityToAttempt

            if (quantityToAttempt <= 0)
            {
                 Debug.Log($"Combiner ({gameObject.name}): TryAddQuantity - Stackable quantity to attempt is 0 or less ({quantityToAttempt}). Nothing to add.");
                 return 0;
            }

            Debug.Log($"Combiner ({gameObject.name}): Attempting to add stackable {itemToAdd.details.Name} (Initial Qty to Attempt: {quantityRemainingToAttempt}).");

            // Attempt to stack with existing items first
            for (int i = 0; i < PhysicalSlotCount; i++)
            {
                Item itemInSlot = inventoryState[i]; // Get item instance in the slot

                // Check if the slot has an item AND if the item being added can stack with it
                if (itemInSlot != null && itemInSlot.CanStackWith(itemToAdd)) // Use the CanStackWith helper method
                {
                    // Calculate how much space is left in this stack
                    int spaceInStack = itemInSlot.details.maxStack - itemInSlot.quantity;

                    // Determine how many items we can take from the remaining quantity to fill this stack
                    int numToStack = Mathf.Min(quantityRemainingToAttempt, spaceInStack);

                    if (numToStack > 0)
                    {
                        // Add the quantity to the existing stack
                        itemInSlot.quantity += numToStack;

                        // Reduce the quantity remaining to be added
                        quantityRemainingToAttempt -= numToStack;

                        // Track total added
                        quantityAdded += numToStack;

                        // Notify the observable array that this slot's item has changed (quantity updated)
                        inventoryState.SetItemAtIndex(itemInSlot, i); // Triggers SlotUpdated

                        Debug.Log($"Combiner ({gameObject.name}): Stacked {numToStack} of {itemToAdd.details.Name} into slot {i}. Remaining to attempt: {quantityRemainingToAttempt}.");

                        // If the remaining quantity is now 0 or less, we are done
                        if (quantityRemainingToAttempt <= 0)
                        {
                            Debug.Log($"Combiner ({gameObject.name}): Item fully added/stacked into existing stacks.");
                            break; // Exit the stacking loop
                        }
                        // Else, continue the loop to find other stacks to add the remaining quantity to
                    }
                }
            }
            // If the loop finishes and quantityRemainingToAttempt is still > 0, it means the remaining quantity
            // could not be fully stacked into existing stacks.

            // If quantity still remains, find empty slots
            if (quantityRemainingToAttempt > 0)
            {
                 Debug.Log($"Combiner ({gameObject.name}): Remaining quantity ({quantityRemainingToAttempt}). Searching for empty slot(s).");
                // Iterate through physical slots to find an empty one
                for (int i = 0; i < PhysicalSlotCount; i++)
                {
                    // Check if the slot is empty
                    if (inventoryState[i] == null)
                    {
                         // Calculate how much of the remaining quantity can fit into a new stack
                         int quantityForThisSlot = Mathf.Min(quantityRemainingToAttempt, itemToAdd.details.maxStack);

                         if (quantityForThisSlot > 0) // Should always be true here if quantityRemainingToAttempt > 0
                         {
                             // Create a NEW instance with the partial quantity for this new stack.
                             // NOTE: This creates a *new* Item instance ID. If you needed to track the original instance,
                             // you would need a different approach, but for stackables, splitting quantity
                             // into new stacks usually implies new instances are fine.
                             Item itemToPlace = itemToAdd.details.Create(quantityForThisSlot);

                             // Add this item instance to the empty slot
                             inventoryState.SetItemAtIndex(itemToPlace, i); // Triggers SlotUpdated

                             // Reduce the quantity remaining to be added
                             quantityRemainingToAttempt -= quantityForThisSlot;

                             // Track total added
                             quantityAdded += quantityForThisSlot;

                             Debug.Log($"Combiner ({gameObject.name}): Added {quantityForThisSlot} of {itemToAdd.details.Name} to empty physical slot {i}. Remaining to attempt: {quantityRemainingToAttempt}.");

                             // If the remaining quantity is now 0 or less, we are done adding the item
                             if (quantityRemainingToAttempt <= 0)
                             {
                                 Debug.Log($"Combiner ({gameObject.name}): Item fully added into new slot(s).");
                                 break; // Exit the empty slot loop
                             }
                             // Else, continue the loop to find the next empty slot for the remaining quantity
                         }
                    }
                }
            }

            // --- Update the quantity of the original item instance (Stackable) ---
            // This is crucial for stackable items. The instance passed IN now reflects what *couldn't* be added.
            itemToAdd.quantity = quantityRemainingToAttempt;

            Debug.Log($"Combiner ({gameObject.name}): TryAddQuantity finished. Added {quantityAdded}. Remaining on original item (for caller reference): {itemToAdd.quantity}.");

            return quantityAdded; // Return how many were actually added
        }


        /// <summary>
        /// Attempts to add a pre-created STACKABLE item instance to the inventory.
        /// Prioritizes stacking with existing compatible stacks before finding empty slots
        /// and creating new stacks if the quantity exceeds maxStack.
        /// Only adds to physical slots (excluding the ghost slot).
        /// Returns true only if the *entire* quantity of the input item was added.
        /// This method is for STACKABLE items ONLY (maxStack > 1).
        /// Filtering check should be done by the caller (e.g., Inventory.AddItem).
        /// </summary>
        /// <param name="itemToAdd">The stackable Item instance to add. Its quantity will be reduced as it's added.</param>
        /// <returns>True if the item was fully added, false otherwise (inventory full or item type not allowed).</returns>
        public bool AddStackableItems(Item itemToAdd) // Renamed from AddItem
        {
            // --- RESTRICT TO STACKABLE ---
            if (itemToAdd == null || itemToAdd.details == null)
            {
                 Debug.LogWarning($"Combiner ({gameObject.name}): AddStackableItems - Attempted to add a null or detail-less item.");
                 return false;
            }
            if (itemToAdd.details.maxStack <= 1)
            {
                 Debug.LogError($"Combiner ({gameObject.name}): AddStackableItems called with non-stackable item '{itemToAdd.details.Name}'. This method is for stackable items only.", this);
                 return false;
            }
             if (itemToAdd.quantity <= 0) // Also check quantity for stackables
             {
                 Debug.LogWarning($"Combiner ({gameObject.name}): AddStackableItems - Attempted to add stackable item '{itemToAdd.details.Name}' with zero or negative quantity ({itemToAdd.quantity}).");
                 return false;
             }
            // --- END RESTRICT ---

            // REMOVED: Redundant filtering check here. Assume caller (Inventory.AddItem) has checked.

            int originalQuantity = itemToAdd.quantity; // Store original quantity

            // Call TryAddQuantity. It will reduce itemToAdd.quantity and return quantity added.
            int quantityAdded = TryAddQuantity(itemToAdd, originalQuantity);

            // Fully added if the remaining quantity on the input item is 0 or less.
            bool fullyAdded = (itemToAdd.quantity <= 0);

            if (fullyAdded)
            {
                 Debug.Log($"Combiner ({gameObject.name}): AddStackableItems successfully finished. Added {quantityAdded}. Original quantity: {originalQuantity}. Remaining on original input item: {itemToAdd.quantity}.");
            }
            else
            {
                 Debug.LogWarning($"Combiner ({gameObject.name}): AddStackableItems failed to add the entire quantity. Added {quantityAdded} (out of {originalQuantity}). Remaining on original input item: {itemToAdd.quantity}.");
            }

            return fullyAdded;
        }


        /// <summary>
        /// Attempts to add a single NON-STACKABLE item instance to the inventory.
        /// Finds the first available empty physical slot.
        /// This method is for NON-STACKABLE items ONLY (maxStack == 1).
        /// Filtering check should be done by the caller (e.g., Inventory.AddItem).
        /// </summary>
        /// <param name="itemToAdd">The non-stackable Item instance to add.</param>
        /// <returns>True if the item instance was successfully added, false otherwise (inventory full or item type not allowed).</returns>
        public bool AddSingleInstance(Item itemToAdd)
        {
            // --- RESTRICT TO NON-STACKABLE ---
            if (itemToAdd == null || itemToAdd.details == null)
            {
                 Debug.LogWarning($"Combiner ({gameObject.name}): AddSingleInstance - Attempted to add a null or detail-less item.");
                 return false;
            }
            if (itemToAdd.details.maxStack > 1)
            {
                 Debug.LogError($"Combiner ({gameObject.name}): AddSingleInstance called with stackable item '{itemToAdd.details.Name}'. This method is for non-stackable items only.", this);
                 return false;
            }
             // For non-stackable, check health if durable, or quantity (should be 1) if non-durable
             bool isDepletedDurable = itemToAdd.details.maxHealth > 0 && itemToAdd.health <= 0;
             bool isDepletedNonDurable = itemToAdd.details.maxHealth <= 0 && itemToAdd.quantity <= 0; // Quantity should be 1 for these

             if (isDepletedDurable || isDepletedNonDurable)
             {
                 Debug.LogWarning($"Combiner ({gameObject.name}): AddSingleInstance - Attempted to add a depleted non-stackable item '{itemToAdd.details.Name}' (Health: {itemToAdd.health}, Qty: {itemToAdd.quantity}).", this);
                 return false;
             }
            // --- END RESTRICT ---

            // REMOVED: Redundant filtering check here. Assume caller (Inventory.AddItem) has checked.

            Debug.Log($"Combiner ({gameObject.name}): Attempting to add single instance of non-stackable item '{itemToAdd.details.Name}'.");

            // Find the first empty physical slot
            for (int i = 0; i < PhysicalSlotCount; i++)
            {
                if (inventoryState[i] == null)
                {
                    // Place the original item instance into the empty slot
                    // Use SetItemAtIndex which triggers the event for the Visualizer
                    inventoryState.SetItemAtIndex(itemToAdd, i);
                    Debug.Log($"Combiner ({gameObject.name}): Added non-stackable item '{itemToAdd.details.Name}' to empty physical slot {i}.");

                    // This method DOES NOT modify the input itemToAdd.quantity.
                    // The caller (Inventory.AddItem) is responsible for managing the input item if this method returns true.
                    return true; // Successfully added the instance
                }
            }

            // If the loop finishes without finding an empty slot
            Debug.Log($"Combiner ({gameObject.name}): Could not add non-stackable item '{itemToAdd.details.Name}'. No empty slot found.");
            return false; // Inventory full
        }


        /// <summary>
        /// Attempts to add a quantity of a stackable item *only* to the specific target slot provided.
        /// Does NOT search for other stacks or empty slots if the target slot fills up.
        /// Modifies the quantity of the input 'itemToAdd' instance to reflect any quantity
        /// that could *not* be added to the target slot.
        /// This method is intended for specific interactions like quick transfer onto an existing stack.
        /// This method should ONLY be called for stackable items (maxStack > 1).
        /// Filtering check should be done by the caller (e.g., DragAndDropManager, ItemTransferHandler calling Inventory.CanAddItem).
        /// </summary>
        /// <param name="itemToAdd">The stackable Item instance containing the quantity to add. Its quantity will be reduced by the amount successfully added.</param>
        /// <param name="targetSlotIndex">The index of the physical slot in this inventory to attempt stacking into.</param>
        /// <returns>The actual quantity of the item successfully added to the specific target slot.</returns>
        internal int TryStackQuantityToSpecificSlot(Item itemToAdd, int targetSlotIndex)
        {
             // --- RESTRICT TO STACKABLE ---
            if (itemToAdd == null || itemToAdd.details == null)
            {
                 Debug.LogWarning($"Combiner ({gameObject.name}): TryStackQuantityToSpecificSlot - Attempted to stack a null or detail-less item.");
                 return 0;
            }
             if (itemToAdd.details.maxStack <= 1)
             {
                  Debug.LogError($"Combiner ({gameObject.name}): TryStackQuantityToSpecificSlot called with non-stackable item '{itemToAdd.details.Name}'. This method is for stackable items only.", this);
                  return 0;
             }
             if (itemToAdd.quantity <= 0) // Also check quantity for stackables
             {
                 Debug.Log($"Combiner ({gameObject.name}): TryStackQuantityToSpecificSlot - Quantity to attempt is 0 or less ({itemToAdd.quantity}). Nothing to add.");
                 return 0;
             }
            // --- END RESTRICT ---

             // Ensure the target index is within the physical slot range
            if (targetSlotIndex < 0 || targetSlotIndex >= PhysicalSlotCount)
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): TryStackQuantityToSpecificSlot - Target index {targetSlotIndex} is out of physical bounds (0-{PhysicalSlotCount-1}).");
                return 0;
            }

            // REMOVED: Redundant filtering check here. Assume caller (DragAndDropManager, ItemTransferHandler) has checked via Inventory.CanAddItem.

            Item itemInTargetSlot = inventoryState[targetSlotIndex]; // Get item instance in the target slot

            // Check if the target slot has an item AND if the item being added can stack with it
            // This check implicitly includes maxStack > 1 because of itemInTargetSlot.CanStackWith() and the early restriction check.
            if (itemInTargetSlot == null || !itemInTargetSlot.CanStackWith(itemToAdd))
            {
                 Debug.LogWarning($"Combiner ({gameObject.name}): TryStackQuantityToSpecificSlot - Target slot {targetSlotIndex} is empty or contains a non-stackable/different item. Cannot perform specific stacking.");
                 return 0; // Target is not a valid stack target
            }

            int quantityToAttempt = itemToAdd.quantity;
            int quantityAdded = 0;

            Debug.Log($"Combiner ({gameObject.name}): Attempting to stack {quantityToAttempt} of {itemToAdd.details.Name} into specific slot {targetSlotIndex}.");

            // Calculate how much space is left in this stack
            int spaceInStack = itemInTargetSlot.details.maxStack - itemInTargetSlot.quantity;

            // Determine how many items we can take from the remaining quantity to fill this stack
            int numToStack = Mathf.Min(quantityToAttempt, spaceInStack);

            if (numToStack > 0)
            {
                // Add the quantity to the existing stack
                itemInTargetSlot.quantity += numToStack;

                // Reduce the quantity remaining to be added (on the original item instance)
                itemToAdd.quantity -= numToStack;

                // Track total added
                quantityAdded += numToStack;

                // Notify the observable array that this slot's item has changed (quantity updated)
                inventoryState.SetItemAtIndex(itemInTargetSlot, targetSlotIndex); // Triggers SlotUpdated

                Debug.Log($"Combiner ({gameObject.name}): Stacked {numToStack} of {itemToAdd.details.Name} into slot {targetSlotIndex}. Remaining on itemToAdd: {itemToAdd.quantity}.");
            }
            else
            {
                 Debug.Log($"Combiner ({gameObject.name}): Specific target slot {targetSlotIndex} is already full for item '{itemToAdd.details.Name}'. Nothing stacked.");
            }

            // Note: We DO NOT search for other slots here. Any remaining quantity is left on itemToAdd.

            Debug.Log($"Combiner ({gameObject.name}): TryStackQuantityToSpecificSlot finished. Added {quantityAdded} to slot {targetSlotIndex}. Remaining on original item: {itemToAdd.quantity}.");

            return quantityAdded; // Return how many were actually added to THIS slot
        }


        /// <summary>
        /// Attempts to remove the item from a specific physical slot index.
        /// Works for both stackable and non-stackable items.
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

            // Remove the item using the ObservableArray method
            inventoryState.RemoveAt(index);

            Debug.Log($"Combiner ({gameObject.name}): Successfully removed item from slot {index}.");
            return true;
        }

        /// <summary>
        /// Attempts to remove a specific quantity of an item type from the inventory.
        /// Searches physical slots first, removing from stacks as needed for stackable items,
        /// and removing instances for non-stackable items.
        /// </summary>
        /// <param name="itemType">The ItemDetails of the type to remove.</param>
        /// <param name="quantityToRemove">The desired quantity to remove (for non-stackable, this should be 1 per instance).</param>
        /// <returns>The actual quantity of the item type successfully removed.</returns>
        public int TryRemoveQuantity(ItemDetails itemType, int quantityToRemove)
        {
            if (itemType == null)
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): TryRemoveQuantity - Cannot remove quantity of a null item type.");
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
                if (itemInSlot != null && itemInSlot.details != null && itemInSlot.details == itemType) // Use ItemDetails == operator
                {
                    // Determine how many to take from this stack/slot
                    // For stackable, take min of remaining needed and current quantity.
                    // For non-stackable, can only ever take 1 instance at a time.
                    int canTake = itemInSlot.details.maxStack > 1 ? itemInSlot.quantity : 1;
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

                        // If stackable quantity <= 0 OR it's a non-stackable item (numToTake was 1, so we're removing the instance)
                        if ((itemInSlot.details.maxStack > 1 && itemInSlot.quantity <= 0) || itemInSlot.details.maxStack == 1)
                        {
                            // If stack is depleted or it's a non-stackable instance being removed, remove the item instance completely from the slot
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
        /// Works for both stackable and non-stackable items.
        /// Searches all slots (including potentially ghost), but typically used with physical slots.
        /// </summary>
        /// <param name="itemInstance">The exact Item instance to remove.</param>
        /// <returns>True if the specific item instance was found and removed, false otherwise.</returns>
        public bool TryRemoveInstance(Item itemInstance)
        {
             if (itemInstance == null)
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): TryRemoveInstance - Cannot remove a null item instance.");
                return false;
            }

            bool removed = inventoryState.TryRemove(itemInstance); // ObservableArray.TryRemove uses Item.Equals (based on ID)

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

        /// <summary>
        /// Attempts to find a specific item instance by its unique instance Id and reduce its health.
        /// Primarily for non-stackable durable items.
        /// Notifies the ObservableArray of the change or removal.
        /// </summary>
        /// <param name="instanceId">The unique ID of the item instance to modify.</param>
        /// <param name="healthToReduce">The amount of health to reduce.</param>
        /// <returns>True if the instance was found and health was reduced (or item removed), false otherwise.</returns>
        public bool ReduceHealthOnInstance(SerializableGuid instanceId, int healthToReduce)
        {
            if (instanceId == SerializableGuid.Empty)
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): ReduceHealthOnInstance - Cannot reduce health for an empty instance ID.");
                return false;
            }
            if (healthToReduce <= 0)
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): ReduceHealthOnInstance - Health to reduce must be positive ({healthToReduce}).");
                return false;
            }
            if (inventoryState == null)
            {
                 Debug.LogError($"Combiner ({gameObject.name}): ReduceHealthOnInstance - InventoryState is null.");
                 return false;
            }

            // Iterate through all slots (physical and ghost) to find the instance
            for (int i = 0; i < inventoryState.Length; i++)
            {
                Item itemInSlot = inventoryState[i];

                // Check if the slot has an item and it matches the instance ID
                if (itemInSlot != null && itemInSlot.Id == instanceId)
                {
                    // Found the instance, now check if it's durable
                    if (itemInSlot.details == null || itemInSlot.details.maxHealth <= 0 || itemInSlot.details.maxStack > 1)
                    {
                        Debug.LogWarning($"Combiner ({gameObject.name}): ReduceHealthOnInstance - Found item '{itemInSlot.details?.Name ?? "Unknown"}' (ID: {instanceId}) but it is not a non-stackable durable item. Cannot reduce health.", this);
                        return false; // Not a durable item
                    }

                    // Reduce health, clamping at 0
                    int oldHealth = itemInSlot.health;
                    itemInSlot.health = Mathf.Max(0, itemInSlot.health - healthToReduce);
                    int actualReduced = oldHealth - itemInSlot.health;

                    Debug.Log($"Combiner ({gameObject.name}): Reduced health of '{itemInSlot.details.Name}' (ID: {instanceId}) by {actualReduced} (attempted {healthToReduce}). New health: {itemInSlot.health}.");

                    // Notify the ObservableArray of the change
                    if (itemInSlot.health <= 0)
                    {
                        // If health reached zero, remove the item instance completely
                        Debug.Log($"Combiner ({gameObject.name}): Item '{itemInSlot.details.Name}' (ID: {instanceId}) health reached zero. Removing instance from slot {i}.");
                        inventoryState.RemoveAt(i); // Use RemoveAt to trigger event
                    }
                    else
                    {
                        // Health reduced but still positive, just update the slot to trigger UI update
                        inventoryState.SetItemAtIndex(itemInSlot, i); // Triggers SlotUpdated
                    }

                    return true; // Instance found and health modified/item removed
                }
            }

            // If the loop finishes without finding the instance
            Debug.LogWarning($"Combiner ({gameObject.name}): ReduceHealthOnInstance - Item instance with ID {instanceId} not found in inventory.", this);
            return false; // Instance not found
        }


        /// <summary>
        /// Attempts to find a specific item instance by its unique instance Id and set its health.
        /// Primarily for non-stackable durable items.
        /// Notifies the ObservableArray of the change.
        /// </summary>
        /// <param name="instanceId">The unique ID of the item instance to modify.</param>
        /// <param name="healthToSet">The health value to set.</param>
        /// <returns>True if the instance was found and health was set, false otherwise.</returns>
        public bool SetHealthOnInstance(SerializableGuid instanceId, int healthToSet)
        {
             if (instanceId == SerializableGuid.Empty)
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): SetHealthOnInstance - Cannot set health for an empty instance ID.");
                return false;
            }
             if (inventoryState == null)
             {
                 Debug.LogError($"Combiner ({gameObject.name}): SetHealthOnInstance - InventoryState is null.");
                 return false;
             }

            // Iterate through all slots (physical and ghost) to find the instance
            for (int i = 0; i < inventoryState.Length; i++)
            {
                Item itemInSlot = inventoryState[i];

                // Check if the slot has an item and it matches the instance ID
                if (itemInSlot != null && itemInSlot.Id == instanceId)
                {
                    // Found the instance, now check if it's durable
                    if (itemInSlot.details == null || itemInSlot.details.maxHealth <= 0 || itemInSlot.details.maxStack > 1)
                    {
                        Debug.LogWarning($"Combiner ({gameObject.name}): SetHealthOnInstance - Found item '{itemInSlot.details?.Name ?? "Unknown"}' (ID: {instanceId}) but it is not a non-stackable durable item. Cannot set health.", this);
                        return false; // Not a durable item
                    }

                    // Use the Item's SetHealth method which handles clamping and gun logic
                    itemInSlot.SetHealth(healthToSet);

                    Debug.Log($"Combiner ({gameObject.name}): Set health of '{itemInSlot.details.Name}' (ID: {instanceId}) to {itemInSlot.health}.");

                    // Notify the ObservableArray of the change
                    // Even if health is 0 after setting, we don't remove here.
                    // Removal on health=0 is handled by the usage logic (ReduceHealthOnInstance)
                    // or other systems explicitly removing depleted items.
                    inventoryState.SetItemAtIndex(itemInSlot, i); // Triggers SlotUpdated

                    return true; // Instance found and health set
                }
            }

            // If the loop finishes without finding the instance
            Debug.LogWarning($"Combiner ({gameObject.name}): SetHealthOnInstance - Item instance with ID {instanceId} not found in inventory.", this);
            return false; // Instance not found
        }


        // TODO: Add methods here later for initial item population triggered by game events
        // e.g., public void AddInitialItem(Item item, int targetSlotIndex = -1) { ... }
    }
}