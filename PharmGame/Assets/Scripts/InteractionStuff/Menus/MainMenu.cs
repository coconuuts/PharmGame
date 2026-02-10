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
            // If a modal is active (like the Delete Confirmation), we let the ModalWindowUI handle the input.
            // We do NOT want to close the Load Menu underneath it.
            if (SimpleModalManager.Instance != null && SimpleModalManager.Instance.IsModalActive)
            {
                return;
            }

            // 2. Sub-menu Check
            // If the Main Buttons are hidden, it means we are in a sub-menu (like Load Game)
            if (mainButtonsPanel != null && !mainButtonsPanel.activeSelf)
            {
                if (loadMenuController != null)
                {
                    // This calls CloseMenu(), which fires OnMenuClosed, which triggers RefreshMenuState()
                    loadMenuController.CloseMenu();
                }
            }
        }
    }

    private void OnNewGameClicked()
    {
        SaveLoadSystem.Instance.ResetGameData();

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