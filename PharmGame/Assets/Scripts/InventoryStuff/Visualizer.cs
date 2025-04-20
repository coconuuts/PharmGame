using UnityEngine;
using Systems.Inventory;
using System.Collections.Generic;
using System;

namespace Systems.Inventory
{
    public class Visualizer : MonoBehaviour
    {
        private ObservableArray<Item> inventoryState;

        // --- ADD THIS DECLARATION ---
        private List<InventorySlotUI> slotUIComponents;
        // --------------------------

        // Removed direct FlexibleGridLayout reference finding in Awake

        /// <summary>
        /// Sets the list of visual InventorySlotUI components for this Visualizer to manage.
        /// Called by the Inventory script.
        /// </summary>
        public void SetSlotUIComponents(List<InventorySlotUI> slotUis)
        {
            if (slotUis == null)
            {
                Debug.LogError($"Visualizer ({gameObject.name}): SetSlotUIComponents received a null list.", this);
                return;
            }
            slotUIComponents = slotUis; // This line now works because slotUIComponents is declared
            Debug.Log($"Visualizer ({gameObject.name}): Received {slotUis.Count} slot UI components.", this);
            // No visual update needed here, RefreshUI will be called by InitialLoad event
        }

        private void Awake()
        {
            Debug.Log($"Visualizer ({gameObject.name}): Awake started. Waiting for Inventory to provide components.", this);
            // Slot finding logic moved to Inventory.Awake
        }

        private void OnDestroy()
        {
            if (inventoryState != null)
            {
                inventoryState.AnyValueChanged -= HandleInventoryChange;
                Debug.Log($"Visualizer ({gameObject.name}): Unsubscribed from state in OnDestroy.", this);
            }
        }

        public void SetInventoryState(ObservableArray<Item> state)
        {
            Debug.Log($"Visualizer ({gameObject.name}): Attempting to set inventory state.", this);
            if (inventoryState != null)
            {
                inventoryState.AnyValueChanged -= HandleInventoryChange;
                Debug.Log($"Visualizer ({gameObject.name}): Unsubscribed from previous state.", this);
            }

            inventoryState = state;

            if (inventoryState != null)
            {
                inventoryState.AnyValueChanged += HandleInventoryChange;
                Debug.Log($"Visualizer ({gameObject.name}): Subscribed to new state.", this);

                Debug.Log($"Visualizer ({gameObject.name}): Requesting initial load event from ObservableArray.", this);
                inventoryState.TriggerInitialLoadEvent(); // Call the specific initial load trigger
            }
             else
             {
                Debug.Log($"Visualizer ({gameObject.name}): Inventory state set to null. Clearing UI.", this);
                var clearInfo = new ArrayChangeInfo<Item>(ArrayChangeType.ArrayCleared, null);
                 HandleInventoryChange(clearInfo);
             }
        }

        private void HandleInventoryChange(ArrayChangeInfo<Item> changeInfo)
        {
             // This method now works because slotUIComponents is declared
             if (changeInfo == null) return;
             Debug.Log($"Visualizer ({gameObject.name}): HandleInventoryChange called. Type: {changeInfo.Type}, Index: {changeInfo.Index}, TargetIndex: {changeInfo.TargetIndex}.", this);

             // Added null check for slotUIComponents before accessing Count
             if (slotUIComponents == null)
             {
                 Debug.LogError($"Visualizer ({gameObject.name}): HandleInventoryChange called but slotUIComponents is null. Cannot update UI.", this);
                 return;
             }

             int visualSlotCount = slotUIComponents.Count;
             if (visualSlotCount == 0)
             {
                 Debug.LogWarning($"Visualizer ({gameObject.name}): No visual slots found to update.", this);
                 return;
             }

             Item[] currentItems = changeInfo.CurrentArrayState; // <--- Used for InitialLoad/ArrayCleared

             switch (changeInfo.Type)
             {
                 case ArrayChangeType.InitialLoad:
                 case ArrayChangeType.ArrayCleared:
                      Debug.Log($"Visualizer ({gameObject.name}): Handling InitialLoad or ArrayCleared. Refreshing all {visualSlotCount} visible slots.", this);
                     for (int i = 0; i < visualSlotCount; i++)
                     {
                         Item item = (currentItems != null && i < currentItems.Length) ? currentItems[i] : null;
                          // This line now works because slotUIComponents is declared
                         slotUIComponents[i].SetItem(item);
                     }
                     // Clear any extra visual slots if the array is smaller (shouldn't happen with ghost slot)
                     for (int i = visualSlotCount; i < slotUIComponents.Count; i++)
                      {
                           // This line now works because slotUIComponents is declared
                           slotUIComponents[i].SetItem(null);
                      }
                     break;

                 case ArrayChangeType.ItemAdded:
                 case ArrayChangeType.ItemRemoved:
                 case ArrayChangeType.SlotUpdated:
                 case ArrayChangeType.QuantityChanged:
                     int singleIndex = changeInfo.Index;
                     if (singleIndex >= 0 && singleIndex < visualSlotCount)
                     {
                          Debug.Log($"Visualizer ({gameObject.name}): Handling single slot update at index {singleIndex}. Calling SetItem.", this);
                          // This line now works because slotUIComponents is declared
                          slotUIComponents[singleIndex].SetItem(changeInfo.NewItem);
                     }
                     else
                     {
                         Debug.Log($"Visualizer ({gameObject.name}): Single slot update received for non-visual index: {singleIndex}. Ignoring visual update.", this);
                     }
                     break;

                 case ArrayChangeType.ItemsSwapped:
                     int index1 = changeInfo.Index;
                     int index2 = changeInfo.TargetIndex;

                     bool updatedVisualSlot1 = false;
                     bool updatedVisualSlot2 = false;

                     if (index1 >= 0 && index1 < visualSlotCount)
                     {
                          Debug.Log($"Visualizer ({gameObject.name}): Handling swap. Updating visual slot {index1}.", this);
                          // This line now works because slotUIComponents is declared
                          slotUIComponents[index1].SetItem(changeInfo.NewItem);
                          updatedVisualSlot1 = true;
                     }
                     else
                     {
                         Debug.Log($"Visualizer ({gameObject.name}): Swap involved data index {index1} (potentially ghost), which is outside visual range (0-{visualSlotCount-1}). Not updating visual slot for this index.", this);
                     }

                     if (index2 >= 0 && index2 < visualSlotCount)
                     {
                           Debug.Log($"Visualizer ({gameObject.name}): Handling swap. Updating visual slot {index2}.", this);
                           // This line now works because slotUIComponents is declared
                           slotUIComponents[index2].SetItem(changeInfo.NewTargetItem);
                           updatedVisualSlot2 = true;
                     }
                     else
                     {
                          Debug.Log($"Visualizer ({gameObject.name}): Swap involved data index {index2} (potentially ghost), which is outside visual range (0-{visualSlotCount-1}). Not updating visual slot for this index.", this);
                     }

                     if (!updatedVisualSlot1 && !updatedVisualSlot2)
                     {
                          Debug.Log($"Visualizer ({gameObject.name}): Swap change received but neither index ({index1}, {index2}) was within the visual slot range (0-{visualSlotCount-1}). No visual update performed.", this);
                     }

                     break;

                 default:
                     Debug.LogWarning($"Visualizer ({gameObject.name}): Received unhandled ArrayChangeType: {changeInfo.Type}", this);
                     break;
             }
        }

        // Need a public method for Inventory to set the list of slots
         // This is now handled by SetSlotUIComponents
    }
}