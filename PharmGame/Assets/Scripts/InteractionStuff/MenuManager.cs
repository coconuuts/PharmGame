// Keep the existing usings and namespace
using UnityEngine;
using System;
using System.Collections.Generic;
using InventoryClass = Systems.Inventory.Inventory;
using Systems.Interaction;
using System.Collections;
using Systems.CameraControl;
using Systems.Minigame;
using Systems.Inventory;
using Systems.UI; // ADDED: Needed to ensure UIManager namespace is known if MenuManager interacts with it directly (it doesn't here, but good practice)
using Systems.Player;

namespace Systems.GameStates
{
    public class MenuManager : MonoBehaviour
    {
        public static MenuManager Instance;

        public enum GameState
        {
            Playing,
            InInventory,
            InPauseMenu,
            InComputer,
            InMinigame,
            InCrafting
        }

        public GameState currentState = GameState.Playing;
        [HideInInspector] public GameState previousState;

        // --- MODIFIED: Delegate signature to include InteractionResponse ---
        public delegate void StateChangedHandler(GameState newState, GameState oldState, InteractionResponse response);
        public static event StateChangedHandler OnStateChanged;
        // -------------------------------------------------------------------

        [Header("Player Settings")]
        public GameObject player;
        public string cameraTag = "MainCamera";
        private PlayerInteractionManager interactionManager;
        private PlayerMovement playerMovement;
        [Tooltip("Drag the player's toolbar InventorySelector GameObject here.")]
        [SerializeField] private InventorySelector playerToolbarInventorySelector;
        public InventorySelector PlayerToolbarInventorySelector => playerToolbarInventorySelector;

        // Fields to track the currently active UI and data for states like InInventory or InComputer
        // Keep these internal fields. They are populated by SetState and accessed via public getters.
        private GameObject currentActiveUIRoot_internal;
        private InventoryClass currentOpenInventoryComponent_internal;
        private ComputerInteractable currentComputerInteractable_internal;
        private CashRegisterInteractable currentCashRegisterInteractable_internal;
        private CraftingStation currentCraftingStation_internal;

        // Public Getters for UIManager and StateActions to access
        // These will reflect the data of the *current* state after SetState finishes.
        public GameObject CurrentActiveUIRoot => currentActiveUIRoot_internal;
        public InventoryClass CurrentOpenInventoryComponent => currentOpenInventoryComponent_internal;
        public ComputerInteractable CurrentComputerInteractable => currentComputerInteractable_internal;
        public CashRegisterInteractable CurrentCashRegisterInteractable => currentCashRegisterInteractable_internal;
        public CraftingStation CurrentCraftingStation => currentCraftingStation_internal;


        [Header("Game State Configuration")]
        [SerializeField] private List<GameStateConfigSO> gameStateConfigsList;
        private Dictionary<GameState, GameStateConfigSO> gameStateConfigMap;

        // Reference to the Simple Action Dispatcher
        private SimpleActionDispatcher simpleActionDispatcher = new SimpleActionDispatcher();


        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Debug.LogWarning("MenuManager: Duplicate instance found. Destroying this one.", this); Destroy(gameObject); return; }
            Debug.Log("MenuManager: Awake completed.");

