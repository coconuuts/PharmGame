using UnityEngine;
using UnityEngine.UI;
using Systems.Persistence;
using Systems.SceneManagement;
using Systems.UI;
using System.Linq; // Added for sanity checks

public class MainMenu : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject mainButtonsPanel;

    [Header("Sub Menus")]
    [SerializeField] private NameInputWindowUI nameInputWindow;
    [SerializeField] private ProfileSelectionMenuController profileSelectionMenu; // NEW

    [Header("Buttons")]
    [SerializeField] private Button playButton; // Replaces New/Continue/Load
    [SerializeField] private Button quitButton;

    // Remove old LoadMenuController reference if it's only used for the old Load button
    // [SerializeField] private LoadGameMenuController loadMenuController; 

    [Header("Scene Configuration")] 
    [SerializeField] private int gameSceneGroupIndex = 1; 

    private void Start()
    {
        if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

        // Setup Profile Selection Events
        if (profileSelectionMenu != null)
        {
            profileSelectionMenu.OnProfileSelected += HandleProfileSelection;
            profileSelectionMenu.OnBackClicked += OnBackFromProfileMenu;
            profileSelectionMenu.CloseMenu(); // Ensure closed at start
        }

        // Initially show main buttons
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
    }

    private void OnDestroy()
    {
        if (profileSelectionMenu != null)
        {
            profileSelectionMenu.OnProfileSelected -= HandleProfileSelection;
            profileSelectionMenu.OnBackClicked -= OnBackFromProfileMenu;
        }
    }

    private void OnPlayClicked()
    {
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);
        if (profileSelectionMenu != null) profileSelectionMenu.OpenMenu();
    }

    private void OnBackFromProfileMenu()
    {
        if (profileSelectionMenu != null) profileSelectionMenu.CloseMenu();
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
    }

    private void HandleProfileSelection(int slotIndex, bool isNewGame, string saveId)
    {
        if (isNewGame)
        {
            // Flow: Slot Selected (Empty) -> Close Slot Menu -> Open Name Input -> Start Game
            if (profileSelectionMenu != null) profileSelectionMenu.CloseMenu();
            
            // Open Name Input
            if (nameInputWindow != null)
            {
                nameInputWindow.Show(
                    onConfirm: (characterName) => StartNewGameProcess(slotIndex, characterName),
                    onCancel: () => 
                    {
                        // Cancelled Name Input -> Return to Profile Menu
                        if (profileSelectionMenu != null) profileSelectionMenu.OpenMenu();
                    }
                );
            }
            else
            {
                // Fallback
                StartNewGameProcess(slotIndex, "Player");
            }
        }
        else
        {
            // Flow: Slot Selected (Occupied) -> Load Game
            SaveLoadSystem.Instance.LoadGame(saveId);
        }
    }

    private void StartNewGameProcess(int slotIndex, string characterName)
    {
        // 1. Prepare New Game (Resets Data + Sets isNewGameTransition = true)
        // We pass 'false' so it doesn't load the scene immediately.
        SaveLoadSystem.Instance.NewGame(false);

        // 2. Set the custom name and SLOT INDEX
        SaveLoadSystem.Instance.gameData.CharacterName = characterName;
        SaveLoadSystem.Instance.gameData.SaveSlotIndex = slotIndex; 

        // 3. Load the Scene
        SceneLoader loader = FindFirstObjectByType<SceneLoader>();
        
        if (loader != null)
        {
            LoadGameScene(loader);
        }
        else
        {
            // Fallback: Use standard SceneManager if SceneLoader isn't found
            UnityEngine.SceneManagement.SceneManager.LoadScene(SaveLoadSystem.Instance.gameData.CurrentLevelName);
        }
    }

    private async void LoadGameScene(SceneLoader loader)
    {
        await loader.LoadSceneGroup(gameSceneGroupIndex);
    }

    private void OnQuitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}