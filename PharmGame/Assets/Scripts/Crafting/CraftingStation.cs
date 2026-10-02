// Systems/Inventory/CraftingStation.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System;
using Systems.CraftingMinigames;
using Systems.GameStates; // Needed for MenuManager
using Systems.Crafting; // Needed for DrugRecipeMappingSO, CraftingItemModifier
using Systems.Player; // Needed for PlayerPrescriptionTracker
using Game.Prescriptions; // Needed for PrescriptionOrder


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
    /// Now references DrugRecipeMappingSO for prescription crafting.
    /// --- MODIFIED: Uses recipe name string from DrugRecipeMappingSO. ---
    /// --- MODIFIED: Added logic to reset from Outputting to Inputting if output is empty on UI open. ---
    /// --- MODIFIED: Updated CheckForRecipeMatch to use separate primary/secondary input lists. ---
    /// --- MODIFIED: Updated OnCraftButtonClicked and CompleteCraft to handle prescription units. ---
    /// --- MODIFIED: Removed redundant IsOutputInventoryEmpty check in HandleStateEntry(Outputting). ---
    /// --- MODIFIED: Receives actual crafted amount from CraftingMinigameManager. ---
    /// --- MODIFIED: Passes actual crafted amount to CraftingExecutor. ---
    /// --- MODIFIED: Removed state transition from HandleOutputInventoryChange. ---
    /// --- MODIFIED: Added state transition logic to CloseCraftingUI. ---
    /// --- MODIFIED: Added field to store patient name for crafted item. --- // <-- ADDED NOTE
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
        private CraftingUIHandler uiHandler;

        [Header("Crafting Minigame")]
        [Tooltip("The CraftingMinigameManager component responsible for running minigames.")]
        [SerializeField] private CraftingMinigameManager craftingMinigameManager;

        // --- NEW: Prescription Crafting Mapping ---
        [Header("Prescription Crafting")]
        [Tooltip("Reference to the ScriptableObject containing mappings from prescription drug names to crafting recipes and output items.")]
        [SerializeField] private DrugRecipeMappingSO drugRecipeMapping; // <-- Added reference
        // --- END NEW ---


        [Header("State")]
        [Tooltip("The current state of the crafting station.")]
        [SerializeField] private CraftingState currentState = CraftingState.Inputting;

        private CraftingRecipe currentMatchedRecipe;
        private int maxCraftableBatches = 0;
        private int totalPrescriptionUnits = 0; // <-- Store total units from prescription order (needed for delivery validation later)
        private int actualCraftedAmount = 0; // <-- NEW: Store the actual amount crafted by the minigame

        // --- NEW FIELD: Patient Name for Crafted Item --- // <-- ADDED
        /// <summary>
        /// Stores the patient name from the active prescription order when entering the Crafting state.
        /// Used to tag the crafted item instance.
        /// </summary>
        private string patientNameForCraftedItem; // <-- ADDED FIELD
        // --- END NEW FIELD ---


        private void Awake()
        {
            // --- MODIFIED: Added drugRecipeMapping to essential references check ---
            if (craftingRecipes == null || primaryInputInventory == null || outputInventory == null || craftingUIRoot == null || drugRecipeMapping == null)
            {
                Debug.LogError($"CraftingStation ({gameObject.name}): Missing essential references in the Inspector! (CraftingRecipes, PrimaryInputInventory, OutputInventory, CraftingUIRoot, DrugRecipeMapping)", this);
                enabled = false;
                return;
            }
            // --- END MODIFIED ---

            uiHandler = craftingUIRoot.GetComponent<CraftingUIHandler>();
            if (uiHandler == null)
            {
                Debug.LogWarning($"CraftingStation ({gameObject.name}): Crafting UI Root GameObject '{craftingUIRoot.name}' is missing the CraftingUIHandler component! UI visuals may not function correctly.", this);
            }

             if (craftingMinigameManager == null)
             {
                 Debug.LogError($"CraftingStation ({gameObject.name}): CraftingMinigameManager reference is not assigned in the inspector! Crafting minigames will not function.", this);
                 // Do NOT disable the station, maybe it can still be used for simple non-minigame recipes if implemented?
             }

            craftingUIRoot.SetActive(false);

            SetupInventoryListeners();
        }

        private void OnEnable()
        {
            if (craftingMinigameManager != null)
            {
                 // Subscribe using the new signature
                 craftingMinigameManager.OnMinigameSessionCompleted -= HandleCraftingMinigameCompleted; // Remove before adding
                 craftingMinigameManager.OnMinigameSessionCompleted += HandleCraftingMinigameCompleted; // <-- MODIFIED Subscription
                 Debug.Log($"CraftingStation ({gameObject.name}): Subscribed to CraftingMinigameManager completion event.", this);
            }
             // Resubscribe to inventory events in OnEnable
             SetupInventoryListeners();
        }

        private void OnDisable()
        {
            if (craftingMinigameManager != null)
            {
                // Unsubscribe using the new signature
                craftingMinigameManager.OnMinigameSessionCompleted -= HandleCraftingMinigameCompleted; // <-- MODIFIED Unsubscription
                Debug.Log($"CraftingStation ({gameObject.name}): Unsubscribed from CraftingMinigameManager completion event.", this);
            }

            // Unsubscribe from inventory events in OnDisable
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
        /// Resumes the state the station was in when the UI was closed,
        /// UNLESS it was in Outputting state and the output inventory is now empty,
        /// in which case it transitions to Inputting.
        /// </summary>
        public void OpenCraftingUI()
        {
            Debug.Log($"CraftingStation ({gameObject.name}): Opening Crafting UI. Current state was: {currentState}.", this);

            CraftingState stateToEnter = currentState; // Assume we resume the old state

            // --- NEW LOGIC: Check if we were in Outputting state and the output is now empty ---
            // This logic correctly handles the case where the player cleared the output *before* closing the UI.
            if (currentState == CraftingState.Outputting)
            {
                if (IsOutputInventoryEmpty())
                {
                    Debug.Log($"CraftingStation ({gameObject.name}): Output inventory is empty upon opening UI. Forcing state to Inputting.", this);
                    stateToEnter = CraftingState.Inputting; // Force transition to Inputting
                }
                else
                {
                     Debug.Log($"CraftingStation ({gameObject.name}): Output inventory is NOT empty upon opening UI. Remaining in Outputting state.", this);
                }
            }
            // --- END NEW LOGIC ---

            craftingUIRoot.SetActive(true);
            // Link UI Handler if it exists
            if (uiHandler != null) uiHandler.LinkCraftingStation(this);

            // SetState with the determined state (either the old one or forced Inputting)
            SetState(stateToEnter);
        }

        /// <summary>
        /// Called to close the crafting UI. The station retains its state,
        /// UNLESS it is in the Outputting state and the output inventory is empty,
        /// in which case it transitions to Inputting.
        /// --- MODIFIED: Added logic to check output inventory and transition state if empty. ---
        /// </summary>
        public void CloseCraftingUI()
        {
            Debug.Log($"CraftingStation ({gameObject.name}): Closing Crafting UI. Current state: {currentState}.", this);

            // --- NEW LOGIC: Check if in Outputting state and output is empty upon closing ---
            // This is the new trigger for transitioning from Outputting to Inputting.
            if (currentState == CraftingState.Outputting)
            {
                if (IsOutputInventoryEmpty())
                {
                    Debug.Log($"CraftingStation ({gameObject.name}): Output inventory is empty upon closing UI. Transitioning to Inputting.", this);
                    SetState(CraftingState.Inputting); // Transition to Inputting state
                }
                else
                {
                     Debug.Log($"CraftingStation ({gameObject.name}): Output inventory is NOT empty upon closing UI. Remaining in Outputting state.", this);
                     // State remains Outputting, so when UI is reopened, it will check again in OpenCraftingUI
                }
            }
            // --- END NEW LOGIC ---

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

            HandleStateExit(previousState);
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
                    Debug.Log($"CraftingStation ({gameObject.name}): Exiting Outputting state.", this);
                    // Clean up after exiting Outputting state (e.g., after items are taken)
                    // No specific cleanup needed here, output inventory handles itself.
                    // The transition back to Inputting is now handled by CloseCraftingUI if output is empty.
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
                    CheckForRecipeMatch(); // Trigger recipe check whenever entering Inputting
                    // Clear stored craft data when returning to Inputting
                    currentMatchedRecipe = null;
                    maxCraftableBatches = 0;
                    totalPrescriptionUnits = 0;
                    actualCraftedAmount = 0; // Clear actual amount
                    patientNameForCraftedItem = null; // <-- CLEAR PATIENT NAME TAG // <-- ADDED
                    break;
                case CraftingState.Crafting:
                    Debug.Log($"CraftingStation ({gameObject.name}): Entering Crafting state. Starting minigame via manager.", this);
                    if (craftingMinigameManager != null)
                    {
                        // Prepare parameters for the minigame, including prescription data if applicable
                        Dictionary<string, object> minigameParameters = new Dictionary<string, object>();

                        // Check if the player has an active prescription order
                        Systems.Player.PlayerPrescriptionTracker playerTracker = null;
                        GameObject playerGO = GameObject.FindGameObjectWithTag("Player"); // Assuming player has the "Player" tag
                        if (playerGO != null)
                        {
                            playerTracker = playerGO.GetComponent<Systems.Player.PlayerPrescriptionTracker>();
                        }

                        // Calculate and store totalPrescriptionUnits if there's an active order
                        totalPrescriptionUnits = 0; // Reset before checking
                        patientNameForCraftedItem = null; // <-- RESET PATIENT NAME TAG BEFORE CAPTURING // <-- ADDED

                        if (playerTracker != null && playerTracker.ActivePrescriptionOrder.HasValue)
                        {
                            Game.Prescriptions.PrescriptionOrder activeOrder = playerTracker.ActivePrescriptionOrder.Value;
                            totalPrescriptionUnits = activeOrder.dosePerDay * activeOrder.lengthOfTreatmentDays;
                            patientNameForCraftedItem = activeOrder.patientName; // <-- CAPTURE PATIENT NAME // <-- ADDED
                            Debug.Log($"CraftingStation ({gameObject.name}): Player has active prescription order for '{activeOrder.prescribedDrug}' (Patient: '{patientNameForCraftedItem}'). Calculated total prescription units: {totalPrescriptionUnits}. Preparing minigame parameters.", this); // <-- MODIFIED LOG

                            // Add the target pill count (total units) to the parameters dictionary for the minigame
                            minigameParameters["TargetPillCount"] = totalPrescriptionUnits; // Use a consistent key

                            // Add other relevant order data if needed by minigame (e.g., drug name)
                            minigameParameters["PrescribedDrugName"] = activeOrder.prescribedDrug; // Example

                            // Note: The recipe itself (currentMatchedRecipe) is already determined by CheckForRecipeMatch
                            // and validated against the order in OnCraftButtonClicked before reaching this point.
                        }
                        else
                        {
                            Debug.Log($"CraftingStation ({gameObject.name}): Player does not have an active prescription order. Starting minigame with default parameters (totalPrescriptionUnits = 0, patientNameForCraftedItem = null).", this); // <-- MODIFIED LOG
                            // If no active order, the minigame will use its default logic (e.g., random target count for pills)
                            // No specific parameters related to prescription are added here, totalPrescriptionUnits remains 0.
                            // patientNameForCraftedItem remains null.
                        }


                        // Start the appropriate minigame based on the matched recipe
                        // Pass the prepared parameters dictionary
                        bool started = craftingMinigameManager.StartCraftingMinigame(currentMatchedRecipe, maxCraftableBatches, minigameParameters);

                        if (!started)
                        {
                            Debug.LogError("CraftingStation: Failed to start crafting minigame. Returning to Inputting.", this);
                            // If minigame failed to start, immediately return to Inputting state
                            SetState(CraftingState.Inputting);
                            // Clearing recipe/batches/units/patient name is handled by entering Inputting state.
                        }
                    }
                    else
                    {
                        Debug.LogError("CraftingStation: CraftingMinigameManager reference is null. Cannot start crafting minigame. Returning to Inputting.", this);
                        SetState(CraftingState.Inputting);
                    }
                    break;
                case CraftingState.Outputting:
                    Debug.Log($"CraftingStation ({gameObject.name}): Crafting complete. Item(s) available in output.", this);
                    // The check for empty output on ENTERING Outputting state is only needed when OPENING the UI.
                    // The transition back to Inputting when the output is cleared is now handled by CloseCraftingUI.
                    break;
            }
        }

        /// <summary>
        /// Handles the completion event from the crafting minigame manager.
        /// Proceeds with item consumption and production if successful, then transitions to Outputting.
        /// Transitions back to Inputting if the minigame failed or was aborted.
        /// --- MODIFIED: Receives actualCraftedAmount parameter. ---
        /// </summary>
        /// <param name="minigameWasSuccessful">Boolean: true if minigame was successful, false if it failed or was aborted.</param>
        /// <param name="actualCraftedAmount">The actual amount crafted by the minigame.</param>
        private void HandleCraftingMinigameCompleted(bool minigameWasSuccessful, int actualCraftedAmount) // <-- MODIFIED Signature
        {
            Debug.Log($"CraftingStation ({gameObject.name}): Received Crafting Minigame Completed event. Outcome: {(minigameWasSuccessful ? "Success" : "Failure/Aborted")}, Actual Amount: {actualCraftedAmount}.", this);

            // Store the actual crafted amount received from the minigame
            this.actualCraftedAmount = actualCraftedAmount; // <-- Store the actual amount

            // Handle success or failure/abort
            if (minigameWasSuccessful)
            {
                Debug.Log($"CraftingStation: Crafting minigame reported success. Proceeding with craft execution.", this);
                // Proceed with the actual item consumption and production
                // Use the stored currentMatchedRecipe, maxCraftableBatches, totalPrescriptionUnits, actualCraftedAmount, AND patientNameForCraftedItem
                CompleteCraft(currentMatchedRecipe, maxCraftableBatches, totalPrescriptionUnits, this.actualCraftedAmount, patientNameForCraftedItem); // <-- Pass actualCraftedAmount AND patientNameForCraftedItem // <-- MODIFIED CALL
                // After completing the craft (items consumed/produced), transition to the Outputting state
                SetState(CraftingState.Outputting);
            }
            else
            {
                Debug.LogWarning($"CraftingStation ({gameObject.name}): Crafting minigame reported failure or was aborted. Not consuming items or producing output. Returning to Inputting state.", this);
                // If the minigame failed or was aborted, return to the input state without completing the craft
                SetState(CraftingState.Inputting);
            }

            // Clearing the matched recipe, batches, units, and patient name is handled by entering Inputting state.
            // The actualCraftedAmount is also cleared when entering Inputting.
        }

        // --- Inventory Event Handling ---

        private void SetupInventoryListeners()
        {
            // Ensure we don't double-subscribe by removing first
            if (primaryInputInventory?.InventoryState != null)
            {
                primaryInputInventory.InventoryState.AnyValueChanged -= HandlePrimaryInputChange; // Remove before adding
                primaryInputInventory.InventoryState.AnyValueChanged += HandlePrimaryInputChange;
                Debug.Log($"CraftingStation ({gameObject.name}): Subscribed to Primary Input Inventory changes.", this);
            }
            else
            {
                Debug.LogError($"CraftingStation ({gameObject.name}): Primary Input Inventory or its ObservableArray is null. Cannot subscribe to changes.", this);
            }

            if (secondaryInputInventory?.InventoryState != null)
            {
                secondaryInputInventory.InventoryState.AnyValueChanged -= HandleSecondaryInputChange; // Remove before adding
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
                outputInventory.InventoryState.AnyValueChanged -= HandleOutputInventoryChange; // Remove before adding
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
        /// Handles changes in the output inventory.
        /// --- MODIFIED: Removed state transition logic. The transition now happens only when the UI is closed. ---
        /// </summary>
        private void HandleOutputInventoryChange(ArrayChangeInfo<Item> changeInfo)
        {
             // We only care about changes when in the Outputting state
             if (currentState != CraftingState.Outputting)
             {
                 return;
             }

             // Check if the *physical* slots are all empty
             if (IsOutputInventoryEmpty())
             {
                 Debug.Log($"CraftingStation ({gameObject.name}): Output inventory is now empty. State will transition back to Inputting when UI is closed.", this);
                 // REMOVED: SetState(CraftingState.Inputting); // <-- THIS LINE IS REMOVED IN STEP 1
             }
             else
             {
                 // Log if a change occurred but the inventory is still not empty
                 // Debug.Log($"CraftingStation ({gameObject.name}): Output inventory changed, but physical slots still contain items. Remaining in Outputting state."); // Too noisy
             }
        }

        // --- Recipe Matching Logic ---
        private void CheckForRecipeMatch()
        {
            // Only check in the Inputting state
            if (currentState != CraftingState.Inputting)
            {
                // Clearing is handled by HandleStateEntry(Inputting)
                if (uiHandler != null) uiHandler.SetCraftButtonInteractable(false);
                return;
            }

            if (craftingRecipes == null || primaryInputInventory?.Combiner?.InventoryState == null || outputInventory == null || drugRecipeMapping == null) // Added drugRecipeMapping check
            {
                Debug.LogError($"CraftingStation ({gameObject.name}): Cannot check for recipe match, missing essential references.", this);
                // Clearing is handled by HandleStateEntry(Inputting)
                if (uiHandler != null) uiHandler.SetCraftButtonInteractable(false);
                return;
            }

            // Collect items from primary and secondary inventories separately
            List<Item> primaryInputItems = new List<Item>();
            if (primaryInputInventory?.Combiner?.InventoryState != null)
            {
                primaryInputItems.AddRange(primaryInputInventory.Combiner.InventoryState.GetCurrentArrayState()
                                            .Take(primaryInputInventory.Combiner.PhysicalSlotCount)
                                            .Where(item => item != null && item.details != null)); // Include items with quantity 0 for health check
            }

            List<Item> secondaryInputItems = new List<Item>();
            if (secondaryInputInventory?.Combiner?.InventoryState != null)
            {
                secondaryInputItems.AddRange(secondaryInputInventory.Combiner.InventoryState.GetCurrentArrayState()
                                            .Take(secondaryInputInventory.Combiner.PhysicalSlotCount)
                                            .Where(item => item != null && item.details != null && item.quantity > 0)); // Only include items with quantity > 0 for secondary
            }

            // Delegate recipe matching to the external helper, passing separate lists
            RecipeMatchResult matchResult = CraftingMatcher.FindRecipeMatch(craftingRecipes, primaryInputItems, secondaryInputItems); // <-- Pass separate lists

            currentMatchedRecipe = matchResult.MatchedRecipe;
            maxCraftableBatches = matchResult.MaxCraftableBatches;
            // totalPrescriptionUnits is NOT set here, it's set in OnCraftButtonClicked just before crafting starts.
            // actualCraftedAmount is NOT set here, it's set in HandleCraftingMinigameCompleted.
            // patientNameForCraftedItem is NOT set here, it's set in HandleStateEntry(Crafting).


            // If there's an active prescription order, validate the matched recipe against it
            Systems.Player.PlayerPrescriptionTracker playerTracker = null;
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player"); // Assuming player has the "Player" tag
            if (playerGO != null)
            {
                playerTracker = playerGO.GetComponent<Systems.Player.PlayerPrescriptionTracker>();
            }

            bool isCorrectPrescriptionRecipe = false;
            if (playerTracker != null && playerTracker.ActivePrescriptionOrder.HasValue)
            {
                 Game.Prescriptions.PrescriptionOrder activeOrder = playerTracker.ActivePrescriptionOrder.Value;
                 // Get required recipe by name using the updated mapping method
                 CraftingRecipe requiredRecipe = drugRecipeMapping.GetCraftingRecipeForDrug(activeOrder.prescribedDrug);

                 if (matchResult.HasMatch && matchResult.MatchedRecipe != requiredRecipe)
                 {
                      // Found a match, but it's NOT the recipe required by the active prescription order
                      Debug.LogWarning($"CraftingStation ({gameObject.name}): Recipe matched ({matchResult.MatchedRecipe.recipeName}) but it does NOT match the recipe required for the active prescription order ('{activeOrder.prescribedDrug}' requires '{requiredRecipe?.recipeName ?? "NULL"}'). Craft button will be enabled, but craft will be blocked on click.", this);
                      // The button is enabled because *a* craft is possible, but the *specific* craft for the current objective is not.
                      // The blocking logic is in OnCraftButtonClicked.
                 }
                 else if (matchResult.HasMatch && matchResult.MatchedRecipe == requiredRecipe)
                 {
                      // Found a match, AND it's the correct recipe for the active prescription order.
                      Debug.Log($"CraftingStation ({gameObject.name}): Recipe matched ({matchResult.MatchedRecipe.recipeName}) and it IS the recipe required for the active prescription order ('{activeOrder.prescribedDrug}'). Craft button will be enabled.", this);
                      isCorrectPrescriptionRecipe = true; // Flag this as the correct objective recipe
                 }
                 else if (!matchResult.HasMatch && requiredRecipe != null)
                 {
                     // No recipe matched, but there's an active prescription order with a required recipe.
                     Debug.Log($"CraftingStation ({gameObject.name}): No recipe matched, but player has active prescription order for '{activeOrder.prescribedDrug}' which requires recipe '{requiredRecipe.recipeName}'. Craft button will be disabled.", this);
                 }
            }


            // Update Craft Button based on result
            // The button is enabled if ANY recipe matches with > 0 batches.
            // The actual crafting (and minigame) is blocked in OnCraftButtonClicked if it's not the prescription recipe.
            if (uiHandler != null)
            {
                uiHandler.SetCraftButtonInteractable(matchResult.HasMatch);

                if (matchResult.HasMatch)
                {
                    // Log moved inside the NEW block above for clarity when prescription is active
                    // If no prescription is active, or if the matched recipe is the correct one, log the positive match.
                    if (!(playerTracker != null && playerTracker.ActivePrescriptionOrder.HasValue && !isCorrectPrescriptionRecipe))
                    {
                         Debug.Log($"CraftingStation ({gameObject.name}): Recipe matched: {currentMatchedRecipe.recipeName}! Can craft {maxCraftableBatches} batch(es).", this);
                    }
                } else {
                     // If no match, log that crafting is not possible.
                     Debug.Log($"CraftingStation ({gameObject.name}): No recipe matched with current input items.", this);
                }
            }
        }

        /// <summary>
        /// Called when the Craft button is clicked. Initiates the crafting process
        /// by transitioning to the Crafting state, which will start the minigame.
        /// Includes validation against active prescription order if present, blocking craft if it's not the required recipe.
        /// </summary>
        private void OnCraftButtonClicked()
        {
            Debug.Log($"CraftingStation ({gameObject.name}): Craft button clicked. Attempting to start craft process.", this);

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

            // Check if there's an active prescription order and if the matched recipe is the correct one
            Systems.Player.PlayerPrescriptionTracker playerTracker = null;
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                playerTracker = playerGO.GetComponent<Systems.Player.PlayerPrescriptionTracker>();
            }

            // Calculate totalPrescriptionUnits here, just before starting the craft
            totalPrescriptionUnits = 0; // Reset before calculating
            patientNameForCraftedItem = null; 

            if (playerTracker != null && playerTracker.ActivePrescriptionOrder.HasValue)
            {
                 Game.Prescriptions.PrescriptionOrder activeOrder = playerTracker.ActivePrescriptionOrder.Value;
                 CraftingRecipe requiredRecipe = drugRecipeMapping.GetCraftingRecipeForDrug(activeOrder.prescribedDrug);

                 if (currentMatchedRecipe != requiredRecipe)
                 {
                      // Recipe matched, but it's NOT the one required by the active prescription order.
                      Debug.LogWarning($"CraftingStation ({gameObject.name}): Cannot craft '{currentMatchedRecipe.recipeName}'. Player has active prescription order for '{activeOrder.prescribedDrug}' which requires recipe '{requiredRecipe?.recipeName ?? "NULL"}'. Craft blocked.", this);
                      // Optionally provide UI feedback here (e.g., a message "This is not the correct drug")
                      return; // Abort the craft process
                 }
                 else
                 {
                      Debug.Log($"CraftingStation ({gameObject.name}): Matched recipe '{currentMatchedRecipe.recipeName}' is the correct recipe for the active prescription order. Proceeding.", this);
                      totalPrescriptionUnits = activeOrder.dosePerDay * activeOrder.lengthOfTreatmentDays; // Calculate units
                      patientNameForCraftedItem = activeOrder.patientName; // <-- CAPTURE PATIENT NAME
                      Debug.Log($"CraftingStation ({gameObject.name}): Calculated total prescription units for order: {totalPrescriptionUnits}. Captured patient name: '{patientNameForCraftedItem}'.", this);

                      // Additional check for prescription crafts - ensure primary input has enough health
                      // Find the primary input requirement in the matched recipe
                      RecipeInput primaryInputRequirement = currentMatchedRecipe.inputs.FirstOrDefault(input => input.isPrimaryInput);

                      if (primaryInputRequirement == null || primaryInputRequirement.amountType != AmountType.Health)
                      {
                           Debug.LogError($"CraftingStation ({gameObject.name}): Matched recipe '{currentMatchedRecipe.recipeName}' is expected to be a prescription recipe but has no primary health input defined! Aborting craft.", this);
                           return; // Abort if recipe structure is invalid for prescription craft
                      }

                      // Find the actual item instance in the primary input inventory (assuming first physical slot)
                      Item primaryInputItem = primaryInputInventory?.Combiner?.InventoryState?.GetCurrentArrayState()
                                            .Take(primaryInputInventory.Combiner.PhysicalSlotCount)
                                            .FirstOrDefault(item => item != null && item.details == primaryInputRequirement.itemDetails); // Use FirstOrDefault to get the instance


                      if (primaryInputItem == null)
                      {
                           // This should have been caught by CheckForRecipeMatch, but defensive check.
                           Debug.LogError($"CraftingStation ({gameObject.name}): Primary input item '{primaryInputRequirement.itemDetails?.Name ?? "NULL"}' required for prescription craft not found in primary input slots! Aborting craft.", this);
                           return; // Abort if primary item is missing
                      }

                      // Check if the primary input item has enough health for the *required* total units from the order
                      // This check is based on the *order requirement*, not the minigame outcome, as the player needs enough stock to *attempt* the full order.
                      if (primaryInputItem.health < totalPrescriptionUnits)
                      {
                           Debug.LogWarning($"CraftingStation ({gameObject.name}): Primary input item '{primaryInputItem.details.Name}' (Health: {primaryInputItem.health}) does not have enough health ({totalPrescriptionUnits} required) for this prescription order. Aborting craft.", primaryInputItem.details);
                           // Optionally provide UI feedback "Not enough stock"
                           return; // Abort if not enough health
                      }
                       Debug.Log($"CraftingStation ({gameObject.name}): Primary input item '{primaryInputItem.details.Name}' has sufficient health ({primaryInputItem.health} >= {totalPrescriptionUnits}). Proceeding.", primaryInputItem.details);
                 }
            }
             else
             {
                 // No active prescription order, allow crafting any matched recipe.
                 Debug.Log($"CraftingStation ({gameObject.name}): No active prescription order. Allowing craft of matched recipe '{currentMatchedRecipe.recipeName}'.", this);
                 totalPrescriptionUnits = 0; // Ensure units are 0 for non-prescription crafts
                 patientNameForCraftedItem = null; // Ensure tag is null for non-prescription crafts
             }


            // Proceed with starting the crafting state, which will trigger the minigame
            SetState(CraftingState.Crafting);
        }

        /// <summary>
        /// Performs the actual item consumption and creation for the calculated batches.
        /// Delegates the core logic to CraftingExecutor.
        /// Called by HandleCraftingMinigameCompleted after the minigame is finished and successful.
        /// --- MODIFIED: Added totalPrescriptionUnits and actualCraftedAmount parameters. ---
        /// --- MODIFIED: Added patientNameTag parameter. --- // <-- ADDED NOTE
        /// </summary>
        private void CompleteCraft(CraftingRecipe recipeToCraft, int batchesToCraft, int totalPrescriptionUnits, int actualCraftedAmount, string patientNameTag) // <-- ADDED patientNameTag PARAM // <-- MODIFIED Signature
        {
             // Use the stored members, which are guaranteed to be valid if this method is reached
             // based on the logic in HandleCraftingMinigameCompleted.
            if (currentMatchedRecipe == null || maxCraftableBatches <= 0) // Use members here
            {
                Debug.LogError($"CraftingStation ({gameObject.name}): CompleteCraft called with invalid stored recipe or batches! Cannot execute craft.", this);
                return;
            }

            // Delegate craft execution to the external helper
            // Pass the totalPrescriptionUnits (for delivery validation later), the actualCraftedAmount (for consumption/production), AND the patientNameTag
            bool executionSuccess = CraftingExecutor.ExecuteCraft(
                currentMatchedRecipe,
                maxCraftableBatches,
                primaryInputInventory,
                secondaryInputInventory,
                outputInventory,
                totalPrescriptionUnits, // Pass the required units from the order
                actualCraftedAmount, // Pass the actual amount crafted
                patientNameTag); // <-- Pass the patient name tag // <-- MODIFIED CALL

            // Handle execution result
            if (!executionSuccess)
            {
                Debug.LogError($"CraftingStation ({gameObject.name}): CRITICAL ERROR: Craft execution failed AFTER minigame completion! Input consumption may be inconsistent. Check CraftingExecutor logs.");
                // TODO: Consider more robust error handling here - maybe try to revert consumed items?
                // For now, we proceed to Outputting state anyway, as inputs *might* have been consumed partially.
            }
            else
            {
                Debug.Log($"CraftingStation ({gameObject.name}): Craft execution successful.");
            }
             // The transition to Outputting is handled in HandleCraftingMinigameCompleted regardless of this executionSuccess flag.
        }

        /// <summary>
        /// Checks if the output inventory is completely empty of physical items.
        /// </summary>
        private bool IsOutputInventoryEmpty()
        {
             if (outputInventory?.Combiner?.InventoryState == null)
             {
                 Debug.LogWarning($"CraftingStation ({gameObject.name}): Cannot check if output inventory is empty, references are null.", this);
                 // Assume empty if we can't check? Or assume not? Let's assume empty if references are broken.
                 return true;
             }

             // Check physical slots only. The ghost slot is not relevant here.
             Item[] outputItems = outputInventory.Combiner.InventoryState.GetCurrentArrayState();

             // Iterate only up to the number of physical slots
             for (int i = 0; i < outputInventory.Combiner.PhysicalSlotCount; i++)
             {
                 // Ensure index is within array bounds and slot is not null/empty
                 // Also check quantity > 0 or health > 0 for non-stackable durable items
                 if (i < outputItems.Length && outputItems[i] != null && outputItems[i].details != null)
                 {
                     if (outputItems[i].details.maxStack > 1)
                     {
                         if (outputItems[i].quantity > 0) return false; // Stackable with quantity
                     }
                     else // Non-stackable
                     {
                         if (outputItems[i].details.maxHealth > 0)
                         {
                             if (outputItems[i].health > 0) return false; // Durable with health
                         }
                         else
                         {
                             if (outputItems[i].quantity > 0) return false; // Non-durable non-stackable with quantity (should be 1)
                         }
                     }
                 }
             }
             // If the loop finishes without finding any items in physical slots with quantity/health > 0
             return true;
        }
    }
}