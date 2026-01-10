using System.Collections.Generic;
using UnityEngine;

namespace Systems.Inventory {
    public static class ItemDatabase {
        static Dictionary<SerializableGuid, ItemDetails> itemDetailsDictionary;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Initialize() {
            itemDetailsDictionary = new Dictionary<SerializableGuid, ItemDetails>();

            var itemDetails = Resources.LoadAll<ItemDetails>("");
            
            foreach (var item in itemDetails) {
                if (itemDetailsDictionary.ContainsKey(item.Id)) {
                    Debug.LogError($"ItemDatabase: Duplicate ID detected! {item.Name} and {itemDetailsDictionary[item.Id].Name} share ID {item.Id}");
                    continue;
                }
                itemDetailsDictionary.Add(item.Id, item);
            }

            Debug.Log($"ItemDatabase: Initialized. Loaded {itemDetailsDictionary.Count} items.");
        }

        public static ItemDetails GetDetailsById(SerializableGuid id) {
            if (itemDetailsDictionary.TryGetValue(id, out ItemDetails details)) {
                return details;
            }
            
            // This error will now print the actual HEX ID thanks to the ToString fix
            Debug.LogError($"ItemDatabase: Cannot find item details with ID: {id}. Total items in DB: {itemDetailsDictionary?.Count ?? 0}");
            return null;
        }
    }
}