             // ... (Keep existing logic to find playerToolbarInventorySelector) ...
             GameObject playerToolbarObject = GameObject.FindGameObjectWithTag("PlayerToolbarInventory"); // Use your actual toolbar tag
             if (playerToolbarObject != null)
             {
                  playerToolbarInventorySelector = playerToolbarObject.GetComponent<InventorySelector>();
                  if (playerToolbarInventorySelector == null)
                  {
                       Debug.LogError($"MenuManager: GameObject with tag '{playerToolbarObject.tag}' found, but it does not have an InventorySelector component.", playerToolbarObject);
                  }
             }
             else
             {
                  Debug.LogWarning($"MenuManager: Player Toolbar Inventory GameObject with tag 'PlayerToolbarInventory' not found. Cannot manage toolbar highlights.", this);
             }
        }


        private void Start()
        {
            // Populate the Game State Config Dictionary
            gameStateConfigMap = new Dictionary<GameState, GameStateConfigSO>();
            if (gameStateConfigsList != null)
            {
                foreach (var config in gameStateConfigsList)
                {
                    if (config != null)
                    {
                         if (!gameStateConfigMap.ContainsKey(config.gameState))
                         {
                              gameStateConfigMap.Add(config.gameState, config);
                         }
                         else
                         {
                              Debug.LogWarning($"MenuManager: Duplicate GameStateConfigSO found for state {config.gameState}. Using the first one found.", config);
                         }
                    }
                }
            }
            else
            {
                Debug.LogError("MenuManager: Game State Configs List is not assigned in the Inspector!", this);
            }

            // Basic validation: Check if configs exist for all enum values
            foreach (GameState state in Enum.GetValues(typeof(GameState)))
            {
                if (!gameStateConfigMap.ContainsKey(state))
                {
                    Debug.LogError($"MenuManager: No GameStateConfigSO assigned for state: {state}!", this);
                }
            }

            // GET PLAYER REFERENCES (Keep this)
            if (player != null)
            {
                interactionManager = player.GetComponent<PlayerInteractionManager>();
                if (interactionManager == null) Debug.LogError("MenuManager: Player GameObject does not have a PlayerInteractionManager component!");

                playerMovement = player.GetComponent<PlayerMovement>();
                if (playerMovement == null) Debug.LogWarning("MenuManager: Player GameObject does not have a PlayerMovement component! Player movement control will not work.", player);

            }
            else
            {
                Debug.LogWarning("MenuManager: Player GameObject reference is missing. Player control features may be disabled.");
            }


            // Initial Game State Setup (Ensures Playing state entry action runs via SetState)
            currentState = GameState.Playing;
            previousState = currentState; // Initialize previous state

            // Call SetState for the initial state to run its entry actions
            Debug.Log("Menu State: " + currentState + " (Initial Setup)");
            SetState(currentState, null, true); // Pass null response and indicate initial setup
             // UIManager will listen to the OnStateChanged event triggered by SetState
             // and activate/deactivate UIs accordingly.
        }

        private void Update()
        {
            // Keep Escape key logic
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (currentState != GameState.Playing)
                {
                    SetState(GameState.Playing, null); // Exiting via Escape (no specific response)
                }
                else if (currentState == GameState.Playing)
                {
                    OpenPauseMenu();
                }
            }
        }

        /// <summary>
        /// Sets the current game state and triggers associated entry/exit actions defined in GameStateConfigSO.
        /// Triggers the OnStateChanged event, passing the InteractionResponse.
        /// Updates internal state data references AFTER the event and exit actions.
        /// </summary>
        /// <param name="newState">The state to transition to.</param>
        /// <param name="response">The InteractionResponse that caused this state change, or null if internal.</param>
        /// <param name="isInitialSetup">Flag to indicate if this is the very first state setting in Start.</param>
        public void SetState(GameState newState, InteractionResponse response = null, bool isInitialSetup = false)
        {
             // Ignore same state transition unless it's the initial setup
            if (currentState == newState && !isInitialSetup)
            {
                 Debug.Log($"MenuManager: Already in {currentState} state. Ignoring SetState call (unless initial setup).");
                 return;
            }

            GameState oldState = currentState; // Capture the old state

            // Find the config for the old state BEFORE changing currentState
             GameStateConfigSO oldStateConfig = null;
             if (gameStateConfigMap.TryGetValue(oldState, out var config))
             {
                  oldStateConfig = config;
             }
             else
             {
                  Debug.LogWarning($"MenuManager: No GameStateConfigSO found for old state {oldState}. Cannot execute exit actions.", this);
             }


            // --- Execute Exit Actions for the old state ---
            // These actions (and UIManager listening to event) will use the public getters,
            // which *still* hold the references from the state being exited at this moment.
            if (oldStateConfig != null)
            {
                oldStateConfig.ExecuteExitActions(response, this); // Pass the response to exit actions
            }


            // --- Trigger the OnStateChanged event AFTER exit actions ---
            // UIManager and other event listeners will react here.
            // UIManager's oldState logic will run NOW, accessing the public getters (which still hold old data).
            // UIManager's newState logic will run NOW, accessing the 'response' parameter.
             OnStateChanged?.Invoke(newState, oldState, response);


            // --- NOW Update the internal state and references for the NEW state ---
            previousState = oldState;
            currentState = newState; // Change the current state here
            Debug.Log($"Menu State: Transitioning from {oldState} to {currentState}.");

             // Clear old dynamic references and set new ones based on the response.
             // This happens AFTER exit logic and the event.
             currentActiveUIRoot_internal = null;
             currentOpenInventoryComponent_internal = null;
             currentComputerInteractable_internal = null;
             currentCashRegisterInteractable_internal = null;
             currentCraftingStation_internal = null;

             if (response is OpenInventoryResponse openInventoryResponse)
             {
                 currentOpenInventoryComponent_internal = openInventoryResponse.InventoryComponent;
                 currentActiveUIRoot_internal = openInventoryResponse.InventoryUIRoot;
             }
             else if (response is EnterComputerResponse enterComputerResponse)
             {
                  currentComputerInteractable_internal = enterComputerResponse.ComputerInteractable;
                  currentActiveUIRoot_internal = enterComputerResponse.ComputerUIRoot;
             }
             else if (response is StartMinigameResponse startMinigameResponse)
             {
                  currentCashRegisterInteractable_internal = startMinigameResponse.CashRegisterInteractable;
                  currentActiveUIRoot_internal = startMinigameResponse.MinigameUIRoot;
             }
             else if (response is OpenCraftingResponse openCraftingResponse)
             {
                  currentCraftingStation_internal = openCraftingResponse.CraftingStationComponent;
             }
             // Add other response types here if they carry data needed by StateActions.
            // -----------------------------------------------------------------------------


            // --- Execute Entry Actions for the new state ---
            // Actions here will use the NEW data now available via the public getters.
             GameStateConfigSO newStateConfig = null;
             if (gameStateConfigMap.TryGetValue(currentState, out var config2))
             {
                  newStateConfig = config2;
             }
             else
             {
                  Debug.LogError($"MenuManager: No GameStateConfigSO found for new state {currentState}. Cannot execute entry actions!", this);
             }

            if (newStateConfig != null)
            {
                newStateConfig.ExecuteEntryActions(response, this); // Pass the response to entry actions
            }
        }

        /// <summary>
        /// Receives and processes InteractionResponse objects to trigger state changes and related actions.
        /// Stores relevant response data for UIManager/StateActions to access.
        /// Called by PlayerInteractionManager.
        /// </summary>
        /// <param name="response">The InteractionResponse received from an interactable.</param>
        public void HandleInteractionResponse(InteractionResponse response)
        {
            if (response == null)
            {
                Debug.LogWarning("MenuManager: Received a null interaction response. Ignoring.", this);
                return;
            }

            // Check the type of the response and trigger the appropriate state change OR action
            // State-changing responses call SetState.
            if (response is OpenInventoryResponse openInventoryResponse)
            {
                 SetState(GameState.InInventory, openInventoryResponse);
            }
            else if (response is EnterComputerResponse enterComputerResponse)
            {
                 SetState(GameState.InComputer, enterComputerResponse);
            }
            else if (response is StartMinigameResponse startMinigameResponse)
            {
                 SetState(GameState.InMinigame, startMinigameResponse);
            }
            else if (response is OpenCraftingResponse openCraftingResponse)
            {
                 SetState(GameState.InCrafting, openCraftingResponse);
            }
             // Pass unhandled responses to the Simple Action Dispatcher
             else
             {
                 simpleActionDispatcher.Dispatch(response);
             }
        }

        // Helper method to clear hover highlights for any given inventory
        // Keep this public as StateActions or UIManager might call it
        public void ClearHoverHighlights(InventoryClass inventory)
        {
            if (inventory != null)
            {
                Visualizer visualizer = inventory.GetComponent<Visualizer>(); // Assuming Visualizer exists
                if (visualizer != null && visualizer.SlotUIComponents != null) // Assuming SlotUIComponents is a public list
                {
                    foreach (var slotUI in visualizer.SlotUIComponents)
                    {
                        if (slotUI != null) // Ensure the slotUI is not null in the list
                        {
                            slotUI.RemoveHoverHighlight(); // Assuming RemoveHoverHighlight exists on SlotUI component
                        }
                    }
                }
                 // Added checks for null visualizer or empty SlotUIComponents list
                else if (visualizer == null) Debug.LogWarning($"MenuManager: Could not find Visualizer component on Inventory GameObject '{inventory.gameObject.name}' to clear hover highlights.", inventory?.gameObject);
                else if (visualizer.SlotUIComponents == null) Debug.LogWarning($"MenuManager: SlotUIComponents list is null on Visualizer component of Inventory GameObject '{inventory.gameObject.name}'.", inventory?.gameObject);
                else if (visualizer.SlotUIComponents.Count == 0) Debug.LogWarning($"MenuManager: SlotUIComponents list is empty on Visualizer component of Inventory GameObject '{inventory.gameObject.name}'.", inventory?.gameObject);
            }
            else Debug.LogWarning("MenuManager: Cannot clear hover highlights - Inventory reference is null.", this);
        }

        // Public methods to open/close pause menu still call SetState
        public void OpenPauseMenu()
        {
            // Ensure we use the correct enum name
            SetState(GameState.InPauseMenu, null);
        }

        public void ClosePauseMenu()
        {
             // When exiting PauseMenu via a dedicated button, return to the state it was in BEFORE PauseMenu.
             // This uses previousState.
             // The Escape key logic in Update already handles returning to Playing from PauseMenu.
             // Let's use previousState here for more flexible Pause Menu exits (e.g., from Inventory -> Pause -> back to Inventory).
             // Ensure previousState is valid and not PauseMenu itself to prevent infinite loops.

             // --- CORRECTED: Use GameState.InPauseMenu ---
             GameState stateAfterPause = (previousState != GameState.InPauseMenu && previousState != GameState.Playing) ? previousState : GameState.Playing;
             SetState(stateAfterPause, null);
             // -------------------------------------------
        }
    }
}