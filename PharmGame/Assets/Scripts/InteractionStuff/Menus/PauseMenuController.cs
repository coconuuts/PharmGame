using UnityEngine;
using UnityEngine.UI;
using Systems.GameStates;
using Systems.SceneManagement;

namespace Systems.UI
{
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [Tooltip("The panel containing Resume, Save Game, Quit buttons.")]
        [SerializeField] private GameObject mainButtonsPanel;
        
        [Tooltip("The panel containing the Save List.")]
        [SerializeField] private GameObject saveMenuPanel;

        [Tooltip("The panel containing the Load List.")]
        [SerializeField] private GameObject loadMenuPanel;

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button saveGameButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button optionsButton; //TODO

        [Header("Scene Configuration")]
        [Tooltip("The Scene Group Index for the Main Menu defined in your SceneLoader (usually 0).")]
        [SerializeField] private int mainMenuSceneGroupIndex = 0;

        [Header("External References")]
        [SerializeField] private SaveGameMenuController saveMenuController;
        [SerializeField] private LoadGameMenuController loadMenuController;

        private void OnEnable()
        {
            ShowMainButtons();
        }

        private void Start()
        {
            // Setup Listeners
            if (resumeButton != null) 
                resumeButton.onClick.AddListener(OnResumeClicked);
                
            if (saveGameButton != null) 
                saveGameButton.onClick.AddListener(OnSaveGameClicked);

            if (loadGameButton != null) 
            loadGameButton.onClick.AddListener(OnLoadGameClicked);

            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);
                
            if (quitButton != null) 
                quitButton.onClick.AddListener(OnQuitClicked);
        }

        private void ShowMainButtons()
        {
            if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
            if (saveMenuPanel != null) saveMenuPanel.SetActive(false);
            if (loadMenuPanel != null) loadMenuPanel.SetActive(false);
        }

        private void OnResumeClicked()
        {
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.ClosePauseMenu();
            }
        }

        private void OnSaveGameClicked()
        {
            // 1. Hide the Main Buttons
            if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);

            // 2. Open the Save Menu (which will enable itself)
            if (saveMenuController != null)
            {
                saveMenuController.OpenMenu();
            }
        }

        private void OnLoadGameClicked()
        {
            // 1. Hide Main Buttons
            if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);

            // 2. Open Load Menu
            if (loadMenuController != null)
            {
                loadMenuController.OpenMenu();
            }
        }

        private async void OnMainMenuClicked()
        {
            Time.timeScale = 1f;

            // 2. Find the SceneLoader to handle the transition
            SceneLoader sceneLoader = FindFirstObjectByType<SceneLoader>();

            if (sceneLoader != null)
            {
                // 3. Load the Main Menu Scene Group
                // This will unload the current game scene and load the menu.
                await sceneLoader.LoadSceneGroup(mainMenuSceneGroupIndex);
            }
            else
            {
                Debug.LogError("PauseMenuController: SceneLoader not found! Cannot return to Main Menu.");
            }
        }

        private void OnQuitClicked()
        {
            // Logic for quitting (e.g., to Main Menu or Desktop)
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}