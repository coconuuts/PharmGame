using UnityEngine;
using System.Collections.Generic;
using Systems.Inventory;
using System.Linq;
using InventoryClass = Systems.Inventory.Inventory; // Your alias
using VisualStorage; // Your namespace for StorageVisuals and ShelfSlotArrangement
using System; // Needed for Action
using Utils.Pooling;

namespace VisualStorage // Your namespace
{
    /// <summary>
    /// Manages the visual representation of items on storage shelves based on the linked Inventory component.
    /// Reacts to inventory changes and populates ShelfSlots with item prefabs using incremental updates
    /// and decoupled item-to-prefab mapping, utilizing object pooling.
    /// </summary>
    [RequireComponent(typeof(InventoryClass))]
    public class StorageObjectVisualizer : MonoBehaviour
    {
        [Tooltip("The Inventory component on this GameObject.")]
        [SerializeField] private InventoryClass targetInventory;

        [Tooltip("Drag the Shelf GameObjects belonging to this storage unit here, ordered by filling priority.")]
        [SerializeField] private List<Shelf> shelves;

        // --- Item Mapping Fields ---
        [Tooltip("Assign the ScriptableObject containing item to prefab mappings.")]
        [SerializeField] private ItemVisualMappingSO itemVisualMappingAsset; // Assign the SO asset in the inspector
        private ItemVisualLookup itemVisualLookup; // Instance of the lookup helper
        // ---------------------------


        // Reference to the DragAndDropManager for subscribing to completion event
        private DragAndDropManager dragAndDropManager;

        // Reference to the PoolingManager for getting and returning visual items
        private PoolingManager poolingManager; // Added reference to PoolingManager

        // Store the total number of available ShelfSlots
        private int totalAvailableShelfSlots;

        // --- Track currently displayed visual items ---
        private List<CurrentlyPlacedVisualItem> currentlyPlacedVisualItems = new List<CurrentlyPlacedVisualItem>();
        // --------------------------------------------


        // ItemPrefabMapping struct is now defined in ItemVisualMappingSO.cs

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


            // --- Item Mapping Lookup Setup ---
            if (itemVisualMappingAsset != null)
            {
                itemVisualLookup = new ItemVisualLookup(itemVisualMappingAsset);
                Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Initialized ItemVisualLookup using '{itemVisualMappingAsset.name}'.");
            }
            else
            {
                Debug.LogError($"StorageObjectVisualizer ({gameObject.name}): Item Visual Mapping Asset is not assigned!", this);
                 enabled = false; // Cannot function without mappings
                 return; // Stop Awake if mappings are missing
            }
            // ------------------------------------


            // Get the DragAndDropManager instance
            dragAndDropManager = DragAndDropManager.Instance;
            if (dragAndDropManager == null)
            {
                Debug.LogError($"StorageObjectVisualizer ({gameObject.name}): DragAndDropManager instance not found! Delayed updates will not work as expected.", this);
            }

            // --- Get the PoolingManager instance ---
            poolingManager = PoolingManager.Instance;
             if (poolingManager == null)
             {
                  Debug.LogError($"StorageObjectVisualizer ({gameObject.name}): PoolingManager instance not found! Object pooling will not be used. Please add a PoolingManager to your scene.", this);
                  // Decide fallback behavior: continue using Instantiate/Destroy or disable?
                  // For now, we'll add null checks for poolingManager calls.
             }
            // ---------------------------------------


            Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Awake completed.");
        }

        private void OnEnable()
        {
             // --- Always subscribe to the ObservableArray's change event ---
             // This ensures visual updates for ANY change to the inventory data,
             // including those made by NPC shopping logic.
             if (targetInventory != null && targetInventory.InventoryState != null)
             {
                  targetInventory.InventoryState.AnyValueChanged += HandleInventoryStateChangedEvent;
             }
             else Debug.LogError($"StorageObjectVisualizer ({gameObject.name}): Cannot subscribe to InventoryState.AnyValueChanged. Inventory or InventoryState is null.", this);
            // -------------------------------------------------------------


            // Keep the drag/drop subscription if DragAndDropManager is available.
            // This might be useful for ensuring a final update after a complex drag sequence.
            dragAndDropManager = DragAndDropManager.Instance;
            if (dragAndDropManager != null)
            {
                dragAndDropManager.OnDragDropCompleted += OnDragDropCompleted;
            }


            // Trigger an initial update on enable to visualize the starting inventory
            // This also happens when the GameObject is first created/activated from the pool
             ForceVisualUpdate();
        }

