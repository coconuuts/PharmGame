using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Systems.Inventory
{
    public interface IObservableArray<T> where T : Item 
    {
         event Action<ArrayChangeInfo<T>> AnyValueChanged;

         int Count { get; }
         int Length { get; }

         T this[int index] { get; }

         void SetItemAtIndex(T item, int index);
         void Swap(int index1, int index2);
         void Clear();
         void RemoveAt(int index);
         bool TryAdd(T item); // This TryAdd adds to the first *any* empty slot (physical or ghost) - Combiner AddItem is smarter
         bool TryRemove(T item); // This TryRemove removes the first *instance* match

         void TriggerInitialLoadEvent();

         // --- ADD THIS METHOD FOR DROP LOGIC ---
         // HandleDrop logic is coupled to Item type, so maybe this should be on Combiner instead?
         // Let's put it here for now as requested, assuming T is Item.
         // **Refinement:** It's often cleaner if Combiner handles the high-level drop logic
         // using the ObservableArray's SetItemAtIndex, Swap, etc. But let's follow the plan.
         void HandleDrop(T itemToDrop, int targetIndex, ObservableArray<T> sourceArray, int sourceOriginalIndex); // Added sourceOriginalIndex

        /// <summary>
        /// Provides read access to the current state of the internal item array.
        /// </summary>
        Item[] GetCurrentArrayState();
    }

    [Serializable]
    public class ObservableArray<T> : IObservableArray<T> where T : Item // Constrain T to be Item for drop logic
    {
        // ... (Existing fields and Awake/Start remain)
        private T[] items;
        public event Action<ArrayChangeInfo<T>> AnyValueChanged = delegate { };
        public int Count => items.Count(i => !EqualityComparer<T>.Default.Equals(i, default(T)));
        public int Length => items.Length;
        public T this[int index] => items[index];

        public ObservableArray(int size = 20, IList<T> initialList = null)
        {
             if (size <= 0) size = 1;
             items = new T[size];
             if (initialList != null)
             {
                 int copyCount = Mathf.Min(size, initialList.Count);
                 for (int i = 0; i < copyCount; i++)
                 {
                     items[i] = initialList[i];
                 }
             }
        }

         // ... (Invoke, TriggerInitialLoadEvent methods remain)
         void Invoke(ArrayChangeInfo<T> changeInfo) => AnyValueChanged.Invoke(changeInfo);

         public void TriggerInitialLoadEvent()
         {
             var changeInfo = new ArrayChangeInfo<T>(ArrayChangeType.InitialLoad, items);
             Invoke(changeInfo);
         }


        /// <summary>
        /// Sets the item at a specific index and triggers the event with SlotUpdated info.
        /// Use with caution, does not check if the index is already occupied.
        /// </summary>
        /// <param name="item">The item to place at the index (can be null to clear).</param>
        /// <param name="index">The index in the array.</param>
        public void SetItemAtIndex(T item, int index)
        {
            if (index < 0 || index >= items.Length)
            {
                Debug.LogError($"ObservableArray ({typeof(T).Name}): SetItemAtIndex - Index {index} is out of bounds (Length: {items.Length}).");
                return;
            }

            T oldItem = items[index];
            items[index] = item;
            T newItem = items[index];

            // Check if the item reference or quantity *actually* changed before sending event?
            // Or rely on the Visualizer to handle no-change updates?
            // Let's always send SlotUpdated for simplicity, Visualizer can optimize if needed.

             var changeInfo = new ArrayChangeInfo<T>(ArrayChangeType.SlotUpdated, index, oldItem, newItem);
             Invoke(changeInfo);
        }

        // ... (Swap, Clear, TryAdd, TryRemove, RemoveAt methods remain the same)
         public void Swap(int index1, int index2)
        {
             if (index1 < 0 || index1 >= items.Length || index2 < 0 || index2 >= items.Length)
             {
                 Debug.LogError($"ObservableArray ({typeof(T).Name}): Swap - Indices {index1}, {index2} are out of bounds (Length: {items.Length}).");
                 return;
             }

             if (index1 == index2) return;

             T oldItem1 = items[index1];
             T oldItem2 = items[index2];

             (items[index1], items[index2]) = (items[index2], items[index1]);

             T newItem1 = items[index1];
             T newItem2 = items[index2];

             var changeInfo = new ArrayChangeInfo<T>(ArrayChangeType.ItemsSwapped, index1, oldItem1, newItem1, index2, oldItem2, newItem2);
             Invoke(changeInfo);
        }

         public void Clear()
        {
             items = new T[items.Length];
             var changeInfo = new ArrayChangeInfo<T>(ArrayChangeType.ArrayCleared, items);
             Invoke(changeInfo);
        }

         // TryAdd adds to the first *any* available slot (physical or ghost)
         public bool TryAdd(T item)
        {
             if (EqualityComparer<T>.Default.Equals(item, default(T)))
             {
                 Debug.LogWarning($"ObservableArray ({typeof(T).Name}): TryAdd - Attempted to add a null or default item.");
                 return false;
             }
             for (var i = 0; i < items.Length; i++)
             {
                 if (EqualityComparer<T>.Default.Equals(items[i], default(T)))
                 {
                     SetItemAtIndex(item, i); // Use SetItemAtIndex to trigger event
                     return true;
                 }
             }
             Debug.LogWarning($"ObservableArray ({typeof(T).Name}): TryAdd - Array is full.");
             return false;
        }

         // TryRemove removes the first *instance* match
         public bool TryRemove(T item)
        {
             if (EqualityComparer<T>.Default.Equals(item, default(T)))
             {
                 Debug.LogWarning($"ObservableArray ({typeof(T).Name}): TryRemove - Cannot remove a null or default item by value.");
                 return false;
             }
             for (var i = 0; i < items.Length; i++)
             {
                 if (EqualityComparer<T>.Default.Equals(items[i], item))
                 {
                     RemoveAt(i); // Use RemoveAt to trigger event
                     return true;
                 }
             }
             Debug.LogWarning($"ObservableArray ({typeof(T).Name}): TryRemove - Item not found in array.");
             return false;
        }

         public void RemoveAt(int index)
        {
             SetItemAtIndex(default, index); // Sets to null/default and triggers event
        }

        // NotifyQuantityChanged is still optional helper, not used internally by OA methods yet
         public void NotifyQuantityChanged(int index, T oldItem, T newItem)
         {
              if (index < 0 || index >= items.Length)
             {
                  Debug.LogError($"ObservableArray ({typeof(T).Name}): NotifyQuantityChanged - Index {index} is out of bounds (Length: {items.Length}).");
                  return;
             }
             if (!ReferenceEquals(items[index], newItem)) // Check if the item reference is still the same instance
             {
                  Debug.LogWarning($"ObservableArray ({typeof(T).Name}): NotifyQuantityChanged called for index {index} but provided new item reference does not match current item reference.");
                  // Maybe force update anyway?
                  SetItemAtIndex(items[index], index); // Force SlotUpdated with current item state
                  return;
             }

              // Only trigger QuantityChanged if quantity actually changed (requires Item logic here)
             // This makes OA coupled to Item's quantity property.
             // Alternative: Only use SlotUpdated and let Visualizer figure out quantity change.
             // Let's stick to SlotUpdated for now for better T generality.
             // If you really need QuantityChanged specifically, you need to add logic here.

             // For now, let's assume quantity changes are handled by SlotUpdated event
             // triggered by SetItemAtIndex when Combiner updates quantity.
              Debug.LogWarning($"ObservableArray ({typeof(T).Name}): NotifyQuantityChanged not fully implemented or used. Using SlotUpdated instead.");
             SetItemAtIndex(items[index], index); // Fallback to SlotUpdated
         }


        /// <summary>
        /// Handles the logic when an item is dropped onto this inventory's ObservableArray.
        /// This method is called by the DragAndDropManager.
        /// It manages adding, swapping, or stacking the item, potentially interacting with the source array.
        /// </summary>
        /// <param name="itemToDrop">The item instance being dropped.</param>
        /// <param name="targetIndex">The index in THIS array (the target) where the item was dropped.</param>
        /// <param name="sourceArray">The ObservableArray the item originated from.</param>
        /// <param name="sourceOriginalIndex">The original physical slot index in the source array.</param>
        public void HandleDrop(T itemToDrop, int targetIndex, ObservableArray<T> sourceArray, int sourceOriginalIndex)
        {
            // Ensure the target index is within the physical slot bounds of THIS array
             // (Combiner provides PhysicalSlotCount, need access to it here or pass it)
             // Let's assume targetIndex check against PhysicalSlotCount is done by DragAndDropManager before calling this.
             // But check array bounds:
             if (targetIndex < 0 || targetIndex >= items.Length) // Check against total length for safety, although D&D should target physical range
             {
                 Debug.LogError($"ObservableArray ({typeof(T).Name}): HandleDrop - Target index {targetIndex} is out of bounds for target array (Length: {items.Length}).");
                 // Item remains in source ghost slot
                 // Need to explicitly move it back to sourceOriginalIndex here if the drop was invalid?
                 // Or DragAndDropManager handles the 'return to source' if HandleDrop fails/is invalid.
                 // Let's assume DragAndDropManager handles return if this method fails.
                 sourceArray.SetItemAtIndex(itemToDrop, sourceArray.Length - 1); // Ensure it's still in source ghost if drop fails
                 return; // Drop failed on target
             }

             if (itemToDrop == null)
             {
                 Debug.LogWarning($"ObservableArray ({typeof(T).Name}): HandleDrop called with null item to drop.");
                 // Clear item from source ghost slot
                 sourceArray.SetItemAtIndex(null, sourceArray.Length - 1);
                 return;
             }

            T targetItem = items[targetIndex]; // Get the item currently in the target slot

             Debug.Log($"ObservableArray ({typeof(T).Name}): Handling drop for '{itemToDrop.details?.Name ?? "Unknown"}' (Qty: {itemToDrop.quantity}) onto slot {targetIndex} (contains: {(targetItem != null ? targetItem.details?.Name ?? "Unknown" : "Empty")}) in target array. Source Original Index: {sourceOriginalIndex}.");


            // --- Drop Logic ---

            // Case 1: Target is empty
            if (targetItem == null)
            {
                Debug.Log($"ObservableArray ({typeof(T).Name}): Dropping onto empty slot {targetIndex}.");
                SetItemAtIndex(itemToDrop, targetIndex); // Place the item in the target slot
                sourceArray.SetItemAtIndex(null, sourceArray.Length - 1); // Clear item from source ghost slot
            }
            // Case 2: Target has the same type and is stackable
            else if (targetItem.CanStackWith(itemToDrop)) // Uses Item.CanStackWith
            {
                Debug.Log($"ObservableArray ({typeof(T).Name}): Dropping onto stackable item of same type at slot {targetIndex}.");

                // New logic: Check if the combined quantity is within the max stack limit
                if (targetItem.quantity + itemToDrop.quantity <= targetItem.details.maxStack)
                {
                    // Proceed with stacking logic
                    int numToStack = itemToDrop.quantity; // Stack the entire dropped quantity

                    targetItem.quantity += numToStack; // Add to target stack
                    itemToDrop.quantity -= numToStack; // Reduce quantity being dropped (will be 0)

                    // Notify the target array that the target slot quantity changed
                    SetItemAtIndex(targetItem, targetIndex); // Triggers SlotUpdated for target

                    Debug.Log($"ObservableArray ({typeof(T).Name}): Stacked {numToStack}. Target Qty: {targetItem.quantity}. Dropped Qty Remaining: {itemToDrop.quantity}.");

                    // Item being dropped is fully consumed by stacking
                    Debug.Log($"ObservableArray ({typeof(T).Name}): Item fully stacked. Clearing source ghost slot.");
                    sourceArray.SetItemAtIndex(null, sourceArray.Length - 1); // Clear item from source ghost slot
                }
                else
                {
                    // The sum exceeds max stack, perform a swap
                    Debug.Log($"ObservableArray ({typeof(T).Name}): Sum of quantities exceeds max stack. Performing swap.");
                    PerformSwap(itemToDrop, targetIndex, targetItem, sourceArray, sourceOriginalIndex); // Use helper for swap
                }
            }
            // If not Case 1 or Case 2 (target is not empty and either different type or not stackable)
            else
            {
                Debug.Log($"ObservableArray ({typeof(T).Name}): Dropping onto different type or non-stackable item. Performing swap.");
                PerformSwap(itemToDrop, targetIndex, targetItem, sourceArray, sourceOriginalIndex); // Use helper for swap
            }
        }

        // Helper method for performing the swap logic during a drop
        private void PerformSwap(T itemToDrop, int targetIndex, T targetItem, ObservableArray<T> sourceArray, int sourceOriginalIndex)
        {
             Debug.Log($"ObservableArray ({typeof(T).Name}): Performing swap between item being dropped and item at target slot {targetIndex}.");

             // Place the item being dropped into the target slot
             SetItemAtIndex(itemToDrop, targetIndex); // Triggers SlotUpdated for target

             // Put the item that was originally in the target slot back into the source's ORIGINAL slot
             // This clears the source ghost slot as part of the process if sourceOriginalIndex is not the ghost index.
             // Handle cross-inventory swap: targetItem goes back to sourceOriginalIndex in sourceArray.
             sourceArray.SetItemAtIndex(targetItem, sourceOriginalIndex); // Triggers SlotUpdated for source original

             // The item was moved from the source ghost slot (Length-1) to targetIndex.
             // The item that was at targetIndex is moved to sourceOriginalIndex.
             // The ghost slot should now be empty if sourceOriginalIndex != Length-1.
             // If sourceOriginalIndex was the ghost slot (meaning we dragged from the ghost),
             // then targetItem goes to the ghost slot, which is not typical D&D, but the logic handles it.
             // Let's explicitly clear the ghost slot just in case, although SetItemAtIndex(targetItem, sourceOriginalIndex)
             // on the source array should cover it unless sourceOriginalIndex == sourceArray.Length - 1.
             // sourceArray.SetItemAtIndex(default, sourceArray.Length - 1); // This seems redundant if the above covers it.

             // Let's ensure the ghost slot is empty in the source array after a successful drop/swap
             // unless the item being dropped was only partially stacked.
             // The current logic in HandleDrop correctly clears the ghost slot (sourceArray.SetItemAtIndex(null, sourceArray.Length - 1))
             // when the itemToDrop quantity reaches <= 0 (fully stacked or swapped). This seems correct.
        }

        /// <summary>
        /// Provides read access to the current state of the internal item array.
        /// </summary>
        public Item[] GetCurrentArrayState() // Implementing the interface method
        {
            // You can choose to return the direct reference or a copy.
            // Returning direct reference is more performant for read-only access.
            // Returning a copy prevents external modification but adds overhead.
            // Let's return the direct reference for performance in this context.
            return items;
        }
    }
}