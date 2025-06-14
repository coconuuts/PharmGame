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

        public Inventory ParentInventory { get; internal set; }

        [Header("Item Filtering")] // Optional: A header for organization in the inspector
        [SerializeField] private List<ItemLabel> allowedLabels = new List<ItemLabel>();
        [Tooltip("If this list is empty, all item labels are allowed.")] // Clarify behavior
        [SerializeField] private bool allowAllIfListEmpty = true; // Option to allow all if the list is left empty


        private void OnValidate()
        {
            if (id == SerializableGuid.Empty)
            {
                id = SerializableGuid.NewGuid();
#if UNITY_EDITOR
                Debug.Log($"Inventory ({gameObject.name}): Assigned new unique ID in OnValidate: {id}", this);
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
                InitializeInventoryData(); // Initialize data structure based on layout
                FindAndAssignSlotUIs(); // Find and link UI slots

                 // --- Register this inventory with the DragAndDropManager ---
                 // This registration should ideally happen in Inventory.cs Awake/OnEnable/OnDisable
                 // as Inventory is the public interface. Moving this registration logic to Inventory.cs.
                 // DragAndDropManager.RegisterInventory(this.ParentInventory); // Needs ParentInventory set first
            }
        }

        // Moved data initialization here from Awake to ensure FlexibleGridLayout is found first
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


         private void FindAndAssignSlotUIs()
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
                    // slotUI.ParentInventory = this.ParentInventory; // This would be done by Inventory
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
            // Only attempt to link if we have Combiner and Visualizer
            if (inventoryState != null && visualizer != null) // Check inventoryState instead of combiner
            {
                 visualizer.SetInventoryState(inventoryState); // This also triggers InitialLoad

                 Debug.Log($"Combiner '{id}' fully initialized and linked components.", this);
            }
            else
            {
                Debug.LogError($"Combiner ({gameObject.name}): Cannot fully initialize in Start because InventoryState or Visualizer is null. Check previous Awake/InitializeInventoryData logs.", this);
            }
        }

        private void OnDestroy()
        {
             // Ensure unsubscribe from ObservableArray events if Visualizer is destroyed separately
             // Visualizer's OnDestroy handles its own unsubscription, which is cleaner.
        }

        /// <summary>
        /// Checks if an item is allowed to be placed in this inventory based on its ItemLabel.
        /// This method is called by ParentInventory.CanAddItem, which is the public interface.
        /// It's kept here for internal Combiner logic that might need it (like TryAddQuantity).
        /// </summary>
        internal bool CheckFiltering(Item item) // Changed to internal as ParentInventory is the public access
        {
             // Null checks for safety
            if (item == null || item.details == null)
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): Attempted to check null or detail-less item for filtering.");
                return false; // Cannot add a null or detail-less item
            }

            // If the allowed list is null or empty and allowAllIfListEmpty is true, bypass filtering
            if (allowedLabels == null || allowedLabels.Count == 0)
            {
                return allowAllIfListEmpty;
            }

            // Check if the item's label is in the allowed list
            return allowedLabels.Contains(item.details.itemLabel);
        }


        /// <summary>
        /// Attempts to add a quantity of a stackable item or a single non-stackable item instance
        /// to the inventory's physical slots. Prioritizes stacking, then empty slots.
        /// Modifies the quantity of the input 'itemToAdd' instance to reflect any quantity
        /// that could *not* be added.
        /// </summary>
        /// <param name="itemToAdd">The Item instance containing the quantity/instance to add. Its quantity will be reduced by the amount successfully added.</param>
        /// <param name="maxQuantityToAttempt">Optional: The maximum quantity to attempt to add from the itemToAdd instance. If -1, attempts to add itemToAdd.quantity.</param>
        /// <returns>The actual quantity of the item successfully added to the inventory.</returns>
        public int TryAddQuantity(Item itemToAdd, int maxQuantityToAttempt = -1)
        {
            // Basic validation
            if (itemToAdd == null || itemToAdd.details == null)
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): TryAddQuantity - Attempted to add a null or detail-less item.");
                return 0;
            }

            // Check filtering *before* attempting to add anything
            // Use the internal CheckFiltering method
            if (!CheckFiltering(itemToAdd))
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): TryAddQuantity - Item '{itemToAdd.details?.Name ?? "Unknown"}' with label '{itemToAdd.details?.itemLabel.ToString() ?? "None"}' is not allowed in this inventory.");
                // itemToAdd.quantity remains unchanged here as nothing was added
                return 0; // Item type not allowed, reject addition
            }

            int quantityToAttempt = (maxQuantityToAttempt == -1 || maxQuantityToAttempt > itemToAdd.quantity) ? itemToAdd.quantity : maxQuantityToAttempt;
            int quantityRemainingToAttempt = quantityToAttempt;
            int quantityAdded = 0;

            if (quantityToAttempt <= 0)
            {
                 Debug.Log($"Combiner ({gameObject.name}): TryAddQuantity - Quantity to attempt is 0 or less ({quantityToAttempt}). Nothing to add.");
                 // itemToAdd.quantity remains unchanged
                 return 0;
            }

            Debug.Log($"Combiner ({gameObject.name}): Attempting to add {quantityToAttempt} of {itemToAdd.details.Name}.");


            // --- Handle Non-Stackable Items (maxStack == 1) ---
            // For non-stackable items, we can only add the single instance.
            // We only proceed if quantityToAttempt is 1 (which it should be for a non-stackable instance).
            if (itemToAdd.details.maxStack == 1)
            {
                 if (quantityToAttempt > 1)
                 {
                      Debug.LogWarning($"Combiner ({gameObject.name}): TryAddQuantity - Attempted to add quantity > 1 ({quantityToAttempt}) for non-stackable item '{itemToAdd.details.Name}'. Only attempting to add 1 instance.");
                      quantityToAttempt = 1; // Clamp to 1 for non-stackable
                      quantityRemainingToAttempt = 1;
                 }

                 // Find the first empty physical slot
                 for (int i = 0; i < PhysicalSlotCount; i++)
                 {
                     if (inventoryState[i] == null)
                     {
                         // Place the original item instance into the empty slot
                         inventoryState.SetItemAtIndex(itemToAdd, i); // Triggers SlotUpdated
                         quantityAdded = 1;
                         quantityRemainingToAttempt = 0; // Fully added the instance
                         Debug.Log($"Combiner ({gameObject.name}): Added non-stackable item '{itemToAdd.details.Name}' to empty physical slot {i}.");
                         break; // Found a slot and added the instance
                     }
                 }

                 // Update the quantity of the original item instance (will be 0 if added, 1 if not)
                 itemToAdd.quantity = quantityRemainingToAttempt;

                 return quantityAdded; // Return 1 if added, 0 if not
            }


            // --- Handle Stackable Items (maxStack > 1) ---
            // Attempt to stack with existing items first
            Debug.Log($"Combiner ({gameObject.name}): Attempting to stack {itemToAdd.details.Name} (Initial Qty to Attempt: {quantityRemainingToAttempt}).");

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
                            Debug.Log($"Combiner ({gameObject.name}): Item fully added/stacked into existing slots.");
                            // itemToAdd.quantity will be updated below
                            break; // Exit the stacking loop
                        }
                        // Else, continue the loop to find other stacks to add the remaining quantity to
                    }
                }
            }
            // If the loop finishes and quantityRemainingToAttempt is still > 0, it means the remaining quantity
            // could not be fully stacked into existing stacks.


            // --- If quantity still remains, find empty slots ---
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
                                 // itemToAdd.quantity will be updated below
                                 break; // Exit the empty slot loop
                             }
                             // Else, continue the loop to find the next empty slot for the remaining quantity
                         }
                    }
                }
            }

            // --- Update the quantity of the original item instance ---
            // This is crucial. The item instance passed IN now reflects what *couldn't* be added.
            itemToAdd.quantity = quantityRemainingToAttempt;

            Debug.Log($"Combiner ({gameObject.name}): TryAddQuantity finished. Added {quantityAdded}. Remaining on original item: {itemToAdd.quantity}.");

            return quantityAdded; // Return how many were actually added
        }


        /// <summary>
        /// Attempts to add a pre-created item instance to the inventory.
        /// Prioritizes stacking with existing compatible stacks before finding an empty slot
        /// and creating new stacks if the quantity exceeds maxStack.
        /// Only adds to physical slots (excluding the ghost slot).
        /// Handles stackable items by quantity and non-stackable items by placing the instance.
        /// Returns true only if the *entire* quantity of the input item was added.
        /// </summary>
        /// <param name="itemToAdd">The Item instance to add (its quantity will be reduced as it's added).</param>
        /// <returns>True if the item was fully added (either stacked or placed in empty slot/new stacks), false otherwise (inventory full).</returns>
        // NOTE: This method's behavior is different from TryAddQuantity. It aims to add the *entire* item instance's quantity.
        // It will be kept as is for compatibility with other potential callers (like crafting output).
        public bool AddItem(Item itemToAdd)
        {
            // Basic validation
            if (itemToAdd == null || itemToAdd.details == null || (itemToAdd.details.maxStack > 1 && itemToAdd.quantity <= 0) || (itemToAdd.details.maxStack == 1 && itemToAdd.health <= 0 && itemToAdd.details.maxHealth > 0))
            {
                 // Check quantity for stackable, or health for durable non-stackable
                Debug.LogWarning($"Combiner ({gameObject.name}): Attempted to add a null, detail-less, or zero/negative quantity/health item. Item: {itemToAdd?.details?.Name ?? "NULL"}, Qty: {itemToAdd?.quantity ?? 0}, Health: {itemToAdd?.health ?? 0}, MaxStack: {itemToAdd?.details?.maxStack ?? 0}, MaxHealth: {itemToAdd?.details?.maxHealth ?? 0}");
                return false;
            }

            // Check filtering *before* attempting to add anything
             // Use the internal CheckFiltering method
            if (!CheckFiltering(itemToAdd))
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): AddItem - Item '{itemToAdd.details?.Name ?? "Unknown"}' with label '{itemToAdd.details?.itemLabel.ToString() ?? "None"}' is not allowed in this inventory.");
                return false; // Item type not allowed, reject addition
            }


            // Use the new TryAddQuantity method to perform the addition logic
            // We attempt to add the full current quantity of the itemToAdd instance.
            int originalQuantity = itemToAdd.quantity;
            int quantityAdded = TryAddQuantity(itemToAdd, originalQuantity); 

            // Return true only if the entire original quantity was added
            bool fullyAdded = (quantityAdded == originalQuantity);

            if (!fullyAdded)
            {
                 Debug.LogWarning($"Combiner ({gameObject.name}): AddItem failed to add the entire quantity. Added {quantityAdded} out of {originalQuantity}. Remaining on original item: {itemToAdd.quantity}.");
            }
            else
            {
                 Debug.Log($"Combiner ({gameObject.name}): AddItem successfully added the entire quantity ({quantityAdded}). Original item quantity is now {itemToAdd.quantity}.");
            }

            return fullyAdded;
        }

        /// <summary>
        /// Attempts to add a quantity of a stackable item *only* to the specific target slot provided.
        /// Does NOT search for other stacks or empty slots if the target slot fills up.
        /// Modifies the quantity of the input 'itemToAdd' instance to reflect any quantity
        /// that could *not* be added to the target slot.
        /// This method is intended for specific interactions like quick transfer onto an existing stack.
        /// </summary>
        /// <param name="itemToAdd">The Item instance containing the quantity to add. Its quantity will be reduced by the amount successfully added.</param>
        /// <param name="targetSlotIndex">The index of the physical slot in this inventory to attempt stacking into.</param>
        /// <returns>The actual quantity of the item successfully added to the specific target slot.</returns>
        internal int TryStackQuantityToSpecificSlot(Item itemToAdd, int targetSlotIndex)
        {
             // Basic validation
            if (itemToAdd == null || itemToAdd.details == null || itemToAdd.details.maxStack <= 1)
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): TryStackQuantityToSpecificSlot - Attempted to stack a null, detail-less, or non-stackable item, or item quantity is <= 0.");
                return 0;
            }

             // Ensure the target index is within the physical slot range
            if (targetSlotIndex < 0 || targetSlotIndex >= PhysicalSlotCount)
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): TryStackQuantityToSpecificSlot - Target index {targetSlotIndex} is out of physical bounds (0-{PhysicalSlotCount-1}).");
                return 0;
            }

            // Check filtering *before* attempting to add anything
            // Use the internal CheckFiltering method
            if (!CheckFiltering(itemToAdd))
            {
                Debug.LogWarning($"Combiner ({gameObject.name}): TryStackQuantityToSpecificSlot - Item '{itemToAdd.details?.Name ?? "Unknown"}' with label '{itemToAdd.details?.itemLabel.ToString() ?? "None"}' is not allowed in this inventory.");
                // itemToAdd.quantity remains unchanged here as nothing was added
                return 0; // Item type not allowed, reject addition
            }

            Item itemInTargetSlot = inventoryState[targetSlotIndex]; // Get item instance in the target slot

            // Check if the target slot has an item AND if the item being added can stack with it
            if (itemInTargetSlot == null || !itemInTargetSlot.CanStackWith(itemToAdd))
            {
                 Debug.LogWarning($"Combiner ({gameObject.name}): TryStackQuantityToSpecificSlot - Target slot {targetSlotIndex} is empty or contains a non-stackable/different item. Cannot perform specific stacking.");
                 // itemToAdd.quantity remains unchanged
                 return 0; // Target is not a valid stack target
            }

            int quantityToAttempt = itemToAdd.quantity;
            int quantityAdded = 0;

             if (quantityToAttempt <= 0)
            {
                 Debug.Log($"Combiner ({gameObject.name}): TryStackQuantityToSpecificSlot - Quantity to attempt is 0 or less ({quantityToAttempt}). Nothing to add.");
                 // itemToAdd.quantity remains unchanged
                 return 0;
            }


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
                 // itemToAdd.quantity remains unchanged
            }

            // Note: We DO NOT search for other slots here. Any remaining quantity is left on itemToAdd.

            Debug.Log($"Combiner ({gameObject.name}): TryStackQuantityToSpecificSlot finished. Added {quantityAdded} to slot {targetSlotIndex}. Remaining on original item: {itemToAdd.quantity}.");

            return quantityAdded; // Return how many were actually added to THIS slot
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