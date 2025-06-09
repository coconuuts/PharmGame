using UnityEngine;
using Systems.Inventory;
using System.Collections.Generic;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Systems.Inventory
{
    public class Inventory : MonoBehaviour
    {
        [SerializeField] private SerializableGuid id = SerializableGuid.Empty;
        [SerializeField] private Combiner combiner;
        [SerializeField] private FlexibleGridLayout flexibleGridLayout;
        [SerializeField] private Visualizer visualizer; // Ensure this is assigned/found

        [Header("Item Filtering")] // Optional: A header for organization in the inspector
        [SerializeField] private List<ItemLabel> allowedLabels = new List<ItemLabel>();
        [Tooltip("If this list is empty, all item labels are allowed.")] // Clarify behavior
        [SerializeField] private bool allowAllIfListEmpty = true; // Option to allow all if the list is left empty


        public SerializableGuid Id => id;
        public Combiner Combiner => combiner;
        public ObservableArray<Item> InventoryState => combiner?.InventoryState;

        private void OnValidate()
        {
            if (id == SerializableGuid.Empty)
            {
                id = SerializableGuid.NewGuid();
#if UNITY_EDITOR
                Debug.Log($"Inventory ({gameObject.name}): Assigned new unique ID in OnValidate: {id}", this);
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }

        private void Awake()
        {
            // Get component references
            if (combiner == null) combiner = GetComponent<Combiner>();
            if (combiner != null)
            {
                combiner.ParentInventory = this; // Assign parent reference
            }
            if (flexibleGridLayout == null) flexibleGridLayout = GetComponent<FlexibleGridLayout>();
            if (visualizer == null) visualizer = GetComponent<Visualizer>(); // Get Visualizer

            if (combiner == null || flexibleGridLayout == null || visualizer == null)
            {
                Debug.LogError($"Inventory on {gameObject.name} is missing required components (Combiner, FlexibleGridLayout, or Visualizer). Inventory functionality may be limited.", this);
                // Don't disable immediately in Awake, let Start potentially catch it
            }
            else
            {
                FindAndAssignSlotUIs();

                 // --- Register this inventory with the DragAndDropManager ---
                DragAndDropManager.RegisterInventory(this);

            }
        }

         private void FindAndAssignSlotUIs()
        {
             if (flexibleGridLayout == null)
             {
                 Debug.LogError($"Inventory ({gameObject.name}): Cannot find and assign SlotUIs, FlexibleGridLayout is null.", this);
                 return;
             }

             List<InventorySlotUI> foundSlots = new List<InventorySlotUI>();

             for (int i = 0; i < flexibleGridLayout.transform.childCount; i++)
             {
                GameObject slotGameObject = flexibleGridLayout.transform.GetChild(i).gameObject;
                InventorySlotUI slotUI = slotGameObject.GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    slotUI.SlotIndex = i; // Assign index
                    slotUI.ParentInventory = this; // *** Assign Parent Inventory Reference ***
                    foundSlots.Add(slotUI);
                     // Debug.Log($"Inventory: Found and assigned InventorySlotUI at index {i} on {slotGameObject.name}.", this); // Optional: very verbose
                }
                else
                {
                    Debug.LogWarning($"Inventory ({gameObject.name}): Child {i} ({slotGameObject.name}) of FlexibleGridLayout is missing InventorySlotUI component.", slotGameObject);
                }
             }

             // Pass the found list to the Visualizer (Visualizer needs a method to accept this list)
             if (visualizer != null)
             {
                 visualizer.SetSlotUIComponents(foundSlots); 
             }
             else
             {
                  Debug.LogError($"Inventory ({gameObject.name}): Visualizer is null, cannot pass slot UI list.", this);
             }
        }


        private void Start()
        {
            // Only attempt to link if we have Combiner and Visualizer
            if (combiner != null && visualizer != null)
            {
                 visualizer.SetInventoryState(combiner.InventoryState); // This also triggers InitialLoad

                 Debug.Log($"Inventory '{id}' fully initialized and linked components.", this);
            }
            else
            {
                Debug.LogError($"Inventory ({gameObject.name}): Cannot fully initialize in Start because Combiner or Visualizer is null. Check previous Awake logs.", this);
            }
        }

         private void OnEnable()
         {
             // Register with the DragAndDropManager when the inventory becomes active
             if (DragAndDropManager.Instance != null)
             {
                 DragAndDropManager.RegisterInventory(this);
             }
             else
             {
                  Debug.LogWarning($"Inventory ({gameObject.name}): DragAndDropManager instance not available in OnEnable. Cannot register.", this);
             }
         }

         private void OnDisable()
         {
              // Unregister from the DragAndDropManager when the inventory becomes inactive
              if (DragAndDropManager.Instance != null)
              {
                  DragAndDropManager.UnregisterInventory(this);
              }
         }

        private void OnDestroy()
        {
             // Ensure unsubscribe from ObservableArray events if Visualizer is destroyed separately
             // Visualizer's OnDestroy handles its own unsubscription, which is cleaner.
        }

        /// <summary>
        /// Checks if an item is allowed to be placed in this inventory based on its ItemLabel.
        /// </summary>
        public bool CanAddItem(Item item)
        {
            // Null checks for safety
            if (item == null || item.details == null)
            {
                Debug.LogWarning($"Inventory ({gameObject.name}): Attempted to check null or detail-less item for filtering.");
                return false; // Cannot add a null or detail-less item
            }

            // If the allowed list is null or empty and allowAllIfListEmpty is true, bypass filtering
            if (allowedLabels == null || allowedLabels.Count == 0)
            {
                return allowAllIfListEmpty;
            }

            // Check if the item's label is in the allowed list
            return allowedLabels.Contains(item.details.itemLabel);
        }
    }
}