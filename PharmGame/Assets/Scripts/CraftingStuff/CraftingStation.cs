// Systems/Inventory/CraftingStation.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System;
using Systems.CraftingMinigames;
using Systems.GameStates; // Needed for MenuManager


namespace Systems.Inventory
{
    /// <summary>
    /// Manages the crafting process for a specific crafting station,
    /// handling input, state transitions, recipe checking, and output.
    /// Delegates UI presentation to CraftingUIHandler.
    /// Implements batch crafting and specific output clear detection.
    /// Preserves state when UI is closed and re-opened.
    /// Delegates minigame execution to CraftingMinigameManager.
    /// Handles minigame outcomes including success and abort.
    /// </summary>
    public class CraftingStation : MonoBehaviour
    {
        public enum CraftingState
        {
            Inputting,
            Crafting, // Minigame active
            Outputting // Output available
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
        private CraftingUIHandler uiHandler; // Assuming this exists and handles UI visuals

        [Header("Crafting Minigame")]
        // --- MODIFIED: Reference is now a serialized field ---
        [Tooltip("The CraftingMinigameManager component responsible for running minigames.")]
        [SerializeField] private CraftingMinigameManager craftingMinigameManager;
        // ----------------------------------------------------

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
                Debug.LogWarning($"CraftingStation ({gameObject.name}): Crafting UI Root GameObject '{craftingUIRoot.name}' is missing the CraftingUIHandler component! UI visuals may not function correctly.", this);
            }

            // --- MODIFIED: Check if serialized manager reference is assigned ---
             if (craftingMinigameManager == null)
             {
                 Debug.LogError($"CraftingStation ({gameObject.name}): CraftingMinigameManager reference is not assigned in the inspector! Crafting minigames will not function.", this);
                 // Do NOT disable the station, maybe it can still be used for simple non-minigame recipes if implemented?
             }
            // ----------------------------------------------------------------


            craftingUIRoot.SetActive(false);

            SetupInventoryListeners();
        }

        private void OnEnable()
        {
            // Subscribe to the CraftingMinigameManager's completion event here,
            // as the manager is a singleton or persistent reference.
            // --- MODIFIED: Use the serialized manager reference ---
            if (craftingMinigameManager != null)
            {
                 craftingMinigameManager.OnMinigameSessionCompleted += HandleCraftingMinigameCompleted;
                 Debug.Log($"CraftingStation ({gameObject.name}): Subscribed to CraftingMinigameManager completion event.", this);
            }
            // ----------------------------------------------------
        }

