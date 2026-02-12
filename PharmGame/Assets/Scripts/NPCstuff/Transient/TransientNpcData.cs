using UnityEngine;
using System;
using System.Collections.Generic;
using Game.Prescriptions;

namespace Game.NPC
{
    [Serializable]
    public class TransientNpcData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public string CurrentStateEnumKey;
        public string CurrentStateEnumType;
        public int SavedBrowseLocationIndex = -1;
        public int QueueIndex = -1;
        public string QueueTypeString;
        public List<TransientInventoryItemData> InventoryItems;
        public bool HasPendingPrescription;
        public PrescriptionOrder AssignedOrder;

        public TransientNpcData()
        {
            InventoryItems = new List<TransientInventoryItemData>();
            AssignedOrder = new PrescriptionOrder();
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