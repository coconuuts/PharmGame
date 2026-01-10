using System;
using System.Collections.Generic;
using Systems.Inventory; // For ItemLabel, SerializableGuid
using UnityEngine;

namespace Systems.Persistence {
    /// <summary>
    /// Serializable data for an Inventory component.
    /// </summary>
    [Serializable]
    public class InventoryData : ISaveable { // Implement ISaveable to fit SaveLoadSystem.Bind signature
        [SerializeField] private SerializableGuid _id;

        // The Interface accesses the field via this property
        public SerializableGuid Id { 
            get => _id; 
            set => _id = value; 
        }
        public List<ItemData> items; // List of items in this inventory
        public List<ItemLabel> allowedLabels;
        public bool allowAllIfListEmpty;

        public InventoryData() {
            items = new List<ItemData>();
            allowedLabels = new List<ItemLabel>();
        }
    }

    /// <summary>
    /// Serializable data for a single Item instance.
    /// </summary>
    [Serializable]
    public class ItemData {
        public SerializableGuid Id; // The unique instance ID of this specific Item
        public SerializableGuid ItemDetailsId; // The ID of the ItemDetails ScriptableObject
        public int quantity;
        public int health;
        public int usageEventsSinceLastLoss;
        public int currentMagazineHealth;
        public int totalReserveHealth;
        public bool isReloading;
        public float reloadStartTime;
        public string patientNameTag;
    }
}