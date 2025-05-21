using UnityEngine;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

namespace Systems.Minigame.UI
{
    // --- Serializable Classes (Keep the same) ---
    [System.Serializable]
    public class NamedGameObject
    {
        [Tooltip("The unique name to identify this GameObject.")]
        public string Name;
        [Tooltip("The GameObject reference.")]
        public GameObject Reference;
    }

    [System.Serializable]
    public class NamedTextMeshProUGUI
    {
        [Tooltip("The unique name to identify this TextMeshProUGUI.")]
        public string Name;
        [Tooltip("The TextMeshProUGUI reference.")]
        public TextMeshProUGUI Reference;
    }

    [System.Serializable]
    public class NamedButton
    {
        [Tooltip("The unique name to identify this Button.")]
        public string Name;
        [Tooltip("The Button reference.")]
        public Button Reference;
    }

    // --- The Main UI Handler Class (Modified) ---

    /// <summary>
    /// General component to manage a set of UI elements for a minigame.
    /// Stores references by type and name and provides lookup methods.
    /// Handles basic show/hide, text updates, and a configurable finish button click event.
    /// Designed to be explicitly initialized for each minigame session after its initial Awake setup.
    /// </summary>
    public class MinigameUIHandler : MonoBehaviour
    {
        [Header("Configure UI Elements (Drag References Here)")]
        [Tooltip("List of GameObjects to manage, accessed by their configured name.")]
        public List<NamedGameObject> gameObjectConfig = new List<NamedGameObject>();

        [Tooltip("List of TextMeshProUGUI elements to manage, accessed by their configured name.")]
        public List<NamedTextMeshProUGUI> textMeshProConfig = new List<NamedTextMeshProUGUI>();

        [Tooltip("List of Buttons to manage, accessed by their configured name.")]
        public List<NamedButton> buttonConfig = new List<NamedButton>();

        [Header("Core Element Names (Used by this script's methods)")]
        [Tooltip("The name (from GameObject Config) of the root element to show/hide.")]
        public string uiRootName = "UIRoot"; // Default name, can be changed

        [Tooltip("The name (from TextMeshProUGUI Config) of the text element to update.")]
        public string countTextName = "CountText"; // Default name

        [Tooltip("The name (from Button Config) of the button triggering the finish event.")]
        public string finishButtonName = "FinishButton"; // Default name

        // --- Internal Dictionaries for Fast Runtime Lookup ---
        // These are populated ONCE in Awake
        private Dictionary<string, GameObject> gameObjectReferences = new Dictionary<string, GameObject>();
        private Dictionary<string, TextMeshProUGUI> textMeshProReferences = new Dictionary<string, TextMeshProUGUI>();
        private Dictionary<string, Button> buttonReferences = new Dictionary<string, Button>();

        // --- Cached References for Core Elements ---
        // These are found EACH TIME RuntimeInitialize is called
        private GameObject uiRoot;
        private TextMeshProUGUI countText;
        private Button finishButton;

        /// <summary>
        /// Event triggered when the finish button is clicked.
        /// Handled by the specific minigame script.
        /// </summary>
        public event Action OnFinishButtonClicked;

        // Flag to check if RuntimeInitialize has been called for the current session
        private bool isSessionInitialized = false; // Renamed for clarity

        /// <summary>
        /// Performs initial setup (populating dictionaries) when the script instance awakens.
        /// This happens ONCE per script instance lifetime.
        /// </summary>
        private void Awake()
        {
            InitializeDictionaries();
            Debug.Log($"MinigameUIHandler ({gameObject.name}): Dictionaries initialized in Awake.", this);
            // We don't set isSessionInitialized here, as it's for *runtime session*
        }

        /// <summary>
        /// Populates the internal dictionaries from the Inspector configured lists.
        /// Called once in Awake.
        /// </summary>
        private void InitializeDictionaries()
        {
             // Only populate if not already done (should only happen once in Awake anyway)
             if (gameObjectReferences.Count > 0 || textMeshProReferences.Count > 0 || buttonReferences.Count > 0)
             {
                  Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): InitializeDictionaries called when dictionaries are already populated.", this);
                  return;
             }


            gameObjectReferences.Clear(); // Should be empty first time, but good practice
            textMeshProReferences.Clear();
            buttonReferences.Clear();

