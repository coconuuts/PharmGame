using UnityEngine;
using System;
using System.Collections.Generic;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;

    // Moved GameState enum *inside* the MenuManager class
    public enum GameState
    {
        Playing,
        InInventory,
        InPauseMenu,
        // Add more states as needed (e.g., InSettings, InMainMenu)
    }

    public GameState currentState = GameState.Playing;

    // Delegate and event for state changes
    public delegate void StateChangedHandler(GameState newState);
    public static event StateChangedHandler OnStateChanged;

    [Header("Player Settings")]
    public GameObject player; //  reference to the Player GameObject.
    public string cameraTag = "MainCamera"; // Tag of the camera GameObject
    private PlayerInteractionManager interactionManager;
    private PlayerCam playerCam;

    [Header("Inventory State")]


    private Dictionary<GameState, Action> stateActions;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Moved DontDestroyOnLoad here
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (player != null)
        {
            interactionManager = player.GetComponent<PlayerInteractionManager>();
            if (interactionManager == null) 
            { 
                Debug.LogError("Player Interactionmanager does not have an InventoryHolder component!");
            }
        }

        // Find the camera by tag
        GameObject cameraObject = GameObject.FindGameObjectWithTag(cameraTag);
        if (cameraObject != null)
        {
            playerCam = cameraObject.GetComponent<PlayerCam>();
            if (playerCam == null)
            {
                Debug.LogError("GameObject with tag '" + cameraTag + "' does not have a PlayerCam component!");
            }
        }
        else
        {
            Debug.LogError("No GameObject found with tag '" + cameraTag + "'.  Make sure your camera is tagged correctly.");
        }

        // Initialize the state actions dictionary
        stateActions = new Dictionary<GameState, Action>
        {
            { GameState.Playing, HandlePlayingState },
            { GameState.InInventory, HandleInInventoryState },
            { GameState.InPauseMenu, HandleInPauseMenu },
            // Add more states as needed
        };

        // Initialize the game state
        SetState(GameState.Playing);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // If in inventory or pause menu, return to Playing
            if (currentState == GameState.InInventory || currentState == GameState.InPauseMenu)
            {
                 SetState(GameState.Playing);
            }
            // Optional: Could add logic here to open Pause Menu if already Playing
        }

        // Optional: Add keybind (e.g., 'I') to open Player's own inventory
        if (Input.GetKeyDown(KeyCode.I) && currentState == GameState.Playing)
        {
            SetState(GameState.InInventory);
        }
    }

    public void SetState(GameState newState)
    {
        if (currentState == newState) return;

        // TODO: Exit logic for the *previous* state ---


        // --- Set the new state ---
        currentState = newState;
        Debug.Log("Game State: " + currentState);
        OnStateChanged?.Invoke(newState);

        // --- Execute the entry action for the *new* state ---
        if (stateActions.TryGetValue(newState, out Action action))
        {
            action.Invoke();
        }
    }

    private void HandlePlayingState()
    {
        Time.timeScale = 1f;
        interactionManager?.EnableRaycast();
        playerCam?.EnableCameraMovement();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void HandleInInventoryState()
    {
        InMenu(); // Pauses time, shows cursor
        interactionManager?.DisableRaycast(); // Don't interact while in UI
        playerCam?.DisableCameraMovement(); // Don't look around
    }

    private void HandleInPauseMenu()
    {
        InMenu();
        interactionManager?.DisableRaycast();
        playerCam?.DisableCameraMovement();
        // TODO: Activate Pause Menu UI
    }

    private void InMenu()
    {
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // --- Public Methods for Changing State ---

    /// <summary>
    /// Opens the inventory view, setting the appropriate game state.
    /// </summary>
    /// <param name="inventoryToView">The InventoryHolder to display.</param>
    /// <param name="sourceObject">The InventoryObject interactable that was triggered (can be null if opening player's own).</param>
    public void OpenInventory()
    {
        SetState(GameState.InInventory);
    }

    public void OpenPauseMenu()
    {
        SetState(GameState.InPauseMenu);
    }

    // Add methods like ActivatePlayerInventoryUI(), DeactivatePlayerInventoryUI() if needed
}
