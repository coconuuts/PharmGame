// --- START OF FILE CraftingMinigameManager.cs ---

// Systems/CraftingMinigames/CraftingMinigameManager.cs
using UnityEngine;
using InventoryClass = Systems.Inventory.Inventory;
using Systems.Inventory;
using System.Collections.Generic;
using System;
using Systems.GameStates;
using Systems.Interaction; // Needed for StartCraftingMinigameResponse


namespace Systems.CraftingMinigames // Use the same namespace
{
    /// <summary>
    /// Manages the activation and orchestration of specific crafting minigames.
    /// Listens for requests from the CraftingStation to start a minigame.
    /// Handles cleanup and communicates minigame outcome (success/failure/abort) back to the station.
    /// </summary>
    public class CraftingMinigameManager : MonoBehaviour
    {
        [System.Serializable]
        public struct CraftingMinigameConfig
        {
            [Tooltip("The internal recipe name this minigame is for (e.g., 'PillRecipe').")]
            public string recipeName;
            [Tooltip("The GameObject prefab containing the CraftingMinigameBase script for this recipe.")]
            public GameObject minigamePrefab;
        }

        [Header("Minigame Configurations")]
        [Tooltip("List of configurations linking recipe names to minigame prefabs.")]
        [SerializeField] private List<CraftingMinigameConfig> minigameConfigs;

        private Dictionary<string, GameObject> minigamePrefabMap;
        private CraftingMinigameBase currentActiveMinigame;

        // Event to notify the CraftingStation when the minigame is completed or aborted
        /// <summary>
        /// Event triggered when a crafting minigame session is completed or aborted.
        /// The object parameter is a boolean: true for success, false for failure/abort.
        /// </summary>
        public event Action<object> OnMinigameSessionCompleted;


        private void Awake()
        {
            minigamePrefabMap = new Dictionary<string, GameObject>();
            if (minigameConfigs != null)
            {
                foreach (var config in minigameConfigs)
                {
                    if (!string.IsNullOrEmpty(config.recipeName) && config.minigamePrefab != null)
                    {
                         if (!minigamePrefabMap.ContainsKey(config.recipeName))
                         {
                             minigamePrefabMap.Add(config.recipeName, config.minigamePrefab);
                             // Ensure the prefab is initially inactive (though prefabs should usually be)
                             if (config.minigamePrefab.activeSelf)
                             {
                                  Debug.LogWarning($"CraftingMinigameManager: Minigame Prefab for '{config.recipeName}' is active in the project! Prefabs should typically be inactive.", config.minigamePrefab);
                             }
                         }
                         else
                         {
                              Debug.LogWarning($"CraftingMinigameManager: Duplicate minigame config found for recipe name '{config.recipeName}'. Using the first one.", this);
                         }
                    }
                    else
                    {
                        Debug.LogWarning($"CraftingMinigameManager: Minigame config has empty recipe name or null prefab assigned.", this);
                    }
                }
            }
            else
            {
                Debug.LogError("CraftingMinigameManager: Minigame Configs list is not assigned in the Inspector!", this);
            }
        }

        private void OnDestroy()
        {
            // Ensure the event is cleared on destruction
            OnMinigameSessionCompleted = null;

            // Clean up any active minigame if manager is destroyed
            if (currentActiveMinigame != null)
            {
                 currentActiveMinigame.OnCraftingMinigameCompleted -= HandleActiveMinigameCompleted;
                 // Call EndMinigame with true to signal cleanup due to external manager destruction
                 currentActiveMinigame.EndMinigame(true); // Call end for cleanup, mark as aborted due to destroy
                 if (currentActiveMinigame?.gameObject != null)
                 {
                      Destroy(currentActiveMinigame.gameObject);
                 }
                 currentActiveMinigame = null;
            }
        }


        /// <summary>
        /// Called by the CraftingStation to start a specific crafting minigame.
        /// --- MODIFIED: Added parameters dictionary ---
        /// </summary>
        /// <param name="recipe">The recipe being crafted.</param>
        /// <param name="batches">The number of batches.</param>
        /// <param name="parameters">A dictionary of additional parameters for minigame setup.</param>
        /// <returns>True if the minigame was successfully initiated, false otherwise.</returns>
        public bool StartCraftingMinigame(CraftingRecipe recipe, int batches, Dictionary<string, object> parameters)
        {
            if (currentActiveMinigame != null)
            {
                Debug.LogWarning($"CraftingMinigameManager: Attempted to start crafting minigame for '{recipe.recipeName}' but a minigame is already active. Ending previous one.", this);
                // Use the external EndCurrentMinigame method for clean shutdown of the previous one.
                // The previous minigame ending will result in a false outcome being sent.
                EndCurrentMinigame(true); // Mark the previous one as aborted
            }

            if (recipe == null || string.IsNullOrEmpty(recipe.recipeName))
            {
                Debug.LogError("CraftingMinigameManager: Cannot start crafting minigame, recipe or recipe name is null/empty.", this);
                return false;
            }

            if (minigamePrefabMap.TryGetValue(recipe.recipeName, out GameObject minigamePrefab))
            {
                GameObject minigameInstanceGO = Instantiate(minigamePrefab, transform);

                currentActiveMinigame = minigameInstanceGO.GetComponent<CraftingMinigameBase>();

                if (currentActiveMinigame != null)
                {
                    Debug.Log($"CraftingMinigameManager: Instantiated and setting up minigame for recipe '{recipe.recipeName}'.", this);

                    currentActiveMinigame.OnCraftingMinigameCompleted += HandleActiveMinigameCompleted;

                    // Activate the GameObject (should be off in prefab but ensure)
                    currentActiveMinigame.gameObject.SetActive(true);

                    // Setup and start the minigame logic (derived class assigns _initialCameraTarget/Duration here)
                    // --- MODIFIED: Pass the parameters dictionary ---
                    currentActiveMinigame.SetupAndStart(recipe, batches, parameters);
                    // --- END MODIFIED ---

                    // Notify MenuManager with the STARTING camera data from the minigame instance
                    if (MenuManager.Instance != null)
                    {
                        var response = new StartCraftingMinigameResponse(
                            currentActiveMinigame,
                            recipe,
                            batches,
                            currentActiveMinigame.InitialCameraTarget,
                            currentActiveMinigame.InitialCameraDuration
                        );
                        // MenuManager.SetState will trigger the SetCameraModeActionSO as an entry action
                        MenuManager.Instance.SetState(MenuManager.GameState.InMinigame, response);
                        Debug.Log($"CraftingMinigameManager: Notified MenuManager to enter InMinigame state with initial camera data.");
                    }
                    else
                    {
                        Debug.LogError("CraftingMinigameManager: MenuManager.Instance is null! Cannot set game state.");
                    }

                    return true; // Successfully initiated setup and state change
                }
                else
                {
                    Debug.LogError($"CraftingMinigameManager: Instantiated prefab for '{recipe.recipeName}' but could not find a CraftingMinigameBase component on it!", minigameInstanceGO);
                    Destroy(minigameInstanceGO); // Clean up instance
                    return false; // Failed to get component
                }
            }
            else
            {
                Debug.LogError($"CraftingMinigameManager: No minigame prefab configured for recipe name '{recipe.recipeName}'!", this);
                return false; // No config found
            }
        }
        // --- END MODIFIED ---

