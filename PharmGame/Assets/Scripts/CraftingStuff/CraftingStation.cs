using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Systems.Inventory
{
    /// <summary>
    /// Manages the crafting process for a specific crafting station,
    /// handling input, state transitions, recipe checking, and output.
    /// Delegates UI presentation to CraftingUIHandler.
    /// Implements batch crafting and specific output clear detection.
    /// Preserves state when UI is closed and re-opened.
    /// </summary>
    public class CraftingStation : MonoBehaviour
    {
        public enum CraftingState
        {
            Inputting,
            Crafting,
            Outputting
        }

        [Header("References")]
        [Tooltip("The ScriptableObject containing all crafting recipes.")]
        [SerializeField] private CraftingRecipesSO craftingRecipes;

        [Tooltip("The Inventory component for the primary input slots.")]
        [SerializeField] public Inventory primaryInputInventory;

        [Tooltip("The Inventory component for the secondary input slots.")]
        [SerializeField] public Inventory secondaryInputInventory;

        [Tooltip("The Inventory component for the output slots.")]
        [SerializeField] public Inventory outputInventory;

        [Tooltip("The root GameObject for the entire crafting UI.")]
        [SerializeField] private GameObject craftingUIRoot;

        [Tooltip("The UI Handler component on the crafting UI root.")]
        private CraftingUIHandler uiHandler;


        [Header("State")]
        [Tooltip("The current state of the crafting station.")]
        [SerializeField] private CraftingState currentState = CraftingState.Inputting;

        private CraftingRecipe currentMatchedRecipe;
        private int maxCraftableBatches = 0;


        private void Awake()
        {
            if (craftingRecipes == null || primaryInputInventory == null || outputInventory == null || craftingUIRoot == null)
            {
                Debug.LogError($"CraftingStation ({gameObject.name}): Missing essential references in the Inspector! (CraftingRecipes, PrimaryInputInventory, OutputInventory, CraftingUIRoot)", this);
                enabled = false;
                return;
            }

            uiHandler = craftingUIRoot.GetComponent<CraftingUIHandler>();
            if (uiHandler == null)
            {
                Debug.LogError($"CraftingStation ({gameObject.name}): Crafting UI Root GameObject '{craftingUIRoot.name}' is missing the CraftingUIHandler component!", this);
                enabled = false;
                return;
            }

            craftingUIRoot.SetActive(false);

            SetupInventoryListeners();
        }

        private void OnDestroy()
        {
            if (primaryInputInventory?.InventoryState != null)
            {
                primaryInputInventory.InventoryState.AnyValueChanged -= HandlePrimaryInputChange;
            }
            if (secondaryInputInventory?.InventoryState != null)
            {
                 secondaryInputInventory.InventoryState.AnyValueChanged -= HandleSecondaryInputChange;
            }
            if (outputInventory?.InventoryState != null)
            {
                 outputInventory.InventoryState.AnyValueChanged -= HandleOutputInventoryChange;
            }
        }

        /// <summary>
        /// Called by the MenuManager to open the crafting UI.
        /// Resumes the state the station was in when the UI was closed.
        /// </summary>
        public void OpenCraftingUI()
        {
            Debug.Log($"CraftingStation ({gameObject.name}): Opening Crafting UI. Resuming state: {currentState}.", this);
            craftingUIRoot.SetActive(true);
            uiHandler.LinkCraftingStation(this);

            // --- CHANGED: Don't force state to Inputting. Update UI based on current state. ---
            // SetState(CraftingState.Inputting); // REMOVED this line
            SetState(currentState); // Call SetState with the *current* state to trigger UI update and entry action
            // ---------------------------------------------------------------------------------
        }

        /// <summary>
        /// Called to close the crafting UI. The station retains its state.
        /// </summary>
        public void CloseCraftingUI()
        {
            Debug.Log($"CraftingStation ({gameObject.name}): Closing Crafting UI. Current state: {currentState}.", this);
            craftingUIRoot.SetActive(false);
        }

        public void NotifyCraftButtonClicked()
        {
             OnCraftButtonClicked();
        }

        // --- State Management ---

        private void SetState(CraftingState newState)
        {
            // Always update the UI handler first, before checking if state actually changed
            if (uiHandler != null)
            {
                 uiHandler.UpdateUIState(newState);
            }

            if (currentState == newState)
            {
                 // If state didn't actually change, we still updated the UI Handler.
                 // We don't call exit/entry actions if the state is the same.
                 return;
            }

            // If we reach here, the state is actually changing
            CraftingState previousState = currentState;
            currentState = newState;
            Debug.Log($"CraftingStation ({gameObject.name}): State actually changed to {currentState}.", this);

            // Handle exit actions for the *previous* state
            HandleStateExit(previousState);

            // Handle entry actions for the *new* state
            HandleStateEntry(currentState);
        }

        private void HandleStateExit(CraftingState state)
        {
            switch (state)
            {
                case CraftingState.Inputting:
                    // Ensure button is disabled when leaving input
                    if (uiHandler != null) uiHandler.SetCraftButtonInteractable(false);
                    // currentMatchedRecipe and maxCraftableBatches are cleared in CompleteCraft
                    break;
                case CraftingState.Crafting:
                    // Clean up after crafting if necessary
                    break;
                case CraftingState.Outputting:
                    // Clean up after exiting Outputting state (e.g., after items are taken)
                    // This might involve clearing any temporary state related to the output
                    break;
            }
        }


        private void HandleStateEntry(CraftingState state)
        {
            switch (state)
            {
                case CraftingState.Inputting:
                    // On entering input state, re-check recipe match
                    CheckForRecipeMatch();
                    break;
                case CraftingState.Crafting:
                    Debug.Log($"CraftingStation ({gameObject.name}): Crafting in progress...", this);
                    CompleteCraft();
                    break;
                case CraftingState.Outputting:
                    Debug.Log($"CraftingStation ({gameObject.name}): Crafting complete. Item(s) available in output.", this);
                    // The transition back to Inputting is handled ONLY by HandleOutputInventoryChange
                    // when the output ghost slot is cleared by a drag.
                    // We DO NOT automatically check IsOutputInventoryEmpty() here on entry.
                    // This allows re-opening the UI and staying in Outputting if items are present.
                    break;
            }
        }


        // --- Inventory Event Handling ---

        private void SetupInventoryListeners()
        {
            if (primaryInputInventory?.InventoryState != null)
            {
                primaryInputInventory.InventoryState.AnyValueChanged += HandlePrimaryInputChange;
                 Debug.Log($"CraftingStation ({gameObject.name}): Subscribed to Primary Input Inventory changes.", this);
            }
             else
             {
                 Debug.LogError($"CraftingStation ({gameObject.name}): Primary Input Inventory or its ObservableArray is null. Cannot subscribe to changes.", this);
             }

            if (secondaryInputInventory?.InventoryState != null)
            {
                 secondaryInputInventory.InventoryState.AnyValueChanged += HandleSecondaryInputChange;
                Debug.Log($"CraftingStation ({gameObject.name}): Subscribed to Secondary Input Inventory changes.", this);
            }
             else
             {
                 Debug.LogWarning($"CraftingStation ({gameObject.name}): Secondary Input Inventory or its ObservableArray is null. Skipping subscription. This is okay if you only use one input inventory.", this);
             }

            // Subscribe to changes in the output inventory to detect when output is taken
            if (outputInventory?.InventoryState != null)
            {
                 outputInventory.InventoryState.AnyValueChanged += HandleOutputInventoryChange;
                 Debug.Log($"CraftingStation ({gameObject.name}): Subscribed to Output Inventory changes.", this);
            }
            else
             {
                 Debug.LogError($"CraftingStation ({gameObject.name}): Output Inventory or its ObservableArray is null. Cannot subscribe to changes.", this);
             }
        }

        private void HandlePrimaryInputChange(ArrayChangeInfo<Item> changeInfo)
        {
             // Only check for recipe matches if we are actively in the Inputting state
             if (currentState == CraftingState.Inputting)
             {
                 CheckForRecipeMatch();
             }
              // If not in Inputting state, changes to input inventories are ignored by recipe logic.
        }

        private void HandleSecondaryInputChange(ArrayChangeInfo<Item> changeInfo)
        {
             // Only check for recipe matches if we are actively in the Inputting state
             if (currentState == CraftingState.Inputting)
             {
                 CheckForRecipeMatch();
             }
              // If not in Inputting state, changes to input inventories are ignored by recipe logic.
        }

        /// <summary>
        /// Handles changes in the output inventory. Specifically checks for
        /// item removal via drag-and-drop from the ghost slot to trigger
        /// state transition if the inventory becomes empty.
        /// </summary>
        private void HandleOutputInventoryChange(ArrayChangeInfo<Item> changeInfo)
        {
             // Debug.Log($"CraftingStation ({gameObject.name}): Output Inventory Change received - Type: {changeInfo.Type}, Index: {changeInfo.Index}, NewItem: {changeInfo.NewItem?.details?.Name ?? "null"}", this); // Verbose

             // We only care about changes when in the Outputting state
             if (currentState != CraftingState.Outputting)
             {
                 // Optional: Log if a change happens in the output while NOT in Outputting state
                 // Debug.LogWarning($"CraftingStation ({gameObject.name}): Output Inventory change detected while not in Outputting state ({currentState}). Ignoring transition check.", this);
                 return;
             }

             // We want to detect when an item has been successfully dragged *out*
             // This is signaled by the source array (which was the output inventory)
             // clearing its *ghost slot* (the last index) as the final step of a drop.
             // Note: The ArrayChangeInfo currently doesn't directly tell us the source array,
             // but because this handler is ONLY subscribed to outputInventory.InventoryState,
             // we know the change originated from the output inventory.
             bool isGhostSlotUpdateFromOutput = changeInfo.Type == ArrayChangeType.SlotUpdated &&
                                                outputInventory?.Combiner?.InventoryState != null && // Ensure references are valid
                                                changeInfo.Index == outputInventory.Combiner.InventoryState.Length - 1 &&
                                                changeInfo.NewItem == null; // Check if the ghost slot became empty

             if (isGhostSlotUpdateFromOutput)
             {
                 Debug.Log($"CraftingStation ({gameObject.name}): Detected Output Inventory ghost slot cleared (likely due to drag completion). Checking if output is now empty.", this);
                 if (IsOutputInventoryEmpty())
                 {
                     Debug.Log($"CraftingStation ({gameObject.name}): Output inventory is now empty. Transitioning back to Inputting.", this);
                     SetState(CraftingState.Inputting); // Transition back when output is fully cleared
                 }
                 else
                 {
                     Debug.Log($"CraftingStation ({gameObject.name}): Output inventory ghost slot cleared, but physical slots still contain items. Remaining in Outputting state.", this);
                 }
             }
              // For any other change type (e.g., item added back, item swapped internally)
              // or index, we don't trigger the state transition back to Inputting.
        }


        // --- Recipe Matching Logic ---

        /// <summary>
        /// Checks if the current items in the input inventories match any recipe
        /// and calculates the maximum number of batches that can be crafted.
        /// Updates the craft button interactability via the UI handler.
        /// Only relevant in Inputting state.
        /// </summary>
        private void CheckForRecipeMatch()
        {
            // Only check in the Inputting state
            if (currentState != CraftingState.Inputting)
            {
                // If we are not in the Inputting state, ensure no recipe is matched and button is off.
                // This prevents bugs if CheckForRecipeMatch is somehow called externally while not in Inputting.
                currentMatchedRecipe = null;
                maxCraftableBatches = 0;
                if (uiHandler != null) uiHandler.SetCraftButtonInteractable(false);
                return;
            }

            if (craftingRecipes == null)
            {
                 Debug.LogError($"CraftingStation ({gameObject.name}): Cannot check for recipe match, Crafting Recipes SO is null.", this);
                 currentMatchedRecipe = null;
                 maxCraftableBatches = 0;
                 if (uiHandler != null) uiHandler.SetCraftButtonInteractable(false);
                 return;
            }

            if (primaryInputInventory?.Combiner?.InventoryState == null)
            {
                 Debug.LogError($"CraftingStation ({gameObject.name}): Primary Input Inventory or its components are null. Cannot check recipe.", this);
                 currentMatchedRecipe = null;
                 maxCraftableBatches = 0;
                 if (uiHandler != null) uiHandler.SetCraftButtonInteractable(false);
                 return;
            }

            // Get the current state of the input inventories (physical slots only)
            Item[] primaryInputs = primaryInputInventory.Combiner.InventoryState.GetCurrentArrayState()
                                    .Take(primaryInputInventory.Combiner.PhysicalSlotCount)
                                    .Where(item => item != null && item.details != null && item.quantity > 0)
                                    .ToArray();

            Item[] secondaryInputs = null;
             if (secondaryInputInventory?.Combiner?.InventoryState != null)
             {
                 secondaryInputs = secondaryInputInventory.Combiner.InventoryState.GetCurrentArrayState()
                                     .Take(secondaryInputInventory.Combiner.PhysicalSlotCount)
                                     .Where(item => item != null && item.details != null && item.quantity > 0)
                                     .ToArray();
             }

            List<Item> allInputItems = new List<Item>();
            allInputItems.AddRange(primaryInputs);
             if (secondaryInputs != null)
             {
                 allInputItems.AddRange(secondaryInputs);
             }

            currentMatchedRecipe = null;
            maxCraftableBatches = 0; // Reset batches

            // --- Recipe Matching Logic ---
            // Find a recipe where the *types* of items present exactly match the *types* required by the recipe,
            // AND calculate the maximum batches based on quantities.

            foreach (var recipe in craftingRecipes.recipes)
            {
                 // --- Check if input item *types* match recipe inputs (exact set) ---
                 HashSet<SerializableGuid> distinctInputItemDetailsIds = new HashSet<SerializableGuid>();
                 foreach (var inputItem in allInputItems)
                 {
                     if (inputItem?.details != null && inputItem.details.Id != SerializableGuid.Empty)
                     {
                         distinctInputItemDetailsIds.Add(inputItem.details.Id);
                     }
                 }

                 // First check: do the *types* of items present exactly match the *types* required by the recipe?
                 // Ensure no extra types are present, and all required types are present.
                 bool typesMatchExactly = (distinctInputItemDetailsIds.Count == recipe.inputs.Count) &&
                                          recipe.inputs.All(required => required?.itemDetails != null && distinctInputItemDetailsIds.Contains(required.itemDetails.Id));

                 if (!typesMatchExactly)
                 {
                      // Debug.Log($"Checking recipe '{recipe.recipeName}': Item types do not match exactly."); // Verbose
                      continue; // Item types don't match this recipe, skip
                 }

                // --- If types match, calculate max batches based on quantities ---
                int potentialBatches = int.MaxValue; // Start with a high number

                bool canCraftAtLeastOne = true; // Flag to ensure we can craft at least one batch

                // For each required input in the recipe
                foreach (var requiredInput in recipe.inputs)
                {
                    if (requiredInput.itemDetails == null)
                    {
                        Debug.LogWarning($"CraftingStation ({gameObject.name}): Recipe '{recipe.recipeName}' has a required input with null ItemDetails. Cannot check batch count.", this);
                        canCraftAtLeastOne = false; // Cannot craft this recipe if an input is invalid
                        break;
                    }
                    if (requiredInput.quantity <= 0)
                    {
                         Debug.LogWarning($"CraftingStation ({gameObject.name}): Recipe '{recipe.recipeName}' has input '{requiredInput.itemDetails.Name}' with quantity <= 0 ({requiredInput.quantity}). Cannot craft.", this);
                         canCraftAtLeastOne = false; // Cannot craft if a required quantity is invalid
                         break;
                    }


                    // Find the total quantity of this item type across all input slots
                    int totalInputQuantity = allInputItems
                                            .Where(item => item.details != null && item.details == requiredInput.itemDetails)
                                            .Sum(item => item.quantity);

                    // If we don't even have the minimum quantity for one batch, this recipe isn't craftable
                    if (totalInputQuantity < requiredInput.quantity)
                    {
                        canCraftAtLeastOne = false;
                        // Debug.Log($"Checking recipe '{recipe.recipeName}': Not enough quantity for {requiredInput.itemDetails.Name} (needed {requiredInput.quantity}, found {totalInputQuantity})."); // Verbose
                        break; // Not enough quantity for this ingredient, cannot craft this recipe at all
                    }

                    // Calculate how many batches *this* ingredient can support
                    int batchesSupportedByThisIngredient = totalInputQuantity / requiredInput.quantity;

                    // The limiting factor is the ingredient that supports the fewest batches
                    potentialBatches = Mathf.Min(potentialBatches, batchesSupportedByThisIngredient);
                }

                // If we can craft at least one batch (all required ingredients are present in sufficient quantity)
                if (canCraftAtLeastOne && potentialBatches >= 1)
                {
                    currentMatchedRecipe = recipe;
                    maxCraftableBatches = potentialBatches;
                    break; // Found a match, no need to check other recipes
                }
                 else
                 {
                      // Debug.Log($"Checking recipe '{recipe.recipeName}': Cannot craft at least one batch."); // Verbose
                 }
            }

            // --- Update Craft Button ---
            if (uiHandler != null)
            {
                // Button is interactable if a recipe is matched AND we can craft at least one batch
                uiHandler.SetCraftButtonInteractable(currentMatchedRecipe != null && maxCraftableBatches >= 1);

                 if (currentMatchedRecipe != null && maxCraftableBatches >= 1)
                 {
                     Debug.Log($"CraftingStation ({gameObject.name}): Recipe matched: {currentMatchedRecipe.recipeName}! Can craft {maxCraftableBatches} batch(es).", this);
                 }
                 // else { Debug.Log($"CraftingStation ({gameObject.name}): No recipe matched or cannot craft any batches. Craft button disabled.", this); } // Verbose only if needed
            }
        }

        /// <summary>
        /// Called when the Craft button is clicked. Initiates the crafting process.
        /// </summary>
        private void OnCraftButtonClicked()
        {
            Debug.Log($"CraftingStation ({gameObject.name}): Craft button clicked. Attempting to craft {maxCraftableBatches} batches.", this);

            // Double-check state, recipe match, and batches
            if (currentState != CraftingState.Inputting || currentMatchedRecipe == null || maxCraftableBatches <= 0)
            {
                Debug.LogWarning($"CraftingStation ({gameObject.name}): Craft button clicked but not in Inputting state, no recipe matched, or zero batches craftable.", this);
                // Re-check recipe match in case inputs changed between enable and click
                 CheckForRecipeMatch();
                 if (currentState != CraftingState.Inputting || currentMatchedRecipe == null || maxCraftableBatches <= 0)
                 {
                     Debug.LogWarning($"CraftingStation ({gameObject.name}): Craft button clicked again with invalid state/recipe after re-check. Aborting.", this);
                     return;
                 }
            }

            // Proceed with crafting
            SetState(CraftingState.Crafting);
        }

        /// <summary>
        /// Performs the actual item consumption and creation for the calculated batches.
        /// Called internally after the crafting 'time' (currently immediate).
        /// </summary>
        private void CompleteCraft()
        {
             Debug.Log($"CraftingStation ({gameObject.name}): Completing craft for recipe: {currentMatchedRecipe?.recipeName ?? "Unknown Recipe"}, {maxCraftableBatches} batch(es).", this);

             if (currentMatchedRecipe == null || maxCraftableBatches <= 0)
             {
                 Debug.LogError($"CraftingStation ({gameObject.name}): CompleteCraft called but no recipe is matched or batches are zero! Matched Recipe Null: { (currentMatchedRecipe == null) }, Batches: {maxCraftableBatches}");
                 // If CompleteCraft is called in a bad state, return to Inputting to allow fixing inputs.
                 if (currentState != CraftingState.Inputting) SetState(CraftingState.Inputting);
                 return;
             }

             if (primaryInputInventory?.Combiner == null || outputInventory?.Combiner == null || craftingRecipes == null)
             {
                 Debug.LogError($"CraftingStation ({gameObject.name}): Cannot complete craft, one or more required components/references are null.");
                 if (currentState != CraftingState.Inputting) SetState(CraftingState.Inputting);
                 return;
             }
             if (secondaryInputInventory != null && secondaryInputInventory.Combiner == null)
             {
                  Debug.LogError($"CraftingStation ({gameObject.name}): Secondary Input Inventory is assigned but its Combiner is null. Cannot complete craft.");
                   if (currentState != CraftingState.Inputting) SetState(CraftingState.Inputting);
                   return;
             }


            // --- Consume Inputs (based on maxCraftableBatches) ---
            bool consumptionSuccess = true;
            foreach (var requiredInput in currentMatchedRecipe.inputs)
            {
                if (requiredInput.itemDetails == null)
                {
                     Debug.LogError($"CraftingStation ({gameObject.name}): Recipe '{currentMatchedRecipe.recipeName}' has a required input with null ItemDetails. Aborting consumption.", this);
                     consumptionSuccess = false;
                     break;
                }

                int totalQuantityToConsume = requiredInput.quantity * maxCraftableBatches;
                Debug.Log($"CraftingStation ({gameObject.name}): Attempting to consume {totalQuantityToConsume} of {requiredInput.itemDetails.Name}.", this);

                int removedFromPrimary = 0;
                if (primaryInputInventory.Combiner != null)
                {
                     removedFromPrimary = primaryInputInventory.Combiner.TryRemoveQuantity(requiredInput.itemDetails, totalQuantityToConsume);
                }

                int remainingToRemove = totalQuantityToConsume - removedFromPrimary;
                int removedFromSecondary = 0;
                if (secondaryInputInventory?.Combiner != null && remainingToRemove > 0)
                {
                    removedFromSecondary = secondaryInputInventory.Combiner.TryRemoveQuantity(requiredInput.itemDetails, remainingToRemove);
                }

                if (removedFromPrimary + removedFromSecondary != totalQuantityToConsume)
                {
                     // This is a critical error - indicates discrepancy between batch calculation and actual removal
                    Debug.LogError($"CraftingStation ({gameObject.name}): CRITICAL ERROR: Failed to fully consume input '{requiredInput.itemDetails.Name}' (needed {totalQuantityToConsume}, removed {removedFromPrimary + removedFromSecondary}) during craft completion! This indicates a major bug in batch calculation or removal logic.");
                    // TODO: Implement more robust error handling here - maybe try to return consumed items?
                    consumptionSuccess = false;
                    break;
                }
                 else
                 {
                      Debug.Log($"CraftingStation ({gameObject.name}): Successfully consumed {totalQuantityToConsume} of {requiredInput.itemDetails.Name}.");
                 }
            }

            if (!consumptionSuccess)
            {
                Debug.LogError($"CraftingStation ({gameObject.name}): Input consumption failed. Aborting craft output.");
                 // If consumption fails, return to Inputting state to allow fixing inputs.
                 if (currentState != CraftingState.Inputting) SetState(CraftingState.Inputting);
                return;
            }


            // --- Produce Outputs (based on maxCraftableBatches) ---
            // Note: Even if adding outputs fails (e.g., output inventory full), we still transition to Outputting state
            // so the player can clear space.
            foreach (var producedOutput in currentMatchedRecipe.outputs)
            {
                if (producedOutput.itemDetails == null)
                {
                    Debug.LogError($"CraftingStation ({gameObject.name}): Recipe '{currentMatchedRecipe.recipeName}' has a produced output with null ItemDetails. Skipping this output item.", this);
                    continue;
                }
                 if (producedOutput.quantity <= 0)
                 {
                      Debug.LogWarning($"CraftingStation ({gameObject.name}): Recipe '{currentMatchedRecipe.recipeName}' specifies output '{producedOutput.itemDetails.Name}' with zero or negative quantity ({producedOutput.quantity}). Skipping this output item.", this);
                      continue;
                 }

                // Calculate total quantity to produce for this output item type
                int totalQuantityToProduce = producedOutput.quantity * maxCraftableBatches;

                // Create a new Item instance for this output type with the total produced quantity
                // AddItem will handle stacking in the output inventory if needed.
                Item outputItemInstance = new Item(producedOutput.itemDetails, totalQuantityToProduce);

                // Attempt to add the output item to the output inventory
                bool added = outputInventory.Combiner.AddItem(outputItemInstance);

                if (!added)
                {
                    Debug.LogError($"CraftingStation ({gameObject.name}): Failed to add output item '{producedOutput.itemDetails.Name}' (Qty: {totalQuantityToProduce}) to output inventory. Output inventory might be full!");
                    // TODO: Handle this failure. Drop item? Place in player inventory?
                    // Crafting still succeeds conceptually, but items couldn't be placed.
                }
                else
                 {
                      Debug.Log($"CraftingStation ({gameObject.name}): Produced and added {totalQuantityToProduce} of {producedOutput.itemDetails.Name} to output inventory.");
                 }
            }

             // Always transition to Outputting after attempting to produce items
             SetState(CraftingState.Outputting);

            // Clear the matched recipe and batches NOW, after production is attempted
            currentMatchedRecipe = null;
            maxCraftableBatches = 0;
            // Button interactability is handled by UI handler based on state (will be off in Outputting)
        }

        /// <summary>
        /// Checks if the output inventory is completely empty of physical items.
        /// </summary>
        private bool IsOutputInventoryEmpty()
        {
             if (outputInventory?.Combiner?.InventoryState == null)
             {
                 Debug.LogWarning($"CraftingStation ({gameObject.name}): Cannot check if output inventory is empty, references are null.", this);
                 return true;
             }

             // Check physical slots only. The ghost slot is not relevant here.
             Item[] outputItems = outputInventory.Combiner.InventoryState.GetCurrentArrayState();

             for (int i = 0; i < outputInventory.Combiner.PhysicalSlotCount; i++)
             {
                 if (i < outputItems.Length && outputItems[i] != null) // Ensure index is valid and slot is not null
                 {
                     return false; // Found an item in a physical slot
                 }
             }
             return true; // No items found in physical slots
        }
    }
}