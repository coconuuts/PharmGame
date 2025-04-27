using UnityEngine;
using InventoryClass = Systems.Inventory.Inventory; // Use the alias
using Systems.Interaction; // ADD THIS USING

// Ensure this script is on the same GameObject as the Collider that the PlayerInteractionManager hits
public class OpenNPCInventory : MonoBehaviour, IInteractable
{
    [Tooltip("The GameObject that contains the FlexibleGridLayout and is the parent of the visual inventory slots.")]
    [SerializeField] private GameObject inventoryUIRoot;

    [Tooltip("The Inventory component associated with this interactable object.")]
    [SerializeField] private InventoryClass inventoryComponent; // Use the alias
    private string interactionPrompt;
    public string InteractionPrompt => interactionPrompt;
    
    // --- IInteractable Implementation ---

    public void ActivatePrompt()
    {
        // Ask the singleton to activate the screen-space prompt
        if (PromptEditor.Instance != null)
        {
             // Use the new method on the singleton to control the prompt
             PromptEditor.Instance.SetScreenSpaceNPCPromptActive(true); // Pass the message
        }
        else
        {
            Debug.LogError("PromptEditor.Instance is null! Cannot activate NPC prompt.");
        }
    }

    public void DeactivatePrompt()
    {
         // Ask the singleton to deactivate the screen-space prompt
         if (PromptEditor.Instance != null)
         {
             PromptEditor.Instance.SetScreenSpaceNPCPromptActive(false);
         }
         else
         {
             Debug.LogError("PromptEditor.Instance is null! Cannot deactivate NPC prompt.");
         }
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
}