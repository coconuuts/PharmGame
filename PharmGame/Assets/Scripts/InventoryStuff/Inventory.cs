using UnityEngine;
using Systems.Inventory;
using System.Collections.Generic;
using System;
using Systems.Persistence;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Systems.Inventory
{
    public class Inventory : MonoBehaviour, IBind<InventoryData>, ISavableComponent
    {
        [SerializeField] private SerializableGuid id = SerializableGuid.Empty;
        [SerializeField] private Combiner combiner;
        [SerializeField] private FlexibleGridLayout flexibleGridLayout; // Still needed for FindAndAssignSlotUIs
        [SerializeField] private Visualizer visualizer; // Ensure this is assigned/found

        [Header("Item Filtering")] // Optional: A header for organization in the inspector
        [SerializeField] private List<ItemLabel> allowedLabels = new List<ItemLabel>();
        [Tooltip("If this list is empty, all item labels are allowed.")] // Clarify behavior
        [SerializeField] private bool allowAllIfListEmpty = true; // Option to allow all if the list is left empty

        // Public properties to expose filtering rules to the Combiner
        public List<ItemLabel> AllowedLabels => allowedLabels;
        public bool AllowAllIfListEmpty => allowAllIfListEmpty;

        // ISaveable and IBind implementation for the ID ---
        public SerializableGuid Id { get { return id; } set { id = value; } } 

        public Combiner Combiner => combiner;
        public ObservableArray<Item> InventoryState => combiner?.InventoryState; // Access through Combiner

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
            // Get component references
            if (combiner == null) combiner = GetComponent<Combiner>();
            // FlexibleGridLayout and Visualizer are also needed directly by Inventory now
            if (flexibleGridLayout == null) flexibleGridLayout = GetComponent<FlexibleGridLayout>();
            if (visualizer == null) visualizer = GetComponent<Visualizer>(); // Get Visualizer


            if (combiner == null || flexibleGridLayout == null || visualizer == null)
            {
                Debug.LogError($"Inventory on {gameObject.name} is missing required components (Combiner, FlexibleGridLayout, or Visualizer). Inventory functionality may be limited.", this);
                // Don't disable immediately in Awake, let Start potentially catch it
            }
            else
            {
                 // Assign parent reference *before* Combiner initializes the ObservableArray
                combiner.ParentInventory = this; // Assign parent reference

                // Initialize Combiner's data structure now that ParentInventory is set
                combiner.InitializeInventoryData();

                // Link the ObservableArray back to this Inventory instance (redundant if Combiner does it, but safe)
                 if(combiner.InventoryState != null)
                 {
                     combiner.InventoryState.ParentInventory = this; // *** Assign Parent Inventory to ObservableArray ***
                 }
                 else
                 {
                      Debug.LogError($"Inventory ({gameObject.name}): Combiner initialized with null InventoryState.", this);
                 }


                // Inventory is responsible for finding slots and linking them to the Visualizer and SlotUIs
                FindAndAssignSlotUIs(); // Find and link UI slots

                 // --- Register this inventory with the DragAndDropManager ---
                DragAndDropManager.RegisterInventory(this); // Moved registration here

            }
        }

        // Moved SlotUI finding logic here, called by Inventory.Awake
         private void FindAndAssignSlotUIs()
        {
             if (flexibleGridLayout == null)
             {
                 Debug.LogError($"Inventory ({gameObject.name}): Cannot find and assign SlotUIs, FlexibleGridLayout is null.", this);
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
                    slotUI.ParentInventory = this; // *** Assign Parent Inventory Reference ***
                    foundSlots.Add(slotUI);
                     // Debug.Log($"Inventory: Found and assigned InventorySlotUI at index {i} on {slotGameObject.name}.", this); // Optional: very verbose
                }
                else
                {
                    Debug.LogWarning($"Inventory ({gameObject.name}): Child {i} ({slotGameObject.name}) of FlexibleGridLayout is missing InventorySlotUI component.", slotGameObject);
                }
             }

             // Pass the found list to the Visualizer (Visualizer needs a method to accept this list)
             if (visualizer != null)
             {
                 visualizer.SetSlotUIComponents(foundSlots);
             }
             else
             {
                  Debug.LogError($"Inventory ({gameObject.name}): Visualizer is null, cannot pass slot UI list.", this);
             }
        }


        private void Start()
        {
            // Only attempt to link if we have Combiner and Visualizer and InventoryState
            if (combiner != null && visualizer != null && combiner.InventoryState != null)
            {
                 // Now link the Visualizer to the Combiner's ObservableArray
                 visualizer.SetInventoryState(combiner.InventoryState); // This also triggers InitialLoad

                 Debug.Log($"Inventory '{id}' fully initialized and linked components.", this);
            }
            else
            {
                Debug.LogError($"Inventory ({gameObject.name}): Cannot fully initialize in Start because Combiner, Visualizer, or InventoryState is null. Check previous Awake/InitializeInventoryData logs.", this);
            }
        }

         private void OnEnable()
         {
             // Register with the DragAndDropManager when the inventory becomes active
             // This is also done in Awake, but OnEnable is good for objects disabled/re-enabled
             // Check if already registered to avoid duplicates
              if (DragAndDropManager.Instance != null)
             {
                  DragAndDropManager.RegisterInventory(this);
             }
             else
             {
                  Debug.LogWarning($"Inventory ({gameObject.name}): DragAndDropManager instance not available in OnEnable. Cannot register.", this);
             }
         }

         private void OnDisable()
         {
              // Unregister from the DragAndDropManager when the inventory becomes inactive
              if (DragAndDropManager.Instance != null)
              {
                  DragAndDropManager.UnregisterInventory(this);
              }
         }


        private void OnDestroy()
        {
             // Unregister from DragAndDropManager if not already handled by OnDisable (e.g., scene change)
              if (DragAndDropManager.Instance != null)
              {
                  DragAndDropManager.UnregisterInventory(this);
              }

             // Ensure unsubscribe from ObservableArray events if Visualizer is destroyed separately
             // Visualizer's OnDestroy handles its own unsubscription, which is cleaner.
        }

        /// <summary>
        /// Generates the InventoryData structure from the current runtime state of this Inventory component.
        /// This replaces the logic previously in SaveLoadSystem.CreateInventoryDataFromComponent.
        /// </summary>
        public ISaveable CreateSaveData() 
        {
            // Check dependencies before proceeding
            if (combiner == null || flexibleGridLayout == null || visualizer == null)
            {
                Debug.LogError($"Inventory ({gameObject.name}, ID: {Id}): Cannot create save data, required components are missing.", this);
                return new InventoryData { Id = this.Id }; // Return minimal data
            }

            InventoryData invData = new InventoryData {
                Id = this.Id,
                allowedLabels = new List<ItemLabel>(this.AllowedLabels), // Copy the list
                allowAllIfListEmpty = this.AllowAllIfListEmpty,
                items = new List<ItemData>()
            };

            if (combiner.InventoryState != null) {
                // Only save items from physical slots, not the ghost slot.
                for (int i = 0; i < combiner.PhysicalSlotCount; i++) {
                    Item item = combiner.InventoryState[i];

                    invData.items.Add(ItemDataConverter.ToItemData(item)); 
                }
            }
            return invData;
        }

        /// <summary>
        /// Checks if an item is allowed to be placed in this inventory based on its ItemLabel.
        /// This method delegates the check to the Combiner, which now accesses the rules from this Inventory.
        /// This is the primary public method for checking filtering rules.
        /// </summary>
        public bool CanAddItem(Item item)
        {
            if (combiner == null)
            {
                Debug.LogError($"Inventory ({gameObject.name}): Cannot check filtering, Combiner is null.", this);
                return false;
            }
            // Delegate the check to the Combiner, which will read the rules from *this* Inventory instance
            return combiner.CheckFiltering(item);
        }

        /// <summary>
        /// Attempts to add an item instance(s) to the inventory using the appropriate Combiner method.
        /// Uses AddStackableItems for stackable, AddSingleInstance for non-stackable.
        /// This is the primary public method for adding items.
        /// </summary>
        /// <param name="itemToAdd">The Item instance to add.</param>
        /// <returns>True if the item was fully added, false otherwise (inventory full, filtering failed, or partial add for stackable).</returns>
        // NOTE: The quantity of the input itemToAdd will be reduced for stackable items if only partially added.
        // For non-stackable, the input itemToAdd parameter's quantity is NOT modified by this method.
        public bool AddItem(Item itemToAdd) // Restored AddItem name as the public interface
        {
             if (combiner == null)
             {
                  Debug.LogError($"Inventory ({gameObject.name}): Cannot add item, Combiner is null.", this);
                  return false;
             }
             if (itemToAdd == null || itemToAdd.details == null)
             {
                  Debug.LogWarning($"Inventory ({gameObject.name}): Attempted to add null or detail-less item.");
                  return false;
             }

             // --- Filtering check: This is the primary place where filtering is enforced for adding ---
             if (!CanAddItem(itemToAdd))
             {
                 Debug.LogWarning($"Inventory ({gameObject.name}): Cannot add item '{itemToAdd.details.Name}' with label '{itemToAdd.details.itemLabel.ToString()}'. Filtering not allowed.");
                 return false; // Filtering not allowed
             }

             bool success;
             if (itemToAdd.details.maxStack > 1)
             {
                  // Add stackable items - Combiner handles finding space/stacking/splitting
                  success = combiner.AddStackableItems(itemToAdd);
                  // itemToAdd.quantity is updated by AddStackableItems
                  if (!success && itemToAdd.quantity > 0)
                  {
                       // Partial add occurred
                       Debug.Log($"Inventory ({gameObject.name}): Partial add for stackable item '{itemToAdd.details.Name}'. {itemToAdd.quantity} remaining.");
                  }
             }
             else // maxStack == 1 (Non-stackable)
             {
                  // Add a single instance - Combiner handles finding an empty slot
                  // Store original quantity (should be 1) for logging/contract adherence
                  int originalQuantity = itemToAdd.quantity; // This is just for logging/debug
                  success = combiner.AddSingleInstance(itemToAdd); // Combiner.AddSingleInstance does NOT modify itemToAdd.quantity.

                  // --- MODIFIED: Removed the incorrect quantity = 0 setting ---
                  // The quantity of a non-stackable item instance should remain 1 as long as it exists.
                  // The input parameter is NOT used as a remainder for non-stackables.
                  if (success)
                  {
                       Debug.Log($"Inventory ({gameObject.name}): Added single instance of '{itemToAdd.details.Name}'. Original quantity was {originalQuantity}.");
                  }
                  else // AddSingleInstance returned false
                  {
                       // Log warning about failure to add single instance
                       Debug.LogWarning($"Inventory ({gameObject.name}): Failed to add single instance of '{itemToAdd.details.Name}'. Inventory full or item depleted.");
                       // itemToAdd.quantity remains its original value (likely 1), correctly indicating it was not added.
                  }
             }

             // --- MODIFIED: Adjusted return logic for non-stackable items ---
             // For stackable, return true if the original quantity was fully added (remaining quantity is 0).
             // For non-stackable, return true if the single instance was successfully added.
             if (itemToAdd.details.maxStack > 1)
             {
                 return itemToAdd.quantity <= 0; // Stackable: true if original quantity is all gone
             }
             else
             {
                 return success; // Non-stackable: true if AddSingleInstance succeeded
             }
             // --- END MODIFIED ---
        }

        // Expose Combiner methods needed by DragAndDropManager and others
         public int PhysicalSlotCount => Combiner?.PhysicalSlotCount ?? 0; // Expose PhysicalSlotCount

         public int TryRemoveQuantity(ItemDetails itemType, int quantityToRemove)
         {
             if (Combiner == null)
             {
                 Debug.LogError($"Inventory ({gameObject.name}): Cannot remove quantity, Combiner is null.", this);
                 return 0;
             }
             return Combiner.TryRemoveQuantity(itemType, quantityToRemove);
         }

         public bool TryRemoveAt(int index)
         {
              if (Combiner == null)
             {
                 Debug.LogError($"Inventory ({gameObject.name}): Cannot remove at index, Combiner is null.", this);
                 return false;
             }
             return Combiner.TryRemoveAt(index);
         }

         /// <summary>
         /// Attempts to stack a quantity of a stackable item *only* into a specific slot.
         /// Filtering check should be done by the caller (e.g., DragAndDropManager, ItemTransferHandler)
         /// by calling Inventory.CanAddItem *before* attempting the transfer/stack.
         /// </summary>
         /// <param name="itemToAdd">The stackable Item instance with quantity to add. Its quantity will be reduced.</param>
         /// <param name="targetSlotIndex">The specific index to stack into.</param>
         /// <returns>The quantity actually added to the target slot.</returns>
         public int TryStackQuantityToSpecificSlot(Item itemToAdd, int targetSlotIndex)
         {
              if (Combiner == null)
             {
                 Debug.LogError($"Inventory ({gameObject.name}): Cannot stack to specific slot, Combiner is null.", this);
                 return 0;
             }
             // Delegate to Combiner's method.
             // Filtering check is assumed to have been done by the caller.
             return Combiner.TryStackQuantityToSpecificSlot(itemToAdd, targetSlotIndex);
         }

        // --- NEW Wrapper Methods for Health Modification ---

        /// <summary>
        /// Attempts to find a specific item instance by its unique instance Id and reduce its health.
        /// Primarily for non-stackable durable items.
        /// Delegates to the Combiner's method.
        /// </summary>
        /// <param name="itemInstance">The exact Item instance to modify.</param>
        /// <param name="healthToReduce">The amount of health to reduce.</param>
        /// <returns>True if the instance was found and health was reduced (or item removed), false otherwise.</returns>
        public bool ReduceItemHealth(Item itemInstance, int healthToReduce)
        {
            if (Combiner == null)
            {
                Debug.LogError($"Inventory ({gameObject.name}): Cannot reduce item health, Combiner is null.", this);
                return false;
            }
            if (itemInstance == null)
            {
                 Debug.LogWarning($"Inventory ({gameObject.name}): Cannot reduce health on a null item instance.");
                 return false;
            }
            // Delegate to Combiner's method
            return Combiner.ReduceHealthOnInstance(itemInstance.Id, healthToReduce);
        }

        /// <summary>
        /// Attempts to find a specific item instance by its unique instance Id and set its health.
        /// Primarily for non-stackable durable items.
        /// Delegates to the Combiner's method.
        /// </summary>
        /// <param name="itemInstance">The exact Item instance to modify.</param>
        /// <param name="healthToSet">The health value to set.</param>
        /// <returns>True if the instance was found and health was set, false otherwise.</returns>
        public bool SetItemHealth(Item itemInstance, int healthToSet)
        {
             if (Combiner == null)
            {
                Debug.LogError($"Inventory ({gameObject.name}): Cannot set item health, Combiner is null.", this);
                return false;
            }
            if (itemInstance == null)
            {
                 Debug.LogWarning($"Inventory ({gameObject.name}): Cannot set health on a null item instance.");
                 return false;
            }
            // Delegate to Combiner's method
            return Combiner.SetHealthOnInstance(itemInstance.Id, healthToSet);
        }

        /// <summary>
        /// Binds the loaded InventoryData to this Inventory component.
        /// (Implementation of IBind<InventoryData> interface)
        /// </summary>
        /// <param name="data">The loaded InventoryData to apply.</param>
        public void Bind(InventoryData data)
        {
            if (data == null)
            {
                Debug.LogWarning($"Inventory ({gameObject.name}, ID: {Id}): Attempted to bind null InventoryData. Initializing to empty/default state.", this);
                // Optionally clear inventory or reset to default if no data is provided.
                if (combiner?.InventoryState != null)
                {
                    combiner.InventoryState.Clear();
                }
                // Reset filtering to defaults if data is null (or keep design-time defaults)
                allowedLabels.Clear(); // Clear existing labels
                allowAllIfListEmpty = true; // Default
                return;
            }

            Debug.Log($"Inventory ({gameObject.name}, ID: {Id}): Binding data for Inventory ID: {data.Id}.");

            // --- Apply Filtering Rules ---
            allowedLabels = new List<ItemLabel>(data.allowedLabels); // Copy the list
            allowAllIfListEmpty = data.allowAllIfListEmpty;

            // --- Populate InventoryState with loaded items ---
            if (combiner?.InventoryState == null)
            {
                Debug.LogError($"Inventory ({gameObject.name}, ID: {Id}): Cannot bind items, Combiner or InventoryState is null.", this);
                return;
            }

            combiner.InventoryState.Clear(); // Clear any existing items first (e.g., from NewGame/Start)

            if (data.items != null)
            {
                // Iterate through the loaded ItemData and convert back to runtime Item instances
                for (int i = 0; i < data.items.Count; i++)
                {
                    ItemData itemData = data.items[i];
                    if (itemData != null)
                    {
                        ItemDetails details = ItemDatabase.GetDetailsById(itemData.ItemDetailsId);
                        if (details != null)
                        {
                            // Create the runtime Item instance
                            Item loadedItem = new Item(details, itemData.quantity); 
                            loadedItem.Id = itemData.Id; // Preserve the instance ID
                            loadedItem.health = itemData.health;
                            loadedItem.usageEventsSinceLastLoss = itemData.usageEventsSinceLastLoss;
                            loadedItem.currentMagazineHealth = itemData.currentMagazineHealth;
                            loadedItem.totalReserveHealth = itemData.totalReserveHealth;
                            loadedItem.isReloading = itemData.isReloading;
                            loadedItem.reloadStartTime = itemData.reloadStartTime;
                            loadedItem.patientNameTag = itemData.patientNameTag;

                            // Place the item at its original index.
                            // Ensure index is within the physical slots range.
                            if (i >= 0 && i < combiner.PhysicalSlotCount)
                            {
                                combiner.InventoryState.SetItemAtIndex(loadedItem, i);
                                Debug.Log($"Inventory ({gameObject.name}, ID: {Id}): Loaded item '{loadedItem.details.Name}' (Instance ID: {loadedItem.Id}) into slot {i}.");
                            }
                            else
                            {
                                Debug.LogWarning($"Inventory ({gameObject.name}, ID: {Id}): Loaded item '{loadedItem.details.Name}' (Instance ID: {loadedItem.Id}) had an out-of-bounds index {i}. Attempting to add it to any available physical slot.", this);
                                // If the original index is invalid (e.g., beyond physical slots), try to add it.
                                // Note: This might change its slot index if the inventory was resized.
                                if (loadedItem.details.maxStack > 1) { combiner.AddStackableItems(loadedItem); }
                                else { combiner.AddSingleInstance(loadedItem); }
                            }
                        }
                        else
                        {
                            Debug.LogError($"Inventory ({gameObject.name}, ID: {Id}): Could not find ItemDetails for ID: {itemData.ItemDetailsId} when binding item at index {i}. Item will be skipped.", this);
                        }
                    }
                }
            }
        }

        // This satisfies the ISavableComponent interface by accepting the generic interface
        // and casting it to the specific type we need.
        public void Bind(ISaveable data)
        {
            if (data is InventoryData inventoryData)
            {
                Bind(inventoryData);
            }
            else
            {
                Debug.LogError($"Inventory: Failed to bind data. Expected InventoryData but got '{data?.GetType().Name ?? "null"}'", this);
            }
        }
    }
}