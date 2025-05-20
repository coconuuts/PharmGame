using UnityEngine;
using Systems.GameStates; // Needed for MenuManager and GameState enum
using Systems.Interaction; // Needed for InteractionResponse types
using Systems.Inventory; // Needed for Inventory (for hover clear)
using Systems.CraftingMinigames; // ADDED: Needed for CraftingStation reference type


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
        /// </summary>
        /// <param name="newState">The new game state.</param>
        /// <param name="oldState">The previous game state.</param>
        /// <param name="response">The InteractionResponse that triggered the state change (can be null).</param>
        private void HandleGameStateChanged(MenuManager.GameState newState, MenuManager.GameState oldState, InteractionResponse response)
        {
            Debug.Log($"UIManager: Handling state change from {oldState} to {newState}.");

            // --- Handle UI Deactivation logic for the old state ---
            // These rely on the MenuManager's public getters which still hold the OLD state data.
            switch (oldState)
            {
                case MenuManager.GameState.InInventory:
                    if (MenuManager.Instance != null && MenuManager.Instance?.CurrentOpenInventoryComponent != null)
                    {
                        MenuManager.Instance.ClearHoverHighlights(MenuManager.Instance.CurrentOpenInventoryComponent);
                    }
                    break;
                case MenuManager.GameState.InComputer:
                    break;

                case MenuManager.GameState.InCrafting:
                    // When leaving Crafting state, potentially close the Crafting UI.
                    // However, if transitioning TO InMinigame (from crafting), the Crafting UI
                    // might need to stay open (just obscured) or managed by the minigame itself.
                    // Let's assume the Crafting UI only closes if we are leaving the crafting/minigame flow entirely.
                    if (MenuManager.Instance != null && MenuManager.Instance.CurrentCraftingStation != null)
                    {
                        // Only explicitly close the Crafting UI if the NEW state is NOT InMinigame or InCrafting
                        // (i.e., exiting the crafting/minigame context)
                        if (newState != MenuManager.GameState.InMinigame && newState != MenuManager.GameState.InCrafting)
                        {
                            Debug.Log($"UIManager: Telling CraftingStation to close UI when leaving {oldState} for {newState}.");
                            MenuManager.Instance.CurrentCraftingStation.CloseCraftingUI();
                        }
                        else
                        {
                            Debug.Log($"UIManager: Staying within a Crafting/Minigame flow. Not explicitly telling CraftingStation to close its UI on exit from {oldState}.");
                            // The specific minigame UI or the InMinigame state config handles obscuring/disabling interactions with Crafting UI.
                        }

                        // Clear hover highlights regardless, as interaction context shifts away from the crafting UI.
                         CraftingStation cs = MenuManager.Instance.CurrentCraftingStation;
                         if (cs != null)
                         {
                             if (cs.primaryInputInventory != null) MenuManager.Instance.ClearHoverHighlights(cs.primaryInputInventory);
                             if (cs.secondaryInputInventory != null) MenuManager.Instance.ClearHoverHighlights(cs.secondaryInputInventory);
                             if (cs.outputInventory != null) MenuManager.Instance.ClearHoverHighlights(cs.outputInventory);
                         }
                    }
                    else Debug.LogWarning($"UIManager: Cannot process Crafting UI exit for {oldState} - no stored station or manager instance.");
                    break;

                case MenuManager.GameState.InPauseMenu:
                    // Deactivate Pause Menu UI
                     if (pauseMenuUIRoot != null)
                     {
                         pauseMenuUIRoot.SetActive(false);
                         Debug.Log("UIManager: Deactivated Pause Menu UI.");
                     }
                    break;

                case MenuManager.GameState.InMinigame:
                    Debug.Log($"UIManager: Leaving InMinigame state.");
                    if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(true);
                    break;

                // Playing state exit doesn't typically require deactivating specific UIs here.
            }


            // --- Handle UI Activation logic for the new state ---
            // These use the NEW state and the response data (if provided).
            switch (newState)
            {
                case MenuManager.GameState.Playing:
                    // Static UIs: Player UI and Toolbar are active in Playing
                    if (playerUIRoot != null) playerUIRoot.SetActive(true);
                    if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(true); // Adjust visibility based on design
                    Debug.Log("UIManager: Activated Player and Toolbar UIs (entering Playing).");
                    break;

                case MenuManager.GameState.InInventory:
                    // Static UIs: Hide Player UI (Toolbar might stay active).
                    if (playerUIRoot != null) playerUIRoot.SetActive(false);
                    // if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(false); // Adjust visibility based on design
                    Debug.Log("UIManager: Deactivated Player UI (entering Inventory).");
                    // Dynamic UI: Activate the Inventory UI root from the response
                    if (response is OpenInventoryResponse openInventoryResponse)
                    {
                         if (openInventoryResponse.InventoryUIRoot != null)
                         {
                             openInventoryResponse.InventoryUIRoot.SetActive(true);
                             Debug.Log($"UIManager: Activated Inventory UI Root from Response.");
                         }
                    }
                    break;

                case MenuManager.GameState.InPauseMenu:
                    // Static UIs: Hide Player UI and Toolbar, Activate Pause Menu UI
                    if (playerUIRoot != null) playerUIRoot.SetActive(false);
                    if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(false);
                    if (pauseMenuUIRoot != null) pauseMenuUIRoot.SetActive(true);
                    Debug.Log("UIManager: Deactivated Player/Toolbar, Activated Pause Menu UI.");
                    break;

                case MenuManager.GameState.InComputer:
                    // Static UIs: Hide Player UI and Toolbar
                    if (playerUIRoot != null) playerUIRoot.SetActive(false);
                    if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(false);
                    Debug.Log("UIManager: Deactivated Player/Toolbar (entering Computer).");
                    // Dynamic UI: Activate the Computer UI root from the response
                    if (response is EnterComputerResponse enterComputerResponse)
                    {
                         if (enterComputerResponse.ComputerUIRoot != null)
                         {
                             enterComputerResponse.ComputerUIRoot.SetActive(true);
                             Debug.Log($"UIManager: Activated Computer UI Root from Response.");
                         }
                    }
                    break;

                case MenuManager.GameState.InMinigame:
                    Debug.Log($"UIManager: Entering InMinigame state.");
                    // Hide static UIs that are not needed in the minigame state
                    if (playerUIRoot != null) playerUIRoot.SetActive(false);
                    if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(false); // Adjust visibility based on design
                    Debug.Log("UIManager: Deactivated Player/Toolbar UI (entering Minigame).");
                    // The specific minigame's UI is assumed to be activated by the minigame instance itself (e.g., in its SetupAndStart or OnEnterBeginningState).
                    break;

                case MenuManager.GameState.InCrafting:
                    Debug.Log($"UIManager: Entering InCrafting state.");
                    // Hide static UIs that are not needed.
                    if (playerUIRoot != null) playerUIRoot.SetActive(false);
                    // if (playerToolbarUIRoot != null) playerToolbarUIRoot.SetActive(false); // Adjust visibility based on design
                    Debug.Log("UIManager: Deactivated Player UI (entering Crafting).");

                    // Dynamic UI: Tell the relevant CraftingStation component to open its UI.
                    // It will be either from the initial OpenCraftingResponse OR the stored reference
                    // on MenuManager if returning from a temporary state (like InMinigame or InPauseMenu).
                    CraftingStation stationToOpenUI = null;
                    if (response is OpenCraftingResponse openCraftingResponse && openCraftingResponse.CraftingStationComponent != null)
                    {
                         stationToOpenUI = openCraftingResponse.CraftingStationComponent;
                         Debug.Log($"UIManager: Found CraftingStation from Response.");
                    }
                    else if (MenuManager.Instance != null && MenuManager.Instance.CurrentCraftingStation != null)
                    {
                        // This handles returning from minigame or pause, or re-entering Crafting state without a direct OpenCraftingResponse.
                        // MenuManager's stored CurrentCraftingStation should still hold the reference from the initial interaction.
                         stationToOpenUI = MenuManager.Instance.CurrentCraftingStation;
                         Debug.Log($"UIManager: Using stored CurrentCraftingStation from MenuManager.");
                    }

                    if (stationToOpenUI != null)
                    {
                        stationToOpenUI.OpenCraftingUI(); // Tell the station to open/resume its UI
                        Debug.Log($"UIManager: Told CraftingStation '{stationToOpenUI.gameObject.name}' to open UI.");
                    }
                    else
                    {
                        Debug.LogWarning("UIManager: Entered InCrafting state, but could not find a valid CraftingStation reference to open UI.");
                    }
                    break;
            }
        }

        // TODO: Add a public method to register a specific UI panel (e.g., Pause Menu UI) if needed
        // public void RegisterUIPanel(MenuManager.GameState state, GameObject uiRoot) { ... }
    }
}