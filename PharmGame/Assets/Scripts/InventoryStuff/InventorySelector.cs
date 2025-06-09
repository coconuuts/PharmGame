using UnityEngine;
using Systems.Inventory; // Needed for InventoryClass, Item, Visualizer, UsageTriggerType
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
                     // The ghost slot (Length - 1) should not be selectable.
                     if (parentInventory.Combiner != null && selectedIndex < parentInventory.Combiner.PhysicalSlotCount)
                     {
                         return parentInventory.InventoryState[selectedIndex];
                     }
                     else if (parentInventory.Combiner == null) // If no Combiner, assume all slots are physical/selectable
                     {
                         // This case should ideally not happen if Inventory is configured correctly
                         Debug.LogWarning($"InventorySelector ({gameObject.name}): Parent Inventory has no Combiner. Assuming all slots are selectable, but this might be incorrect.", this);
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

                // Ensure the initial selected index is valid within physical slots
                if (parentInventory != null && parentInventory.Combiner != null)
                {
                    selectedIndex = Mathf.Clamp(selectedIndex, 0, parentInventory.Combiner.PhysicalSlotCount - 1);
                }
                else
                {
                     // Fallback if Combiner is missing (should be caught in Awake/Inventory Start)
                     selectedIndex = Mathf.Clamp(selectedIndex, 0, slotUIs.Count - 1);
                }


                if (MenuManager.Instance == null)
                {
                    Debug.LogError("InventorySelector: MenuManager Instance is null in Start! Cannot subscribe to state changes or determine initial state.", this);
                    enabled = false; // Disable if MenuManager is missing
                    return; // Exit Start if MenuManager is missing
                }

                // --- Subscribe to state changes ---
                MenuManager.OnStateChanged += HandleGameStateChanged;
                Debug.Log("InventorySelector: Subscribed to MenuManager.OnStateChanged.");

                // --- Check current state and apply highlight if already in Playing ---
                // This handles the scenario where the toolbar UI is activated AFTER the initial
                // MenuManager state transition has already occurred.
                if (MenuManager.Instance.currentState == MenuManager.GameState.Playing)
                {
                    Debug.Log("InventorySelector: Starting while in Playing state, applying initial selection highlight.");
                    ApplyHighlight(selectedIndex); // Apply selection highlight to the default/saved index
                }
                // --- END ADDED CODE ---
                 else
                 {
                     // Ensure highlight is off if starting in a state other than Playing
                      if (slotUIs != null && selectedIndex >= 0 && selectedIndex < slotUIs.Count)
                      {
                          slotUIs[selectedIndex].RemoveSelectionHighlight();
                      }
                 }
            }
            else
            {
                enabled = false; // Disable this script if visualizer is null
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

            // Ensure highlight is removed on destroy
             if (slotUIs != null && selectedIndex >= 0 && selectedIndex < slotUIs.Count)
             {
                 slotUIs[selectedIndex].RemoveSelectionHighlight();
             }
        }

        /// <summary>
        /// Event handler for MenuManager.OnStateChanged.
        /// Manages the visibility of the selection highlight based on the game state transition.
        /// </summary>
        private void HandleGameStateChanged(MenuManager.GameState newState, MenuManager.GameState oldState, InteractionResponse response)
        {
            Debug.Log($"InventorySelector: Handling state change from {oldState} to {newState}.");
            // If entering Playing state, apply the selection highlight to the current index
            if (newState == MenuManager.GameState.Playing)
            {
                Debug.Log("InventorySelector: Entering Playing state, applying selection highlight.");
                // ApplyHighlight no longer has the state check, so this call works correctly.
                ApplyHighlight(selectedIndex); // Apply selection highlight
            }
            // If exiting Playing state, remove the selection highlight from the current index
            // Note: This happens BEFORE the MenuManager's state variable is updated to the new state.
            // The RemoveSelectionHighlight method on SlotUI doesn't have a state check, so it's safe to call.
            else if (oldState == MenuManager.GameState.Playing)
            {
                 Debug.Log("InventorySelector: Exiting Playing state, removing selection highlight.");
                 // Remove highlight from the previously selected slot (which is still selectedIndex)
                 if (slotUIs != null && selectedIndex >= 0 && selectedIndex < slotUIs.Count)
                 {
                     slotUIs[selectedIndex].RemoveSelectionHighlight(); // Remove selection highlight
                     Debug.Log($"InventorySelector: Removed selection highlight from slot {selectedIndex}.");
                 }
            }
            // Note: When entering InInventory or InCrafting, the selection highlight is
            // implicitly removed by the check above when exiting Playing. Hover highlight takes over
            // (handled by InventorySlotUI itself).
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

                // Wrap around physical slots only
                int physicalSlotCount = (parentInventory != null && parentInventory.Combiner != null) ? parentInventory.Combiner.PhysicalSlotCount : slotUIs.Count;
                if (selectedIndex < 0) selectedIndex = physicalSlotCount - 1;
                else if (selectedIndex >= physicalSlotCount) selectedIndex = 0;
            }

            // Handle number key input (1-0 for slots 0-9)
            int maxKeyIndex = (parentInventory != null && parentInventory.Combiner != null) ? Mathf.Min(parentInventory.Combiner.PhysicalSlotCount, 10) : Mathf.Min(slotUIs.Count, 10);
            for (int i = 0; i < maxKeyIndex; i++)
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
                // The ApplyHighlight method no longer has the state check, so calling it here is fine.
                // The HandleInput method itself only runs in the Playing state, ensuring this only
                // updates the highlight while in Playing.
                ApplyHighlight(selectedIndex, previousSelectedIndex); // Apply/Remove selection highlights
            }
        }

        /// <summary>
        /// Applies the visual selection highlight to the newly selected slot and removes it from the previously selected slot.
        /// Called by HandleInput (when in Playing state) or HandleGameStateChanged (when entering Playing state),
        /// or from Start if the state is already Playing.
        /// Requires InventorySlotUI to have ApplySelectionHighlight() and RemoveSelectionHighlight() methods.
        /// NOTE: This method assumes the caller has determined that selection highlighting is appropriate (i.e., in Playing state).
        /// </summary>
        private void ApplyHighlight(int newIndex, int previousIndex = -1)
        {
             // Keep null check for safety, but the state check is removed as it's handled by the callers.
             if (MenuManager.Instance == null)
             {
                  Debug.LogError("InventorySelector: MenuManager Instance is null in ApplyHighlight!", this);
                  return;
             }

            if (slotUIs == null || slotUIs.Count == 0)
            {
                 Debug.LogWarning("InventorySelector: slotUIs list is null or empty in ApplyHighlight.");
                 return;
            }

            // Ensure indices are valid within the collected slot list (which should match physical slots)
             int physicalSlotCount = (parentInventory != null && parentInventory.Combiner != null) ? parentInventory.Combiner.PhysicalSlotCount : slotUIs.Count;
            newIndex = Mathf.Clamp(newIndex, 0, physicalSlotCount - 1);
            // previousIndex can be -1 initially, allow that.
            previousIndex = Mathf.Clamp(previousIndex, -1, physicalSlotCount - 1);


            // Remove selection highlight from the previous slot (if a valid previous index exists and it's different from the new index)
             if (previousIndex >= 0 && previousIndex < slotUIs.Count && previousIndex != newIndex) // Check against slotUIs.Count for safety
             {
                 if(slotUIs[previousIndex] != null) slotUIs[previousIndex].RemoveSelectionHighlight();
                 else Debug.LogWarning($"InventorySelector: slotUIs[{previousIndex}] is null when attempting to remove selection highlight.", this);
             }

            // Apply selection highlight to the new slot
            if (newIndex >= 0 && newIndex < slotUIs.Count) // Check against slotUIs.Count for safety
            {
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
        /// <param name="trigger">The type of action that triggered this usage.</param> // NEW PARAMETER
        public bool UseSelectedItem(UsageTriggerType trigger) // NEW PARAMETER
        {
            Item itemToUse = GetSelectedItem();

            if (itemToUse != null)
            {
                Debug.Log($"InventorySelector: Item '{itemToUse.details.Name}' found in selected slot {selectedIndex}. Attempting to use via trigger: {trigger}.", this);
                if (ItemUsageManager.Instance != null) // Assumes ItemUsageManager exists
                {
                    // Pass the item INSTANCE, the parent inventory, the selected slot INDEX, AND the trigger type
                    return ItemUsageManager.Instance.UseItem(itemToUse, parentInventory, selectedIndex, trigger); // Pass trigger
                }
                else Debug.LogError("InventorySelector: ItemUsageManager Instance is null! Cannot use item.", this);
                return false;
            }
            else Debug.Log("InventorySelector: No item in selected slot to use.", this);
            return false;
        }
    }
}