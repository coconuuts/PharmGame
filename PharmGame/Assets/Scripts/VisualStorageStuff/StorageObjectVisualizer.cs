using UnityEngine;
using System.Collections.Generic;
using Systems.Inventory; // Needed for InventoryClass, ItemDetails, and likely ArrayChangeInfo
using System.Linq;
using InventoryClass = Systems.Inventory.Inventory; // Your alias
using VisualStorage; // Your namespace for StorageVisuals and ShelfSlotArrangement

namespace VisualStorage // Your namespace
{
    /// <summary>
    /// Manages the visual representation of items on storage shelves based on the linked Inventory component.
    /// Reacts to inventory changes and populates ShelfSlots with item prefabs.
    /// </summary>
    [RequireComponent(typeof(InventoryClass))]
    public class StorageObjectVisualizer : MonoBehaviour
    {
        [Tooltip("The Inventory component on this GameObject.")]
        [SerializeField] private InventoryClass targetInventory;

        [Tooltip("Drag the Shelf GameObjects belonging to this storage unit here, ordered by filling priority.")]
        [SerializeField] private List<Shelf> shelves;

        [Tooltip("Map ItemDetails ScriptableObjects to their corresponding 3D item prefab GameObjects.")]
        [SerializeField] private List<ItemPrefabMapping> itemPrefabMappings;

        private Dictionary<ItemDetails, GameObject> itemPrefabsDictionary;

        // Store the total number of available ShelfSlots
        private int totalAvailableShelfSlots;

        [System.Serializable]
        public struct ItemPrefabMapping
        {
            public ItemDetails itemDetails;
            public GameObject prefab3D;
        }


        private void Awake()
        {
            if (targetInventory == null)
            {
                targetInventory = GetComponent<InventoryClass>();
            }

            if (targetInventory == null)
            {
                Debug.LogError($"StorageObjectVisualizer ({gameObject.name}): Inventory component not found!", this);
                enabled = false;
                return;
            }

            if (shelves == null || shelves.Count == 0)
            {
                Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): No Shelves assigned!", this);
                // System might still work for inventory changes, but nothing will be visualized.
            }
            else
            {
                 // Calculate the total number of available shelf slots
                 totalAvailableShelfSlots = shelves.Sum(shelf => (shelf != null && shelf.ShelfSlots != null) ? shelf.ShelfSlots.Count : 0);
                 Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Total available ShelfSlots: {totalAvailableShelfSlots}");
            }


            itemPrefabsDictionary = new Dictionary<ItemDetails, GameObject>();
            if (itemPrefabMappings != null)
            {
                foreach (var mapping in itemPrefabMappings)
                {
                     if (mapping.itemDetails != null && mapping.prefab3D != null)
                     {
                          if (!itemPrefabsDictionary.ContainsKey(mapping.itemDetails))
                          {
                              itemPrefabsDictionary.Add(mapping.itemDetails, mapping.prefab3D);
                          }
                          else
                          {
                              Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Duplicate ItemDetails mapping found for '{mapping.itemDetails.Name}'. Using the first one.", this);
                          }
                     }
                     else
                     {
                          Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Item Prefab Mapping has null ItemDetails or Prefab for slot.", this);
                     }
                }
            }
            else Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Item Prefab Mappings list is null!", this);


            Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Awake completed.");
        }

