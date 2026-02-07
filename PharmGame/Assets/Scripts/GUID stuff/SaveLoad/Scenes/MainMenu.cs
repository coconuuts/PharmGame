using UnityEngine;
using UnityEngine.UI;
using Systems.Persistence;
using System.Linq;
using Systems.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton; // "Load Game"
    [SerializeField] private Button quitButton;

    [Header("Scene Configuration")] 
    [SerializeField] private int gameSceneGroupIndex = 1; 

    private void Start()
    {
        // 1. Hook up button events
        newGameButton.onClick.AddListener(OnNewGameClicked);
        continueButton.onClick.AddListener(OnContinueClicked);
        quitButton.onClick.AddListener(OnQuitClicked);

        // 2. check for saves to enable/disable the Continue button
        // We use a small delay or check in Start to ensure SaveLoadSystem is ready
        if (SaveLoadSystem.HasInstance)
        {
            var saves = SaveLoadSystem.Instance.GetAllSaves();
            continueButton.interactable = saves.Any();
        }
        else
        {
            // If the system isn't found (e.g. testing menu scene alone), disable continue
            continueButton.interactable = false;
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
            // For a foundational menu, we just load the most recent (or first found) save.
            // Later, you can build a save slot selector UI here.
            string saveToLoad = saves.First(); 
            SaveLoadSystem.Instance.LoadGame(saveToLoad);
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