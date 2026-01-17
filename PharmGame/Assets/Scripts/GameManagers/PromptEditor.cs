using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class PromptEditor : MonoBehaviour
{
    [Header("Prompt Settings")]
    public Vector3 defaultTextPromptOffset = Vector3.zero; // Default offset
    public Vector3 defaultTextPromptRotationOffset = Vector3.zero; // Default rotation offset
    private Quaternion initialPromptRotation;
    private TMP_Text promptText; // Reference to the shared prompt

    [Tooltip("The screen-space GameObject containing the TMP_Text for NPC interaction prompts.")]
    [SerializeField] private GameObject screenSpaceNPCPrompt; // Renamed for clarity
    [Tooltip("The TMP Text component *inside* the screen-space NPC prompt GameObject.")] // NEW FIELD
    [SerializeField] private TMP_Text screenSpaceNPCText;
    public static bool HasInstance => instance != null;


    private static PromptEditor instance; // Singleton instance

    private void Awake()
    {
        // Singleton pattern to ensure only one instance exists
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject); // Destroy duplicate
            return;
        }

        // Initial setup
        RefreshReferences();
    }
    private void OnDestroy()
    {
        // If this object is the registered instance, clear the reference
        if (instance == this)
        {
            instance = null;
        }
    }

    // --- Subscribe to Scene Loading ---
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshReferences();
    }

    private void RefreshReferences()
    {
        GameObject promptObject = GameObject.FindGameObjectWithTag("InteractionPrompt");
        if (promptObject != null)
        {
            promptText = promptObject.GetComponent<TMP_Text>();
            if (promptText == null)
            {
                Debug.LogError("PromptEditor: GameObject with tag 'InteractionPrompt' does not have a TMP_Text component.");
            }
            else
            {
                // Re-capture initial rotation if needed
                initialPromptRotation = promptText.transform.rotation;
                
                // Ensure prompts are hidden on load
                HidePrompt();
            }
        }
        else
        {
            Debug.LogWarning("PromptEditor: No GameObject found with the tag 'InteractionPrompt' in this scene.");
        }
        
        // Note: screenSpaceNPCPrompt is a serialized field. 
        // If that UI is also destroyed/recreated on load, you might need to find it dynamically too 
        // or ensure this PromptEditor is part of the UI prefab that gets re-instantiated.
    }

    private void Start()
    {
        
    }

    /// <summary>
    /// Displays the prompt text at the specified position and rotation, using object-specific offsets.
    /// </summary>
    /// <param name="targetTransform">The transform of the interactable object.</param>
    /// <param name="promptMessage">The text to display on the prompt.</param>
    /// <param name="positionOffset">The position offset for this specific object.</param>
    /// <param name="rotationOffset">The rotation offset for this specific object.</param>
    public void DisplayPrompt(Transform targetTransform, string promptMessage, Vector3 positionOffset, Vector3 rotationOffset)
    {
        if (promptText == null)
        {
            RefreshReferences();
            Debug.LogError("PromptEditor: promptText is null. Cannot display prompt.");
            return; // Exit if promptText is not assigned
        }

        promptText.transform.position = targetTransform.position + positionOffset;
        promptText.transform.rotation = initialPromptRotation * Quaternion.Euler(rotationOffset);
        promptText.text = promptMessage;
        promptText.enabled = true;
    }

    /// <summary>
    /// Displays the prompt text at the specified position and rotation, using the default offset.
    /// </summary>
    /// <param name="targetTransform">The transform of the interactable object.</param>
    /// <param name="promptMessage">The text to display on the prompt.</param>
    public void DisplayPrompt(Transform targetTransform, string promptMessage)
    {
        DisplayPrompt(targetTransform, promptMessage, defaultTextPromptOffset, defaultTextPromptRotationOffset);
    }

    /// <summary>
    /// Hides the prompt text.
    /// </summary>
    public void HidePrompt()
    {
        if (promptText != null)
        {
            promptText.text = "";
            promptText.enabled = false;
        }
    }

    public static PromptEditor Instance
    {
        get
        {
            if (instance == null)
            {
                Debug.LogWarning("PromptEditor Instance is null.  There needs to be one in the scene.");
            }
            return instance;
        }
    }

    /// <summary>
    /// Sets the active state and text of the SCREEN-SPACE NPC prompt.
    /// </summary>
    /// <param name="isActive">True to show the prompt, false to hide it.</param>
    /// <param name="message">The text to display. Only used if isActive is true.</param>
    public void SetScreenSpaceNPCPromptActive(bool isActive, string message = "")
    {
        if (screenSpaceNPCPrompt != null)
        {
            screenSpaceNPCPrompt.SetActive(isActive);
            if (isActive && screenSpaceNPCText != null)
            {
                screenSpaceNPCText.text = message;
            }
            else if (!isActive && screenSpaceNPCText != null) // Clear text when hiding
            {
                screenSpaceNPCText.text = "";
            }
        }
        else
        {
            Debug.LogWarning("PromptEditor: Screen Space NPC Prompt GameObject is not assigned. Cannot set active state or text.");
        }

        // IMPORTANT: When activating the screen-space prompt, ensure the world-space one is hidden.
        // And vice-versa. This prevents overlap.
        if (isActive && promptText != null && promptText.enabled)
        {
            HidePrompt(); // Hide world-space if screen-space is activated
        }
        else if (!isActive && promptText != null && !promptText.enabled)
        {
            // If screen-space is being hidden, we might need to re-evaluate showing a world-space prompt
            // if the player is now looking at something else. This is handled by PlayerInteractionManager's raycast loop.
            // We don't need to explicitly re-activate the world-space one here.
        }
    }
}
