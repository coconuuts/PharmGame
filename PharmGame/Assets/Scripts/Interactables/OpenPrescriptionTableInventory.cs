using UnityEngine;
using Systems.Interaction; 

public class OpenPrescriptionTableInventory : MonoBehaviour, IInteractable
{
    [Tooltip("The CraftingStation component associated with this interactable object.")]
    [SerializeField] private Systems.Inventory.CraftingStation craftingStation; // Reference the CraftingStation

    [Tooltip("The text to display in the interaction prompt.")]
    [SerializeField] private string interactionPrompt = "Access Prescription Table (E)"; // Default prompt

    [Header("Prompt Settings")]
    public Vector3 textPromptOffset = Vector3.zero;
    public Vector3 textPromptRotationOffset = Vector3.zero;

    // Implement the IInteractable properties
    public string InteractionPrompt => interactionPrompt;


    // --- MonoBehaviour Lifecycle ---
    private void Awake()
    {
        // Basic validation
        if (craftingStation == null)
        {
            Debug.LogError($"OpenPrescriptionTableInventory ({gameObject.name}): Crafting Station reference is not assigned in the inspector!", this);
            enabled = false; // Disable component if not configured
        }
    }

    // --- IInteractable Implementation ---

    /// <summary>
    /// Called by PlayerInteractionManager when the player looks at this object.
    /// Activates the prompt UI.
    /// </summary>
    public void ActivatePrompt()
    {
        if (PromptEditor.Instance != null)
        {
            // Use the generic prompt settings fields
            PromptEditor.Instance.DisplayPrompt(transform, InteractionPrompt, textPromptOffset, textPromptRotationOffset);
        }
        else
        {
            Debug.LogWarning($"OpenPrescriptionTableInventory ({gameObject.name}): PromptEditor.Instance is null. Cannot display prompt.");
        }
    }

    /// <summary>
    /// Called by PlayerInteractionManager when the player looks away.
    /// Deactivates the prompt UI.
    /// </summary>
    public void DeactivatePrompt()
    {
        if (PromptEditor.Instance != null)
        {
            PromptEditor.Instance.HidePrompt();
        }
    }

    /// <summary>
    /// Runs the object's specific interaction logic.
    /// Changed to return InteractionResponse.
    /// </summary>
    /// <returns>An InteractionResponse object describing the result of the interaction.</returns>
    public InteractionResponse Interact()
    {
        Debug.Log($"OpenPrescriptionTableInventory ({gameObject.name}): Interact called. Returning OpenCraftingResponse.", this);

        if (craftingStation == null)
        {
            Debug.LogError($"OpenPrescriptionTableInventory ({gameObject.name}): Crafting Station reference is null! Cannot create response.", this);
            // Return null response on error, although the PlayerInteractionManager logs a warning
            return null;
        }

        return new OpenCraftingResponse(craftingStation);
    }

    private void OnValidate()
    {
        if (craftingStation == null)
        {
            Debug.LogWarning($"OpenPrescriptionTableInventory ({gameObject.name}): Crafting Station reference is not assigned.", this);
        }
    }
}