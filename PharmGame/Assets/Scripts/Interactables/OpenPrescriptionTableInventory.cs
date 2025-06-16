// --- START OF FILE OpenPrescriptionTableInventory.cs ---

using UnityEngine;
using Systems.Interaction; // Needed for IInteractable, InteractionResponse, and the new InteractionManager
using Systems.GameStates; // Needed for PromptEditor

public class OpenPrescriptionTableInventory : MonoBehaviour, IInteractable
{
    [Tooltip("The CraftingStation component associated with this interactable object.")]
    [SerializeField] private Systems.Inventory.CraftingStation craftingStation; // Reference the CraftingStation

    [Tooltip("The text to display in the interaction prompt.")]
    [SerializeField] private string interactionPrompt = "Access Prescription Table (E)"; // Default prompt

    [Tooltip("Should this interactable be enabled by default when registered?")]
    [SerializeField] private bool enableOnStart = true;

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
            // REMOVED: enabled = false; // InteractionManager will handle initial enabled state
        }

        // --- NEW: Register with the singleton InteractionManager ---
        if (Systems.Interaction.InteractionManager.Instance != null) // Use full namespace if needed
        {
            Systems.Interaction.InteractionManager.Instance.RegisterInteractable(this);
        }
        else
        {
            // This error is critical as the component won't be managed
            Debug.LogError($"OpenPrescriptionTableInventory on {gameObject.name}: InteractionManager.Instance is null in Awake! Cannot register.", this);
            // Optionally disable here if registration is absolutely required for function
            // enabled = false;
        }
        // --- END NEW ---
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

    // --- NEW: OnDestroy Method for Unregistration ---
    private void OnDestroy()
    {
         // --- NEW: Unregister from the singleton InteractionManager ---
         if (Systems.Interaction.InteractionManager.Instance != null)
         {
             Systems.Interaction.InteractionManager.Instance.UnregisterInteractable(this);
         }
         // --- END NEW ---
    }
    // --- END NEW ---
}
// --- END OF FILE OpenPrescriptionTableInventory.cs ---