using UnityEngine;
using Systems.Inventory; // Needed for InventoryClass, Item, Visualizer
using System.Collections.Generic;
using System.Linq; // Needed for .ToList()
using Systems.GameStates; // Needed for MenuManager and GameState enum
using Systems.Interaction;


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
            get
            {
                 if (parentInventory != null && parentInventory.InventoryState != null &&
                     selectedIndex >= 0 && selectedIndex < parentInventory.InventoryState.Length)
                 {
                      // Check if the index is within the bounds of physical slots if Combiner exists
                      if (parentInventory.Combiner != null && selectedIndex < parentInventory.Combiner.PhysicalSlotCount)
                      {
                           return parentInventory.InventoryState[selectedIndex];
                      }
                      else if (parentInventory.Combiner == null) // If no Combiner, assume all slots are physical/selectable
                      {
                           return parentInventory.InventoryState[selectedIndex];
                      }
                 }
                 return null; // Return null if index is out of bounds or InventoryState is null
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
                    enabled = false; // Disable this script if no slots are found
                    return;
                }

                // Ensure the initial selected index is valid
                selectedIndex = Mathf.Clamp(selectedIndex, 0, slotUIs.Count - 1);

                // Apply initial highlight IF starting in Playing state (handled by event subscription now)
                // We no longer need to explicitly check here in Start, the initial SetState call
                // in MenuManager.Start will trigger the event and the handler below.
            }
            else
            {
                enabled = false; // Disable this script if visualizer is null
            }

            if (MenuManager.Instance == null)
            {
                Debug.LogError("InventorySelector: MenuManager Instance is null! Cannot subscribe to state changes.", this);
                enabled = false; // Disable if MenuManager is missing
            }
            else
            {
                // --- Subscribe to state changes ---
                MenuManager.OnStateChanged += HandleGameStateChanged;
                Debug.Log("InventorySelector: Subscribed to MenuManager.OnStateChanged.");

                // Apply the correct highlight based on the *initial* state after subscribing
                // This handles the scenario where the game starts not in the Playing state.
                 if (MenuManager.Instance.currentState == MenuManager.GameState.Playing)
                 {
                      ApplyHighlight(selectedIndex); // Apply selection highlight if starting in Playing
                 }
                 else
                 {
                       // Ensure highlight is off if not starting in Playing
                       if (slotUIs != null && selectedIndex >= 0 && selectedIndex < slotUIs.Count)
                       {
                           slotUIs[selectedIndex].RemoveSelectionHighlight();
                       }
                 }
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

            // Optional: Ensure highlight is removed on destroy
             if (slotUIs != null && selectedIndex >= 0 && selectedIndex < slotUIs.Count)
             {
                 slotUIs[selectedIndex].RemoveSelectionHighlight();
             }
        }


        // --- MODIFIED: Handle Game State Changes to manage selection highlight visibility ---
        /// <summary>
        /// Event handler for MenuManager.OnStateChanged.
        /// Manages the visibility of the selection highlight based on the game state.
        /// </summary>
        private void HandleGameStateChanged(MenuManager.GameState newState, MenuManager.GameState oldState, InteractionResponse response)
        {
            Debug.Log($"InventorySelector: Handling state change from {oldState} to {newState}.");
            // If entering Playing state, apply the selection highlight to the current index
            if (newState == MenuManager.GameState.Playing)
            {
                Debug.Log("InventorySelector: Entering Playing state, applying selection highlight.");
                ApplyHighlight(selectedIndex); // Apply selection highlight
            }
            // If exiting Playing state, remove the selection highlight from the current index
            else if (oldState == MenuManager.GameState.Playing) // Check if the *previous* state was Playing
            {
                 Debug.Log("InventorySelector: Exiting Playing state, removing selection highlight.");
                 // Remove highlight from the previously selected slot (which is still selectedIndex)
                 if (slotUIs != null && selectedIndex >= 0 && selectedIndex < slotUIs.Count)
                 {
                      slotUIs[selectedIndex].RemoveSelectionHighlight(); // Remove selection highlight
                      Debug.Log($"InventorySelector: Removed selection highlight from slot {selectedIndex}.");
                 }
            }
            // Note: When entering InInventory state, the hover highlight takes over
            // (This is handled by the InventorySlotUI itself, not here).
            // When exiting InInventory, the MenuManager's ClearHoverHighlights action
            // will remove hover highlights from *all* slots in the inventory.
        }


        private void Update()
        {
            // Input handling for selection should only be active in the Playing state
            // This check remains correct.
            if (MenuManager.Instance != null && MenuManager.Instance.currentState == MenuManager.GameState.Playing)
            {
                HandleInput();
            }
            // If in InInventory or other states, input for selection is ignored here.
        }

        private void HandleInput()
        {
            if (slotUIs == null || slotUIs.Count == 0) return; // Ensure we have slots before processing input

            int previousSelectedIndex = selectedIndex;
            float scroll = Input.GetAxis(scrollAxis);
            if (scroll != 0f)
            {
                if (scroll > 0f) selectedIndex--;
                else selectedIndex++;

                // Wrap around
                if (selectedIndex < 0) selectedIndex = slotUIs.Count - 1;
                else if (selectedIndex >= slotUIs.Count) selectedIndex = 0;
            }

            // Handle number key input (1-0 for slots 0-9)
            for (int i = 0; i < slotUIs.Count && i < 10; i++)
            {
                KeyCode key = KeyCode.Alpha1 + i;
                if (i == 9) key = KeyCode.Alpha0; // KeyCode.Alpha0 is for the '0' key

                if (Input.GetKeyDown(key))
                {
                    selectedIndex = i;
                    break; // Exit loop once a key is pressed
                }
            }

            // If the selected index changed, update the visual highlight IF currently in Playing state
            // The HandleGameStateChanged method handles applying highlight when entering Playing.
            // This logic handles changing the highlight *while* already in Playing.
            if (selectedIndex != previousSelectedIndex)
            {
                // Only update highlight visually if currently in Playing state
                // This check is technically redundant because HandleInput only runs in Playing,
                // but it adds an extra layer of safety.
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
        /// Requires InventorySlotUI to have ApplySelectionHighlight() and RemoveSelectionHighlight() methods.
        /// </summary>
        private void ApplyHighlight(int newIndex, int previousIndex = -1)
        {
            // This method now specifically manages the *selection* highlight.
            // It should only proceed if the game state is Playing.
            if (MenuManager.Instance != null && MenuManager.Instance.currentState != MenuManager.GameState.Playing)
            {
                 // Debug.Log("InventorySelector: ApplyHighlight called, but state is not Playing. Skipping selection highlight update."); // Too verbose?
                 return; // Do not apply selection highlight if not in Playing state
            }
             else if (MenuManager.Instance == null)
             {
                  Debug.LogError("InventorySelector: MenuManager Instance is null in ApplyHighlight!");
                  return;
             }


            if (slotUIs == null || slotUIs.Count == 0)
            {
                 Debug.LogWarning("InventorySelector: slotUIs list is null or empty in ApplyHighlight.");
                 return;
            }

            // Ensure indices are valid within the collected slot list
            newIndex = Mathf.Clamp(newIndex, 0, slotUIs.Count - 1);
            // previousIndex can be -1 initially, allow that.
            previousIndex = Mathf.Clamp(previousIndex, -1, slotUIs.Count - 1);


            // Remove selection highlight from the previous slot (if a valid previous index exists and it's different from the new index)
             if (previousIndex >= 0 && previousIndex < slotUIs.Count && previousIndex != newIndex)
            {
                 // Call the new RemoveSelectionHighlight method on the SlotUI
                if(slotUIs[previousIndex] != null) slotUIs[previousIndex].RemoveSelectionHighlight();
                else Debug.LogWarning($"InventorySelector: slotUIs[{previousIndex}] is null when attempting to remove selection highlight.", this);
            }

            // Apply selection highlight to the new slot
            if (newIndex >= 0 && newIndex < slotUIs.Count)
            {
                 // Call the new ApplySelectionHighlight method on the SlotUI
                 if(slotUIs[newIndex] != null) slotUIs[newIndex].ApplySelectionHighlight();
                 else Debug.LogWarning($"InventorySelector: slotUIs[{newIndex}] is null when attempting to apply selection highlight.", this);
            }
            else
            {
                Debug.LogError($"InventorySelector: Attempted to apply selection highlight to invalid index {newIndex} after clamping.", this);
            }
        }


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
                if (ItemUsageManager.Instance != null) // Assumes ItemUsageManager exists
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

        // Removed obsolete methods: Highlight(), Unhighlight() if they were just visual toggles
        // Assuming ApplySelectionHighlight and RemoveSelectionHighlight on InventorySlotUI replace them for selection state
    }
}