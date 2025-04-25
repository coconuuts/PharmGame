using UnityEngine;
using System;
using System.Collections.Generic;
using InventoryClass = Systems.Inventory.Inventory;
using Systems.Interaction; // Needed for InteractionResponse types
using System.Collections;
using Systems.CameraControl;
using Systems.Minigame;
using Systems.Inventory;    

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;

    // --- ADD THE NEW STATE ---
    public enum GameState
    {
        Playing,
        InInventory,
        InPauseMenu,
        InComputer,
        InMinigame // ADD THIS STATE
    }
    // ------------------------

    public GameState currentState = GameState.Playing;
    [HideInInspector] public GameState previousState;

    public delegate void StateChangedHandler(GameState newState);
    public static event StateChangedHandler OnStateChanged;

    [Header("Player Settings")]
    public GameObject player;
    public string cameraTag = "MainCamera";
    private PlayerInteractionManager interactionManager;
    private PlayerMovement playerMovement;
    [Tooltip("Drag the player's toolbar InventorySelector GameObject here.")]
    [SerializeField] private InventorySelector playerToolbarInventorySelector;

    // Fields to track the currently active UI and data for states like InInventory or InComputer
    private GameObject currentActiveUIRoot; // Renamed for generality
    private InventoryClass currentOpenInventoryComponent;
    private ComputerInteractable currentComputerInteractable;
    private CashRegisterInteractable currentCashRegisterInteractable;

    // --- ADDED FIELDS FOR CAMERA MANAGEMENT ---
    private Transform playerCameraTransform; // This should be set by CameraManager Start now, but kept for reference if needed.
    private Vector3 originalCameraPosition; // Managed by CameraManager
    private Quaternion originalCameraRotation; // Managed by CameraManager
    private float storedCinematicDuration = 0.25f; // Store the duration for cinematic moves
    // Coroutine is in CameraManager now
    // ----------------------------------------


    public delegate void StateActionHandler(InteractionResponse response);
    private Dictionary<GameState, StateActionHandler> stateEntryActions;
    private Dictionary<GameState, StateActionHandler> stateExitActions;


    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Debug.LogWarning("MenuManager: Duplicate instance found. Destroying this one.", this); Destroy(gameObject); return; }
        Debug.Log("MenuManager: Awake completed.");

         // Ensure playerToolbarInventorySelector is assigned or try finding it
         if (playerToolbarInventorySelector == null)
         {
             GameObject playerToolbarObject = GameObject.FindGameObjectWithTag("PlayerToolbarInventory"); // Use your actual toolbar tag
             if (playerToolbarObject != null)
             {
                 playerToolbarInventorySelector = playerToolbarObject.GetComponent<InventorySelector>();
                 if (playerToolbarInventorySelector == null)
                 {
                      Debug.LogError($"MenuManager: GameObject with tag 'PlayerToolbarInventory' found, but it does not have an InventorySelector component.", playerToolbarObject);
                 }
             }
             else
             {
                  Debug.LogWarning($"MenuManager: Player Toolbar Inventory GameObject with tag 'PlayerToolbarInventory' not found. Cannot manage toolbar highlights.", this);
             }
         }
         else
         {
              Debug.Log("MenuManager: Player Toolbar Inventory Selector assigned in Inspector.", this);
         }
    }

    private void Start()
    {
        // --- GET REFERENCES ---
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
        // ---------------------


        // --- Initialize Dictionaries with the new state handlers ---
        stateEntryActions = new Dictionary<GameState, StateActionHandler>
        {
            { GameState.Playing, HandlePlayingStateEntry },
            { GameState.InInventory, HandleInInventoryStateEntry },
            { GameState.InPauseMenu, HandleInPauseMenuEntry },
            { GameState.InComputer, HandleInComputerStateEntry },
            { GameState.InMinigame, HandleInMinigameStateEntry } // REGISTER MINIGAME ENTRY HANDLER
        };

         stateExitActions = new Dictionary<GameState, StateActionHandler>
         {
             { GameState.Playing, HandlePlayingStateExit },
             { GameState.InInventory, HandleInInventoryStateExit },
             { GameState.InPauseMenu, HandleInPauseMenuExit },
             { GameState.InComputer, HandleInComputerStateExit },
             { GameState.InMinigame, HandleInMinigameStateExit } // REGISTER MINIGAME EXIT HANDLER
         };
        // ----------------------------------------------------------


        // TODO: Ensure all interactable UI roots start disabled in their own setup

        // Initial Game State Setup (Ensures Playing state entry action runs)
        currentState = GameState.Playing;
        previousState = currentState;
        Debug.Log("Menu State: " + currentState + " (Initial Setup)");

        if (stateEntryActions.TryGetValue(currentState, out var entryAction))
        {
             Debug.Log($"MenuManager: Invoking initial entry action for state {currentState}.");
             entryAction.Invoke(null); // Pass null as there's no response
        }
        else
        {
             Debug.LogWarning($"MenuManager: No entry action defined for initial state {currentState}.", this);
        }

        OnStateChanged?.Invoke(currentState); // Trigger initial event
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Allow escaping from Minigame state too
            if (currentState == GameState.InInventory || currentState == GameState.InPauseMenu || currentState == GameState.InComputer || currentState == GameState.InMinigame) // ADD InMinigame
            {
                SetState(GameState.Playing, null); // Exiting via Escape
            }
            else if (currentState == GameState.Playing)
            {
                OpenPauseMenu(); // Call public method for consistency
            }
        }
    }

    /// <summary>
    /// Sets the current game state and triggers associated entry/exit actions.
    /// Called by HandleInteractionResponse or internal transitions like Escape/Pause.
    /// </summary>
    /// <param name="newState">The state to transition to.</param>
    /// <param name="response">The InteractionResponse that caused this state change, or null if internal.</param>
    public void SetState(GameState newState, InteractionResponse response = null)
    {
         // Check for same state transition (ignore unless specific handling is needed)
         if (currentState == newState)
         {
             // Specific check for trying to open the same inventory again
             if (newState == GameState.InInventory && response is OpenInventoryResponse openInvResponse && openInvResponse.InventoryComponent == currentOpenInventoryComponent)
             {
                  Debug.Log($"MenuManager: Already in {currentState} state with the same inventory '{currentOpenInventoryComponent?.Id}'. Ignoring SetState call.", this);
                  return;
             }
             // Add similar checks for other states if needed (e.g., re-entering same computer?)
             // else if (newState == GameState.InComputer && response is EnterComputerResponse enterCompResponse && enterCompResponse.ComputerInteractable == currentComputerInteractable) { ... }
             // else if (newState != GameState.InInventory) // General check for other states if ignoring is desired
             else
             {
                  Debug.Log($"MenuManager: Already in {currentState} state. Ignoring SetState call.");
                  return;
             }
         }

        previousState = currentState;

        if (stateExitActions.TryGetValue(currentState, out var exitAction))
        {
            exitAction.Invoke(response); // Pass the response to the EXIT action
        }

        currentState = newState;
        Debug.Log("Menu State: " + currentState);
        OnStateChanged?.Invoke(newState); // Trigger the event *after* the state is set

        if (stateEntryActions.TryGetValue(currentState, out var entryAction))
        {
            entryAction.Invoke(response); // Pass the response to the ENTRY action
        }

         // Clear state-specific references if not handled in exit action
         // Note: It's cleaner for exit actions to clear their own state's references AFTER using them.
         // currentActiveUIRoot = null; // Managed by exit actions
         // currentOpenInventoryComponent = null; // Managed by exit actions
         // currentComputerInteractable = null; // Managed by exit actions
    }

    /// <summary>
    /// Receives and processes InteractionResponse objects to trigger state changes and related actions.
    /// Called by PlayerInteractionManager.
    /// </summary>
    /// <param name="response">The InteractionResponse received from an interactable.</param>
    public void HandleInteractionResponse(InteractionResponse response)
    {
        if (response == null)
        {
            Debug.LogWarning("MenuManager: Received a null interaction response.", this);
            return; // Do not proceed with null response unless it's a specific internal signal
        }

        // Check the type of the response and trigger the appropriate state change OR action
        if (response is OpenInventoryResponse openInventoryResponse)
        {
            SetState(GameState.InInventory, openInventoryResponse);
        }
        else if (response is EnterComputerResponse enterComputerResponse)
        {
            SetState(GameState.InComputer, enterComputerResponse);
        }
        else if (response is StartMinigameResponse startMinigameResponse) // ADD HANDLING FOR MINIGAME RESPONSE
        {
             SetState(GameState.InMinigame, startMinigameResponse); // Transition to the new minigame state
        }
        else if (response is ToggleLightResponse toggleLightResponse)
        {
             // For simple actions that don't require a state change, execute them directly
             Debug.Log("MenuManager: Executing ToggleLightResponse action.");
             toggleLightResponse.LightSwitch?.ToggleLightState();
        }
        // TODO: Add handling for other InteractionResponse types here
        else
        {
            Debug.LogWarning($"MenuManager: Received unhandled InteractionResponse type: {response.GetType().Name}", this);
        }
    }


    // --- State Entry Actions ---
    // Signatures accept InteractionResponse

    private void HandlePlayingStateEntry(InteractionResponse response)
    {
        Time.timeScale = 1f;
        playerMovement.moveSpeed = 7f;
        interactionManager?.EnableRaycast();

        if (CameraManager.Instance != null)
        {
             CameraManager.Instance.SetCameraMode(CameraManager.CameraMode.MouseLook);
        }
        else Debug.LogError("MenuManager: CameraManager Instance is null! Cannot set camera mode.");

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerToolbarInventorySelector != null && playerToolbarInventorySelector.SlotUIComponents != null)
        {
             foreach (var slotUI in playerToolbarInventorySelector.SlotUIComponents)
             {
                  if (slotUI != null)
                  {
                       slotUI.RemoveHoverHighlight(); // Ensure hover state is false and visual updated
                  }
             }
        }
        else
        {
             Debug.LogWarning("MenuManager: Player Toolbar Inventory Selector or its SlotUIComponents list is null. Cannot clear hover highlights on Playing entry.", this);
        }

        // Clear specific state references (redundant with exit actions clearing, but safe)
        // currentActiveUIRoot = null;
        // currentOpenInventoryComponent = null;
        // currentComputerInteractable = null;
        // Reset stored duration (safe)
        // storedCinematicDuration = 0.5f; // Or some default
    }

    private void HandleInInventoryStateEntry(InteractionResponse response)
    {
        InMenu();
        interactionManager?.DisableRaycast();

        if (CameraManager.Instance != null)
        {
             CameraManager.Instance.SetCameraMode(CameraManager.CameraMode.Locked);
        }
        else Debug.LogError("MenuManager: CameraManager Instance is null! Cannot set camera mode.");

        if (response is OpenInventoryResponse openInvResponse)
        {
            currentOpenInventoryComponent = openInvResponse.InventoryComponent;
            currentActiveUIRoot = openInvResponse.InventoryUIRoot; // Use generic field

            if (currentActiveUIRoot != null)
            {
                currentActiveUIRoot.SetActive(true);
                Debug.Log($"MenuManager: Activated UI for inventory: {currentOpenInventoryComponent?.Id}.", this);
            }
            else Debug.LogWarning("MenuManager: OpenInventoryResponse did not contain a valid UI Root to activate.", this);
        }
        else Debug.LogError("MenuManager: Entered InInventory state, but the response was not an OpenInventoryResponse!", this);

        currentComputerInteractable = null;
        currentCashRegisterInteractable = null;
    }

    private void HandleInPauseMenuEntry(InteractionResponse response)
    {
        InMenu();
        Time.timeScale = 1f;
        interactionManager?.DisableRaycast();

        if (CameraManager.Instance != null)
        {
             CameraManager.Instance.SetCameraMode(CameraManager.CameraMode.Locked);
        }
        else Debug.LogError("MenuManager: CameraManager Instance is null! Cannot set camera mode.");

        // TODO: Activate Pause Menu UI GameObject (You'll need a field for this in MenuManager)
        Debug.LogWarning("MenuManager: Pause Menu UI activation is not implemented yet.");
         // Pause Menu entry doesn't need data from a response currently

        currentActiveUIRoot = null;
        currentOpenInventoryComponent = null;
        currentComputerInteractable = null;
        currentCashRegisterInteractable = null;
    }

    private void HandleInComputerStateEntry(InteractionResponse response)
    {
        InMenu();
        interactionManager?.DisableRaycast();

        if (response is EnterComputerResponse computerResponse)
        {
             currentComputerInteractable = computerResponse.ComputerInteractable;
             currentActiveUIRoot = computerResponse.ComputerUIRoot; // Use generic field
             storedCinematicDuration = computerResponse.CameraMoveDuration; // Store the duration

             if (CameraManager.Instance != null)
             {
                 // Start camera movement TO the computer view point
                 CameraManager.Instance.SetCameraMode(
                     CameraManager.CameraMode.CinematicView,
                     computerResponse.CameraTargetView, // Pass the target Transform
                     computerResponse.CameraMoveDuration // Pass the duration
                 );
             }
             else Debug.LogError("MenuManager: CameraManager Instance is null! Cannot set camera mode.");
        }
        else Debug.LogError("MenuManager: Entered InComputer state, but the response was not an EnterComputerResponse!", this);

        currentOpenInventoryComponent = null;
        currentCashRegisterInteractable = null;
    }

    private void HandleInMinigameStateEntry(InteractionResponse response)
    {
        InMenu();
        interactionManager?.DisableRaycast();

        if (response is StartMinigameResponse minigameResponse)
        {
             currentCashRegisterInteractable = minigameResponse.CashRegisterInteractable;
             currentActiveUIRoot = minigameResponse.MinigameUIRoot;
             storedCinematicDuration = minigameResponse.CameraMoveDuration;

             if (CameraManager.Instance != null)
             {
                 CameraManager.Instance.SetCameraMode(
                     CameraManager.CameraMode.CinematicView,
                     minigameResponse.CameraTargetView,
                     minigameResponse.CameraMoveDuration
                 );
             }
             else Debug.LogError("MenuManager: CameraManager Instance is null! Cannot set camera mode.");

             if (currentActiveUIRoot != null)
             {
                 currentActiveUIRoot.SetActive(true);
             }
             else Debug.LogWarning("MenuManager: StartMinigameResponse did not contain a valid UI Root to activate.", this);

             // --- Initialize and Start the Minigame Logic ---
             if (MinigameManager.Instance != null) // Check if MinigameManager instance exists
             {
                  // Pass the target click count from the response
                  MinigameManager.Instance.StartMinigame(minigameResponse.ItemsToScan, minigameResponse.CashRegisterInteractable); // Call the StartMinigame method
             }
             else Debug.LogError("MenuManager: MinigameManager Instance is null! Cannot start minigame.");
             // --------------------------------------------
        }
        else Debug.LogError("MenuManager: Entered InMinigame state, but the response was not a StartMinigameResponse!", this);

        // Ensure other state references are null
        currentOpenInventoryComponent = null;
        currentComputerInteractable = null;
    }


    // --- State Exit Actions ---
    // Signatures accept InteractionResponse

    private void HandlePlayingStateExit(InteractionResponse response)
    {

    }

    private void HandleInInventoryStateExit(InteractionResponse response)
    {
         // Deactivate the UI using the stored generic field
         if (currentActiveUIRoot != null)
         {
             currentActiveUIRoot.SetActive(false);

             // --- FIND AND CLEAR HOVER HIGHLIGHTS ON ALL SLOTS ---
             // Get the Visualizer from the inventory component's GameObject
             if (currentOpenInventoryComponent != null)
             {
                  Visualizer visualizer = currentOpenInventoryComponent.GetComponent<Visualizer>();
                  if (visualizer != null && visualizer.SlotUIComponents != null)
                  {
                       foreach (var slotUI in visualizer.SlotUIComponents)
                       {
                            // Call RemoveHoverHighlight on each slot
                            if (slotUI != null)
                            {
                                slotUI.RemoveHoverHighlight();
                            }
                       }
                  }
                  else Debug.LogWarning("MenuManager: Could not find Visualizer or SlotUIComponents on the closing Inventory GameObject to clear hover highlights.", currentOpenInventoryComponent?.gameObject);
             }
             else Debug.LogWarning("MenuManager: currentOpenInventoryComponent is null when exiting InInventory state. Cannot clear hover highlights.");
             // ----------------------------------------------------
         }
         else Debug.LogWarning("MenuManager: No stored UI Root GameObject reference to deactivate when exiting InInventory.", this);


         // Clear the stored references AFTER using them
         currentOpenInventoryComponent = null;
         currentActiveUIRoot = null;

         // Camera mode and player controls handled in HandlePlayingStateEntry if transitioning to Playing
    }

    private void HandleInPauseMenuExit(InteractionResponse response)
    {
        // TODO: Deactivate Pause Menu UI GameObject
        // Camera mode and player controls handled in HandlePlayingStateEntry if transitioning to Playing
    }

    private void HandleInComputerStateExit(InteractionResponse response)
    {
         // --- Tell CameraManager to go back to MouseLook mode ---
         if (CameraManager.Instance != null)
         {
             // Use the stored duration for the return trip
             CameraManager.Instance.SetCameraMode(
                 CameraManager.CameraMode.MouseLook,
                 null, // Target view is null for MouseLook return
                 storedCinematicDuration // Pass the stored duration for the return move
             );
         }
         else Debug.LogError("MenuManager: CameraManager Instance is null! Cannot set camera mode.");
        // -----------------------------------------------------

         // Call ResetInteraction on the ComputerInteractable using the stored reference
         if (currentComputerInteractable != null)
         {
             currentComputerInteractable.ResetInteraction();
         }
         else Debug.LogWarning("MenuManager: No stored ComputerInteractable instance to call ResetInteraction.", this);

        // Clear the stored references after use
        currentComputerInteractable = null;
        currentActiveUIRoot = null; // Clear this too
        // currentOpenInventoryComponent is null for Computer state

        // Re-enable player movement and interaction raycast here explicitly on exit from Computer
         interactionManager?.EnableRaycast();
    }

    private void HandleInMinigameStateExit(InteractionResponse response)
    {
         if (currentActiveUIRoot != null)
         {
             currentActiveUIRoot.SetActive(false);
         }
         else Debug.LogWarning("MenuManager: No stored UI Root GameObject reference to deactivate when exiting Minigame.", this);


         if (CameraManager.Instance != null)
         {
             CameraManager.Instance.SetCameraMode(
                 CameraManager.CameraMode.MouseLook,
                 null,
                 storedCinematicDuration
             );
         }
         else Debug.LogError("MenuManager: CameraManager Instance is null! Cannot set camera mode.");


        // --- Reset Minigame Logic ---
         if (MinigameManager.Instance != null) // Check if MinigameManager instance exists
         {
             MinigameManager.Instance.ResetMinigame(); // Call the ResetMinigame method
         }
         else Debug.LogError("MenuManager: MinigameManager Instance is null! Cannot reset minigame.");

         // Call ResetInteraction on the CashRegisterInteractable using the stored reference
         if (currentCashRegisterInteractable != null)
         {
             currentCashRegisterInteractable.ResetInteraction();
         }
         else Debug.LogWarning("MenuManager: No stored CashRegisterInteractable instance to call ResetInteraction.", this);


        // Clear the stored references after use
        currentCashRegisterInteractable = null;
        currentActiveUIRoot = null;


        interactionManager?.EnableRaycast();
    }

    // --- Helper Method ---
    private void InMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        playerMovement.moveSpeed = 0f;
    }

        public void OpenPauseMenu()
    {
        SetState(GameState.InPauseMenu, null);
    }
    public void ClosePauseMenu()
    {
         SetState(GameState.Playing, null);
    }
}