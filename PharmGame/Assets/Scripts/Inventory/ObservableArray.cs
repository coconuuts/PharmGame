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

         // Removed HandleDrop - logic moved to DragAndDropManager

        /// <summary>
        /// Provides read access to the current state of the internal item array.
        /// </summary>
        Item[] GetCurrentArrayState();

        // Added ParentInventory property
        Inventory ParentInventory { get; }
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

        // Added ParentInventory field and public property
        [NonSerialized] // Don't try to serialize this runtime reference
        private Inventory parentInventory;
        public Inventory ParentInventory { get => parentInventory; internal set => parentInventory = value; }


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

             (items[index1], items[index2]) = (items[index2], items[index1]); // Corrected swap syntax

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

        // Removed HandleDrop method

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