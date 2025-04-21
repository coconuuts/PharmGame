using UnityEngine;
using System;
using System.Collections.Generic;
using Systems.Inventory; // Needed for Inventory class reference

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;

    // Add the new state here
    public enum GameState
    {
        Playing,
        InInventory,
        InPauseMenu,
        InComputer // Added state for computer interaction
    }

    public GameState currentState = GameState.Playing;
    private GameState previousState; // Keep track of the previous state

    public delegate void StateChangedHandler(GameState newState);
    public static event StateChangedHandler OnStateChanged;

    [Header("Player Settings")]
    public GameObject player;
    public string cameraTag = "MainCamera";
    private PlayerInteractionManager interactionManager;
    private PlayerCam playerCam; // Assuming you have this script

    // These fields will track the currently open inventory data and its UI root.
    private GameObject currentOpenInventoryUIRoot;
    private Inventory currentOpenInventoryComponent;


    private Dictionary<GameState, Action> stateEntryActions;
    private Dictionary<GameState, Action> stateExitActions;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Optional: DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogWarning("MenuManager: Duplicate instance found. Destroying this one.", this);
            Destroy(gameObject);
            return;
        }
        Debug.Log("MenuManager: Awake completed.");
    }

    private void Start()
    {
        Debug.Log("MenuManager: Start started.");
        if (player != null)
        {
            interactionManager = player.GetComponent<PlayerInteractionManager>();
            if (interactionManager == null)
            {
                Debug.LogError("MenuManager: Player GameObject does not have a PlayerInteractionManager component!");
            }
            playerCam = player.GetComponentInChildren<PlayerCam>();
             if (playerCam == null && !string.IsNullOrEmpty(cameraTag))
             {
                 GameObject cameraObject = GameObject.FindGameObjectWithTag(cameraTag);
                 if (cameraObject != null)
                 {
                     playerCam = cameraObject.GetComponent<PlayerCam>();
                 }
             }

            if (playerCam == null)
            {
                Debug.LogWarning($"MenuManager: PlayerCam component not found on Player or GameObject with tag '{cameraTag}'. Camera control features will be disabled.");
            }

        }
        else
        {
             Debug.LogWarning("MenuManager: Player GameObject reference is missing. Player control features may be disabled.");
        }


        stateEntryActions = new Dictionary<GameState, Action>
        {
            { GameState.Playing, HandlePlayingStateEntry },
            { GameState.InInventory, HandleInInventoryStateEntry },
            { GameState.InPauseMenu, HandleInPauseMenuEntry },
            { GameState.InComputer, HandleInComputerStateEntry } // Register the new entry handler
        };

         stateExitActions = new Dictionary<GameState, Action>
         {
             { GameState.Playing, HandlePlayingStateExit },
             { GameState.InInventory, HandleInInventoryStateExit },
             { GameState.InPauseMenu, HandleInPauseMenuExit },
             { GameState.InComputer, HandleInComputerStateExit } // Register the new exit handler
         };


        // Initialize UI states - assume all inventory UIs start disabled
        // No need to reference a specific player UI here
        // TODO: Ensure all interactable inventory UIs start disabled in their own setup

        previousState = currentState; // Initialize previous state
        SetState(GameState.Playing); // Set initial state
        Debug.Log("MenuManager: Start completed. Initial state: " + currentState);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // If in inventory, pause menu, OR computer, return to Playing
            if (currentState == GameState.InInventory || currentState == GameState.InPauseMenu || currentState == GameState.InComputer)
            {
                SetState(GameState.Playing);
            }
            else if (currentState == GameState.Playing)
            {
                 SetState(GameState.InPauseMenu);
            }
        }
    }

    /// <summary>
    /// Sets the current game state and triggers associated entry/exit actions.
    /// </summary>
    /// <param name="newState">The state to transition to.</param>
    /// <param name="inventoryToOpen">Optional: The Inventory data component to open if transitioning to InInventory state.</param>
    /// <param name="inventoryUIRootToOpen">Optional: The GameObject containing the UI for the inventory to activate if transitioning to InInventory state.</param>
    public void SetState(GameState newState, Inventory inventoryToOpen = null, GameObject inventoryUIRootToOpen = null)
    {
        // Check if trying to open the SAME inventory again (or transition to the same state)
         if (currentState == newState)
         {
             if (newState == GameState.InInventory && inventoryToOpen == currentOpenInventoryComponent)
             {
                Debug.Log($"MenuManager: Already in {currentState} state with the same inventory '{inventoryToOpen?.Id}'. Ignoring SetState call.", this);
                return;
             }
             else if (newState != GameState.InInventory) // For non-inventory states, just ignore if already in that state
             {
                 Debug.Log($"MenuManager: Already in {currentState} state. Ignoring SetState call.", this);
                 return;
             }
         }


        // --- Execute Exit Action for the previous state ---
        Debug.Log($"MenuManager: Exiting state: {currentState}");
        if (stateExitActions.TryGetValue(currentState, out Action exitAction))
        {
            exitAction.Invoke();
        }

        // --- Store the old state before changing ---
        previousState = currentState;


        // --- Update the currently open inventory references BEFORE setting the new state ---
         // The exit action for InInventory uses these references to know which UI to close
         // Store these temporarily if needed by the *entry* action of the *new* state
         GameObject previousOpenInventoryUIRoot = currentOpenInventoryUIRoot;
         Inventory previousOpenInventoryComponent = currentOpenInventoryComponent;


        // --- Set the new state ---
        currentState = newState;
        Debug.Log("Menu State: " + currentState);
        OnStateChanged?.Invoke(newState); // Notify subscribers *after* state has changed


        // --- Update references for the NEW state ---
        if (newState == GameState.InInventory)
        {
            // If transitioning to Inventory state, store the references passed in
            currentOpenInventoryComponent = inventoryToOpen;
            currentOpenInventoryUIRoot = inventoryUIRootToOpen;
             if (currentOpenInventoryComponent == null || currentOpenInventoryUIRoot == null)
             {
                 Debug.LogWarning("MenuManager: Transitioned to InInventory state but no Inventory or UI Root was provided! UI cannot be activated.", this);
                 // Optionally transition back to Playing if invalid open attempt?
                 // SetState(GameState.Playing); // Example: Auto-close if invalid
             }
             else
             {
                  Debug.Log($"MenuManager: Setting current open inventory to '{currentOpenInventoryComponent.Id}' with UI root '{currentOpenInventoryUIRoot.name}'.", this);
             }
        }
        else // If transitioning to any other state, clear the open inventory references
        {
            currentOpenInventoryComponent = null;
            currentOpenInventoryUIRoot = null;
             Debug.Log("MenuManager: Clearing current open inventory references.", this);
        }


        // --- Execute Entry Action for the new state ---
        Debug.Log($"MenuManager: Entering state: {currentState}");
        if (stateEntryActions.TryGetValue(newState, out Action entryAction))
        {
            entryAction.Invoke();
        }
    }

    // --- State Entry Actions ---

    private void HandlePlayingStateEntry()
    {
        Debug.Log("MenuManager: Handling entry to Playing state.");
        Time.timeScale = 1f;
        interactionManager?.EnableRaycast();
        playerCam?.EnableCameraMovement(); // Re-enable player camera control
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void HandleInInventoryStateEntry()
    {
        Debug.Log("MenuManager: Handling entry to InInventory state.");
        InMenu(); // Handles Time.timeScale, Cursor
        interactionManager?.DisableRaycast();
        playerCam?.DisableCameraMovement(); // Disable player camera control
         // --- Activate the specific inventory UI that is being opened ---
         // Use the reference that was set in SetState()
         if (currentOpenInventoryUIRoot != null)
         {
             currentOpenInventoryUIRoot.SetActive(true);
             Debug.Log($"MenuManager: Activated UI for inventory: {currentOpenInventoryComponent?.Id}.", this);
         }
         else
         {
              Debug.LogWarning("MenuManager: No Inventory UI Root GameObject reference available to activate for InInventory state entry.", this);
         }
    }

    private void HandleInPauseMenuEntry()
    {
        Debug.Log("MenuManager: Handling entry to InPauseMenu state.");
        InMenu(); // Handles Time.timeScale, Cursor
        interactionManager?.DisableRaycast();
        playerCam?.DisableCameraMovement(); // Disable player camera control
        // TODO: Activate Pause Menu UI GameObject (You'll need a field for this in MenuManager)
        Debug.LogWarning("MenuManager: Pause Menu UI activation is not implemented yet.");
    }

    // New handler for entering the computer state
    private void HandleInComputerStateEntry()
    {
        Debug.Log("MenuManager: Handling entry to InComputer state.");
        InMenu(); // Handles Time.timeScale, Cursor
        // Note: Camera movement is handled by the ComputerInteractable script,
        // but we disable the standard player camera movement here.
        playerCam?.DisableCameraMovement();
        interactionManager?.DisableRaycast(); // Player shouldn't interact with world while on computer
    }


    // --- State Exit Actions ---

    private void HandlePlayingStateExit()
    {
         Debug.Log("MenuManager: Handling exit from Playing state.");
    }

    private void HandleInInventoryStateExit()
    {
        Debug.Log("MenuManager: Handling exit from InInventory state.");
         // --- Deactivate the inventory UI that was open ---
         // Use the reference that was set before currentState was changed in SetState()
         if (currentOpenInventoryUIRoot != null) // currentOpenInventoryUIRoot still holds the reference to the UI that *was* open
         {
             currentOpenInventoryUIRoot.SetActive(false);
             Debug.Log($"MenuManager: Deactivated UI for inventory: {currentOpenInventoryComponent?.Id}.", this);
         }
         else
         {
              Debug.LogWarning("MenuManager: No Inventory UI Root GameObject reference to deactivate when exiting InInventory.", this);
         }
         // currentOpenInventoryComponent and currentOpenInventoryUIRoot are cleared in SetState AFTER the exit action runs
         // Re-enable player movement and camera control is handled in HandlePlayingStateEntry
    }

    private void HandleInPauseMenuExit()
    {
        Debug.Log("MenuManager: Handling exit from InPauseMenu state.");
        // TODO: Deactivate Pause Menu UI GameObject
        Debug.LogWarning("MenuManager: Pause Menu UI deactivation is not implemented yet.");
        // Re-enable player movement and camera control is handled in HandlePlayingStateEntry
    }

     // New handler for exiting the computer state
    private void HandleInComputerStateExit()
    {
        Debug.Log("MenuManager: Handling exit from InComputer state.");
        // Player movement and camera control re-enabled in HandlePlayingStateEntry
        interactionManager?.EnableRaycast(); // Re-enable world interaction raycast
    }


    // --- Helper Method ---
    private void InMenu()
    {
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // Note: Player movement scripts should ideally check Time.timeScale or MenuManager.currentState
        // to prevent movement while in a menu/UI state.
    }

    // --- Public Methods for Changing State (Simplified for Interaction) ---

    /// <summary>
    /// Opens the inventory view, setting the appropriate game state and displaying the UI for the specified inventory.
    /// Called by an OpenInventory interactable.
    /// </summary>
    /// <param name="inventoryToView">The Inventory data component to display.</param>
    /// <param name="inventoryUIRootToView">The GameObject containing the UI for the inventory to activate.</param>
    public void OpenInventory(Inventory inventoryToView, GameObject inventoryUIRootToView)
    {
         SetState(GameState.InInventory, inventoryToView, inventoryUIRootToView);
    }

     /// <summary>
     /// Enters the computer interaction state.
     /// Called by a ComputerInteractable.
     /// </summary>
     public void EnterComputerState()
     {
         SetState(GameState.InComputer);
     }


    public void OpenPauseMenu()
    {
        SetState(GameState.InPauseMenu);
    }
    public void ClosePauseMenu()
    {
         SetState(GameState.Playing);
    }

    // You might want a generic CloseMenu() method
    // public void CloseMenu()
    // {
    //     if (currentState != GameState.Playing)
    //     {
    //         SetState(GameState.Playing);
    //     }
    // }
}