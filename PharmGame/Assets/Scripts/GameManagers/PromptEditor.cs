using UnityEngine;
using TMPro;

public class PromptEditor : MonoBehaviour
{
    [Header("Prompt Settings")]
    public Vector3 defaultTextPromptOffset = Vector3.zero; // Default offset
    public Vector3 defaultTextPromptRotationOffset = Vector3.zero; // Default rotation offset
    private Quaternion initialPromptRotation;
    private TMP_Text promptText; // Reference to the shared prompt

    [Tooltip("The screen-space GameObject containing the TMP_Text for NPC interaction prompts.")]
    [SerializeField] private GameObject screenSpaceNPCPrompt; // Renamed for clarity


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

        // Find the shared prompt text element.  Important to do this in Awake.
        GameObject promptObject = GameObject.FindGameObjectWithTag("InteractionPrompt");
        if (promptObject != null)
        {
            promptText = promptObject.GetComponent<TMP_Text>();
            if (promptText == null)
            {
                Debug.LogError("GameObject with tag 'InteractionPrompt' does not have a TMP_Text component.");
            }
        }
        else
        {
            Debug.LogError("No GameObject found with the tag 'InteractionPrompt'.");
        }
    }

    private void Start()
    {
        // Store the initial rotation of the prompt text element.
        if (promptText != null)
        {
            initialPromptRotation = promptText.transform.rotation;
        }
        else
        {
            Debug.LogWarning("PromptEditor: promptText is null in Start().  Make sure the InteractionPrompt tag is set and the object exists in the scene.");
        }

        if (screenSpaceNPCPrompt != null)
        {
             // Ensure the GameObject itself is inactive, not just the text component
            screenSpaceNPCPrompt.SetActive(false);
        }
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
                Debug.LogError("PromptEditor Instance is null.  There needs to be one in the scene.");
            }
            return instance;
        }
    }

        /// <summary>
    /// Sets the active state and text of the SCREEN-SPACE NPC prompt.
    /// </summary>
    public void SetScreenSpaceNPCPromptActive(bool isActive)
    {
        if (screenSpaceNPCPrompt != null)
        {
            // Activate/Deactivate the parent GameObject
            screenSpaceNPCPrompt.SetActive(isActive);
        }
    }
}
