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
            Debug.Log($"Inventory ({gameObject.name}): Awake started - Getting references.", this);

            // Get component references
            if (combiner == null) combiner = GetComponent<Combiner>();
            if (flexibleGridLayout == null) flexibleGridLayout = GetComponent<FlexibleGridLayout>();
            if (visualizer == null) visualizer = GetComponent<Visualizer>(); // Get Visualizer

            Debug.Log($"Inventory ({gameObject.name}): References after getting components in Awake - Combiner: {(combiner != null ? "Assigned" : "NULL")}, Visualizer: {(visualizer != null ? "Assigned" : "NULL")}, FlexibleGridLayout: {(flexibleGridLayout != null ? "Assigned" : "NULL")}", this);

            if (combiner == null || flexibleGridLayout == null || visualizer == null)
            {
                Debug.LogError($"Inventory on {gameObject.name} is missing required components (Combiner, FlexibleGridLayout, or Visualizer). Inventory functionality may be limited.", this);
                // Don't disable immediately in Awake, let Start potentially catch it
            }
            else
            {
                Debug.Log($"Inventory ({gameObject.name}): All required references obtained in Awake.", this);

                // --- Assign ParentInventory reference to all InventorySlotUI children ---
                // The Visualizer has already found these in its Awake.
                // Get the list of slot UIs from the Visualizer and assign this Inventory instance.
                // Note: Visualizer.slotUIComponents should likely be public or have a public getter.
                // Let's make Visualizer.slotUIComponents public temporarily for this.
                // **Refinement:** A cleaner way is for Visualizer's Awake to call back to Inventory
                // with the list, or Inventory's Awake finds them directly.
                // Let's adjust Visualizer's Awake to allow getting the list.

                // **Alternative Refinement (cleaner):** Move the slot finding logic from Visualizer.Awake
                // to Inventory.Awake. Inventory finds the slots, assigns ParentInventory, and then
                // gives this list to the Visualizer.

                // --- Let's move slot finding to Inventory.Awake ---
                FindAndAssignSlotUIs();

                 // --- Register this inventory with the DragAndDropManager ---
                DragAndDropManager.RegisterInventory(this);

            }
             Debug.Log($"Inventory ({gameObject.name}): Awake finished.", this);
        }

         // New method in Inventory.cs to find slots and assign parent
         private void FindAndAssignSlotUIs()
        {
             if (flexibleGridLayout == null)
             {
                 Debug.LogError($"Inventory ({gameObject.name}): Cannot find and assign SlotUIs, FlexibleGridLayout is null.", this);
                 return;
             }

             List<InventorySlotUI> foundSlots = new List<InventorySlotUI>();
             Debug.Log($"Inventory ({gameObject.name}): Searching for InventorySlotUI components among {flexibleGridLayout.transform.childCount} children of FlexibleGridLayout.", this);

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

            Debug.Log($"Inventory ({gameObject.name}): Finished finding and assigning {foundSlots.Count} InventorySlotUI components. Providing list to Visualizer.", this);

             // Pass the found list to the Visualizer (Visualizer needs a method to accept this list)
             if (visualizer != null)
             {
                 visualizer.SetSlotUIComponents(foundSlots); // Visualizer needs this new public method
             }
             else
             {
                  Debug.LogError($"Inventory ({gameObject.name}): Visualizer is null, cannot pass slot UI list.", this);
             }
        }


        private void Start()
        {
            Debug.Log($"Inventory ({gameObject.name}): Start started - Linking Visualizer to Combiner.", this);

            // Only attempt to link if we have Combiner and Visualizer
            if (combiner != null && visualizer != null)
            {
                 // Combiner initializes the ObservableArray in its Awake.
                 // Now, in Start, we can safely get the reference and give it to the Visualizer.
                 Debug.Log($"Inventory ({gameObject.name}): Calling Visualizer.SetInventoryState with Combiner's InventoryState from Start.", this);
                 visualizer.SetInventoryState(combiner.InventoryState); // This also triggers InitialLoad

                 Debug.Log($"Inventory '{id}' fully initialized and linked components.", this);
            }
            else
            {
                Debug.LogError($"Inventory ({gameObject.name}): Cannot fully initialize in Start because Combiner or Visualizer is null. Check previous Awake logs.", this);
            }

            Debug.Log($"Inventory ({gameObject.name}): Start finished.", this);
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

        // ... rest of the script (e.g., TestAddSampleItem if you kept it)
    }
}