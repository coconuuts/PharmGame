// --- START OF FILE PlayerUIPopups.cs ---

using UnityEngine;
using TMPro; // Required for TMP_Text
using System.Collections; // Required for using Coroutines
using System.Collections.Generic; // Required for List
using Game.Prescriptions; // Needed for PrescriptionOrder (if still used elsewhere, otherwise remove)

public class PlayerUIPopups : MonoBehaviour
{
    // --- Configuration Class for Each Popup Type ---
    [System.Serializable] // Make this class visible and editable in the Inspector
    public class UIPopupConfig
    {
        [Tooltip("A unique name for this popup type (e.g., 'InvalidItem', 'WrongPrescription', 'PrescriptionOrder').")]
        public string popupName;

        [Tooltip("The root GameObject for this specific popup UI.")]
        public GameObject rootObject;

        [Tooltip("Optional: The TMP Text component within this popup to display messages. Leave null if no text is needed.")]
        public TMPro.TMP_Text textComponent;

        [Tooltip("If true, the popup will automatically hide after the duration. If false, it must be hidden manually.")]
        public bool isTimed = false;

        [Tooltip("Duration (in seconds) the popup stays visible if 'Is Timed' is true.")]
        [SerializeField] private float duration = 3f; // Default duration

        // Public property for duration, allows getting the private field
        public float Duration => duration;

        // --- Internal state managed by the PlayerUIPopups script ---
        [HideInInspector] // Hide this in the Inspector as it's managed by code
        public Coroutine activeCoroutine; // To keep track of the running coroutine for timed popups
    }
    // --- End Configuration Class ---

    [Tooltip("List of all configurable UI popups.")]
    [SerializeField] private List<UIPopupConfig> popups = new List<UIPopupConfig>();

    // --- REFACTORED SINGLETON ---
    // This public static property provides access to the singleton instance.
    // The 'private set' ensures that only this class can assign the instance.
    // This is much faster and safer than using FindObjectOfType.
    public static PlayerUIPopups Instance { get; private set; }

    private void Awake()
    {
        // --- SINGLETON INITIALIZATION ---
        // This is the core of the robust singleton pattern.
        // It guarantees 'Instance' is set here and only here.
        if (Instance == null)
        {
            Instance = this;
            // Optional: DontDestroyOnLoad(gameObject); // Uncomment if you need this object across scenes
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Multiple PlayerUIPopups instances found. Destroying duplicate.", gameObject);
            Destroy(gameObject); // Destroy duplicate
            return; // Exit to prevent further execution in this duplicate
        }
    }

    public void Start()
    {
        // Ensure all configured popups are initially inactive
        foreach (var config in popups)
        {
            if (config.rootObject != null)
            {
                config.rootObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning($"PlayerUIPopups: Popup config '{config.popupName}' has no Root Object assigned!", this);
            }
        }
    }

    // The old 'Instance' property with FindObjectOfType has been completely removed.
    // The new property above handles everything.

    /// <summary>
    /// Finds a popup configuration by its unique name.
    /// </summary>
    private UIPopupConfig GetPopupConfig(string popupName)
    {
        foreach (var config in popups)
        {
            if (config.popupName == popupName)
            {
                return config;
            }
        }
        Debug.LogError($"PlayerUIPopups: Popup config with name '{popupName}' not found. Make sure it's added and named correctly in the Inspector.", this);
        return null; // Return null if not found
    }

