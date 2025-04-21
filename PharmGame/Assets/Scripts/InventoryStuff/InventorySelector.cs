using UnityEngine;
using Systems.Inventory;
using System.Collections.Generic;

namespace Systems.Inventory
{
    // ... (InventorySelector class declaration and fields remain)

    // RequireComponent attributes remain
    [RequireComponent(typeof(Inventory))]
    [RequireComponent(typeof(Visualizer))]
    public class InventorySelector : MonoBehaviour
    {
        private Inventory parentInventory;
        private Visualizer inventoryVisualizer;
        private List<InventorySlotUI> slotUIs;

        [SerializeField] private int selectedIndex = 0;
        [SerializeField] private string scrollAxis = "Mouse ScrollWheel";

        public Item SelectedItem
        {
            get
            {
                if (parentInventory != null && parentInventory.InventoryState != null &&
                    selectedIndex >= 0 && selectedIndex < parentInventory.InventoryState.Length)
                {
                     if (parentInventory.Combiner != null && selectedIndex < parentInventory.Combiner.PhysicalSlotCount)
                     {
                         return parentInventory.InventoryState[selectedIndex];
                     }
                     else
                     {
                          return null;
                     }
                }
                return null;
            }
        }

        // ... (Awake, Start methods remain)
        private void Awake()
        {
             parentInventory = GetComponent<Inventory>();
             inventoryVisualizer = GetComponent<Visualizer>();

             if (parentInventory == null) Debug.LogError("InventorySelector requires an Inventory component.", this);
             if (inventoryVisualizer == null) Debug.LogError("InventorySelector requires a Visualizer component.", this);
        }

        private void Start()
        {
              if (inventoryVisualizer != null)
             {
                 slotUIs = inventoryVisualizer.SlotUIComponents;
                 if (slotUIs == null || slotUIs.Count == 0)
                 {
                      Debug.LogWarning("InventorySelector: No InventorySlotUI components found via Visualizer. Selection may not work correctly.", this);
                      enabled = false;
                      return;
                 }

                 selectedIndex = Mathf.Clamp(selectedIndex, 0, slotUIs.Count - 1);
                 ApplyHighlight(selectedIndex);
             }
             else
             {
                  enabled = false;
             }

              if (MenuManager.Instance == null)
              {
                  Debug.LogError("InventorySelector: MenuManager Instance is null! Cannot check game state for input.");
              }
        }


        // ... (Update, HandleInput, ApplyHighlight methods remain)
        private void Update()
        {
             if (MenuManager.Instance != null && MenuManager.Instance.currentState == MenuManager.GameState.Playing)
             {
                 HandleInput();
             }
        }

         private void HandleInput()
        {
             int previousSelectedIndex = selectedIndex;
             float scroll = Input.GetAxis(scrollAxis);
             if (scroll != 0f)
             {
                 if (scroll > 0f) selectedIndex--;
                 else selectedIndex++;

                 if (selectedIndex < 0) selectedIndex = slotUIs.Count - 1;
                 else if (selectedIndex >= slotUIs.Count) selectedIndex = 0;
             }

             for (int i = 0; i < slotUIs.Count && i < 10; i++)
             {
                 KeyCode key = KeyCode.Alpha1 + i;
                 if (i == 9) key = KeyCode.Alpha0;

                 if (Input.GetKeyDown(key))
                 {
                     selectedIndex = i;
                     break;
                 }
             }

             if (selectedIndex != previousSelectedIndex)
             {
                 ApplyHighlight(selectedIndex, previousSelectedIndex);
             }
        }

         private void ApplyHighlight(int newIndex, int previousIndex = -1)
        {
              if (slotUIs == null || slotUIs.Count == 0) return;

              newIndex = Mathf.Clamp(newIndex, 0, slotUIs.Count - 1);
              previousIndex = Mathf.Clamp(previousIndex, -1, slotUIs.Count - 1);

              if (previousIndex >= 0 && previousIndex < slotUIs.Count)
              {
                  slotUIs[previousIndex].Unhighlight();
              }

              if (newIndex >= 0 && newIndex < slotUIs.Count)
              {
                   slotUIs[newIndex].Highlight();
              }
              else
              {
                   Debug.LogError($"InventorySelector: Attempted to highlight invalid index {newIndex} after clamping.", this);
              }
        }


        // --- Methods for Item Usage ---

        /// <summary>
        /// Gets the Item instance in the currently selected slot.
        /// </summary>
        public Item GetSelectedItem()
        {
            return SelectedItem;
        }

        /// <summary>
        /// Attempts to use the item in the currently selected slot.
        /// Called by the input handling system (e.g., ItemUsageManager).
        /// </summary>
        /// <returns>True if an item was available in the selected slot and usage was attempted, false otherwise.</returns>
        public bool UseSelectedItem()
        {
            Item itemToUse = GetSelectedItem(); // Get the item instance

            if (itemToUse != null)
            {
                Debug.Log($"InventorySelector: Item '{itemToUse.details.Name}' found in selected slot {selectedIndex}. Attempting to use.", this);
                // --- Call the central ItemUsageManager to handle the actual usage logic ---
                if (ItemUsageManager.Instance != null)
                {
                    // Pass the item INSTANCE, the parent inventory, AND the selected slot INDEX
                    return ItemUsageManager.Instance.UseItem(itemToUse, parentInventory, selectedIndex); // PASS SELECTED INDEX
                }
                else
                {
                     Debug.LogError("InventorySelector: ItemUsageManager Instance is null! Cannot use item.", this);
                     return false;
                }
            }
            else
            {
                Debug.Log("InventorySelector: No item in selected slot to use.", this);
                return false; // No item to use
            }
        }

        // TODO: Add methods for dropping the selected item from the toolbar?

    }
}