        private void OnEnable()
        {
            // Subscribe to inventory changes
            if (targetInventory != null && targetInventory.InventoryState != null)
            {
                targetInventory.InventoryState.AnyValueChanged += OnInventoryStateChanged;
                Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Subscribed to InventoryState.AnyValueChanged.");

                // Trigger an initial update on enable to visualize the starting inventory
                 OnInventoryStateChanged(new ArrayChangeInfo<Item>(ArrayChangeType.InitialLoad, targetInventory.InventoryState.GetCurrentArrayState()));
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from inventory changes
            if (targetInventory != null && targetInventory.InventoryState != null)
            {
                targetInventory.InventoryState.AnyValueChanged -= OnInventoryStateChanged;
                Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Unsubscribed from InventoryState.AnyValueChanged.");
            }
        }

         private void OnDestroy()
         {
             OnDisable();
              // Clean up any instantiated item prefabs when the visualizer is destroyed.
              if (shelves != null)
              {
                  foreach (var shelf in shelves)
                  {
                       if (shelf != null && shelf.ShelfSlots != null)
                       {
                           foreach (var slot in shelf.ShelfSlots)
                           {
                                if (slot != null && slot.CurrentItemPrefab != null)
                                {
                                     Destroy(slot.CurrentItemPrefab);
                                }
                           }
                       }
                  }
              }
              Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Destroyed. Cleaned up visual items.");
         }


        /// <summary>
        /// Event handler called when the linked inventory's state changes.
        /// This is the main trigger for updating the visual display.
        /// Implements the core item placement algorithm.
        /// </summary>
        private void OnInventoryStateChanged(ArrayChangeInfo<Item> changeInfo)
        {
            Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Inventory state changed. Updating visual display.");

            // --- Phase 1: Clear Current Visuals ---
            ClearAllVisualItems();
            // -------------------------------------

            // --- Phase 2: Core Mapping and Placement Algorithm ---
            Debug.Log("StorageObjectVisualizer: Running item placement algorithm...");

            // --- Step 4a: Get Current Inventory State ---
            Item[] currentInventoryItems = targetInventory.InventoryState.GetCurrentArrayState();
            // -------------------------------------------

            // --- Step 4b: Identify and Order Item Instances ---
            List<ItemInstanceInfo> itemInstances = new List<ItemInstanceInfo>();
            Dictionary<int, int> occupiedInventorySlotQuantities = new Dictionary<int, int>();

            for (int i = 0; i < currentInventoryItems.Length; i++)
            {
                Item item = currentInventoryItems[i];
                if (item != null && item.quantity > 0)
                {
                     if (occupiedInventorySlotQuantities.ContainsKey(i)) occupiedInventorySlotQuantities[i] += item.quantity;
                     else occupiedInventorySlotQuantities.Add(i, item.quantity);

                    for (int j = 0; j < item.quantity; j++)
                    {
                        itemInstances.Add(new ItemInstanceInfo(item.details, i, j));
                    }
                }
            }

            // --- Step 4c: Order Item Instances ---
            itemInstances = itemInstances.OrderBy(instance => instance.OriginalInventorySlotIndex).ToList();
            Debug.Log($"StorageObjectVisualizer: Found {itemInstances.Count} total item instances in inventory.");
            // ------------------------------------


            // --- Determine which instances to place visually (Step 4f - Selection) ---
            List<ItemInstanceInfo> instancesToPlaceVisually;
            bool shelvesArePotentiallyFull = itemInstances.Count > totalAvailableShelfSlots; // Simplified check

            if (shelvesArePotentiallyFull)
            {
                Debug.Log($"StorageObjectVisualizer: Shelves are potentially full. Implementing proportional distribution logic...");

                instancesToPlaceVisually = new List<ItemInstanceInfo>();

                // Rule: At least one instance per occupied inventory slot must be displayed (if possible)
                int occupiedSlotCount = occupiedInventorySlotQuantities.Count;
                int totalVisualSlotsToAllocate = Mathf.Min(totalAvailableShelfSlots, itemInstances.Count);

                int instancesAddedForBaseline = 0;
                foreach(var kvp in occupiedInventorySlotQuantities)
                {
                    int inventorySlotIndex = kvp.Key;
                    ItemInstanceInfo? firstInstanceInSlot = itemInstances.FirstOrDefault(inst => inst.OriginalInventorySlotIndex == inventorySlotIndex);

                    if(firstInstanceInSlot.HasValue && instancesAddedForBaseline < totalVisualSlotsToAllocate)
                    {
                        instancesToPlaceVisually.Add(firstInstanceInSlot.Value);
                        instancesAddedForBaseline++;
                    }
                }

                int remainingVisualSlotsToAllocate = totalVisualSlotsToAllocate - instancesAddedForBaseline;
                if (remainingVisualSlotsToAllocate > 0 && occupiedSlotCount > 0)
                {
                     Debug.Log($"StorageObjectVisualizer: Distributing {remainingVisualSlotsToAllocate} remaining visual slots proportionally.");

                     int totalQuantity = occupiedInventorySlotQuantities.Sum(kvp => kvp.Value);
                     if (totalQuantity > 0)
                     {
                         List<ItemInstanceInfo> instancesAvailableForProportional = itemInstances
                             .Where(inst => !instancesToPlaceVisually.Contains(inst))
                             .ToList();

                         Dictionary<int, int> targetAdditionalInstancesPerSlot = new Dictionary<int, int>();
                          foreach(var kvp in occupiedInventorySlotQuantities)
                          {
                              float quantityRatio = (float)kvp.Value / totalQuantity;
                              int targetAdditional = Mathf.FloorToInt(quantityRatio * remainingVisualSlotsToAllocate);
                              targetAdditionalInstancesPerSlot.Add(kvp.Key, targetAdditional);
                          }

                          // Handle remainder slots
                          int currentTotalAdditionalAssigned = targetAdditionalInstancesPerSlot.Sum(kvp => kvp.Value);
                          int remainderSlots = remainingVisualSlotsToAllocate - currentTotalAdditionalAssigned;
                          if (remainderSlots > 0)
                          {
                               // Simple distribution of remainders to slots with largest quantities first
                               var sortedSlotsByQuantity = occupiedInventorySlotQuantities.OrderByDescending(kvp => kvp.Value).ToList();
                                for(int i = 0; i < remainderSlots && i < sortedSlotsByQuantity.Count; i++)
                                {
                                    targetAdditionalInstancesPerSlot[sortedSlotsByQuantity[i].Key]++;
                                }
                          }


                          // Select the additional instances based on calculated targets
                          foreach(var kvp in targetAdditionalInstancesPerSlot)
                          {
                              int inventorySlotIndex = kvp.Key;
                              int additionalToSelect = kvp.Value;

                              var availableForSlot = instancesAvailableForProportional
                                  .Where(inst => inst.OriginalInventorySlotIndex == inventorySlotIndex)
                                  .ToList();

                              for(int i = 0; i < additionalToSelect && i < availableForSlot.Count; i++)
                              {
                                  instancesToPlaceVisually.Add(availableForSlot[i]);
                              }
                          }
                     }
                     else Debug.LogWarning("StorageObjectVisualizer: Total quantity is zero, cannot perform proportional distribution based on quantity.");
                }
            }
            else // Shelves are NOT full - all instances are candidates for placement
            {
                 Debug.Log("StorageObjectVisualizer: Shelves are not full. Attempting to place all item instances.");
                 instancesToPlaceVisually = itemInstances;
            }
            // Sort the final list of instances selected for display by their original inventory slot index (again)
            instancesToPlaceVisually = instancesToPlaceVisually.OrderBy(instance => instance.OriginalInventorySlotIndex).ToList();
             Debug.Log($"StorageObjectVisualizer: Final list of instances selected for visual display: {instancesToPlaceVisually.Count}");
        // --------------------------------------------------------------


        // --- Step 4e & 4f (cont.): Map Selected Instances to Shelf Slots ---
        List<ItemInstancePlacement> finalDesiredPlacements = new List<ItemInstancePlacement>();
        List<ShelfSlot> remainingAvailableSlots = new List<ShelfSlot>(); // Slots available *during* this placement cycle

        // Collect all available slots from all shelves initially (they are all available after ClearAllVisualItems)
         if (shelves != null)
         {
             foreach(var shelf in shelves)
             {
                 if (shelf != null && shelf.ShelfSlots != null)
                 {
                      foreach(var slot in shelf.ShelfSlots)
                      {
                          if (slot != null) // Check for null slots in the list
                          {
                               // Add all slots initially, the block finding will check availability
                              remainingAvailableSlots.Add(slot);
                          }
                      }
                 }
                 else if (shelf != null) Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Shelf '{shelf.gameObject.name}' is null or its ShelfSlots list is null during available slot collection.", shelf.gameObject);
             }
         }
         else Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Shelves list is null during available slot collection.");

        Debug.Log($"StorageObjectVisualizer: Initially collected {remainingAvailableSlots.Count} available slots.");

        // Now, iterate through the selected instances and find a block for each in the remaining available slots
        foreach (var instance in instancesToPlaceVisually)
        {
            GetShelfItemDimensions(instance.ItemDetails, out int neededRows, out int neededColumns);

            Debug.Log($"StorageObjectVisualizer: Processing instance from inventory slot {instance.OriginalInventorySlotIndex} ({instance.ItemDetails.Name}). Needs {neededRows}x{neededColumns} block.");

            if (neededRows <= 0 || neededColumns <= 0) continue; // Skip invalid dimensions

            List<ShelfSlot> foundSlotBlock = null;
            bool placed = false;

            // --- Implement the block-finding logic within the Visualizer ---
            // Iterate through shelves in order, then through slots on each shelf in order,
            // considering each slot as a potential start of a block.
            if (shelves != null)
            {
                 foreach (var shelf in shelves)
                 {
                     if (shelf == null || shelf.ShelfSlots == null) continue;

                     foreach (var potentialStartSlot in shelf.ShelfSlots) // Iterate through slots on the shelf
                     {
                         if (potentialStartSlot == null) continue;

                         Debug.Log($"StorageObjectVisualizer: Considering potential start slot: '{potentialStartSlot.gameObject.name}' (Grid: {potentialStartSlot.RowIndex},{potentialStartSlot.ColumnIndex}). Is available? {remainingAvailableSlots.Contains(potentialStartSlot)}.");
                         // Check if this potential start slot is currently available in the remaining list
                          if (!remainingAvailableSlots.Contains(potentialStartSlot)) continue;

                         // Assume the potential start slot's grid coordinates represent the top-left of the block
                         int startRow = potentialStartSlot.RowIndex;
                         int startColumn = potentialStartSlot.ColumnIndex;

                         bool blockIsAvailable = true;
                         List<ShelfSlot> currentBlock = new List<ShelfSlot>();

                         // Check the block of slots required by the item's dimensions
                         for (int r = 0; r < neededRows; r++)
                         {
                             for (int c = 0; c < neededColumns; c++)
                             {
                                 int currentRow = startRow + r;
                                 int currentColumn = startColumn + c;

                                 // Find the slot at these coordinates on this shelf
                                 ShelfSlot currentSlot = shelf.GetSlot(currentRow, currentColumn);

                            if (currentSlot == null)
                            {
                                Debug.Log($"StorageObjectVisualizer: Block check: Slot at calculated grid ({currentRow},{currentColumn}) on Shelf '{shelf.gameObject.name}' is null in GetSlot result.");
                            }
                            else
                            {
                                Debug.Log($"StorageObjectVisualizer: Block check: Checking slot '{currentSlot.gameObject.name}' (Grid: {currentSlot.RowIndex},{currentSlot.ColumnIndex}) found via GetSlot for calculated grid ({currentRow},{currentColumn}). Is in remaining available list? {remainingAvailableSlots.Contains(currentSlot)}.");
                            }

                                 // Check if the slot is valid AND is in the list of remaining available slots
                                 if (currentSlot == null || !remainingAvailableSlots.Contains(currentSlot))
                                 {
                                     blockIsAvailable = false; // Slot is null or not available
                                     break;
                                 }

                                 currentBlock.Add(currentSlot); // Add the available slot to the current block
                             }
                             if (!blockIsAvailable) break; // Break inner loop if block is not available
                         }

                         // If the entire block is available and matches the needed size
                         if (blockIsAvailable && currentBlock.Count == neededRows * neededColumns)
                         {
                             foundSlotBlock = currentBlock; // Found a block
                             placed = true;
                             Debug.Log($"StorageObjectVisualizer: Found suitable block for instance from inventory slot {instance.OriginalInventorySlotIndex} ({instance.ItemDetails.Name}) starting at '{potentialStartSlot.gameObject.name}' (Grid: {potentialStartSlot.RowIndex},{potentialStartSlot.ColumnIndex}) on Shelf '{shelf.gameObject.name}'. Block contains {foundSlotBlock.Count} slots. First slot in block: '{foundSlotBlock[0]?.gameObject.name}'.");

                             // Remove the used slots from the global remaining list
                             foreach(var usedSlot in foundSlotBlock)
                             {
                                 if (remainingAvailableSlots.Contains(usedSlot))
                                 {
                                     remainingAvailableSlots.Remove(usedSlot);
                                 }
                                 else
                                 {
                                     Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Attempted to remove slot from remainingAvailableSlots that was not present! Slot: ({usedSlot.RowIndex},{usedSlot.ColumnIndex}) on Shelf '{shelf.gameObject.name}'.", usedSlot);
                                 }
                             }

                             // Debug.Log($"StorageObjectVisualizer: Placed instance from slot {instance.OriginalInventorySlotIndex} ({instance.ItemDetails.Name}) in block starting at ({potentialStartSlot.RowIndex},{potentialStartSlot.ColumnIndex}) on Shelf '{shelf.gameObject.name}'.");

                             break; // Move to the next item instance once placed
                         }
                         else
                         {
                              currentBlock.Clear(); // Clear the partial block
                         }
                     }
                     if (placed) break; // Move to the next item instance once placed on *any* shelf
                 }
             }
            // --- End of block-finding logic ---


             if (placed)
             {
                  finalDesiredPlacements.Add(new ItemInstancePlacement(instance, foundSlotBlock));
             }
             else
             {
                 Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Item instance from slot {instance.OriginalInventorySlotIndex} ({instance.ItemDetails.Name}) could not be placed visually even after selection for display. Remaining slots: {remainingAvailableSlots.Count}. This might indicate a fragmentation issue or mismatch between calculated needed slots and available blocks.", instance.ItemDetails);
             }
        }
         // The 'finalDesiredPlacements' list now holds the final plan for what should be displayed and where.
         Debug.Log($"StorageObjectVisualizer: Finished placement algorithm. Determined {finalDesiredPlacements.Count} final desired placements.");

        // --- Step 4g: Update Visuals ---
        // Now compare the 'finalDesiredPlacements' to the *actual* current state of the ShelfSlots
        // and perform instantiate/destroy/move prefabs.

        // The ClearAllVisualItems at the start destroyed everything.
        // We now just need to instantiate based on 'finalDesiredPlacements'.

        Debug.Log($"StorageObjectVisualizer: Instantiating visual items based on final desired placements ({finalDesiredPlacements.Count}).");

        // Instantiate prefabs for all final desired placements
        foreach (var placement in finalDesiredPlacements)
        {
             if (placement.ShelfSlotBlock.Count > 0)
             {
                  ShelfSlot startSlot = placement.ShelfSlotBlock[0];
                  GameObject requiredPrefab = GetItemPrefab(placement.ItemInstanceInfo.ItemDetails);

                  if (startSlot != null && requiredPrefab != null)
                  {
                       // Instantiate the prefab at the start slot's transform, parented to the start slot
                       GameObject instantiatedPrefab = Instantiate(requiredPrefab, startSlot.SlotTransform.position, startSlot.SlotTransform.rotation, startSlot.SlotTransform);

                       // Mark ALL slots in the block as occupied by this SAME prefab instance
                        foreach(var blockSlot in placement.ShelfSlotBlock)
                        {
                            if (blockSlot != null)
                            {
                                // Note: Only the start slot needs the actual prefab transform.
                                // Other slots in the block are logically occupied by the same item.
                                blockSlot.Occupy(instantiatedPrefab);
                            }
                        }
                        // Optional: Add a component to instantiatedPrefab to link it back to ItemDetails
                        // (useful for future incremental updates or interaction with visualized items)
                        // Example: instantiatedPrefab.AddComponent<VisualizedItem>().ItemDetails = placement.ItemInstanceInfo.ItemDetails;
                  }
                  else
                  {
                       Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Cannot instantiate prefab for final placement. Start slot is null ({startSlot == null}) or Required Prefab is null ({requiredPrefab == null}).");
                  }
             }
             else
             {
                 Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Final desired placement has an empty ShelfSlotBlock list.");
             }
        }

         Debug.Log($"StorageObjectVisualizer: Visual update complete. Total visual items placed: {finalDesiredPlacements.Count}.");
    }

        /// <summary>
        /// Destroys all currently displayed item prefabs and vacates all ShelfSlots.
        /// Called at the start of the visual update process.
        /// </summary>
        private void ClearAllVisualItems()
        {
             Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Clearing all visual items from shelves.");
             if (shelves != null)
             {
                 foreach (var shelf in shelves)
                 {
                     if (shelf != null && shelf.ShelfSlots != null)
                     {
                         foreach (var slot in shelf.ShelfSlots)
                         {
                             if (slot != null && slot.CurrentItemPrefab != null)
                             {
                                 Destroy(slot.CurrentItemPrefab);
                             }
                             if (slot != null) slot.Vacate();
                         }
                     }
                     else if (shelf != null) Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Shelf '{shelf.gameObject.name}' is null or its ShelfSlots list is null during clearing.", shelf.gameObject);
                 }
             }
             else Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Shelves list is null during clearing.");

             Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Finished clearing visual items.");
        }


        /// <summary>
        /// Helper method to get the required grid dimensions for a visual item based on its arrangement.
        /// </summary>
        private void GetShelfItemDimensions(ItemDetails details, out int rows, out int columns)
        {
            rows = 0;
            columns = 0;

            if (details == null) return;

            switch (details.shelfArrangement)
            {
                case ShelfSlotArrangement.OneByOne:
                    rows = 1;
                    columns = 1;
                    break;
                case ShelfSlotArrangement.OneByTwo:
                    rows = 1;
                    columns = 2;
                    break;
                case ShelfSlotArrangement.TwoByOne:
                    rows = 2;
                    columns = 1;
                    break;
                case ShelfSlotArrangement.TwoByTwo:
                    rows = 2;
                    columns = 2;
                    break;
                 default:
                     Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Unhandled ShelfSlotArrangement '{details.shelfArrangement}' for ItemDetails '{details.Name}'. Defaulting to 1x1.", details);
                     rows = 1;
                     columns = 1;
                     break;
            }
        }

        /// <summary>
        /// Helper method to get item prefab for a given ItemDetails (using the dictionary).
        /// </summary>
        private GameObject GetItemPrefab(ItemDetails itemDetails)
        {
             if (itemDetails != null && itemPrefabsDictionary != null && itemPrefabsDictionary.TryGetValue(itemDetails, out GameObject prefab))
             {
                 return prefab;
             }
             else if (itemDetails != null)
             {
                  Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): No 3D prefab mapping found for ItemDetails: {itemDetails.Name}.", itemDetails);
             }
            return null;
        }