        private void OnDisable()
        {
             // --- Always unsubscribe from the ObservableArray's change event ---
             if (targetInventory != null && targetInventory.InventoryState != null)
             {
                  targetInventory.InventoryState.AnyValueChanged -= HandleInventoryStateChangedEvent;
                  Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Unsubscribed from InventoryState.AnyValueChanged.");
             }
            // -----------------------------------------------------------------


            // Unsubscribe from drag/drop completion event
            if (dragAndDropManager != null)
            {
                dragAndDropManager.OnDragDropCompleted -= OnDragDropCompleted;
                Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Unsubscribed from DragAndDropManager.OnDragDropCompleted.");
            }
            // Removed the 'else' block with the fallback unsubscription


            // Clean up any remaining visuals when disabled (e.g., scene changes, returning to pool)
             ReturnAllVisualItemsToPool();
        }

         private void OnDestroy()
         {
             OnDisable(); // Ensure unsubscription
              Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Destroyed. Returned visual items to pool.");
         }


        // Event handler for DragAndDropManager completion
        private void OnDragDropCompleted()
        {
            ForceVisualUpdate();
        }

        // Fallback event handler if DragAndDropManager is not available
        private void HandleInventoryStateChangedEvent(ArrayChangeInfo<Item> changeInfo) // Corrected ArrayChangeInfo namespace
        {
            if (DragAndDropManager.Instance != null && DragAndDropManager.Instance.IsDragging)
            {
                // A drag is happening, ignore this intermediate change event.
                // The visual update will be triggered by OnDragDropCompleted instead.
                Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Inventory change detected during drag. Skipping visual update.");
                return;
            }

            // If no drag is in progress, or DragAndDropManager is not available,
            // treat this as a standard inventory change and update visuals.
            Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Inventory change detected (no drag or D&D Manager missing). Forcing visual update.");
            ForceVisualUpdate();
        }


