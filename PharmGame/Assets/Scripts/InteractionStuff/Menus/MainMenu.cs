using UnityEngine;
using UnityEngine.UI;
using Systems.Persistence;
using System.Linq;
using Systems.SceneManagement;
using Systems.UI;

public class MainMenu : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject mainButtonsPanel;

    [Header("New Game Setup")]
    [SerializeField] private NameInputWindowUI nameInputWindow;
    
    [Header("Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton; 
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button quitButton;

    [Header("Controllers")]
    [SerializeField] private LoadGameMenuController loadMenuController;

    [Header("Scene Configuration")] 
    [SerializeField] private int gameSceneGroupIndex = 1; 

    private void Awake()
    {
        if (continueButton != null) continueButton.gameObject.SetActive(false);
        if (loadGameButton != null) loadGameButton.interactable = false;
    }

    private void Start()
    {
        if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGameClicked);
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
        if (loadGameButton != null) loadGameButton.onClick.AddListener(OnLoadGameClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

        // Subscribe to the event so we refresh when coming back from the Load/Delete menu
        if (loadMenuController != null)
        {
            loadMenuController.OnMenuClosed += RefreshMenuState;
        }

        RefreshMenuState();
    }

    // Good practice to unsubscribe to prevent memory leaks or errors on scene destroy
    private void OnDestroy()
    {
        if (loadMenuController != null)
        {
            loadMenuController.OnMenuClosed -= RefreshMenuState;
        }
    }

    private void OnEnable()
    {
        RefreshMenuState();
    }

    private void RefreshMenuState()
    {
        bool hasSaves = false;

        if (SaveLoadSystem.HasInstance)
        {
            var saves = SaveLoadSystem.Instance.GetAllSaves();
            hasSaves = saves.Any();
        }

        // Apply state: Set visibility/interactivity based on hasSaves (True OR False)
        if (continueButton != null) 
        {
            continueButton.gameObject.SetActive(hasSaves);
        }

        if (loadGameButton != null)
        {
            loadGameButton.interactable = hasSaves;
        }
    }
    
    private void Update()
    {
        // Check for Escape key press
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 1. Priority Check: Is the Modal Open?
            if (SimpleModalManager.Instance != null && SimpleModalManager.Instance.IsModalActive)
            {
                return;
            }

            // 2. Name Input Check
            // If the Name Input window is open, let IT handle the input (via its own Update loop).
            // We return here to prevent the Main Menu from processing the key press.
            if (nameInputWindow != null && nameInputWindow.IsActive)
            {
                return;
            }

            // 3. Sub-menu Check
            // If the Main Buttons are hidden, it means we are in a sub-menu (like Load Game)
            if (mainButtonsPanel != null && !mainButtonsPanel.activeSelf)
            {
                if (loadMenuController != null)
                {
                    loadMenuController.CloseMenu();
                }
            }
        }
    }

    private void OnNewGameClicked()
    {
        // Check if we have the window assigned
        if (nameInputWindow != null)
        {
            // Hide main buttons so UI isn't cluttered
            if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);

            // Open the Name Input Window
            nameInputWindow.Show(
                onConfirm: (characterName) => 
                {
                    // User confirmed name -> Start the game
                    StartNewGameProcess(characterName);
                },
                onCancel: () => 
                {
                    // User cancelled -> Show main buttons again
                    if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
                }
            );
        }
        else
        {
            // Fallback if UI isn't assigned
            Debug.LogWarning("MainMenu: NameInputWindowUI not assigned! using default.");
            StartNewGameProcess("Player"); 
        }
    }

    private void StartNewGameProcess(string characterName)
    {
        // 1. Reset Data
        SaveLoadSystem.Instance.ResetGameData();

        // 2. Set the custom name
        SaveLoadSystem.Instance.gameData.CharacterName = characterName;

        // 3. Load the Scene
        SceneLoader loader = FindFirstObjectByType<SceneLoader>();
        
        if (loader != null)
        {
            LoadGameScene(loader);
        }
        else
        {
            Debug.LogError("SceneLoader not found! Falling back to instant load.");
            SaveLoadSystem.Instance.NewGame();
        }

        // 4. Create the initial Autosave immediately
        SaveLoadSystem.Instance.AutosaveGame();
    }

    private async void LoadGameScene(SceneLoader loader)
    {
        await loader.LoadSceneGroup(gameSceneGroupIndex);
    }

    private void OnContinueClicked()
    {
        var saves = SaveLoadSystem.Instance.GetAllSaves();
        
        if (saves.Any())
        {
            string saveToLoad = saves.First(); 
            SaveLoadSystem.Instance.LoadGame(saveToLoad);
        }
    }

    private void OnLoadGameClicked()
    {
        if (loadMenuController != null)
        {
            loadMenuController.OpenMenu();
        }
    }

    private void OnQuitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}