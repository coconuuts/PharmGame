using UnityEngine;
using Systems.GameStates; // Needed for MenuManager and GameState enum
using Systems.Interaction; // Needed for InteractionResponse types
using Systems.Inventory; // Needed for Inventory (for hover clear)

namespace Systems.UI
{
    /// <summary>
    /// Manages the visibility of various UI elements based on the game state.
    /// Listens to MenuManager state changes and uses InteractionResponse data.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance;

        [Header("Static UI References")]
        [Tooltip("The GameObject root for the general Player UI. Will find by tag if null.")]
        [SerializeField] private GameObject playerUIRoot;

        [Tooltip("The GameObject root for the Player Toolbar UI. Will find by tag if null.")]
        [SerializeField] private GameObject playerToolbarUIRoot;

        // Add a reference for the Pause Menu UI if you have one
        [Header("Specific UI References")]
        [Tooltip("The GameObject root for the Pause Menu UI.")]
        [SerializeField] private GameObject pauseMenuUIRoot;


        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Debug.LogWarning("UIManager: Duplicate instance found. Destroying this one.", this); Destroy(gameObject); return; }
            Debug.Log("UIManager: Awake completed.");

            // --- Find static UI roots by tag if not assigned ---
            if (playerUIRoot == null)
            {
                playerUIRoot = GameObject.FindGameObjectWithTag("PlayerUI"); // Use your actual tag
                if (playerUIRoot == null) Debug.LogError("UIManager: Player UI Root not assigned and GameObject with tag 'PlayerUI' not found!", this);
            }
            if (playerToolbarUIRoot == null)
            {
                playerToolbarUIRoot = GameObject.FindGameObjectWithTag("PlayerToolbar"); // Use your actual tag
                if (playerToolbarUIRoot == null) Debug.LogWarning("UIManager: Player Toolbar UI Root not assigned and GameObject with tag 'PlayerToolbar' not found. Toolbar visibility may not be managed.", this);
            }
            // ---------------------------------------------------

            // Ensure static and known UIs are off initially
            if (playerUIRoot != null) playerUIRoot.SetActive(false); // Start off, MenuManager.Start sets initial state which will turn it on
            if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(false); // Start off
            if (pauseMenuUIRoot != null) pauseMenuUIRoot.SetActive(false); // Start off

        }

        private void OnEnable()
        {
            // Subscribe to the state change event
            MenuManager.OnStateChanged += HandleGameStateChanged;
            Debug.Log("UIManager: Subscribed to MenuManager.OnStateChanged.");
        }

        private void OnDisable()
        {
            // Unsubscribe from the state change event
            MenuManager.OnStateChanged -= HandleGameStateChanged;
            Debug.Log("UIManager: Unsubscribed from MenuManager.OnStateChanged.");

            // Optional: Ensure UIs are hidden when this manager is disabled
            if (playerUIRoot != null) playerUIRoot.SetActive(false);
            if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(false);
            if (pauseMenuUIRoot != null) pauseMenuUIRoot.SetActive(false);
            // Dynamic UIs should ideally be handled by exiting their state
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

/// <summary>
/// Handles the visibility of various UI elements based on game state changes.
/// This method is the primary entry point for UI management.
/// MODIFIED: Removed access to StartMinigameResponse.MinigameUIRoot.
/// </summary>
/// <param name="newState">The new game state.</param>
/// <param name="oldState">The previous game state.</param>
/// <param name="response">The InteractionResponse that triggered the state change (can be null).</param> // ADDED PARAM
private void HandleGameStateChanged(MenuManager.GameState newState, MenuManager.GameState oldState, InteractionResponse response) // ADDED PARAM
{
     Debug.Log($"UIManager: Handling state change from {oldState} to {newState}.");

    // --- Handle UI Deactivation for the old state ---
    // This happens AFTER exit actions in MenuManager but BEFORE entry actions.
    // We rely on MenuManager's public getters which still hold old state data at this point.
    switch (oldState)
    {
        case MenuManager.GameState.InInventory:
            // Deactivate the Inventory UI root that was active (accessed via MenuManager getter)
            if (MenuManager.Instance != null && MenuManager.Instance.CurrentActiveUIRoot != null)
            {
                 MenuManager.Instance.CurrentActiveUIRoot.SetActive(false);
                 Debug.Log($"UIManager: Deactivated Inventory UI Root.");
                 // Call the MenuManager helper to clear hover highlights
                 if (MenuManager.Instance.CurrentOpenInventoryComponent != null)
                 {
                     MenuManager.Instance.ClearHoverHighlights(MenuManager.Instance.CurrentOpenInventoryComponent);
                 }
            }
            else Debug.LogWarning($"UIManager: Cannot deactivate Inventory UI when leaving {oldState} - no stored root or manager instance.");
            break;

        case MenuManager.GameState.InComputer:
            break;

        case MenuManager.GameState.InMinigame:
             // --- MODIFIED: Deactivation of Minigame UI Root is now handled by the MinigameManager deactivating its GameObject ---
             // The UIManager should likely hide static UI elements when entering InMinigame, and
             // those static elements should show up when exiting. The specific minigame UI is
             // activated/deactivated by the central MinigameManager activating/deactivating the
             // minigame's GameObject.
             // We no longer access response.MinigameUIRoot here.
             Debug.Log($"UIManager: Leaving InMinigame state. Specific minigame UI deactivation handled by MinigameManager.");

             // Ensure static UIs that might have been hidden are shown again when leaving minigame *if* returning to Playing
             if (newState == MenuManager.GameState.Playing)
             {
                 if (playerUIRoot != null) playerUIRoot.SetActive(true);
                 if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(true); // Decide if toolbar hides in minigame
                  Debug.Log("UIManager: Activated Player and Toolbar UIs (exiting Minigame to Playing).");
             }

            break;

        case MenuManager.GameState.InCrafting:
            // Tell the CraftingStation component to close its UI (accessed via MenuManager getter)
            if (MenuManager.Instance != null && MenuManager.Instance.CurrentCraftingStation != null)
            {
                 MenuManager.Instance.CurrentCraftingStation.CloseCraftingUI(); // Assumes this method exists
                 // Call the MenuManager helper to clear hover highlights for crafting inventories
                 CraftingStation currentCraftingStation = MenuManager.Instance.CurrentCraftingStation;
                 if (currentCraftingStation != null)
                 {
                     if (currentCraftingStation.primaryInputInventory != null) MenuManager.Instance.ClearHoverHighlights(currentCraftingStation.primaryInputInventory);
                     if (currentCraftingStation.secondaryInputInventory != null) MenuManager.Instance.ClearHoverHighlights(currentCraftingStation.secondaryInputInventory);
                     if (currentCraftingStation.outputInventory != null) MenuManager.Instance.ClearHoverHighlights(currentCraftingStation.outputInventory);
                 }
                 else Debug.LogWarning($"UIManager: CurrentCraftingStation is null when trying to clear highlights for oldState {oldState}.");
            }
            else Debug.LogWarning($"UIManager: Cannot deactivate Crafting UI when leaving {oldState} - no stored station or manager instance.");
            break;
        case MenuManager.GameState.InPauseMenu:
             // Deactivate Pause Menu UI
             if (pauseMenuUIRoot != null)
             {
                  pauseMenuUIRoot.SetActive(false);
                  Debug.Log("UIManager: Deactivated Pause Menu UI.");
             }
             else Debug.LogWarning("UIManager: Cannot deactivate Pause Menu UI - reference is null.");
            break;
        // Playing state doesn't have specific UI to deactivate on exit (Player/Toolbar handled below or in Minigame exit)
    }


    // --- Handle UI Activation for the new state ---
    // This happens AFTER deactivating UI for the old state.
    // For dynamic UIs, we use the 'response' parameter passed with the event.
    switch (newState)
    {
        case MenuManager.GameState.Playing:
            // Static UIs: Player UI and Toolbar are active in Playing
            // Note: This is also handled when exiting Minigame to Playing above.
            // Could potentially remove this here and rely *only* on the exit logic from other states.
            // For safety, keeping it ensures they are on if somehow entering Playing from an unhandled state exit.
            if (playerUIRoot != null) playerUIRoot.SetActive(true);
            if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(true);
             Debug.Log("UIManager: Activated Player and Toolbar UIs.");
            break;

        case MenuManager.GameState.InInventory:
            // Static UIs: Hide Player UI and Toolbar (unless toolbar stays active)
            if (playerUIRoot != null) playerUIRoot.SetActive(false);
            // if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(false); // Decide if toolbar hides in inventory
             Debug.Log("UIManager: Deactivated Player UI.");

            // Dynamic UI: Activate the Inventory UI root directly from the response
            if (response is OpenInventoryResponse openInventoryResponse)
            {
                 if (openInventoryResponse.InventoryUIRoot != null)
                 {
                     openInventoryResponse.InventoryUIRoot.SetActive(true);
                     Debug.Log($"UIManager: Activated Inventory UI Root from Response.");
                 }
                 else Debug.LogWarning("UIManager: OpenInventoryResponse did not contain a valid UI Root to activate.");
            }
            else Debug.LogWarning("UIManager: Entered InInventory state, but response was not OpenInventoryResponse.");
            break;

        case MenuManager.GameState.InPauseMenu:
            // Static UIs: Hide Player UI and Toolbar
            if (playerUIRoot != null) playerUIRoot.SetActive(false);
            if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(false);
             Debug.Log("UIManager: Deactivated Player and Toolbar UIs.");

            // Activate Pause Menu UI
            if (pauseMenuUIRoot != null)
            {
                 pauseMenuUIRoot.SetActive(true);
                 Debug.Log("UIManager: Activated Pause Menu UI.");
            }
            else Debug.LogWarning("UIManager: Cannot activate Pause Menu UI - reference is null.");
            break;

        case MenuManager.GameState.InComputer:
            // Static UIs: Hide Player UI and Toolbar
            if (playerUIRoot != null) playerUIRoot.SetActive(false);
            if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(false);
             Debug.Log("UIManager: Deactivated Player and Toolbar UIs.");

            // Dynamic UI: Activate the Computer UI root directly from the response
            if (response is EnterComputerResponse enterComputerResponse)
            {
                 if (enterComputerResponse.ComputerUIRoot != null)
                 {
                     enterComputerResponse.ComputerUIRoot.SetActive(true);
                     Debug.Log($"UIManager: Activated Computer UI Root from Response.");
                 }
                 else Debug.LogWarning("UIManager: EnterComputerResponse did not contain a valid UI Root to activate.");
            }
            else Debug.LogWarning("UIManager: Entered InComputer state, but response was not EnterComputerResponse.");
            break;

        case MenuManager.GameState.InMinigame:
             // --- MODIFIED: Activation of Minigame UI is handled by the MinigameManager activating its GameObject ---
             // The UIManager should only hide static UI elements when entering InMinigame.
             // The specific minigame UI is activated by the central MinigameManager activating its GameObject.
             Debug.Log($"UIManager: Entering InMinigame state.");

             // Hide static UIs that are not needed in the minigame state
             if (playerUIRoot != null) playerUIRoot.SetActive(false);
             // if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(false); // Decide if toolbar hides
              Debug.Log("UIManager: Deactivated Player UI (entering Minigame).");

            // We no longer access response.MinigameUIRoot here for activation.
            // The central MinigameManager will activate the specific minigame GameObject.
            // if (response is StartMinigameResponse startMinigameResponse) { ... } // No longer activate UI here
            break;

        case MenuManager.GameState.InCrafting:
            // Static UIs: Hide Player UI and Toolbar
            if (playerUIRoot != null) playerUIRoot.SetActive(false);
            // if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(false); // Decide if toolbar hides in crafting
             Debug.Log("UIManager: Deactivated Player UI.");

            // Dynamic UI: Tell the CraftingStation component to open its UI (reference from response)
            if (response is OpenCraftingResponse openCraftingResponse)
            {
                 if (openCraftingResponse.CraftingStationComponent != null)
                 {
                     openCraftingResponse.CraftingStationComponent.OpenCraftingUI(); // Assumes this method exists
                      Debug.Log($"UIManager: Told CraftingStation to open UI from Response.");
                 }
                 else Debug.LogWarning("UIManager: OpenCraftingResponse did not contain a valid CraftingStation to open UI.");
            }
            else Debug.LogWarning("UIManager: Entered InCrafting state, but response was not OpenCraftingResponse.");
            break;
    }
}

        // TODO: Add a public method to register a specific UI panel (e.g., Pause Menu UI) if needed
        // public void RegisterUIPanel(MenuManager.GameState state, GameObject uiRoot) { ... }
    }
}