        /// <summary>
        /// Performs the core visual update logic: determines desired placements and
        /// updates the visual display incrementally using object pooling.
        /// </summary>
        private void ForceVisualUpdate()
        {
            // --- Phase 1: Determine Desired Visual Placements ---
            // ... (Logic to get inventory state, identify instances, proportional selection,
            // and prioritized block finding remains the same, populating finalDesiredPlacements) ...

            Item[] currentInventoryItems = targetInventory.InventoryState.GetCurrentArrayState();
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

            List<ItemInstanceInfo> orderedItemInstances = itemInstances.OrderBy(instance => instance.OriginalInventorySlotIndex).ToList();

            bool shelvesArePotentiallyFull = orderedItemInstances.Count >= totalAvailableShelfSlots;
            int totalVisualSlotsToFill = totalAvailableShelfSlots;
            int targetVisualCount = shelvesArePotentiallyFull ? totalVisualSlotsToFill : orderedItemInstances.Count;


            List<ItemInstanceInfo> instancesToPlaceVisually;

            if (shelvesArePotentiallyFull)
            {
                instancesToPlaceVisually = new List<ItemInstanceInfo>();
                int totalQuantity = occupiedInventorySlotQuantities.Sum(kvp => kvp.Value);

                if (totalQuantity > 0)
                {
                    Dictionary<int, float> targetVisualCountPerSlot = new Dictionary<int, float>();
                    foreach(var kvp in occupiedInventorySlotQuantities)
                    {
                        targetVisualCountPerSlot.Add(kvp.Key, (float)kvp.Value / totalQuantity * totalVisualSlotsToFill);
                    }

                    List<(int inventorySlotIndex, float remainder)> remainderSlots = new List<(int, float)>();

                    foreach(var kvp in targetVisualCountPerSlot)
                    {
                        int inventorySlotIndex = kvp.Key;
                        float targetCount = kvp.Value;
                        int integerPart = Mathf.FloorToInt(targetCount);
                        float remainder = targetCount - integerPart;

                        var instancesFromSlot = orderedItemInstances
                            .Where(inst => inst.OriginalInventorySlotIndex == inventorySlotIndex)
                            .Take(integerPart)
                            .ToList();

                         instancesToPlaceVisually.AddRange(instancesFromSlot);

                        if (remainder > 0)
                        {
                            remainderSlots.Add((inventorySlotIndex, remainder));
                        }
                    }

                    remainderSlots = remainderSlots.OrderByDescending(r => r.remainder).ToList();

                    int remainingSlotsToAllocate = totalVisualSlotsToFill - instancesToPlaceVisually.Count;

                    if (remainingSlotsToAllocate > 0 && remainderSlots.Count > 0)
                    {
                        int currentRemainderCandidateIndex = 0;

                        while (remainingSlotsToAllocate > 0 && currentRemainderCandidateIndex < remainderSlots.Count)
                        {
                             int inventorySlotIndex = remainderSlots[currentRemainderCandidateIndex].inventorySlotIndex;
                              int currentlySelectedFromSlot = instancesToPlaceVisually.Count(inst => inst.OriginalInventorySlotIndex == inventorySlotIndex);

                             ItemInstanceInfo? nextInstance = orderedItemInstances
                                 .Where(inst => inst.OriginalInventorySlotIndex == inventorySlotIndex)
                                 .Skip(currentlySelectedFromSlot)
                                 .FirstOrDefault();

                             if (nextInstance.HasValue)
                             {
                                 instancesToPlaceVisually.Add(nextInstance.Value);
                                 remainingSlotsToAllocate--;
                                 if (remainingSlotsToAllocate == 0) break;
                             }
                             else
                             {
                                  Debug.Log($"StorageObjectVisualizer: Inventory slot {inventorySlotIndex} has no more instances available for remainder allocation.");
                              }
                             currentRemainderCandidateIndex++;
                             if (currentRemainderCandidateIndex >= remainderSlots.Count && remainingSlotsToAllocate > 0)
                             {
                                 currentRemainderCandidateIndex = 0;
                             }
                        }
                    }
                }
            }
            else
            {
                 instancesToPlaceVisually = orderedItemInstances;
            }

            // Map selected instances to shelf slots (Placement Pass)
            List<ItemInstancePlacement> finalDesiredPlacements = new List<ItemInstancePlacement>();
            List<ShelfSlot> placementPassAvailableSlots = new List<ShelfSlot>(totalAvailableShelfSlots);

            if (shelves != null)
            {
                 foreach(var shelf in shelves)
                 {
                     if (shelf != null && shelf.ShelfSlots != null)
                     {
                          foreach(var slot in shelf.ShelfSlots)
                          {
                              if (slot != null)
                              {
                                   placementPassAvailableSlots.Add(slot);
                              }
                          }
                     }
                      else if (shelf != null) Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Shelf '{shelf.gameObject.name}' is null or its ShelfSlots list is null during placement pass slot collection.", shelf.gameObject);
                 }
            }
                  else {Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Shelves list is null during placement pass slot collection.");
            }

            int successfullyPlacedInPass = 0;

            foreach (var instance in instancesToPlaceVisually)
            {
                GetShelfItemDimensions(instance.ItemDetails, out int neededRows, out int neededColumns);

                if (neededRows <= 0 || neededColumns <= 0)
                {
                    Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Selected instance from inventory slot {instance.OriginalInventorySlotIndex} has invalid dimensions ({neededRows}x{neededColumns}). Skipping placement attempt for this instance.");
                    continue;
                }

                List<ShelfSlot> foundSlotBlock = null;
                bool foundBlockForThisInstance = false;

                // --- Block-finding logic with Prioritized Shelf Search ---
                if (shelves != null && shelves.Count > 0)
                {
                    int preferredShelfIndex = instance.OriginalInventorySlotIndex % shelves.Count;
                    var shelfIndicesToSearch = Enumerable.Range(0, shelves.Count).Select(i => (preferredShelfIndex + i) % shelves.Count);

                    foreach (int currentShelfIndex in shelfIndicesToSearch)
                    {
                        Shelf currentShelf = shelves[currentShelfIndex];

                        if (currentShelf == null || currentShelf.ShelfSlots == null) continue;

                        foreach (var potentialStartSlot in currentShelf.ShelfSlots)
                        {
                            if (potentialStartSlot == null || !placementPassAvailableSlots.Contains(potentialStartSlot)) continue;

                            int startRow = potentialStartSlot.RowIndex;
                            int startColumn = potentialStartSlot.ColumnIndex;

                            bool blockIsAvailable = true;
                            List<ShelfSlot> currentBlock = new List<ShelfSlot>();

                            for (int r = 0; r < neededRows; r++)
                            {
                                for (int c = 0; c < neededColumns; c++)
                                {
                                    int currentRow = startRow + r;
                                    int currentColumn = startColumn + c;

                                     if (currentRow >= currentShelf.Rows || currentColumn >= currentShelf.Columns)
                                     {
                                         blockIsAvailable = false;
                                         break;
                                     }

                                    ShelfSlot currentSlot = currentShelf.GetSlot(currentRow, currentColumn);

                                    if (currentSlot == null || !placementPassAvailableSlots.Contains(currentSlot))
                                    {
                                        blockIsAvailable = false;
                                        break;
                                    }
                                    currentBlock.Add(currentSlot);
                                }
                                if (!blockIsAvailable) break;
                            }

                            if (blockIsAvailable && currentBlock.Count == neededRows * neededColumns)
                            {
                                foundSlotBlock = currentBlock;
                                foundBlockForThisInstance = true;

                                 foreach(var usedSlot in foundSlotBlock)
                                 {
                                     if (placementPassAvailableSlots.Contains(usedSlot))
                                     {
                                         placementPassAvailableSlots.Remove(usedSlot);
                                     }
                                     else
                                     {
                                         Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Attempted to remove slot from placementPassAvailableSlots that was not present after finding block! Slot: ({usedSlot.RowIndex},{usedSlot.ColumnIndex}) on Shelf '{currentShelf.gameObject.name}'.", usedSlot);
                                     }
                                 }

                                break; // Found block on this shelf, move to next instance
                            }
                             else
                            {
                                 currentBlock.Clear();
                            }
                        }
                        if (foundBlockForThisInstance) break; // Found block across shelves, move to next instance
                    }
                }
                // --- End of Block-finding logic ---


                 // --- Add to Final Desired Placements if a block was found ---
                 if (foundBlockForThisInstance)
                 {
                      // This instance successfully found a block, add it to the desired list for comparison.
                      finalDesiredPlacements.Add(new ItemInstancePlacement(instance, foundSlotBlock));
                      successfullyPlacedInPass++; // Track successful placements within this pass
                 }
                 else
                 {
                     Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Proportionally selected instance from inventory slot {instance.OriginalInventorySlotIndex} ({instance.ItemDetails.Name}) could NOT find an available block after checking all shelves (fragmentation?). Remaining slots: {placementPassAvailableSlots.Count}.");
                 }
            }
            Debug.Log($"StorageObjectVisualizer: Finished placement pass. Determined {finalDesiredPlacements.Count} final desired placements (out of {instancesToPlaceVisually.Count} selected). Total successfully placed in pass: {successfullyPlacedInPass}.");


            // --- Phase 2: Incremental Visual Update using Pooling ---
            List<CurrentlyPlacedVisualItem> nextPlacedVisualItems = new List<CurrentlyPlacedVisualItem>();

            List<ItemInstancePlacement> itemsToCreate = new List<ItemInstancePlacement>();
            List<CurrentlyPlacedVisualItem> itemsToDestroy = new List<CurrentlyPlacedVisualItem>();

            HashSet<ItemInstancePlacement> desiredPlacementsSet = new HashSet<ItemInstancePlacement>(finalDesiredPlacements);
            HashSet<CurrentlyPlacedVisualItem> currentlyPlacedSet = new HashSet<CurrentlyPlacedVisualItem>(currentlyPlacedVisualItems);


            // Identify items to destroy (currently placed items that are not in the desired state)
            foreach (var placedItem in currentlyPlacedVisualItems)
            {
                 bool isStillDesiredAtSameLocation = false;
                 if (desiredPlacementsSet.Any(desired => placedItem.MatchesDesiredPlacement(desired)))
                 {
                     isStillDesiredAtSameLocation = true;
                 }

                 if (!isStillDesiredAtSameLocation)
                 {
                     itemsToDestroy.Add(placedItem);
                 }
            }

            // Identify items to create (desired placements that are not currently placed)
            foreach (var desiredPlacement in finalDesiredPlacements)
            {
                 bool isAlreadyPlacedMatching = false;
                 if (currentlyPlacedSet.Any(placed => placed.MatchesDesiredPlacement(desiredPlacement)))
                 {
                     isAlreadyPlacedMatching = true;
                 }

                 if (!isAlreadyPlacedMatching)
                 {
                     itemsToCreate.Add(desiredPlacement);
                 }
            }


            // --- Execute Changes ---

            // Destroy/Return Items to Pool
            foreach (var itemToDestroy in itemsToDestroy)
            {
                Debug.Log($"StorageObjectVisualizer: Returning visual item for Inv Slot {itemToDestroy.ItemInstanceInfo.OriginalInventorySlotIndex} ({itemToDestroy.ItemInstanceInfo.ItemDetails.Name}) to pool.");
                if (itemToDestroy.VisualGameObject != null && poolingManager != null) // Check poolingManager
                {
                     // --- RETURN TO POOL ---
                     poolingManager.ReturnPooledObject(itemToDestroy.VisualGameObject);
                     // --------------------
                }
                 else if (itemToDestroy.VisualGameObject != null && poolingManager == null)
                 {
                      Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): PoolingManager is null. Destroying item instead of returning to pool.", itemToDestroy.VisualGameObject);
                      Destroy(itemToDestroy.VisualGameObject); // Fallback destroy
                 }

                // Also need to vacate the ShelfSlots it occupied
                if (itemToDestroy.OccupiedSlots != null)
                {
                     foreach(var slot in itemToDestroy.OccupiedSlots)
                     {
                          if (slot != null) slot.Vacate(); // Make the slot available again visually
                     }
                 }
            }
             Debug.Log($"StorageObjectVisualizer: Returned {itemsToDestroy.Count} visual items to pool (or destroyed).");