        // --- Helper Structs for the Placement Algorithm ---

        /// <summary>
        /// Represents a single item instance logically existing in the inventory.
        /// Stores ItemDetails and its original inventory slot index.
        /// </summary>
        private struct ItemInstanceInfo
        {
            public ItemDetails ItemDetails { get; }
            public int OriginalInventorySlotIndex { get; }
            // Optional: public int InstanceIndexInSlot; // If needed for specific ordering within stacks

            public ItemInstanceInfo(ItemDetails details, int inventorySlotIndex, int instanceIndex)
            {
                ItemDetails = details;
                OriginalInventorySlotIndex = inventorySlotIndex;
                // InstanceIndexInSlot = instanceIndex; // Not used in sorting yet
            }

            // Need Equals and GetHashCode to compare instances if using Contains() or similar
            public override bool Equals(object obj)
            {
                if (obj is ItemInstanceInfo other)
                {
                    // Compare by ItemDetails and OriginalInventorySlotIndex.
                    // If InstanceIndexInSlot was used, include it here.
                    return ItemDetails == other.ItemDetails &&
                           OriginalInventorySlotIndex == other.OriginalInventorySlotIndex;
                }
                return false;
            }

            public override int GetHashCode()
            {
                 // Combine hash codes. Use a prime number multiplier.
                 unchecked // Overflow is fine
                 {
                     int hash = 17;
                     hash = hash * 23 + (ItemDetails != null ? ItemDetails.GetHashCode() : 0);
                     hash = hash * 23 + OriginalInventorySlotIndex.GetHashCode();
                     // if using InstanceIndexInSlot, add it: hash = hash * 23 + InstanceIndexInSlot.GetHashCode();
                     return hash;
                 }
            }
        }

        /// <summary>
        /// Represents the desired visual placement of an item instance onto a block of ShelfSlots.
        /// </summary>
        private struct ItemInstancePlacement
        {
            public ItemInstanceInfo ItemInstanceInfo { get; }
            public List<ShelfSlot> ShelfSlotBlock { get; } // The block of slots this instance should occupy

            public ItemInstancePlacement(ItemInstanceInfo instanceInfo, List<ShelfSlot> slotBlock)
            {
                ItemInstanceInfo = instanceInfo;
                ShelfSlotBlock = slotBlock;
            }
        }
    }
}