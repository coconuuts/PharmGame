using UnityEngine;
using System;
using System.Collections.Generic;

namespace Game.NPC
{
    [Serializable]
    public class TransientNpcData
    {
        // --- Physical State ---
        public Vector3 Position;
        public Quaternion Rotation;

        // --- Logic State ---
        // Storing as strings to handle different Enum types (CustomerState, GeneralState, etc.)
        public string CurrentStateEnumKey;
        public string CurrentStateEnumType;

        public int SavedBrowseLocationIndex = -1;
        public int QueueIndex = -1;
        public string QueueTypeString;

        // --- Inventory State ---
        public List<TransientInventoryItemData> InventoryItems;

        public TransientNpcData()
        {
            InventoryItems = new List<TransientInventoryItemData>();
        }
    }

    [Serializable]
    public struct TransientInventoryItemData
    {
        public string ItemId;
        public int Quantity;

        public TransientInventoryItemData(string id, int qty)
        {
            ItemId = id;
            Quantity = qty;
        }
    }
}