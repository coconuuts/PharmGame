// PrescriptionTableManager.cs
using UnityEngine;
using Systems.Inventory; // Ensure this matches your Inventory system namespace
using System;
using System.Collections.Generic;
using UnityEngine.UI; // Needed for the Button component
using System.Linq; // Needed for FirstOrDefault, Sum, Where

namespace Prescription // Use the Prescription namespace
{
    /// <summary>
    /// Manages the top-level UI state and orchestrates the Prescription system using events.
    /// Activates/Deactivates specific inventories, button, and output UI based on overall UI root state.
    /// Fires events when the process button is clicked.
    /// </summary>
    public class PrescriptionTableManager : MonoBehaviour
    {
        [Header("Inventories")] // Consolidated Header
        [SerializeField] private Inventory mainPrescriptiontableInventory; // The inventory this manager primarily monitors (for button checks)
        [SerializeField] private Inventory toolbarInventory; // The character's main inventory (for UI close item return, if needed)
        [SerializeField] private Inventory outputInventory; // The inventory where crafted items appear

        [Header("UI Roots")]
        [SerializeField] private GameObject prescriptionTableInventoryUIRoot; // The overall UI root for this manager's interface
        [SerializeField] private GameObject pillInventoryUIRoot;
        [SerializeField] private GameObject liquidInventoryUIRoot;
        [SerializeField] private GameObject inhalerInventoryUIRoot;
        [SerializeField] private GameObject insulinInventoryUIRoot;
        [SerializeField] private GameObject outputInventoryUIRoot; // UI root for the output inventory


        [Header("Specific Prescription Inventories")]
        [SerializeField] private Inventory pillInventory; // The Inventory component for the pill prescription table
        [SerializeField] private Inventory liquidInventory; // The Inventory component for the liquid prescription table
        [SerializeField] private Inventory inhalerInventory; // The Inventory component for the inhaler prescription table
        [SerializeField] private Inventory insulinInventory; // The Inventory component for the insulin prescription table

        [Header("Process Button")]
        [SerializeField] private GameObject processButtonUIRoot; // The button UI GameObject to activate/deactivate
        private Button _processButton; // Reference to the Button component

        [Header("Crafting Recipes")] // New Header for recipes
        [Tooltip("Define the recipes mapping secondary ingredients to results and required main ingredients.")]
        [SerializeField] private List<CraftingRecipe> craftingRecipes; // List of recipes defined in inspector
        private Dictionary<ItemDetails, CraftingRecipe> _recipeMap; // Dictionary for quick lookup


        [Header("Processors")]
        [Tooltip("Assign all concrete PrescriptionProcessor scripts here.")]
        [SerializeField] private List<PrescriptionProcessor> allProcessors; // List of all processor scripts assigned in inspector
        // Processors are self-sufficient and listen to events. Manager initializes them.


        // Using Dictionaries to map ItemLabels to their corresponding specific UI roots and Inventory components
        private Dictionary<ItemLabel, GameObject> specificUIRoots;
        private Dictionary<ItemLabel, Inventory> specificInventories;

        // To track the previous active state of the main UI root for detecting changes
        private bool _wasPrescriptionTableUIRootActive;

        // _activeProcessorLabel is still useful in OnProcessButtonClick to find the main item/processor type.
        // It no longer drives UI visibility directly.
        private ItemLabel _activeProcessorLabel = ItemLabel.None;


        // Fields related to monitoring specific inventory content for button state are removed:
        // private Inventory _currentlyMonitoredSpecificInventory;
        // private ItemLabel _previousRelevantLabel;


