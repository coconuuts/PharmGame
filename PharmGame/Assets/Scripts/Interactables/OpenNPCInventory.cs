// --- START OF FILE OpenNPCInventory.cs ---

using UnityEngine;
using InventoryClass = Systems.Inventory.Inventory;
using Systems.Interaction; // Needed for IInteractable and InteractionResponse
using Systems.UI; // Needed for FlexibleGridLayout (used in OnValidate)

namespace Game.Interaction // Place in a suitable namespace, e.g., Game.Interaction
{
    /// <summary>
    /// IInteractable component for NPCs that allows the player to open their inventory.
    /// </summary>
    // Ensure this script is on the same GameObject as the Collider that the PlayerInteractionManager hits
    public class OpenNPCInventory : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [Tooltip("The message displayed when the player looks at this interactable.")]
        [SerializeField] private string interactionPromptMessage = "Open Inventory (E)"; // Added serialized field

        [Header("Inventory References")]
        [Tooltip("The GameObject that contains the FlexibleGridLayout and is the parent of the visual inventory slots.")]
        [SerializeField] private GameObject inventoryUIRoot;

        [Tooltip("The Inventory component associated with this interactable object.")]
        [SerializeField] private InventoryClass inventoryComponent;

        // Public property to expose the prompt message via the IInteractable interface
        public string InteractionPrompt => interactionPromptMessage; // Updated to use the serialized field


        // --- IInteractable Implementation ---

        public void ActivatePrompt()
        {
            // Ask the singleton to activate the screen-space prompt and SET ITS TEXT
            if (PromptEditor.Instance != null)
            {
                 Debug.Log($"{gameObject.name}: Activating screen-space NPC prompt for Inventory with message: '{interactionPromptMessage}'.", this);
                 // Use the new method on the singleton to control the prompt and pass the message
                 PromptEditor.Instance.SetScreenSpaceNPCPromptActive(true, interactionPromptMessage); // MODIFIED: Pass the message
            }
            else
            {
                Debug.LogError("PromptEditor.Instance is null! Cannot activate NPC prompt.");
            }
        }

        public void DeactivatePrompt()
        {
            // Ask the singleton to deactivate the screen-space prompt and CLEAR ITS TEXT
            if (PromptEditor.Instance != null)
            {
                 Debug.Log($"{gameObject.name}: Deactivating screen-space NPC prompt for Inventory.", this);
                 // Use the new method on the singleton to control the prompt and clear the message
                 PromptEditor.Instance.SetScreenSpaceNPCPromptActive(false, ""); // MODIFIED: Clear the message when hiding
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
            Debug.Log($"OpenNPCInventory ({gameObject.name}): Interact called. Returning OpenInventoryResponse.", this);

            if (inventoryUIRoot == null)
            {
                Debug.LogError($"OpenNPCInventory ({gameObject.name}): Inventory UI Root GameObject is not assigned! Cannot create response.", this);
                return null;
            }
             if (inventoryComponent == null)
             {
                 Debug.LogError($"OpenNPCInventory ({gameObject.name}): Inventory Component is not assigned! Cannot create response.", this);
                 return null;
             }

            // --- Create and return the response ---
            // The PlayerInteractionManager will receive this and pass it to the MenuManager
            return new OpenInventoryResponse(inventoryComponent, inventoryUIRoot);
        }

        // --- Optional: Add validation in editor ---
        private void OnValidate()
        {
             if (inventoryUIRoot != null && inventoryUIRoot.GetComponent<FlexibleGridLayout>() == null)
             {
                 Debug.LogWarning($"OpenNPCInventory ({gameObject.name}): Assigned Inventory UI Root GameObject '{inventoryUIRoot.name}' does not have a FlexibleGridLayout component. Are you sure this is the correct GameObject?", this);
             }
             // Note: Need to check for Combiner on the GameObject that holds the Inventory component
             if (inventoryComponent != null && inventoryComponent.GetComponent<Systems.Inventory.Combiner>() == null) // Use full namespace or alias for Combiner check
             {
                 Debug.LogWarning($"OpenNPCInventory ({gameObject.name}): Assigned Inventory Component GameObject '{inventoryComponent.gameObject.name}' does not have a Combiner component. Are you sure this is the correct GameObject?", this);
             }
        }
    }
}

// --- END OF FILE OpenNPCInventory.cs ---