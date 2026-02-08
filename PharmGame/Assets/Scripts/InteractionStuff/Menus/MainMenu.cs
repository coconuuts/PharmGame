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

        RefreshMenuState();
    }

    private void OnEnable()
    {
        // Ensure state is correct if we return to this menu from a sub-menu
        RefreshMenuState();
    }

    private void RefreshMenuState()
    {
        bool hasSaves = false;

        // Use HasInstance to avoid creating the singleton if it doesn't exist yet (though it should)
        if (SaveLoadSystem.HasInstance)
        {
            var saves = SaveLoadSystem.Instance.GetAllSaves();
            hasSaves = saves.Any();
        }

        // Apply state: Enable/Show only if we found saves
        if (continueButton != null && hasSaves) 
        {
            continueButton.gameObject.SetActive(true);
        }

        if (loadGameButton != null && hasSaves)
        {
            loadGameButton.interactable = true;
        }
    }
    
    private void Update()
    {
        // Check for Escape key press
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // If the Main Buttons are hidden, it means we are in a sub-menu (like Load Game)
            if (mainButtonsPanel != null && !mainButtonsPanel.activeSelf)
            {
                // If the Load Menu controller is assigned, tell it to close.
                // This will re-enable the mainButtonsPanel automatically via its CloseMenu logic.
                if (loadMenuController != null)
                {
                    loadMenuController.CloseMenu();
                }
            }
        }
    }

    private void OnNewGameClicked()
    {
        // 1. Reset the data ONLY (Do not load scene yet)
        SaveLoadSystem.Instance.ResetGameData();

        // 2. Find the SceneLoader and use it to load the group with the loading screen
        SceneLoader loader = FindFirstObjectByType<SceneLoader>();
        
        if (loader != null)
        {
            // We use an async wrapper or just fire-and-forget here
            LoadGameScene(loader);
        }
        else
        {
            Debug.LogError("SceneLoader not found! Falling back to instant load.");
            SaveLoadSystem.Instance.NewGame();
        }
    }

    private async void LoadGameScene(SceneLoader loader)
    {
        // This triggers the loading screen, progress bar, and additive loading
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