        private void OnDisable()
        {
            // Unsubscribe from the CraftingMinigameManager's completion event here.
            // --- MODIFIED: Use the serialized manager reference ---
            if (craftingMinigameManager != null)
            {
                 craftingMinigameManager.OnMinigameSessionCompleted -= HandleCraftingMinigameCompleted;
                 Debug.Log($"CraftingStation ({gameObject.name}): Unsubscribed from CraftingMinigameManager completion event.", this);
            }
            // ----------------------------------------------------

             // Unsubscribe from inventory events
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

        private void OnDestroy()
        {
            // OnDisable handles unsubscribing from the manager.
            // OnDisable also handles unsubscribing from inventory events.
        }

        /// <summary>
        /// Called by the MenuManager to open the crafting UI.
        /// Resumes the state the station was in when the UI was closed.
        /// </summary>
        public void OpenCraftingUI()
        {
            Debug.Log($"CraftingStation ({gameObject.name}): Opening Crafting UI. Resuming state: {currentState}.", this);
            craftingUIRoot.SetActive(true);
            // Link UI Handler if it exists
            if (uiHandler != null) uiHandler.LinkCraftingStation(this);

            // SetState with the *current* state to trigger UI update and entry action
            SetState(currentState);
        }

        /// <summary>
        /// Called to close the crafting UI. The station retains its state.
        /// </summary>
        public void CloseCraftingUI()
        {
            Debug.Log($"CraftingStation ({gameObject.name}): Closing Crafting UI. Current state: {currentState}.", this);
            craftingUIRoot.SetActive(false);
        }

        /// <summary>
        /// Called by the UI Handler when the craft button is clicked.
        /// </summary>
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
                    break;
                case CraftingState.Crafting:
                    Debug.Log($"CraftingStation ({gameObject.name}): Exiting Crafting state.", this);
                    // When leaving CraftingState, the minigame is assumed to be ending or ended,
                    // orchestrated by the MenuManager/CraftingMinigameManager flow.
                    // We do NOT explicitly call EndCurrentMinigame here from the station's internal state exit.
                    break;
                case CraftingState.Outputting:
                    // Clean up after exiting Outputting state (e.g., after items are taken)
                    break;
            }
        }


        private void HandleStateEntry(CraftingState state)
        {
            switch (state)
            {
                case CraftingState.Inputting:
                    // On entering input state, re-check recipe match
                    Debug.Log($"CraftingStation ({gameObject.name}): Entering Inputting state. Checking for recipe match.", this);
                    CheckForRecipeMatch();
                    // currentMatchedRecipe and maxCraftableBatches are cleared in HandleCraftingMinigameCompleted or on failure
                    break;
                case CraftingState.Crafting:
                    Debug.Log($"CraftingStation ({gameObject.name}): Entering Crafting state. Starting minigame via manager.", this);
                    // --- MODIFIED: Use the serialized manager reference ---
                    if (craftingMinigameManager != null)
                    {
                        // Start the appropriate minigame based on the matched recipe
                        // The CraftingMinigameManager will transition MenuManager.GameState to InMinigame
                        // and will handle subscribing its own completion event (which we are subscribed to in OnEnable).
                        bool started = craftingMinigameManager.StartCraftingMinigame(currentMatchedRecipe, maxCraftableBatches);

                        if (!started)
                        {
                            Debug.LogError("CraftingStation: Failed to start crafting minigame. Returning to Inputting.", this);
                            // If minigame failed to start, immediately return to Inputting state
                            SetState(CraftingState.Inputting);
                            // Clear the potentially problematic recipe/batches - this is handled below in HandleCraftingMinigameCompleted on failure.
                        }
                         // IMPORTANT: Do NOT clear currentMatchedRecipe/maxCraftableBatches here!
                         // The minigame needs this data implicitly via the recipe.
                         // They are cleared in HandleCraftingMinigameCompleted after the outcome is processed.
                    }
                    else
                    {
                        Debug.LogError("CraftingStation: CraftingMinigameManager reference is null. Cannot start crafting minigame. Returning to Inputting.", this);
                        // If manager is null, immediately return to Inputting state
                        SetState(CraftingState.Inputting);
                        // Clear the potentially problematic recipe/batches - handled below.
                    }
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

        /// <summary>
        /// Handles the completion event from the crafting minigame manager.
        /// Proceeds with item consumption and production if successful, then transitions to Outputting.
        /// Transitions back to Inputting if the minigame failed or was aborted.
        /// Parameter is boolean indicating success.
        /// </summary>
        /// <param name="resultData">Boolean: true if minigame was successful, false if it failed or was aborted.</param>
        private void HandleCraftingMinigameCompleted(object resultData)
        {
            bool minigameWasSuccessful = (resultData is bool success) ? success : false;
            Debug.Log($"CraftingStation ({gameObject.name}): Received Crafting Minigame Completed event. Outcome: {(minigameWasSuccessful ? "Success" : "Failure/Aborted")}.", this);

            // Handle success or failure/abort
            if (minigameWasSuccessful)
            {
                Debug.Log($"CraftingStation: Crafting minigame reported success. Proceeding with craft execution.", this);
                // Proceed with the actual item consumption and production
                // Use the stored currentMatchedRecipe and maxCraftableBatches from BEFORE the minigame started
                // These are guaranteed to be valid IF minigameWasSuccessful is true,
                // because they would only be cleared *after* calling this method.
                // However, the CompleteCraft method relies on parameters.
                // Let's call CompleteCraft with the stored members, as they are available at this point in the success case.
                CompleteCraft(currentMatchedRecipe, maxCraftableBatches); // Pass values if needed, current design passes them.

                // After completing the craft (items consumed/produced), transition to the Outputting state
                SetState(CraftingState.Outputting);
            }
            else
            {
                Debug.LogWarning($"CraftingStation: Crafting minigame reported failure or was aborted. Not consuming items or producing output. Returning to Inputting state.", this);
                // If the minigame failed or was aborted, return to the input state without completing the craft
                SetState(CraftingState.Inputting);
                // Optionally, show a message to the player about the failure
                // if (uiHandler != null) uiHandler.ShowMessage("Crafting Failed!");
            }

            // Clear the matched recipe and batches NOW, after execution or failure handling
            // This happens regardless of success or failure, preventing leftover state.
            currentMatchedRecipe = null;
            maxCraftableBatches = 0;
             // Button interactability is handled by UI handler based on state (will be off in Outputting/Inputting)
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
        }

        private void HandleSecondaryInputChange(ArrayChangeInfo<Item> changeInfo)
        {
             // Only check for recipe matches if we are actively in the Inputting state
             if (currentState == CraftingState.Inputting)
             {
                 CheckForRecipeMatch();
             }
        }

        /// <summary>
        /// Handles changes in the output inventory. Specifically checks for
        /// item removal via drag-and-drop from the ghost slot to trigger
        /// state transition if the inventory becomes empty.
        /// </summary>
        private void HandleOutputInventoryChange(ArrayChangeInfo<Item> changeInfo)
        {
             // We only care about changes when in the Outputting state
             if (currentState != CraftingState.Outputting)
             {
                 return;
             }

             // We want to detect when an item has been successfully dragged *out*
             // This is signaled by the source array (which was the output inventory)
             // clearing its *ghost slot* (the last index) as the final step of a drop.
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
        }


        // --- Recipe Matching Logic ---

        private void CheckForRecipeMatch()
        {
            // Only check in the Inputting state
            if (currentState != CraftingState.Inputting)
            {
                currentMatchedRecipe = null;
                maxCraftableBatches = 0;
                if (uiHandler != null) uiHandler.SetCraftButtonInteractable(false);
                return;
            }

            if (craftingRecipes == null || primaryInputInventory?.Combiner?.InventoryState == null)
            {
                Debug.LogError($"CraftingStation ({gameObject.name}): Cannot check for recipe match, missing essential references.", this);
                currentMatchedRecipe = null;
                maxCraftableBatches = 0;
                if (uiHandler != null) uiHandler.SetCraftButtonInteractable(false);
                return;
            }

            // Collect all relevant items from input inventories
            List<Item> allInputItems = new List<Item>();
            allInputItems.AddRange(primaryInputInventory.Combiner.InventoryState.GetCurrentArrayState()
                                    .Take(primaryInputInventory.Combiner.PhysicalSlotCount)
                                    .Where(item => item != null && item.details != null && item.quantity > 0));

            if (secondaryInputInventory?.Combiner?.InventoryState != null)
            {
                allInputItems.AddRange(secondaryInputInventory.Combiner.InventoryState.GetCurrentArrayState()
                                        .Take(secondaryInputInventory.Combiner.PhysicalSlotCount)
                                        .Where(item => item != null && item.details != null && item.quantity > 0));
            }

            // --- Delegate recipe matching to the external helper ---
            RecipeMatchResult matchResult = CraftingMatcher.FindRecipeMatch(craftingRecipes, allInputItems); // Assuming CraftingMatcher exists

            currentMatchedRecipe = matchResult.MatchedRecipe;
            maxCraftableBatches = matchResult.MaxCraftableBatches;

            // --- Update Craft Button based on result ---
            if (uiHandler != null)
            {
                uiHandler.SetCraftButtonInteractable(matchResult.HasMatch);

                if (matchResult.HasMatch)
                {
                    Debug.Log($"CraftingStation ({gameObject.name}): Recipe matched: {currentMatchedRecipe.recipeName}! Can craft {maxCraftableBatches} batch(es).", this);
                }
            }
        }

        /// <summary>
        /// Called when the Craft button is clicked. Initiates the crafting process
        /// by transitioning to the Crafting state, which will start the minigame.
        /// </summary>
        private void OnCraftButtonClicked()
        {
            Debug.Log($"CraftingStation ({gameObject.name}): Craft button clicked. Attempting to transition to Crafting state.", this);

            // Double-check state, recipe match, and batches before allowing transition
            if (currentState != CraftingState.Inputting || currentMatchedRecipe == null || maxCraftableBatches <= 0)
            {
                Debug.LogWarning($"CraftingStation ({gameObject.name}): Craft button clicked but not in Inputting state, no recipe matched, or zero batches craftable. Re-checking recipe match.", this);
                // Re-check recipe match in case inputs changed between enable and click
                CheckForRecipeMatch();
                if (currentState != CraftingState.Inputting || currentMatchedRecipe == null || maxCraftableBatches <= 0)
                {
                    Debug.LogWarning($"CraftingStation ({gameObject.name}): Craft button clicked again with invalid state/recipe after re-check. Aborting transition.", this);
                    return; // Abort if still not ready
                }
            }

            // Proceed with starting the crafting state, which will trigger the minigame
            SetState(CraftingState.Crafting);
            // The minigame will be started by HandleStateEntry(CraftingState.Crafting)
        }

        /// <summary>
        /// Performs the actual item consumption and creation for the calculated batches.
        /// Delegates the core logic to CraftingExecutor.
        /// Called by HandleCraftingMinigameCompleted after the minigame is finished and successful.
        /// --- MODIFIED: Made private again and takes recipe/batches as parameters ---
        /// </summary>
        private void CompleteCraft(CraftingRecipe recipeToCraft, int batchesToCraft)
        {
             // Use the stored members, which are guaranteed to be valid if this method is reached
             // based on the logic in HandleCraftingMinigameCompleted.
            if (currentMatchedRecipe == null || maxCraftableBatches <= 0) // Use members here
            {
                Debug.LogError($"CraftingStation ({gameObject.name}): CompleteCraft called with invalid stored recipe or batches! Cannot execute craft.", this);
                return;
            }

            // --- Delegate craft execution to the external helper ---
            bool executionSuccess = CraftingExecutor.ExecuteCraft( // Assuming CraftingExecutor exists
                currentMatchedRecipe,
                maxCraftableBatches,
                primaryInputInventory,
                secondaryInputInventory,
                outputInventory);

            // --- Handle execution result ---
            if (!executionSuccess)
            {
                Debug.LogError($"CraftingStation ({gameObject.name}): CRITICAL ERROR: Craft execution failed AFTER minigame completion! Input consumption may be inconsistent. Check CraftingExecutor logs.");
            }
            else
            {
                Debug.Log($"CraftingStation ({gameObject.name}): Craft execution successful.");
            }

            // State transition to Outputting is handled by HandleCraftingMinigameCompleted AFTER calling this method.
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
                 // Ensure index is within array bounds and slot is not null
                 if (i < outputItems.Length && outputItems[i] != null && outputItems[i].quantity > 0)
                 {
                     return false; // Found an item in a physical slot
                 }
             }
             return true; // No items found in physical slots
        }
    }
}