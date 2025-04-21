using UnityEngine;
using Systems.Inventory; // Needed for the Inventory script reference

// Ensure this script is on the same GameObject as the Collider that the PlayerInteractionManager hits
public class OpenInventory : MonoBehaviour, IInteractable
{
    [Tooltip("The GameObject that contains the FlexibleGridLayout and is the parent of the visual inventory slots.")]
    [SerializeField] private GameObject inventoryUIRoot;

    [Tooltip("The Inventory component associated with this interactable object.")]
    [SerializeField] private Inventory inventoryComponent;

    [Tooltip("The text to display in the interaction prompt.")]
    [SerializeField] private string interactionPrompt = "Open Inventory (E)"; // Default prompt

    [Header("Prompt Settings")]
    public Vector3 inventoryTextPromptOffset = Vector3.zero;
    public Vector3 inventoryTextPromptRotationOffset = Vector3.zero;

    // Get the prompt text from the inspector field
    public string InteractionPrompt => interactionPrompt;

    // --- IInteractable Implementation ---

    /// <summary>
    /// Called by PlayerInteractionManager when the player looks at this object.
    /// We don't need to activate the prompt here; the manager handles the UI.
    /// </summary>
    public void ActivatePrompt()
    {
        PromptEditor.Instance.DisplayPrompt(transform, InteractionPrompt, inventoryTextPromptOffset, inventoryTextPromptRotationOffset);
    }

    /// <summary>
    /// Called by PlayerInteractionManager when the player looks away.
    /// We don't need to deactivate the prompt here; the manager handles the UI.
    /// </summary>
    public void DeactivatePrompt()
    {
        PromptEditor.Instance.HidePrompt();
    }


    /// <summary>
    /// Called by PlayerInteractionManager when the player interacts with this object (e.g., presses E).
    /// Triggers the inventory opening sequence.
    /// </summary>
    public void Interact()
    {
        Debug.Log($"OpenInventory ({gameObject.name}): Interact called. Attempting to open inventory.", this);

        if (inventoryUIRoot == null)
        {
            Debug.LogError($"OpenInventory ({gameObject.name}): Inventory UI Root GameObject is not assigned!", this);
            return;
        }
         if (inventoryComponent == null)
         {
             Debug.LogError($"OpenInventory ({gameObject.name}): Inventory Component is not assigned!", this);
             return;
         }

        // Tell the MenuManager to open this specific inventory's UI
        // We need to modify MenuManager to accept the Inventory reference.
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.OpenInventory(inventoryComponent, inventoryUIRoot); // Pass both the data component and the UI root
        }
        else
        {
            Debug.LogError("OpenInventory: MenuManager Instance is null!");
        }
    }

    // --- Optional: Add validation in editor ---
    private void OnValidate()
    {
         if (inventoryUIRoot != null && inventoryUIRoot.GetComponent<FlexibleGridLayout>() == null)
         {
             Debug.LogWarning($"OpenInventory ({gameObject.name}): Assigned Inventory UI Root GameObject '{inventoryUIRoot.name}' does not have a FlexibleGridLayout component. Are you sure this is the correct GameObject?", this);
         }
          if (inventoryComponent != null && inventoryComponent.GetComponent<Combiner>() == null)
         {
             Debug.LogWarning($"OpenInventory ({gameObject.name}): Assigned Inventory Component GameObject '{inventoryComponent.gameObject.name}' does not have a Combiner component. Are you sure this is the correct GameObject?", this);
         }
    }
}