        /// <summary>
        /// Called when the currently active crafting minigame reports completion or abortion via its event.
        /// </summary>
        /// <param name="resultData">A boolean: true for success, false for failure/abort.</param>
        private void HandleActiveMinigameCompleted(object resultData)
        {
            bool minigameWasSuccessful = (resultData is bool success) ? success : false;
            Debug.Log($"CraftingMinigameManager: Received completion event from active minigame. Outcome: {(minigameWasSuccessful ? "Success" : "Failure/Aborted")}.", this);

            // Unsubscribe from the completed minigame's event and clean it up
            // Check if currentActiveMinigame is still valid before trying to unsubscribe/cleanup.
            // It might be null if EndCurrentMinigame was called externally just before this handler fired.
            if (currentActiveMinigame != null)
            {
                 currentActiveMinigame.OnCraftingMinigameCompleted -= HandleActiveMinigameCompleted;
                 // The minigame's EndMinigame method should have been called internally already
                 // by the minigame itself when it transitioned to the None state, or by an external EndCurrentMinigame call.
                 // We clear the reference and destroy the GameObject here.
                 if (currentActiveMinigame?.gameObject != null)
                 {
                    // Note: EndMinigame(bool) might have already triggered a cleanup routine in the minigame itself.
                    // Ensure destroying the GameObject doesn't cause errors if the minigame is already partially cleaned up.
                     Destroy(currentActiveMinigame.gameObject);
                 }
                 currentActiveMinigame = null; // Clear the reference
            }
            else
            {
                Debug.LogWarning("CraftingMinigameManager: Received completion event but currentActiveMinigame was null. Cleanup may have already occurred.", this);
            }


            // Notify the CraftingStation that the minigame session is complete, passing the result data
            OnMinigameSessionCompleted?.Invoke(minigameWasSuccessful); // Pass the boolean result


            // --- Conditional State Transition ---
            // Only transition back to InCrafting if the minigame was successful AND
            // if MenuManager is *still* in the InMinigame state.
            // If MenuManager is no longer InMinigame, it means an external state change (like Escape)
            // has already started the exit process, and we should not force a return to InCrafting.
            if (minigameWasSuccessful && MenuManager.Instance != null && MenuManager.Instance.currentState == MenuManager.GameState.InMinigame)
            {
                 Debug.Log($"CraftingMinigameManager: Minigame was successful and state is still InMinigame. Notifying MenuManager to return to InCrafting state.");
                 MenuManager.Instance.SetState(MenuManager.GameState.InCrafting, null); // Pass null response as it's an internal transition back
            }
            else
            {
                 // If not successful, or if MenuManager state is already changing,
                 // we let MenuManager continue its transition (likely to Playing).
                 Debug.Log($"CraftingMinigameManager: Minigame not successful OR state is already changing ({MenuManager.Instance?.currentState}). Not forcing return to InCrafting.");
            }
        }

        /// <summary>
        /// Ends the current active crafting minigame, if any.
        /// This is called internally or externally (e.g., by MenuManager during an emergency exit like Escape).
        /// </summary>
        /// <param name="wasAborted">True if the minigame is being ended prematurely (e.g., by Escape).</param>
        public void EndCurrentMinigame(bool wasAborted)
        {
             if (currentActiveMinigame != null)
             {
                  Debug.Log($"CraftingMinigameManager: Ending current active minigame session externally. Aborted: {wasAborted}.", this);
                  // Call the minigame's EndMinigame method, passing the abort status.
                  // This will trigger the minigame's internal cleanup and then its completion event.
                  currentActiveMinigame.EndMinigame(wasAborted);
                  // The HandleActiveMinigameCompleted method will receive the event, clear the reference, and destroy the GameObject.
             }
             else
             {
                 Debug.LogWarning("CraftingMinigameManager: Attempted to end minigame, but none was active.", this);
             }
        }
    }
}
// --- END OF FILE CraftingMinigameManager.cs ---