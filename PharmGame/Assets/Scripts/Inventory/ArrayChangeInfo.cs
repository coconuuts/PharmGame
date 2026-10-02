using UnityEngine; // Needed for Debug.LogError

namespace Systems.Inventory
{
    /// <summary>
    /// Specifies the type of change that occurred in an ObservableArray.
    /// </summary>
    public enum ArrayChangeType
    {
        /// <summary> An item was added to a previously empty slot. </summary>
        ItemAdded,
        /// <summary> An item was removed, leaving the slot empty. </summary>
        ItemRemoved,
        /// <summary> An item's quantity changed within the same slot/instance (requires custom logic to detect). </summary>
        QuantityChanged, // Note: Detecting this requires checking old vs new item quantity/details
        /// <summary> A specific slot's content was updated (can be add, remove, or replace). This is a general 'something changed here' notification. </summary>
        SlotUpdated,
        /// <summary> Items were swapped between two slots. </summary>
        ItemsSwapped,
        /// <summary> The entire array was cleared. </summary>
        ArrayCleared,
        /// <summary> The initial state of the array upon subscription or loading. </summary>
        InitialLoad
    }

    /// <summary>
    /// Contains information about a change that occurred in an ObservableArray.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array.</typeparam>
    public class ArrayChangeInfo<T>
    {
        /// <summary> The type of change that occurred. </summary>
        public ArrayChangeType Type { get; }

        /// <summary> The primary index affected by the change. </summary>
        public int Index { get; }

        /// <summary> The item/value at the primary index BEFORE the change. </summary>
        public T OldItem { get; }

        /// <summary> The item/value at the primary index AFTER the change. </summary>
        public T NewItem { get; }

        /// <summary> The secondary index affected by the change (used for Swaps). </summary>
        public int TargetIndex { get; }

        /// <summary> The item/value at the secondary index BEFORE the change (used for Swaps). </summary>
        public T OldTargetItem { get; }

        /// <summary> The item/value at the secondary index AFTER the change (used for Swaps). </summary>
        public T NewTargetItem { get; }

        /// <summary> The full state of the array AFTER the change (used for Clear or InitialLoad). </summary>
        public T[] CurrentArrayState { get; }

        // --- Constructors ---

        // Constructor for single-slot changes (ItemAdded, ItemRemoved, SlotUpdated, QuantityChanged)
        public ArrayChangeInfo(ArrayChangeType type, int index, T oldItem, T newItem)
        {
            if (type == ArrayChangeType.ItemsSwapped || type == ArrayChangeType.ArrayCleared || type == ArrayChangeType.InitialLoad)
            {
                Debug.LogError($"ArrayChangeInfo single-slot constructor called with incorrect type: {type}");
            }
            Type = type;
            Index = index;
            OldItem = oldItem;
            NewItem = newItem;
            TargetIndex = -1; // Indicate no secondary index
            OldTargetItem = default;
            NewTargetItem = default;
            CurrentArrayState = null; // Not needed for single-slot changes
        }

        // Constructor for swap changes
        public ArrayChangeInfo(ArrayChangeType type, int index1, T oldItem1, T newItem1, int index2, T oldItem2, T newItem2)
        {
            if (type != ArrayChangeType.ItemsSwapped)
            {
                Debug.LogError($"ArrayChangeInfo swap constructor called with incorrect type: {type}");
            }
            Type = ArrayChangeType.ItemsSwapped;
            Index = index1;
            OldItem = oldItem1;
            NewItem = newItem1;
            TargetIndex = index2;
            OldTargetItem = oldItem2;
            NewTargetItem = newItem2;
            CurrentArrayState = null;
        }

        // Constructor for full array changes (ArrayCleared, InitialLoad)
        public ArrayChangeInfo(ArrayChangeType type, T[] currentArrayState)
        {
            if (type != ArrayChangeType.ArrayCleared && type != ArrayChangeType.InitialLoad)
             {
                 Debug.LogError($"ArrayChangeInfo full array constructor called with incorrect type: {type}");
             }
            Type = type;
            CurrentArrayState = currentArrayState;
            Index = -1; // Indicate no single primary index
            OldItem = default;
            NewItem = default;
            TargetIndex = -1;
            OldTargetItem = default;
            NewTargetItem = default;
        }
    }
}