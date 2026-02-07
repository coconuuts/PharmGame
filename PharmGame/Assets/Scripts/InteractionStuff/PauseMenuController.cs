using UnityEngine;
using UnityEngine.UI;
using Systems.SceneManagement; // For SceneLoader
using Systems.GameStates;    // For MenuManager

namespace Systems.UI
{
    public class PauseMenuController : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Scene Configuration")]
        [Tooltip("The Scene Group Index for the Main Menu defined in your SceneLoader (usually 0).")]
        [SerializeField] private int mainMenuSceneGroupIndex = 0;

        private void Start()
        {
            // Attach listeners safely
            if (resumeButton != null)
                resumeButton.onClick.AddListener(OnResumeClicked);
            else
                Debug.LogWarning("PauseMenuController: Resume Button not assigned.");

            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            else
                Debug.LogWarning("PauseMenuController: Main Menu Button not assigned.");
        }

        private void OnResumeClicked()
        {
            // Let the MenuManager handle the state transition back to Playing
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.ClosePauseMenu();
            }
        }

        private async void OnMainMenuClicked()
        {
            // 1. IMPORTANT: Unpause time immediately. 
            // If we load the menu while Time.timeScale is 0, animations in the menu might not play.
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
    }
}