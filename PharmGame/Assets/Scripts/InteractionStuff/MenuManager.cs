// Systems/GameStates/MenuManager.cs
// Keep existing usings and namespace

using UnityEngine;
using System;
using System.Collections.Generic;
using InventoryClass = Systems.Inventory.Inventory;
using Systems.Interaction;
using System.Collections;
using Systems.CameraControl;
using Systems.Minigame; // Ensure this is included
using Systems.Inventory;
using Systems.UI;
using Systems.Player;
using Systems.CraftingMinigames; // Ensure this is included


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
            InMinigame, // This state is generic for ALL minigames 
            InCrafting  // This state represents the crafting *UI* state, NOT the crafting minigame
        }

        public GameState currentState = GameState.Playing;
        [HideInInspector] public GameState previousState;

        // Delegate signature to include InteractionResponse
        public delegate void StateChangedHandler(GameState newState, GameState oldState, InteractionResponse response);
        public static event StateChangedHandler OnStateChanged;

        [Header("Player Settings")]
        public GameObject player;
        public string cameraTag = "MainCamera";
        private PlayerInteractionManager interactionManager;
        private PlayerMovement playerMovement;
        [Tooltip("Drag the player's toolbar InventorySelector GameObject here.")]
        [SerializeField] private InventorySelector playerToolbarInventorySelector;
        public InventorySelector PlayerToolbarInventorySelector => playerToolbarInventorySelector;

        // Fields to track the currently active UI and data for states
        private GameObject currentActiveUIRoot_internal;
        private InventoryClass currentOpenInventoryComponent_internal;
        private ComputerInteractable currentComputerInteractable_internal;
        private CashRegisterInteractable currentCashRegisterInteractable_internal; // For cash register minigame
        private CraftingStation currentCraftingStation_internal; // Reference to the active crafting station
        private CraftingMinigameBase currentActiveCraftingMinigame_internal; // Reference to the active crafting minigame component
        private IMinigame currentActiveGeneralMinigame_internal; // Reference for the active general minigame component (from MinigameManager)


        // Public Getters for UIManager and StateActions to access
        public GameObject CurrentActiveUIRoot => currentActiveUIRoot_internal;
        public InventoryClass CurrentOpenInventoryComponent => currentOpenInventoryComponent_internal;
        public ComputerInteractable CurrentComputerInteractable => currentComputerInteractable_internal;
        public CashRegisterInteractable CurrentCashRegisterInteractable => currentCashRegisterInteractable_internal;
        public CraftingStation CurrentCraftingStation => currentCraftingStation_internal;
        public CraftingMinigameBase CurrentActiveCraftingMinigame => currentActiveCraftingMinigame_internal;
        public IMinigame CurrentActiveGeneralMinigame => currentActiveGeneralMinigame_internal;


        [Header("Game State Configuration")]
        [SerializeField] private List<GameStateConfigSO> gameStateConfigsList;
        private Dictionary<GameState, GameStateConfigSO> gameStateConfigMap;

        // Reference to the Simple Action Dispatcher
        private SimpleActionDispatcher simpleActionDispatcher = new SimpleActionDispatcher();

        [Header("Manager References")]
        [Tooltip("Drag the General MinigameManager GameObject here.")]
        [SerializeField] private MinigameManager generalMinigameManagerInstance; // Reference to the general MinigameManager

        [Tooltip("Drag the CraftingMinigameManager GameObject here.")]
        [SerializeField] private CraftingMinigameManager craftingMinigameManagerInstance; // Reference to the CraftingMinigameManager

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Debug.LogWarning("MenuManager: Duplicate instance found. Destroying this one.", this); Destroy(gameObject); return; }
            Debug.Log("MenuManager: Awake completed.");

            // --- Find static UI roots by tag if not assigned ---
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

             // --- Check if serialized manager references are assigned ---
             if (generalMinigameManagerInstance == null)
             {
                 Debug.LogError($"MenuManager: General Minigame Manager reference is not assigned in the inspector!", this);
                 // Do NOT disable, let other systems potentially work, but minigames won't
             }
             if (craftingMinigameManagerInstance == null)
             {
                 Debug.LogError($"MenuManager: Crafting Minigame Manager reference is not assigned in the inspector!", this);
                  // Do NOT disable
             }
             // ------------------------------------------------------
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
                if (!gameStateConfigMap.ContainsKey(state) && state != GameState.Playing) // Allow Playing to not have a SO if it's the default/fallback
                {
                     Debug.LogWarning($"MenuManager: No GameStateConfigSO assigned for state: {state}!", this); // Changed to Warning, maybe not all states *need* a SO
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

            Debug.Log("Menu State: " + currentState + " (Initial Setup)");
            SetState(currentState, null, true); // Pass null response and indicate initial setup
        }

        private void Update()
        {
            // Handle Escape key
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Allow Escape from any state except Playing to return to Playing
                 if (currentState != GameState.Playing)
                {
                    // The Exit actions for the current state (including InMinigame) will handle cleanup.
                    SetState(GameState.Playing, null);
                }
                 else // current state IS Playing
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

            // If a drag is currently in progress and we are changing state, abort the drag.
            // This handles scenarios like pressing Escape while dragging.
            if (DragAndDropManager.Instance != null && DragAndDropManager.Instance.IsDragging)
            {
                // Check if the state we are entering is one where dragging should not persist.
                Debug.Log($"MenuManager: Drag is active ({DragAndDropManager.Instance.IsDragging}). Aborting drag due to state change from {oldState} to {newState}.");
                DragAndDropManager.Instance.AbortDrag();
            }

            // --- Handle Specific Exit Logic Here Before Event ---
            // This is where we trigger manager-level cleanup based on *what state we are leaving*.
            // This happens BEFORE the OnStateChanged event, so UIManager and others react correctly.
            // Critically, this happens *after* StateActionSO Exit Actions.

            // If exiting InMinigame TO Playing 
            if (oldState == GameState.InMinigame && newState == GameState.Playing)
            {
                Debug.Log("MenuManager: Explicitly handling exit from InMinigame to Playing (likely via Escape). Triggering minigame managers to abort.");
                // Tell both crafting and general minigame managers to end their current session, marking it as aborted.
                if (craftingMinigameManagerInstance != null)
                {
                    craftingMinigameManagerInstance.EndCurrentMinigame(true); // Call EndCurrentMinigame with ABORT=true
                }
                else Debug.LogWarning("MenuManager: CraftingMinigameManagerInstance is null when exiting InMinigame. Cannot signal abort for crafting minigame.");

                if (generalMinigameManagerInstance != null)
                {
                    generalMinigameManagerInstance.EndCurrentMinigame(true); // Need this method in general MinigameManager
                }
                else Debug.LogWarning("MenuManager: GeneralMinigameManagerInstance is null when exiting InMinigame. Cannot signal abort for general minigame.");

                // Clear the minigame references held by MenuManager when explicitly exiting InMinigame
                currentActiveCraftingMinigame_internal = null;
                currentActiveGeneralMinigame_internal = null;
            }

            // --- Trigger the OnStateChanged event AFTER exit actions and specific exit logic ---
            OnStateChanged?.Invoke(newState, oldState, response);


            // --- NOW Update the internal state and references for the NEW state ---
            previousState = oldState;
            currentState = newState; // Change the current state here
            Debug.Log($"Menu State: Transitioning from {oldState} to {currentState}.");

            // Clear old dynamic references and set new ones based on the response or previous state context.
            // Note: Some references (like CraftingStation) persist across certain state changes (Crafting <-> Minigame <-> Pause).
            // They are cleared explicitly when exiting the whole flow (e.g., Crafting -> Playing).

            // Clear all unless they are explicitly set by the response for the NEW state,
            // or need to persist (handled in explicit logic below).
            currentActiveUIRoot_internal = null;
            currentOpenInventoryComponent_internal = null;
            currentComputerInteractable_internal = null;
            currentCashRegisterInteractable_internal = null;
            // CraftingStation, CraftingMinigame, and GeneralMinigame references are handled explicitly below


             // Handle setting new state references based on the response
             if (response is OpenInventoryResponse openInventoryResponse)
             {
                 currentOpenInventoryComponent_internal = openInventoryResponse.InventoryComponent;
                 currentActiveUIRoot_internal = openInventoryResponse.InventoryUIRoot;
                 // Ensure crafting/minigame refs are cleared if entering Inventory
                 currentCraftingStation_internal = null;
                 currentActiveCraftingMinigame_internal = null;
                 currentActiveGeneralMinigame_internal = null;
             }
             else if (response is EnterComputerResponse enterComputerResponse)
             {
                 currentComputerInteractable_internal = enterComputerResponse.ComputerInteractable;
                 currentActiveUIRoot_internal = enterComputerResponse.ComputerUIRoot;
                  // Ensure crafting/minigame refs are cleared if entering Computer
                 currentCraftingStation_internal = null;
                 currentActiveCraftingMinigame_internal = null;
                 currentActiveGeneralMinigame_internal = null;
             }
             else if (response is StartMinigameResponse startMinigameResponse) // For non-crafting minigames like Cash Register
             {
                 currentCashRegisterInteractable_internal = startMinigameResponse.CashRegisterInteractable;
                 // The general MinigameManager handles setting its active IMinigame instance.
                 // MenuManager doesn't need to store IMinigame from this response.
                  // Ensure crafting refs are cleared if starting a General Minigame (unlikely flow, but safe)
                 currentCraftingStation_internal = null;
                 currentActiveCraftingMinigame_internal = null;
             }
             // --- Handle StartCraftingMinigameResponse ---
             else if (response is StartCraftingMinigameResponse startCraftingMinigameResponse)
             {
                 Debug.Log("MenuManager: Received StartCraftingMinigameResponse. Storing Crafting Minigame Component.");
                 currentActiveCraftingMinigame_internal = startCraftingMinigameResponse.MinigameComponent; // Corrected typo
                 // CraftingStation reference (currentCraftingStation_internal) should persist from OpenCraftingResponse.
                 // It is NOT cleared here.
                 currentActiveGeneralMinigame_internal = null; // Clear general minigame ref
             }
             // ---------------------------------------------------
             else if (response is OpenCraftingResponse openCraftingResponse)
             {
                 Debug.Log("MenuManager: Received OpenCraftingResponse. Storing Crafting Station.");
                 currentCraftingStation_internal = openCraftingResponse.CraftingStationComponent;
                 // ActiveUIRoot is NOT set here; CraftingStation manages its own UI activation.
                  // Ensure minigame refs are cleared if entering Crafting UI directly
                 currentActiveCraftingMinigame_internal = null;
                 currentActiveGeneralMinigame_internal = null;
             }
             else // Response is null or a type that doesn't set a modal reference
             {
                 // Explicitly clear references based on the NEW state we are entering,
                 // especially when transitioning to states that shouldn't have modal UIs active.
                 switch(newState)
                 {
                     case GameState.Playing:
                     case GameState.InPauseMenu:
                          // If transitioning to Playing or Pause with a null response, clear all modal references.
                          // This covers exiting Inventory, Computer, Crafting, or Minigame via Escape.
                          currentOpenInventoryComponent_internal = null;
                          currentComputerInteractable_internal = null;
                          currentCashRegisterInteractable_internal = null;
                          currentCraftingStation_internal = null; // Clear crafting station reference when leaving crafting/minigame flow
                          currentActiveCraftingMinigame_internal = null;
                          currentActiveGeneralMinigame_internal = null;
                          break;
                     case GameState.InCrafting:
                          // If transitioning TO InCrafting with a null response (e.g., returning from minigame/pause)
                          // The CraftingStation reference (currentCraftingStation_internal) *should* still be held
                          // from the initial OpenCraftingResponse that led into the crafting flow.
                          // We do NOT clear it here.
                          // Ensure other references are cleared.
                          currentOpenInventoryComponent_internal = null;
                          currentComputerInteractable_internal = null;
                          currentCashRegisterInteractable_internal = null;
                          // currentCraftingStation_internal persists
                          currentActiveCraftingMinigame_internal = null; // Should be cleared when leaving minigame state
                          currentActiveGeneralMinigame_internal = null;
                          break;
                     case GameState.InMinigame:
                           // If transitioning TO InMinigame with a null response (unlikely for crafting/general minigames which use specific responses)
                           // This case might be relevant if a minigame starts without a structured response.
                           // For robustness, clear other modal references, but minigame refs should be set by the responsible manager.
                           currentOpenInventoryComponent_internal = null;
                           currentComputerInteractable_internal = null;
                           currentCashRegisterInteractable_internal = null;
                           // currentCraftingStation_internal persists if coming from Crafting
                           currentActiveCraftingMinigame_internal = null; // Should be set by response or manager
                           currentActiveGeneralMinigame_internal = null; // Should be set by response or manager
                           break;
                     default:
                           // For other states, clear all modal references
                           currentOpenInventoryComponent_internal = null;
                           currentComputerInteractable_internal = null;
                           currentCashRegisterInteractable_internal = null;
                           currentCraftingStation_internal = null;
                           currentActiveCraftingMinigame_internal = null;
                           currentActiveGeneralMinigame_internal = null;
                           break;
                 }
             }


            // --- Execute Entry Actions for the new state ---
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
             else if (response is StartMinigameResponse startMinigameResponse) // For non-crafting minigames like Cash Register
             {
                 // Call the general minigame manager to start the minigame.
                 // The general manager is responsible for instantiating, setting up,
                 // and transitioning MenuManager.GameState to InMinigame.
                 if (generalMinigameManagerInstance != null)
                 {
                     generalMinigameManagerInstance.StartMinigame(startMinigameResponse); 
                 }
                 else
                 {
                     Debug.LogError("MenuManager: Cannot start general minigame - GeneralMinigameManagerInstance is null!");
                 }
                 // MenuManager will receive the InMinigame state change request from MinigameManager.StartMinigame
             }
             // StartCraftingMinigameResponse is handled by CraftingMinigameManager internally calling SetState,
             // not typically initiated via HandleInteractionResponse from PlayerInteractionManager.
             else if (response is OpenCraftingResponse openCraftingResponse)
             {
                  SetState(GameState.InCrafting, openCraftingResponse); // Transition to InCrafting
             }
              // Pass unhandled responses to the Simple Action Dispatcher
             else
             {
                  simpleActionDispatcher.Dispatch(response);
             }
        }

        // Helper method to clear hover highlights for any given inventory
        public void ClearHoverHighlights(InventoryClass inventory)
        {
             if (inventory != null)
             {
                  Visualizer visualizer = inventory.GetComponent<Visualizer>(); 
                  if (visualizer != null && visualizer.SlotUIComponents != null) 
                  {
                       foreach (var slotUI in visualizer.SlotUIComponents)
                       {
                            if (slotUI != null) // Ensure the slotUI is not null in the list
                            {
                                 slotUI.RemoveHoverHighlight(); 
                            }
                       }
                  }
                 else if (visualizer == null) Debug.LogWarning($"MenuManager: Could not find Visualizer component on Inventory GameObject '{inventory?.gameObject?.name}' to clear hover highlights.", inventory?.gameObject);
                 else if (visualizer.SlotUIComponents == null) Debug.LogWarning($"MenuManager: SlotUIComponents list is null on Visualizer component of Inventory GameObject '{inventory?.gameObject?.name}'.", inventory?.gameObject);
                 else if (visualizer.SlotUIComponents.Count == 0) Debug.LogWarning($"MenuManager: SlotUIComponents list is empty on Visualizer component of Inventory GameObject '{inventory?.gameObject?.name}'.", inventory?.gameObject);
             }
             else Debug.LogWarning("MenuManager: Cannot clear hover highlights - Inventory reference is null.", this);
        }


        // Public methods to open/close pause menu still call SetState

        public void OpenPauseMenu()
        {
            // Only open pause menu if not already in a modal state like InMinigame or InCrafting
             if (currentState != GameState.Playing)
             {
                 Debug.LogWarning($"MenuManager: Attempted to open Pause Menu from {currentState} state. Only allowed from Playing.");
                 return;
             }
            SetState(GameState.InPauseMenu, null);
        }

        public void ClosePauseMenu()
        {
             // When exiting PauseMenu via a dedicated button, return to the state it was in BEFORE PauseMenu.
             // Use previousState, but prevent infinite loops or returning to states that shouldn't be returned to directly.
             GameState stateAfterPause = (previousState == GameState.InPauseMenu || previousState == GameState.Playing) ? GameState.Playing : previousState;
             SetState(stateAfterPause, null);
        }
    }
}