    /// <summary>
    /// Displays a UI popup based on its configured name.
    /// </summary>
    /// <param name="popupName">The unique name of the popup as defined in the Inspector.</param>
    /// <param name="message">Optional: The text message to display if the popup has a text component.</param>
    /// <param name="overrideDuration">Optional: Override the configured duration for timed popups.</param>
    public void ShowPopup(string popupName, string message = null, float? overrideDuration = null)
    {
        UIPopupConfig config = GetPopupConfig(popupName);

        if (config == null || config.rootObject == null)
        {
            // Error already logged in GetPopupConfig if config is null
            if (config != null && config.rootObject == null)
            {
                 Debug.LogWarning($"PlayerUIPopups: Cannot show popup '{popupName}' because its Root Object is not assigned.", this);
            }
            return;
        }

        // Set text if a message is provided and the popup has a text component
        if (message != null)
        {
            if (config.textComponent != null)
            {
                config.textComponent.text = message;
            }
            else
            {
                Debug.LogWarning($"PlayerUIPopups: Message provided for popup '{popupName}', but it has no Text Component assigned.", this);
            }
        }

        // Stop any existing coroutine for this popup (to reset the timer if timed)
        if (config.activeCoroutine != null)
        {
            StopCoroutine(config.activeCoroutine);
            config.activeCoroutine = null; // Clear the reference
        }

        // Activate the root object
        if (!config.rootObject.activeSelf) // Only activate if not already active
        {
             config.rootObject.SetActive(true);
             Debug.Log($"PlayerUIPopups: Showing popup '{popupName}'.", this);
        }


        // If it's a timed popup, start the coroutine to hide it later
        if (config.isTimed)
        {
            float actualDuration = overrideDuration ?? config.Duration; // Use override if provided, otherwise use config duration
            if (actualDuration > 0)
            {
                 config.activeCoroutine = StartCoroutine(DisablePopUpAfterDelay(config, actualDuration));
            }
            else
            {
                Debug.LogWarning($"PlayerUIPopups: Timed popup '{popupName}' has duration 0 or less. It will not automatically hide.", this);
            }
        }
        // If not timed, it stays active until HidePopup is called.
    }

    /// <summary>
    /// Hides a UI popup based on its configured name.
    /// </summary>
    /// <param name="popupName">The unique name of the popup as defined in the Inspector.</param>
    public void HidePopup(string popupName)
    {
        UIPopupConfig config = GetPopupConfig(popupName);

        if (config == null || config.rootObject == null)
        {
             // Error already logged in GetPopupConfig if config is null
             if (config != null && config.rootObject == null)
             {
                 Debug.LogWarning($"PlayerUIPopups: Cannot hide popup '{popupName}' because its Root Object is not assigned.", this);
             }
            return;
        }

        // Stop the coroutine if it's running for this specific popup
        if (config.activeCoroutine != null)
        {
            StopCoroutine(config.activeCoroutine);
            config.activeCoroutine = null; // Clear the reference
        }

        // Deactivate the root object if it's active
        if (config.rootObject.activeSelf)
        {
             config.rootObject.SetActive(false);
             Debug.Log($"PlayerUIPopups: Hiding popup '{popupName}'.", this);
        }


        // Optional: Clear the text when hiding
        if (config.textComponent != null)
        {
            config.textComponent.text = "";
        }
    }

    /// <summary>
    /// Coroutine to wait for a specified duration and then deactivate a popup's root object.
    /// This version takes the popup config directly.
    /// </summary>
    /// <param name="config">The configuration of the popup to disable.</param>
    /// <param name="delay">The duration in seconds to wait before deactivating.</param>
    private IEnumerator DisablePopUpAfterDelay(UIPopupConfig config, float delay)
    {
        yield return new WaitForSeconds(delay);

        // After the delay, deactivate the item if it's still the same popup that was shown
        // We check rootObject != null just in case it was destroyed during the delay
        if (config != null && config.rootObject != null && config.rootObject.activeSelf)
        {
            config.rootObject.SetActive(false);
            Debug.Log($"PlayerUIPopups: Auto-hiding timed popup '{config.popupName}' after {delay} seconds.", this);
        }

        // Clear the coroutine reference *within the config* when it finishes naturally
        if (config != null)
        {
             config.activeCoroutine = null;
        }
    }
}
// --- END OF FILE PlayerUIPopups.cs ---