            // Populate GameObject dictionary
            foreach (var item in gameObjectConfig)
            {
                 if (string.IsNullOrEmpty(item.Name)) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): GameObject config entry has no name.", this); continue; }
                 if (item.Reference == null) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): GameObject config entry '{item.Name}' has no reference assigned.", this); continue; }
                 if (gameObjectReferences.ContainsKey(item.Name)) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): Duplicate GameObject name '{item.Name}' found. Only the first will be kept.", this); continue; }
                 gameObjectReferences[item.Name] = item.Reference;
            }

            // Populate TextMeshProUGUI dictionary
            foreach (var item in textMeshProConfig)
             {
                 if (string.IsNullOrEmpty(item.Name)) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): TextMeshProUGUI config entry has no name.", this); continue; }
                 if (item.Reference == null) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): TextMeshProUGUI config entry '{item.Name}' has no reference assigned.", this); continue; }
                 if (textMeshProReferences.ContainsKey(item.Name)) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): Duplicate TextMeshProUGUI name '{item.Name}' found. Only the first will be kept.", this); continue; }
                 textMeshProReferences[item.Name] = item.Reference;
             }

            // Populate Button dictionary
            foreach (var item in buttonConfig)
            {
                 if (string.IsNullOrEmpty(item.Name)) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): Button config entry has no name.", this); continue; }
                 if (item.Reference == null) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): Button config entry '{item.Name}' has no reference assigned.", this); continue; }
                 if (buttonReferences.ContainsKey(item.Name)) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): Duplicate Button name '{item.Name}' found. Only the first will be kept.", this); continue; }
                 buttonReferences[item.Name] = item.Reference;
            }
        }

        /// <summary>
        /// Initializes the handler for a specific minigame session.
        /// Finds core references and hooks up button listeners.
        /// Should be called by the minigame script when a new session starts.
        /// </summary>
        public void RuntimeInitialize()
        {
            if (isSessionInitialized)
            {
                 Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): RuntimeInitialize called, but session is already initialized. Performing cleanup and re-initializing.", this);
                 PerformCleanup(); // Clean up the old session before starting a new one
            }

            // Ensure dictionaries were populated (they should be from Awake)
             if (gameObjectReferences.Count == 0 && gameObjectConfig.Count > 0 ||
                 textMeshProReferences.Count == 0 && textMeshProConfig.Count > 0 ||
                 buttonReferences.Count == 0 && buttonConfig.Count > 0)
             {
                  Debug.LogError($"MinigameUIHandler ({gameObject.name}): Dictionaries were not populated in Awake! Calling InitializeDictionaries now, but check script execution order.", this);
                  InitializeDictionaries(); // Emergency fallback
             }


            // --- Find Core Elements using configured names ---
            uiRoot = GetGameObject(uiRootName);
            countText = GetTextMeshPro(countTextName);
            finishButton = GetButton(finishButtonName);

            if (uiRoot == null)
            {
                 Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): UI Root GameObject with name '{uiRootName}' not found or not assigned. Show/Hide methods will do nothing.", this);
            } else {
                 // Ensure it starts hidden for the new session
                 uiRoot.SetActive(false);
            }

            if (countText == null)
            {
                Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): Count TextMeshProUGUI with name '{countTextName}' not found or not assigned. UpdateText method will do nothing.", this);
            }

            // --- Hook up Finish Button Listener ---
            if (finishButton != null)
            {
                 // Remove listeners first just in case (though PerformCleanup should handle this)
                 finishButton.onClick.RemoveAllListeners(); // Use RemoveAllListeners here for robustness
                 finishButton.onClick.AddListener(HandleFinishButtonClick);
                 Debug.Log($"MinigameUIHandler ({gameObject.name}): Hooked up listener for finish button '{finishButtonName}'.", this);
            } else {
                 Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): Finish Button with name '{finishButtonName}' not found or not assigned. Button click event will not be hooked up.", this);
            }

            isSessionInitialized = true; // Mark as initialized for this session
            Debug.Log($"MinigameUIHandler ({gameObject.name}): Runtime session initialization complete.", this);
        }


        // --- Public Methods for Accessing Elements by Name (Keep the same, add session check) ---

        /// <summary>
        /// Gets a GameObject reference by its configured name.
        /// </summary>
        public GameObject GetGameObject(string name)
        {
            // We allow getting references even if session isn't initialized,
            // as minigames might need setup references before starting the UI session.
            // However, log a warning if the session wasn't ready for UI interaction.
             if (!isSessionInitialized) Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): GetGameObject('{name}') called before RuntimeInitialize.", this);

            if (string.IsNullOrEmpty(name)) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): Attempted to get GameObject with null or empty name.", this); return null; }
            if (gameObjectReferences.TryGetValue(name, out GameObject reference))
            {
                return reference;
            }
            Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): GameObject with name '{name}' not found in references.", this);
            return null;
        }

        /// <summary>
        /// Gets a TextMeshProUGUI reference by its configured name.
        /// </summary>
        public TextMeshProUGUI GetTextMeshPro(string name)
        {
             if (!isSessionInitialized) Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): GetTextMeshPro('{name}') called before RuntimeInitialize.", this);

             if (string.IsNullOrEmpty(name)) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): Attempted to get TextMeshProUGUI with null or empty name.", this); return null; }
            if (textMeshProReferences.TryGetValue(name, out TextMeshProUGUI reference))
            {
                return reference;
            }
            Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): TextMeshProUGUI with name '{name}' not found in references.", this);
            return null;
        }

        /// <summary>
        /// Gets a Button reference by its configured name.
        /// </summary>
        public Button GetButton(string name)
        {
             if (!isSessionInitialized) Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): GetButton('{name}') called before RuntimeInitialize.", this);

             if (string.IsNullOrEmpty(name)) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): Attempted to get Button with null or empty name.", this); return null; }
            if (buttonReferences.TryGetValue(name, out Button reference))
            {
                return reference;
            }
            Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): Button with name '{name}' not found in references.", this);
            return null;
        }

        // --- Original Functionality (Now Uses Session Initialization Check) ---

        /// <summary>
        /// Shows the UI panel configured as the root element.
        /// </summary>
        public void Show()
        {
            // Check for session initialization instead of just 'isInitialized'
            if (!isSessionInitialized) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): Show called before RuntimeInitialize.", this); return; }
            if (uiRoot != null)
            {
                uiRoot.SetActive(true);
                // Debug.Log($"MinigameUIHandler ({gameObject.name}): Showing UI (root: {uiRootName}).", this);
            }
        }

        /// <summary>
        /// Hides the UI panel configured as the root element.
        /// </summary>
        public void Hide()
        {
            // Check for session initialization instead of just 'isInitialized'
            if (!isSessionInitialized) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): Hide called before RuntimeInitialize.", this); return; }
            if (uiRoot != null)
            {
                uiRoot.SetActive(false);
                // Debug.Log($"MinigameUIHandler ({gameObject.name}): Hiding UI (root: {uiRootName}).", this);
            }
        }

        /// <summary>
        /// Updates the text display element configured as the count text.
        /// </summary>
        /// <param name="text">The string to display.</param>
        public void UpdateText(string text)
        {
             // Check for session initialization instead of just 'isInitialized'
             if (!isSessionInitialized) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): UpdateText called before RuntimeInitialize.", this); return; }
            if (countText != null)
            {
                countText.text = text;
            }
             else Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): UpdateText called but countText reference is null ('{countTextName}').", this);
        }

        /// <summary>
        /// Sets the interactable state of the button configured as the finish button.
        /// </summary>
        public void SetFinishButtonInteractable(bool interactable)
        {
             // Check for session initialization instead of just 'isInitialized'
             if (!isSessionInitialized) { Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): SetFinishButtonInteractable called before RuntimeInitialize.", this); return; }
            if (finishButton != null)
            {
                finishButton.interactable = interactable;
            }
             else Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): SetFinishButtonInteractable called but finishButton reference is null ('{finishButtonName}').", this);
        }

        /// <summary>
        /// Internal handler for the finish button click.
        /// </summary>
        private void HandleFinishButtonClick()
        {
             // Allow click event even if isSessionInitialized becomes false mid-minigame?
             // Probably not, if the session is no longer active, button clicks should ideally stop being processed.
             if (!isSessionInitialized)
             {
                  Debug.LogWarning($"MinigameUIHandler ({gameObject.name}): Finish button clicked but session is not initialized. Ignoring.", this);
                  return;
             }
            Debug.Log($"MinigameUIHandler ({gameObject.name}): Finish button '{finishButtonName}' clicked (internal handler).", this);
            OnFinishButtonClicked?.Invoke(); // Trigger the public event
        }

        /// <summary>
        /// Cleanup method to remove button listeners and clear the event.
        /// Should be called by the minigame script when the session ends.
        /// </summary>
        public void PerformCleanup()
        {
            if (!isSessionInitialized)
            {
                 // Already cleaned up or never initialized for a session
                 Debug.Log($"MinigameUIHandler ({gameObject.name}): PerformCleanup called but session was not initialized. Skipping cleanup.", this);
                 return;
            }

            Hide(); // Ensure UI is hidden for the session

            // Remove the listener hooked up in RuntimeInitialize
            if (finishButton != null)
            {
                finishButton.onClick.RemoveListener(HandleFinishButtonClick);
            }

             // Clear the event subscribers
             if (OnFinishButtonClicked != null)
             {
                  foreach (Delegate d in OnFinishButtonClicked.GetInvocationList())
                  {
                      OnFinishButtonClicked -= (Action)d;
                  }
             }
             OnFinishButtonClicked = null;

            // Null out cached references - important for the next session's RuntimeInitialize
            uiRoot = null;
            countText = null;
            finishButton = null;

            isSessionInitialized = false; // Mark as uninitialized for the next session
            Debug.Log($"MinigameUIHandler ({gameObject.name}): Performed session cleanup.", this);
        }

         // Optional: Add OnDestroy to clean up button listeners if the script is destroyed
         private void OnDestroy()
         {
              PerformCleanup(); // Clean up button listeners, event, and reset flag
              // Note: Dictionaries are *not* cleared here, they persist for the script instance.
         }
    }
}