using UnityEngine;
using Systems.Inventory;
using System.Collections.Generic;
using Systems.Interaction; // Needed if MenuManager uses InteractionResponse

namespace Systems.Inventory
{
    /// <summary>
    /// Manages item selection for a specific inventory (intended for the player toolbar).
    /// Handles input for cycling through slots and provides access to the selected item.
    /// Controls selection highlighting when in the Playing state.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    [RequireComponent(typeof(Visualizer))]
    public class InventorySelector : MonoBehaviour
    {
        private Inventory parentInventory;
        private Visualizer inventoryVisualizer;
        private List<InventorySlotUI> slotUIs;

        /// <summary>
        /// Provides access to the list of InventorySlotUI components managed by this selector's Visualizer.
        /// </summary>
        public List<InventorySlotUI> SlotUIComponents
        {
            get
            {
                // Return the list directly. If you needed to prevent external modification,
                // you would return a copy: return slotUIs?.ToList();
                return slotUIs;
            }
        }

        [Tooltip("The index of the currently selected slot (0-based).")]
        [SerializeField] private int selectedIndex = 0;

        [Tooltip("The input axis name for scrolling (e.g., 'Mouse ScrollWheel').")]
        [SerializeField] private string scrollAxis = "Mouse ScrollWheel";

        public Item SelectedItem
        {
            // ... (SelectedItem getter remains)
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

                 // Ensure the initial selected index is valid
                 selectedIndex = Mathf.Clamp(selectedIndex, 0, slotUIs.Count - 1);

                 // Apply initial highlight IF starting in Playing state
                  if (MenuManager.Instance != null && MenuManager.Instance.currentState == MenuManager.GameState.Playing)
                 {
                     ApplyHighlight(selectedIndex); // Apply highlight only if in Playing state
                 }
             }
             else
             {
                  enabled = false;
             }

             if (MenuManager.Instance == null)
             {
                 Debug.LogError("InventorySelector: MenuManager Instance is null! Cannot check game state for input.");
             }
             else
             {
                  // --- Subscribe to state changes ---
                  MenuManager.OnStateChanged += HandleGameStateChanged;
                  Debug.Log("InventorySelector: Subscribed to MenuManager.OnStateChanged.");
             }
        }

        private void OnDestroy()
        {
             // --- Unsubscribe from state changes ---
             if (MenuManager.Instance != null)
             {
                  MenuManager.OnStateChanged -= HandleGameStateChanged;
                  Debug.Log("InventorySelector: Unsubscribed from MenuManager.OnStateChanged.");
             }
        }


        // --- Handle Game State Changes to manage selection highlight visibility ---
        private void HandleGameStateChanged(MenuManager.GameState newState)
        {
             // If entering Playing state, apply the selection highlight to the current index
             if (newState == MenuManager.GameState.Playing)
             {
                 ApplyHighlight(selectedIndex); // Apply selection highlight
             }
             // If exiting Playing state, remove the selection highlight from the current index
             else if (MenuManager.Instance.previousState == MenuManager.GameState.Playing) // Check if the *previous* state was Playing
             {
                  // Remove highlight from the previously selected slot (which is still selectedIndex)
                  if (slotUIs != null && selectedIndex >= 0 && selectedIndex < slotUIs.Count)
                  {
                      slotUIs[selectedIndex].RemoveSelectionHighlight(); // Remove selection highlight
                      Debug.Log($"InventorySelector: Removed selection highlight when exiting Playing state from slot {selectedIndex}.");
                  }
             }
             // When entering InInventory state, the hover highlight takes over (handled by InventorySlotUI)
        }


        private void Update()
        {
             // Input handling for selection should only be active in the Playing state
             if (MenuManager.Instance != null && MenuManager.Instance.currentState == MenuManager.GameState.Playing)
             {
                 HandleInput();
             }
             // If in InInventory or other states, input for selection is ignored here.
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

             // If the selected index changed AND we are in the Playing state, update the visual highlight
             // Highlight is now managed by HandleGameStateChanged when entering Playing,
             // and by this method when selection changes *within* the Playing state.
             if (selectedIndex != previousSelectedIndex)
             {
                  // Only update highlight visually if currently in Playing state
                 if (MenuManager.Instance.currentState == MenuManager.GameState.Playing)
                 {
                     ApplyHighlight(selectedIndex, previousSelectedIndex); // Apply/Remove selection highlights
                 }
                 // If not in Playing, the selection state changes, but the visual highlight is not immediately updated here.
                 // It will be updated when returning to Playing state in HandleGameStateChanged.
             }
        }

        /// <summary>
        /// Applies the visual selection highlight to the newly selected slot and removes it from the previously selected slot.
        /// ONLY applies highlight if the game state is Playing.
        /// </summary>
        private void ApplyHighlight(int newIndex, int previousIndex = -1)
        {
             // This method now specifically manages the *selection* highlight.
             // It should only proceed if the game state is Playing.
             if (MenuManager.Instance != null && MenuManager.Instance.currentState != MenuManager.GameState.Playing)
             {
                 Debug.Log("InventorySelector: ApplyHighlight called, but state is not Playing. Skipping selection highlight update.");
                 return; // Do not apply selection highlight if not in Playing state
             }

             if (slotUIs == null || slotUIs.Count == 0) return;

             // Ensure indices are valid within the collected slot list
             newIndex = Mathf.Clamp(newIndex, 0, slotUIs.Count - 1);
             previousIndex = Mathf.Clamp(previousIndex, -1, slotUIs.Count - 1); // -1 is valid for initial state

             // Remove selection highlight from the previous slot (if a valid previous index exists)
             if (previousIndex >= 0 && previousIndex < slotUIs.Count)
             {
                  // Call the new RemoveSelectionHighlight method
                 slotUIs[previousIndex].RemoveSelectionHighlight();
             }

             // Apply selection highlight to the new slot
             if (newIndex >= 0 && newIndex < slotUIs.Count)
             {
                  // Call the new ApplySelectionHighlight method
                  slotUIs[newIndex].ApplySelectionHighlight();
             }
             else
             {
                  Debug.LogError($"InventorySelector: Attempted to apply selection highlight to invalid index {newIndex} after clamping.", this);
             }
        }

        // ... (GetSelectedItem, UseSelectedItem methods remain)
         /// <summary>
         /// Gets the Item instance in the currently selected slot.
         /// </summary>
         public Item GetSelectedItem() { return SelectedItem; }

         /// <summary>
         /// Attempts to use the item in the currently selected slot.
         /// Called by the input handling system (e.g., ItemUsageManager).
         /// </summary>
         public bool UseSelectedItem()
         {
             Item itemToUse = GetSelectedItem();

             if (itemToUse != null)
             {
                 Debug.Log($"InventorySelector: Item '{itemToUse.details.Name}' found in selected slot {selectedIndex}. Attempting to use.", this);
                 if (ItemUsageManager.Instance != null)
                 {
                     // Pass the item INSTANCE, the parent inventory, AND the selected slot INDEX
                     return ItemUsageManager.Instance.UseItem(itemToUse, parentInventory, selectedIndex);
                 }
                 else Debug.LogError("InventorySelector: ItemUsageManager Instance is null! Cannot use item.", this);
                 return false;
             }
             else Debug.Log("InventorySelector: No item in selected slot to use.", this);
             return false;
         }

        // Removed obsolete methods: Highlight(), Unhighlight()
    }
}