        public void Awake()
        {
             // Ensure the namespace is set correctly for all related scripts
            // Note: This is handled by the 'namespace' keyword in each script file.
            // Ensure your .asmdef files (if used) correctly reference each other and include the namespaces.

            // Initialize the dictionaries mapping relevant ItemLabels to their specific UI GameObjects and Inventory components
            specificUIRoots = new Dictionary<ItemLabel, GameObject>
            {
                { ItemLabel.PillStock, pillInventoryUIRoot },
                { ItemLabel.LiquidStock, liquidInventoryUIRoot },
                { ItemLabel.InhalerStock, inhalerInventoryUIRoot },
                { ItemLabel.InsulinStock, insulinInventoryUIRoot }
            };

            specificInventories = new Dictionary<ItemLabel, Inventory>
            {
                { ItemLabel.PillStock, pillInventory },
                { ItemLabel.LiquidStock, liquidInventory },
                { ItemLabel.InhalerStock, inhalerInventory },
                { ItemLabel.InsulinStock, insulinInventory }
            };

            // Initialize the recipe map for quick lookup
            _recipeMap = new Dictionary<ItemDetails, CraftingRecipe>();
            if (craftingRecipes != null)
            {
                foreach (var recipe in craftingRecipes)
                {
                    if (recipe != null && recipe.secondaryIngredient != null)
                    {
                        if (!_recipeMap.ContainsKey(recipe.secondaryIngredient))
                        {
                             _recipeMap.Add(recipe.secondaryIngredient, recipe);
                             Debug.Log($"Added recipe: Secondary='{recipe.secondaryIngredient.Name}' -> Result='{recipe.resultItem?.Name}'", this);
                        }
                        else
                        {
                            Debug.LogWarning($"Crafting Recipe list contains a duplicate secondary ingredient details: '{recipe.secondaryIngredient.Name}'. Only the first recipe for this ingredient will be used.", this);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Crafting Recipe list contains a null entry or an entry with a null secondary ingredient (ItemDetails). Cannot add recipe.", this);
                    }
                }
            }
            else
            {
                 Debug.LogWarning("PrescriptionTableManager: Crafting Recipes list is null! Define your recipes in the inspector.", this);
            }


            // Initialize processors
            if (allProcessors != null)
            {
                foreach (var processor in allProcessors)
                {
                    if (processor != null)
                    {
                         // Pass the specific inventory for this processor's type, output inventory, and recipe map
                         Inventory specificInvForProcessor = specificInventories.ContainsKey(processor.ProcessorType) ? specificInventories[processor.ProcessorType] : null;
                         if (specificInvForProcessor != null)
                         {
                              processor.Initialize(specificInvForProcessor, outputInventory, _recipeMap);
                         }
                         else
                         {
                             Debug.LogWarning($"Processor '{processor.GetType().Name}' handles unsupported ItemLabel '{processor.ProcessorType}' or missing specific inventory. It will not be fully initialized.", this);
                         }
                    }
                }
            }
            else
            {
                Debug.LogWarning("PrescriptionTableManager: All Processors list is null! Assign your processor scripts in the inspector.", this);
            }


            // Ensure UI roots are initially inactive (state will be set in Start/Update based on overall root active state)
            SetAllSpecificInventoryUIRootsActive(false);
            if (outputInventoryUIRoot != null) SetOutputInventoryUIActive(false);
            if (processButtonUIRoot != null) SetProcessButtonActive(false);

        }

        private void Start()
        {
            // We no longer subscribe to main inventory state changes for UI visibility logic.
            // The only subscription needed is to the overall UI root state changes.

            if (prescriptionTableInventoryUIRoot != null)
            {
                 _wasPrescriptionTableUIRootActive = prescriptionTableInventoryUIRoot.activeInHierarchy;
            }
             else
             {
                 Debug.LogError("PrescriptionTableManager: prescriptionTableInventoryUIRoot is not assigned! Cannot monitor UI state.", this);
                 _wasPrescriptionTableUIRootActive = false;
             }
             if (outputInventoryUIRoot == null) Debug.LogWarning("PrescriptionTableManager: Output Inventory UI Root is not assigned!", this);


            // --- Subscribe to relevant Prescription Events ---
            PrescriptionEvents.OnPrescriptionUIOpened += HandlePrescriptionUIOpened; // New handler
            PrescriptionEvents.OnPrescriptionUIClosed += HandlePrescriptionUIClosed; // Keep this handler

            // We still subscribe to crafting results to update button state (interactability) or provide feedback
            PrescriptionEvents.OnCraftingComplete += HandleCraftingComplete;
            PrescriptionEvents.OnCraftingFailed += HandleCraftingFailed;


            // Get the Button component and hook up the click event to fire an EVENT
            if (processButtonUIRoot != null)
            {
                _processButton = processButtonUIRoot.GetComponent<Button>();
                if (_processButton != null)
                {
                    _processButton.onClick.AddListener(OnProcessButtonClick); // Button click fires event after checks
                     Debug.Log("Process button click listener added to fire OnProcessButtonClicked.", this);
                }
                else
                {
                    Debug.LogError("PrescriptionTableManager: Process Button UI Root has no Button component! Please add one.", this);
                }
            }

            // Perform initial UI state update based on the overall root's current state
            UpdateOverallUIState(); // New method to check state and fire open/close event
        }

        private void Update()
        {
            // Monitor the active state of the main Prescription Table UI root
            if (prescriptionTableInventoryUIRoot != null)
            {
                bool isCurrentlyActive = prescriptionTableInventoryUIRoot.activeInHierarchy;

                // Detect if the active state of the overall UI root has changed
                if (_wasPrescriptionTableUIRootActive != isCurrentlyActive)
                {
                    Debug.Log($"Prescription Table UI Root changed state: {_wasPrescriptionTableUIRootActive} -> {isCurrentlyActive}.", this);

                    // Update the overall UI state based on the change
                    UpdateOverallUIState();

                    // Update the tracker for the next frame
                    _wasPrescriptionTableUIRootActive = isCurrentlyActive;
                }
            }
        }


        private void OnDestroy()
        {
            // Remove button listener
            if (_processButton != null)
            {
                 _processButton.onClick.RemoveListener(OnProcessButtonClick);
                 Debug.Log("Process button click listener removed.", this);
            }

            // --- Unsubscribe from Prescription Events ---
            PrescriptionEvents.OnPrescriptionUIOpened -= HandlePrescriptionUIOpened;
            PrescriptionEvents.OnPrescriptionUIClosed -= HandlePrescriptionUIClosed;
            PrescriptionEvents.OnCraftingComplete -= HandleCraftingComplete;
            PrescriptionEvents.OnCraftingFailed -= HandleCraftingFailed;
            // Unsubscribe from OnProcessButtonClicked event - this is handled by the Processors themselves in OnDisable
        }

        /// <summary>
        /// Checks the current active state of the overall UI root and fires the appropriate event.
        /// </summary>
        private void UpdateOverallUIState()
        {
            if (prescriptionTableInventoryUIRoot == null) return;

            bool isCurrentlyActive = prescriptionTableInventoryUIRoot.activeInHierarchy;

            if (isCurrentlyActive)
            {
                 PrescriptionEvents.InvokePrescriptionUIOpened();
            }
            else
            {
                 PrescriptionEvents.InvokePrescriptionUIClosed();
            }
        }


        // OnMainInventoryStateChanged is removed as it no longer drives UI visibility.
        // CheckForSetupReady is removed as it no longer drives UI visibility or setup readiness events.
        // ManageSpecificInventorySubscription and OnSpecificInventoryStateChanged are removed.


        /// <summary>
        /// Handles the click event from the process button.
        /// Performs checks (items present, recipe exists, quantity sufficient) and fires the OnProcessButtonClicked event.
        /// </summary>
        public void OnProcessButtonClick()
        {
            Debug.Log("Process Button Clicked! Attempting to fire OnProcessButtonClicked event.", this);

            // Determine the current relevant label from the main inventory for checking
            ItemLabel currentRelevantLabel = FindRelevantLabelInMainInventory();
            _activeProcessorLabel = currentRelevantLabel; // Update active label here

            // We need the specific inventory that corresponds to the relevant label
            Inventory specificInv = null;
            if (currentRelevantLabel != ItemLabel.None && specificInventories.TryGetValue(currentRelevantLabel, out specificInv))
            {
                // Find the item instances required for the event parameters
                Item secondaryItem = specificInv.InventoryState?.Count > 0 ? specificInv.InventoryState[0] : null;

                // Need the *specific* item instance from the main inventory that corresponds to _activeProcessorLabel
                Item mainItem = mainPrescriptiontableInventory?.InventoryState?.GetCurrentArrayState()?.FirstOrDefault(item => item != null && item.details != null && item.details.itemLabel == currentRelevantLabel);

                // Check if items are valid and a recipe exists for the secondary item
                 if (mainItem != null && mainItem.details != null && secondaryItem != null && secondaryItem.details != null && _recipeMap.TryGetValue(secondaryItem.details, out CraftingRecipe recipe))
                 {
                      // Pre-check quantities before firing the event
                      int availableMainQty = mainPrescriptiontableInventory.InventoryState.GetCurrentArrayState().Where(item => item != null && item.details == recipe.mainIngredientToConsume).Sum(item => item.quantity);
                       int availableSecondaryQty = specificInv.InventoryState.GetCurrentArrayState().Where(item => item != null && item.details == recipe.secondaryIngredient).Sum(item => item.quantity);

                      if (availableMainQty >= recipe.mainQuantityToConsume && availableSecondaryQty >= recipe.secondaryQuantityToConsume)
                      {
                          // All conditions met, fire the event with all necessary data
                          PrescriptionEvents.InvokeProcessButtonClicked(currentRelevantLabel, mainPrescriptiontableInventory, mainItem, specificInv, secondaryItem);
                          Debug.Log("Conditions met. OnProcessButtonClicked event fired.", this);

                          // Button interactability might be temporarily disabled here or handled by a listener
                          // For now, rely on processor success/failure to trigger feedback.
                      }
                      else
                      {
                          Debug.LogWarning("Process button clicked, but conditions NOT met for firing OnProcessButtonClicked. Insufficient quantities.", this);
                          // TODO: Provide feedback to the player about insufficient quantity.
                      }
                 }
                 else
                 {
                      // Debugging which item/recipe is missing/invalid
                      if (mainItem == null || mainItem.details == null) Debug.LogWarning($"OnProcessButtonClick: Main item invalid for label {currentRelevantLabel}.");
                      if (secondaryItem == null || secondaryItem.details == null) Debug.LogWarning($"OnProcessButtonClick: Secondary item invalid from specific inventory.");
                      if (secondaryItem != null && secondaryItem.details != null && !_recipeMap.ContainsKey(secondaryItem.details)) Debug.LogWarning($"OnProcessButtonClick: No recipe found for secondary item '{secondaryItem.details.Name}'.");
                      Debug.LogWarning("Process button clicked, but ingredient items or recipe invalid. Cannot fire event.", this);
                 }
            }
            else
            {
                Debug.LogWarning("Process button clicked, but no relevant item found in main inventory or corresponding specific inventory missing/empty. Cannot fire event.", this);
                // This state should theoretically not be possible if the button is only interactable when items are present,
                // but the button is now always visible when the UI is open. Added safety logs.
            }
        }


        // --- Event Handlers ---

        /// <summary>
        /// Handles the OnPrescriptionUIOpened event. Activates all relevant UI roots and the button.
        /// </summary>
        private void HandlePrescriptionUIOpened()
        {
            Debug.Log("HandlePrescriptionUIOpened: Received event. Activating all related UI.", this);

            // Activate all specific UI roots
            SetAllSpecificInventoryUIRootsActive(true);
            Debug.Log("Activated all specific UI roots.", this);

            // Activate the process button UI root
            SetProcessButtonActive(true);
            Debug.Log("Activated process button UI root.", this);

            // Activate the Output UI root
            SetOutputInventoryUIActive(true);
            Debug.Log("Activated Output UI root.", this);

            // _activeProcessorLabel does NOT need to be set here. It's determined OnProcessButtonClick.
        }


        /// <summary>
        /// Handles the OnPrescriptionUIClosed event. Deactivates all relevant UI roots and removes item movement logic.
        /// </summary>
        private void HandlePrescriptionUIClosed()
        {
            Debug.Log("HandlePrescriptionUIClosed: Received event. Deactivating all related UI and skipping item cleanup.", this);

            // Deactivate all specific UI roots
            SetAllSpecificInventoryUIRootsActive(false);
            Debug.Log("Deactivated all specific UI roots.", this);

            // Deactivate the process button
            SetProcessButtonActive(false);
            Debug.Log("Deactivated process button.", this);

            // Deactivate the Output UI root
            SetOutputInventoryUIActive(false);
            Debug.Log("Deactivated Output UI root.", this);


            // *** REMOVED ITEM MOVEMENT LOGIC ***
            // Items will now STAY in the specific and output inventories when the UI is closed.
            // If you need item movement on UI close, this logic would need to be added back here.
            // The logic for moving items back to toolbar based on main item removal (previous versions) is also gone.
            // *** END REMOVAL ***


            // Reset active label tracker
            _activeProcessorLabel = ItemLabel.None; // Reset active label when UI closes
            Debug.Log("Reset active label tracker.", this);
        }

        /// <summary>
        /// Handles the OnCraftingComplete event. Provides feedback/updates related to successful crafting.
        /// UI visibility is NOT managed here anymore.
        /// </summary>
        private void HandleCraftingComplete(ItemLabel label, Item craftedItem)
        {
            Debug.Log($"HandleCraftingComplete: Received event for label {label} and crafted item '{craftedItem?.details?.Name}'. Crafting successful feedback/updates go here.", this);
            // UI visibility is already handled by OnPrescriptionUIOpened/Closed events.
            // This handler can be used for effects, sounds, player feedback messages, etc.
        }

         /// <summary>
         /// Handles the OnCraftingFailed event. Provides feedback/updates related to crafting failure.
         /// UI visibility is NOT managed here anymore.
         /// </summary>
         private void HandleCraftingFailed(ItemLabel label, string reason)
         {
            Debug.LogWarning($"HandleCraftingFailed: Received event for label {label} with reason '{reason}'. Crafting failed feedback/updates go here.", this);
            // UI visibility is already handled by OnPrescriptionUIOpened/Closed events.
            // This handler can be used for failure effects, sounds, player feedback messages, etc.
         }


        /// <summary>
        /// Sets the active state of the process button UI root GameObject.
        /// Called by HandlePrescriptionUIOpened/Closed.
        /// </summary>
        private void SetProcessButtonActive(bool active)
        {
            // Only change state if necessary and if the button GameObject is assigned
            if (processButtonUIRoot != null && processButtonUIRoot.activeSelf != active)
            {
                 processButtonUIRoot.SetActive(active);
                 // Debug.Log($"Process Button UI Root set active: {active}", this); // Less noisy log
            }
            else if (processButtonUIRoot == null)
            {
                 // Warning is already logged in Awake if not assigned
            }
        }

         /// <summary>
         /// Helper method to set the active state of the Output Inventory UI root GameObject.
         /// Called by HandlePrescriptionUIOpened/Closed.
         /// </summary>
         private void SetOutputInventoryUIActive(bool active)
         {
              if (outputInventoryUIRoot != null && outputInventoryUIRoot.activeSelf != active)
              {
                   outputInventoryUIRoot.SetActive(active);
                   // Debug.Log($"Output Inventory UI Root set active: {active}", this); // Less noisy log
              }
              else if (outputInventoryUIRoot == null)
             {
                  // Warning is already logged in Start if not assigned
             }
         }


        /// <summary>
        /// Helper method to set the active state of all tracked specific UI roots GameObjects.
        /// Called by HandlePrescriptionUIOpened/Closed.
        /// </summary>
        private void SetAllSpecificInventoryUIRootsActive(bool active)
        {
            if (specificUIRoots == null) return;
            // Iterate through all specific UI roots and set their active state
            foreach (var pair in specificUIRoots)
            {
                if (pair.Value != null)
                {
                    // Only change state if necessary
                    if (pair.Value.activeSelf != active)
                    {
                       pair.Value.SetActive(active);
                       // Debug.Log($"Set specific UI root for {pair.Key} active: {active}.", this);
                    }
                }
                else
                {
                    Debug.LogWarning($"PrescriptionTableManager: Specific UI Root GameObject for {pair.Key} is not assigned!", this);
                }
            }
        }

         // SetSingleSpecificUIRootActive is no longer needed as all are active/inactive together.


        /// <summary>
        /// Helper method to find the first item in the main inventory's physical slots that matches one of the specific processor labels.
        /// Returns the ItemLabel of that item. Used by OnProcessButtonClick to identify the target processor.
        /// </summary>
        private ItemLabel FindRelevantLabelInMainInventory()
        {
             if (mainPrescriptiontableInventory == null || mainPrescriptiontableInventory.InventoryState == null || mainPrescriptiontableInventory.Combiner == null)
             {
                  Debug.LogWarning("FindRelevantLabelInMainInventory: Main Inventory, InventoryState, or Combiner is null.", this);
                  return ItemLabel.None;
             }

             Item[] currentItems = mainPrescriptiontableInventory.InventoryState.GetCurrentArrayState();
             int physicalSlotCount = mainPrescriptiontableInventory.Combiner.PhysicalSlotCount;

             // Loop through physical slots only
             for (int i = 0; i < physicalSlotCount; i++)
             {
                  // Safety check in case the array returned is unexpectedly smaller than physical count
                  if (i < currentItems.Length)
                  {
                       Item item = currentItems[i];

                       if (item != null && item.details != null)
                       {
                            ItemLabel currentItemLabel = item.details.itemLabel;
                            // Check if this item's label is one of the stock labels that correspond to a specific processor
                            // We don't need specificUIRoots.ContainsKey here anymore, just check if it's a valid processor type label.
                            // We can check against the keys in the specificInventories dictionary.
                            if (specificInventories.ContainsKey(currentItemLabel))
                            {
                                 return currentItemLabel; // Return the ItemLabel of the first relevant item found
                            }
                       }
                  }
                  else
                  {
                       Debug.LogError($"FindRelevantLabelInMainInventory: Loop index {i} exceeded currentItems array length ({currentItems.Length}) while still within physical slots ({physicalSlotCount}). This indicates an inconsistency in the Inventory system.", this);
                       break; // Prevent index out of bounds
                  }
             }
             return ItemLabel.None; // No relevant label found in physical slots
        }
    }
}