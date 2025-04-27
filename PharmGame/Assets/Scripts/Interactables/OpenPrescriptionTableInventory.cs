using UnityEngine;
using InventoryClass = Systems.Inventory.Inventory; // Use the alias
using Systems.Interaction; // ADD THIS USING

// Ensure this script is on the same GameObject as the Collider that the PlayerInteractionManager hits
public class OpenPrescriptionTableInventory : MonoBehaviour, IInteractable
{
    [Tooltip("The GameObject that contains the FlexibleGridLayout and is the parent of the visual inventory slots.")]
    [SerializeField] private GameObject inventoryUIRoot;

    [Tooltip("The Inventory component associated with this interactable object.")]
    [SerializeField] private InventoryClass inventoryComponent; // Use the alias

    [Tooltip("The text to display in the interaction prompt.")]
    [SerializeField] private string interactionPrompt = "Access prescription table (E)"; // Default prompt
    public string InteractionPrompt => interactionPrompt;

    [SerializeField] private GameObject pillInventoryUIRoot;
    [SerializeField] private GameObject liquidInventoryUIRoot;
    [SerializeField] private GameObject inhalerInventoryUIRoot;
    [SerializeField] private GameObject insulinInventoryUIRoot;

    public void Awake()
    {
        pillInventoryUIRoot.SetActive(false);
        liquidInventoryUIRoot.SetActive(false);
        inhalerInventoryUIRoot.SetActive(false);
        insulinInventoryUIRoot.SetActive(false);
    }
   
    // --- IInteractable Implementation ---

    /// <summary>
    /// Called by PlayerInteractionManager when the player looks at this object.
    /// We don't need to activate the prompt here; the manager handles the UI.
    /// </summary>
    public void ActivatePrompt()
    {
         if (PromptEditor.Instance != null)
         {
             PromptEditor.Instance.DisplayPrompt(transform, InteractionPrompt, inventoryTextPromptOffset, inventoryTextPromptRotationOffset); // Assuming prompt offsets are part of the interactable now
         }
         else
         {
              Debug.LogWarning($"OpenInventory ({gameObject.name}): PromptEditor.Instance is null. Cannot display prompt.");
         }
    }

    /// <summary>
    /// Called by PlayerInteractionManager when the player looks away.
    /// We don't need to deactivate the prompt here; the manager handles the UI.
    /// </summary>
    public void DeactivatePrompt()
    {
         if (PromptEditor.Instance != null)
         {
             PromptEditor.Instance.HidePrompt();
         }
    }

    public void Update()
    {

    }

    /// <summary>
    /// Runs the object's specific interaction logic and returns a response describing the outcome.
    /// Changed to return InteractionResponse.
    /// </summary>
    /// <returns>An InteractionResponse object describing the result of the interaction.</returns>
    public InteractionResponse Interact() // CHANGED RETURN TYPE
    {
        Debug.Log($"OpenInventory ({gameObject.name}): Interact called. Returning OpenInventoryResponse.", this);

        if (inventoryUIRoot == null)
        {
            Debug.LogError($"OpenInventory ({gameObject.name}): Inventory UI Root GameObject is not assigned! Cannot create response.", this);
            return null; // Return null response on error
        }
         if (inventoryComponent == null)
         {
             Debug.LogError($"OpenInventory ({gameObject.name}): Inventory Component is not assigned! Cannot create response.", this);
             return null; // Return null response on error
         }

        // --- Create and return the response ---
        // The PlayerInteractionManager will receive this and pass it to the MenuManager
        return new OpenInventoryResponse(inventoryComponent, inventoryUIRoot);
        // --------------------------------------

        // Removed: Direct call to MenuManager.Instance.OpenInventory(inventoryComponent, inventoryUIRoot);
    }

    // --- Optional: Add validation in editor ---
    private void OnValidate()
    {
         if (inventoryUIRoot != null && inventoryUIRoot.GetComponent<FlexibleGridLayout>() == null)
         {
             Debug.LogWarning($"OpenInventory ({gameObject.name}): Assigned Inventory UI Root GameObject '{inventoryUIRoot.name}' does not have a FlexibleGridLayout component. Are you sure this is the correct GameObject?", this);
         }
         // Note: Need to check for Combiner on the GameObject that holds the Inventory component
         if (inventoryComponent != null && inventoryComponent.GetComponent<Systems.Inventory.Combiner>() == null) // Use full namespace or alias for Combiner check
         {
             Debug.LogWarning($"OpenInventory ({gameObject.name}): Assigned Inventory Component GameObject '{inventoryComponent.gameObject.name}' does not have a Combiner component. Are you sure this is the correct GameObject?", this);
         }
    }

    // Assuming computerTextPromptOffset and computerTextPromptRotationOffset are defined elsewhere or were a copy-paste error.
    // If they are specific to the computer, remove them from OpenInventory or handle them generically.
     [Header("Prompt Settings")] // Assuming prompt settings are common, move these up if needed
     public Vector3 inventoryTextPromptOffset = Vector3.zero; // Consider renaming or removing
     public Vector3 inventoryTextPromptRotationOffset = Vector3.zero; // Consider renaming or removing
}