            // Create/Get Items from Pool
            List<CurrentlyPlacedVisualItem> newlyCreatedItems = new List<CurrentlyPlacedVisualItem>(); // Temporary list for newly created/gotten items

            foreach (var placementToCreate in itemsToCreate)
            {
                 if (placementToCreate.ShelfSlotBlock.Count > 0)
                 {
                      ShelfSlot startSlot = placementToCreate.ShelfSlotBlock[0];
                      // Use ItemVisualLookup to get the prefab reference
                       GameObject requiredPrefab = itemVisualLookup?.GetItemPrefab(placementToCreate.ItemInstanceInfo.ItemDetails);


                      GameObject instantiatedPrefab = null;

                       if (startSlot != null && requiredPrefab != null && poolingManager != null) // Check poolingManager
                       {
                            // --- GET FROM POOL ---
                            instantiatedPrefab = poolingManager.GetPooledObject(requiredPrefab);
                            // -------------------

                            if (instantiatedPrefab != null)
                            {
                                // Position, rotate, and parent the object gotten from the pool
                                instantiatedPrefab.transform.SetParent(startSlot.SlotTransform, false); // Parent to the start slot's transform
                                instantiatedPrefab.transform.localPosition = Vector3.zero; // Reset local position
                                instantiatedPrefab.transform.localRotation = Quaternion.identity; // Reset local rotation


                                // Mark ALL slots in the block as occupied by this SAME prefab instance
                                foreach(var blockSlot in placementToCreate.ShelfSlotBlock)
                                {
                                    if (blockSlot != null)
                                    {
                                        blockSlot.Occupy(instantiatedPrefab); // Link the visual object to the slot
                                    }
                                }

                                // Add the newly placed item to a temporary list
                                 newlyCreatedItems.Add(new CurrentlyPlacedVisualItem(
                                    placementToCreate.ItemInstanceInfo,
                                    instantiatedPrefab,
                                    placementToCreate.ShelfSlotBlock
                                 ));
                            }
                            else
                            {
                                Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Failed to get pooled object for prefab '{requiredPrefab.name}'.");
                            }

                       }
                       else if (startSlot != null && requiredPrefab != null && poolingManager == null)
                       {
                            Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): PoolingManager is null. Instantiating item instead of pooling.", requiredPrefab);
                             // Fallback instantiate if poolingManager is null
                             instantiatedPrefab = Instantiate(requiredPrefab, startSlot.SlotTransform.position, startSlot.SlotTransform.rotation, startSlot.SlotTransform);

                             if (instantiatedPrefab != null)
                             {
                                  // Mark ALL slots in the block as occupied by this SAME prefab instance
                                   foreach(var blockSlot in placementToCreate.ShelfSlotBlock)
                                   {
                                       if (blockSlot != null)
                                       {
                                           blockSlot.Occupy(instantiatedPrefab);
                                       }
                                   }

                                  // Note: Fallback instantiated items are NOT tracked by PooledObjectInfo,
                                  // so they will be Destroyed by the fallback in ReturnAllVisualItemsToPool.
                                   Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Fallback instantiated item will not be pooled on return.", instantiatedPrefab);


                                   // Add the newly placed item to a temporary list (tracking it even if not pooled)
                                    newlyCreatedItems.Add(new CurrentlyPlacedVisualItem(
                                       placementToCreate.ItemInstanceInfo,
                                       instantiatedPrefab,
                                       placementToCreate.ShelfSlotBlock
                                    ));
                             }
                       }
                       else
                       {
                            Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Cannot get/instantiate prefab for desired placement. Start slot is null ({startSlot == null}) or Required Prefab is null ({requiredPrefab == null}).");
                       }
                 }
                 else
                 {
                     Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): Desired placement has an empty ShelfSlotBlock list.");
                 }
            }

            // --- Update Tracking List ---
            // The next state is the items that were kept + the items that were newly created/gotten.
            nextPlacedVisualItems = new List<CurrentlyPlacedVisualItem>();

            // Add items that were kept (iterate through the original list, exclude those marked for destruction)
            foreach (var placedItem in currentlyPlacedVisualItems)
            {
                 // Check if this placed item is in the list of items to destroy
                 if(!itemsToDestroy.Contains(placedItem))
                 {
                      nextPlacedVisualItems.Add(placedItem); // If it was NOT marked for destruction, keep it for the next state
                 }
            }

            // Add newly created/gotten items
            nextPlacedVisualItems.AddRange(newlyCreatedItems);

            // Update the main tracking list
            currentlyPlacedVisualItems = nextPlacedVisualItems;

            Debug.Log($"StorageObjectVisualizer: Incremental update complete. Total visual items currently tracked: {currentlyPlacedVisualItems.Count}.");
        }

        /// <summary>
        /// Returns all currently tracked visual items to the object pool (or destroys them if pooling is not available).
        /// Used for cleanup on disable/destroy.
        /// </summary>
        private void ReturnAllVisualItemsToPool() // Renamed method
        {
            Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Returning all tracked visual items to pool.");
            // Iterate through a copy or backwards to avoid issues while modifying the list
            foreach(var placedItem in currentlyPlacedVisualItems.ToList()) // Use ToList() to iterate over a copy
            {
                if (placedItem.VisualGameObject != null)
                {
                    if (poolingManager != null) // Check poolingManager
                    {
                         // --- RETURN TO POOL ---
                         poolingManager.ReturnPooledObject(placedItem.VisualGameObject);
                         // --------------------
                    }
                    else
                    {
                         Debug.LogWarning($"StorageObjectVisualizer ({gameObject.name}): PoolingManager is null. Destroying item instead of returning to pool.", placedItem.VisualGameObject);
                         Destroy(placedItem.VisualGameObject); // Fallback destroy
                    }
                }
                 if (placedItem.OccupiedSlots != null)
                 {
                     foreach(var slot in placedItem.OccupiedSlots)
                     {
                          if (slot != null) slot.Vacate(); // Make the slot available again visually
                     }
                 }
            }
            currentlyPlacedVisualItems.Clear();
             Debug.Log($"StorageObjectVisualizer ({gameObject.name}): Finished returning all tracked visual items to pool (or destroying).");
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
                     rows = 1;
                     columns = 1;
                     break;
            }
        }

        /// <summary>
        /// Helper method to get item prefab for a given ItemDetails (using the ItemVisualLookup).
        /// </summary>
        private GameObject GetItemPrefab(ItemDetails itemDetails)
        {
             if (itemVisualLookup == null)
             {
                 Debug.LogError($"StorageObjectVisualizer ({gameObject.name}): ItemVisualLookup is not initialized!");
                 return null;
             }
             return itemVisualLookup.GetItemPrefab(itemDetails);
        }

        // --- Helper Structs for the Placement Algorithm and Tracking ---

        /// <summary>
        /// Represents a single item instance logically existing in the inventory.
        /// Stores ItemDetails and its original inventory slot index.
        /// Implements IEquatable for clearer comparison.
        /// </summary>
        private struct ItemInstanceInfo : IEquatable<ItemInstanceInfo> // Implement IEquatable for clearer comparison
        {
            public ItemDetails ItemDetails { get; }
            public int OriginalInventorySlotIndex { get; }
            // You might need a unique identifier for the instance if ItemDetails and SlotIndex aren't enough
            // e.g., if you can have multiple non-stackable items of the same type originating from the same slot
            // but representing distinct instances (less common with standard inventory arrays).
            // public int InstanceUniqueId; // Add this if needed

            public ItemInstanceInfo(ItemDetails details, int inventorySlotIndex, int instanceIndex) // Kept instanceIndex for potential future use or debugging
            {
                ItemDetails = details;
                OriginalInventorySlotIndex = inventorySlotIndex;
                // InstanceUniqueId = ... generate a unique ID here if needed ...;
            }

            // Implement IEquatable for type-safe comparison
            public bool Equals(ItemInstanceInfo other)
            {
                // Compare by ItemDetails and OriginalInventorySlotIndex.
                // Using ReferenceEquals check for ItemDetails might be safer if ItemDetails are ScriptableObjects
                 return ReferenceEquals(ItemDetails, other.ItemDetails) && // Compare ScriptableObject references
                       OriginalInventorySlotIndex == other.OriginalInventorySlotIndex;
                       // && (InstanceUniqueId == other.InstanceUniqueId); // Include if using unique ID
            }

            public override bool Equals(object obj)
            {
                if (obj is ItemInstanceInfo other)
                {
                    return Equals(other); // Use the type-safe Equals
                }
                return false;
            }

            public override int GetHashCode()
            {
                 unchecked // Overflow is fine
                 {
                     int hash = 17;
                     // Use GetInstanceID() for ScriptableObjects to get a unique hash per asset instance
                     hash = hash * 23 + (ItemDetails != null ? ItemDetails.GetInstanceID() : 0);
                     hash = hash * 23 + OriginalInventorySlotIndex.GetHashCode();
                     // if using InstanceUniqueId, add it: hash = hash * 23 + InstanceUniqueId.GetHashCode();
                     return hash;
                 }
            }

             public static bool operator ==(ItemInstanceInfo left, ItemInstanceInfo right) => left.Equals(right);
             public static bool operator !=(ItemInstanceInfo left, ItemInstanceInfo right) => !(left == right);
        }

        /// <summary>
        /// Represents the desired visual placement of an item instance onto a block of ShelfSlots.
        /// Links an item instance to the specific slots it should occupy.
        /// Implements IEquatable for comparison.
        /// </summary>
        private struct ItemInstancePlacement : IEquatable<ItemInstancePlacement> // Implement IEquatable
        {
            public ItemInstanceInfo ItemInstanceInfo { get; }
            public List<ShelfSlot> ShelfSlotBlock { get; } // The block of slots this instance should occupy

            public ItemInstancePlacement(ItemInstanceInfo instanceInfo, List<ShelfSlot> slotBlock)
            {
                ItemInstanceInfo = instanceInfo;
                ShelfSlotBlock = slotBlock;
            }

             // Implement IEquatable for type-safe comparison
             public bool Equals(ItemInstancePlacement other)
             {
                 // ItemInstanceInfo must match
                 if (!ItemInstanceInfo.Equals(other.ItemInstanceInfo))
                 {
                     return false;
                 }

                 // Slot blocks must match exactly (same slots in the same order)
                 // Use SequenceEqual for ordered list comparison
                 if (ShelfSlotBlock == null && other.ShelfSlotBlock == null) return true;
                 if (ShelfSlotBlock == null || other.ShelfSlotBlock == null) return false;

                 return ShelfSlotBlock.SequenceEqual(other.ShelfSlotBlock);
             }

             public override bool Equals(object obj)
             {
                 if (obj is ItemInstancePlacement other)
                 {
                     return Equals(other);
                 }
                 return false;
             }

             public override int GetHashCode()
             {
                  unchecked
                  {
                      int hash = ItemInstanceInfo.GetHashCode();
                      // Hashing lists is tricky and can be slow. SequenceEqual is used for comparison.
                      // If this struct is used as a dictionary key, a proper list hash code would be needed,
                      // potentially based on slot references/IDs in a consistent order.
                      // For now, just hash the ItemInstanceInfo as primary key.
                      return hash;
                  }
             }

             public static bool operator ==(ItemInstancePlacement left, ItemInstancePlacement right) => left.Equals(right);
             public static bool operator !=(ItemInstancePlacement left, ItemInstancePlacement right) => !(left == right);
        }

        /// <summary>
        /// Represents an item instance that is currently visually placed on the shelves.
        /// Links the item instance info to the actual instantiated GameObject and the slots it occupies.
        /// Implements IEquatable for comparison.
        /// </summary>
        private struct CurrentlyPlacedVisualItem : IEquatable<CurrentlyPlacedVisualItem>
        {
            public ItemInstanceInfo ItemInstanceInfo { get; }
            public GameObject VisualGameObject { get; } // Reference to the instantiated prefab
            public List<ShelfSlot> OccupiedSlots { get; } // The slots this visual item occupies

            public CurrentlyPlacedVisualItem(ItemInstanceInfo instanceInfo, GameObject visualGameObject, List<ShelfSlot> occupiedSlots)
            {
                ItemInstanceInfo = instanceInfo;
                VisualGameObject = visualGameObject;
                OccupiedSlots = occupiedSlots;
            }

            // Helper to check if this placed item matches a desired placement
             public bool MatchesDesiredPlacement(ItemInstancePlacement desiredPlacement)
             {
                 // Check if the item instance info matches
                 if (!ItemInstanceInfo.Equals(desiredPlacement.ItemInstanceInfo))
                 {
                     return false;
                 }

                 // Check if the occupied slots match (order matters for a specific placement)
                 if (OccupiedSlots == null && desiredPlacement.ShelfSlotBlock == null) return true;
                 if (OccupiedSlots == null || desiredPlacement.ShelfSlotBlock == null) return false;

                 return OccupiedSlots.SequenceEqual(desiredPlacement.ShelfSlotBlock); // Use SequenceEqual for ordered list comparison
             }

            // Implement IEquatable for type-safe comparison
            public bool Equals(CurrentlyPlacedVisualItem other)
            {
                // Compare all relevant fields for equality
                // Note: Comparing VisualGameObject by reference is important for tracking the specific instantiated object
                return ItemInstanceInfo.Equals(other.ItemInstanceInfo) &&
                       VisualGameObject == other.VisualGameObject && // Compare GameObject references
                       (OccupiedSlots == null && other.OccupiedSlots == null || (OccupiedSlots != null && other.OccupiedSlots != null && OccupiedSlots.SequenceEqual(other.OccupiedSlots))); // Compare slot lists
            }

            public override bool Equals(object obj)
            {
                if (obj is CurrentlyPlacedVisualItem other)
                {
                    return Equals(other);
                }
                return false;
            }

            public override int GetHashCode()
            {
                 unchecked
                 {
                     int hash = ItemInstanceInfo.GetHashCode();
                     hash = hash * 23 + (VisualGameObject != null ? VisualGameObject.GetHashCode() : 0);
                     // Hashing lists is tricky and can be slow. Use SequenceEqual for comparison.
                     // If this struct is used as a dictionary key, a proper list hash code would be needed.
                     return hash;
                 }
            }

             public static bool operator ==(CurrentlyPlacedVisualItem left, CurrentlyPlacedVisualItem right) => left.Equals(right);
             public static bool operator !=(CurrentlyPlacedVisualItem left, CurrentlyPlacedVisualItem right) => !(left == right);

        }
    }
}   