using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Systems.Inventory; // Adjust namespace if your ItemDetails is elsewhere

namespace VisualStorage // Your namespace for visual storage related things
{
    /// <summary>
    /// Provides a lookup service to get the 3D prefab for an ItemDetails using a mapping ScriptableObject.
    /// </summary>
    public class ItemVisualLookup
    {
        private Dictionary<ItemDetails, GameObject> itemPrefabsDictionary;

        public ItemVisualLookup(ItemVisualMappingSO mappingAsset)
        {
            itemPrefabsDictionary = new Dictionary<ItemDetails, GameObject>();

            if (mappingAsset == null)
            {
                Debug.LogError("ItemVisualLookup: Mapping ScriptableObject is null!");
                return;
            }

            if (mappingAsset.itemPrefabMappings != null)
            {
                foreach (var mapping in mappingAsset.itemPrefabMappings)
                {
                    if (mapping.itemDetails != null && mapping.prefab3D != null)
                    {
                         if (!itemPrefabsDictionary.ContainsKey(mapping.itemDetails))
                         {
                             itemPrefabsDictionary.Add(mapping.itemDetails, mapping.prefab3D);
                         }
                         else
                         {
                             Debug.LogWarning($"ItemVisualLookup: Duplicate ItemDetails mapping found for '{mapping.itemDetails.Name}'. Using the first one.");
                         }
                    }
                    else
                    {
                         Debug.LogWarning("ItemVisualLookup: Item Prefab Mapping has null ItemDetails or Prefab for a slot.");
                    }
                }
            }
            else
            {
                 Debug.LogWarning("ItemVisualLookup: Item Prefab Mappings list in the ScriptableObject is null!");
            }
        }

        /// <summary>
        /// Gets the 3D prefab associated with the given ItemDetails.
        /// Returns null if no mapping is found or if ItemDetails is null.
        /// </summary>
        public GameObject GetItemPrefab(ItemDetails itemDetails)
        {
            if (itemDetails != null && itemPrefabsDictionary != null && itemPrefabsDictionary.TryGetValue(itemDetails, out GameObject prefab))
            {
                return prefab;
            }
            else if (itemDetails != null)
            {
                 Debug.LogWarning($"ItemVisualLookup: No 3D prefab mapping found for ItemDetails: {itemDetails.Name}.");
            }
            return null;
        }
    }
}