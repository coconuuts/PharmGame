using UnityEngine;
using System.Collections.Generic;
using Systems.Inventory; // Adjust namespace if your ItemDetails is elsewhere

namespace VisualStorage // Your namespace for visual storage related things
{
    [CreateAssetMenu(fileName = "ItemVisualMapping", menuName = "Inventory/Item Visual Mapping", order = 10)]
    public class ItemVisualMappingSO : ScriptableObject
    {
        [System.Serializable]
        public struct ItemPrefabMapping
        {
            public ItemDetails itemDetails;
            public GameObject prefab3D;
        }

        [Tooltip("Map ItemDetails ScriptableObjects to their corresponding 3D item prefab GameObjects.")]
        public List<ItemPrefabMapping> itemPrefabMappings;

        // We could build the dictionary here on Awake/OnEnable in the SO,
        // but it's often cleaner to build it in the consumer (ItemVisualLookup)
        // to avoid potential issues with SO loading order or editor scripting context.
    }
}   