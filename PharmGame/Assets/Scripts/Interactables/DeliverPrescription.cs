// --- START OF FILE DeliverPrescription.cs ---

using UnityEngine;
using Systems.Interaction; // Needed for IInteractable and InteractionResponse
using Systems.GameStates; // Needed for PromptEditor, MenuManager
using Systems.Player; // Needed for PlayerPrescriptionTracker
using Game.Prescriptions; // Needed for PrescriptionOrder, PrescriptionManager
using Systems.Inventory; // Needed for Inventory, Item, ItemDetails, ItemLabel, Combiner
using Game.NPC; // Needed for NpcStateMachineRunner, CustomerState
using Game.Events; // Needed for EventManager and new event

namespace Game.Interaction // Place in a suitable namespace, e.Interaction
{
    /// <summary>
    /// IInteractable component for NPCs in the WaitingForDelivery state,
    /// allowing the player to deliver the crafted prescription item.
    /// </summary>
    [RequireComponent(typeof(NpcStateMachineRunner))] // Ensure Runner is present
    public class DeliverPrescription : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [Tooltip("The message displayed when the player looks at this interactable.")]
        [SerializeField] private string interactionPromptMessage = "Deliver prescription (E)"; // Default message

        private NpcStateMachineRunner runner; // Cache the Runner reference

        private void Awake()
        {
            runner = GetComponent<NpcStateMachineRunner>();
            if (runner == null)
            {
                Debug.LogError($"DeliverPrescription on {gameObject.name}: NpcStateMachineRunner component not found! This interactable requires it. Self-disabling.", this);
                enabled = false;
            }
        }

        // --- IInteractable Implementation ---

        public string InteractionPrompt => interactionPromptMessage;

        public void ActivatePrompt()
        {
            // Ask the singleton to activate the screen-space prompt and SET ITS TEXT
            if (PromptEditor.Instance != null)
            {
                Debug.Log($"{gameObject.name}: Activating screen-space NPC prompt with message: '{interactionPromptMessage}'.", this);
                PromptEditor.Instance.SetScreenSpaceNPCPromptActive(true, interactionPromptMessage);
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
                Debug.Log($"{gameObject.name}: Deactivating screen-space NPC prompt.", this);
                PromptEditor.Instance.SetScreenSpaceNPCPromptActive(false, ""); // Clear the message when hiding
            }
            else
            {
                Debug.LogError("PromptEditor.Instance is null! Cannot deactivate NPC prompt.");
            }
        }

        /// <summary>
        /// Handles the player interacting with this component to deliver a prescription.
        /// Checks player inventory for the correct item and completes the delivery if found.
        /// </summary>
        /// <returns>An InteractionResponse object describing the result of the interaction.</returns>
        public InteractionResponse Interact()
        {
            Debug.Log($"{gameObject.name}: Interact called on DeliverPrescription.", this);

            // --- PHASE 5 LOGIC ---

            // 1. Get PlayerPrescriptionTracker and the active order.
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player"); // Assuming player has the "Player" tag
            PlayerPrescriptionTracker playerTracker = null;
            if (playerGO != null)
            {
                playerTracker = playerGO.GetComponent<PlayerPrescriptionTracker>();
            }

            if (playerTracker == null || !playerTracker.ActivePrescriptionOrder.HasValue)
            {
                 Debug.LogWarning($"{gameObject.name}: Player does not have an active prescription order. Cannot deliver.", this);
                 // Provide player feedback
                 PlayerUIPopups.Instance?.ShowWrongPrescriptionMessage("You don't have an active prescription order!"); // More specific feedback
                 return null; // No active order, interaction fails
            }

            PrescriptionOrder activeOrder = playerTracker.ActivePrescriptionOrder.Value;
            Debug.Log($"{gameObject.name}: Player has active order for '{activeOrder.prescribedDrug}'. Checking inventory.", this);


            // 2. Get the expected ItemDetails using the PrescriptionManager.
            PrescriptionManager prescriptionManager = PrescriptionManager.Instance;
            if (prescriptionManager == null)
            {
                 Debug.LogError($"{gameObject.name}: PrescriptionManager.Instance is null! Cannot validate delivery item.", this);
                 // Provide player feedback
                 PlayerUIPopups.Instance?.SetInvalidItemActive(true); // Generic error feedback for system issue
                 return null; // Cannot validate, interaction fails
            }

            ItemDetails expectedItemDetails = prescriptionManager.GetExpectedOutputItemDetails(activeOrder);
            if (expectedItemDetails == null)
            {
                 Debug.LogError($"{gameObject.name}: Could not get expected ItemDetails from PrescriptionManager for drug '{activeOrder.prescribedDrug}'. Mapping missing or invalid?", this);
                 // Provide player feedback
                 PlayerUIPopups.Instance?.SetInvalidItemActive(true); // Generic error feedback for system issue
                 return null; // Cannot validate, interaction fails
            }
            Debug.Log($"{gameObject.name}: Expected delivery item: '{expectedItemDetails.Name}' with label '{expectedItemDetails.itemLabel}'.", this);


            // 3. Find the player's toolbar inventory.
            // Assuming the player's toolbar inventory has the tag "PlayerToolbar"
            GameObject playerToolbarGO = GameObject.FindGameObjectWithTag("PlayerToolbarInventory");
            Inventory playerInventory = null;
            if (playerToolbarGO != null)
            {
                 playerInventory = playerToolbarGO.GetComponent<Inventory>();
            }

            if (playerInventory?.Combiner?.InventoryState == null)
            {
                 Debug.LogError($"{gameObject.name}: Player Toolbar Inventory or its Combiner/State not found! Cannot check inventory.", this);
                 // Provide player feedback
                 PlayerUIPopups.Instance?.SetInvalidItemActive(true); // Generic error feedback for system issue
                 return null; // Cannot check inventory, interaction fails
            }
            Debug.Log($"{gameObject.name}: Found Player Toolbar Inventory.", this);


            // 4. Check the player's toolbar inventory for the expected item.
            bool foundCorrectItem = false;
            Item itemToConsume = null; // Keep track of the specific instance to consume

            Item[] inventoryItems = playerInventory.Combiner.InventoryState.GetCurrentArrayState();
            // Only check physical slots
            for (int i = 0; i < playerInventory.Combiner.PhysicalSlotCount; i++)
            {
                 Item itemInSlot = inventoryItems[i];

                 // Check if the slot has an item, it's the correct type, has the correct label, and quantity >= 1
                 if (itemInSlot != null &&
                     itemInSlot.details == expectedItemDetails && // Use ItemDetails == operator for type check
                     itemInSlot.details.itemLabel == ItemLabel.PrescriptionPrepared && // Check for the specific label
                     itemInSlot.quantity >= 1) // Ensure there's at least one to deliver
                 {
                      Debug.Log($"{gameObject.name}: Found correct delivery item '{itemInSlot.details.Name}' in player inventory slot {i}.", this);
                      foundCorrectItem = true;
                      itemToConsume = itemInSlot; // Store the instance reference
                      break; // Found the item, no need to check other slots
                 }
                 else if (itemInSlot != null && itemInSlot.details != null)
                 {
                      // Debug log for items that don't match the criteria
                      // Debug.Log($"Checking slot {i}: Item '{itemInSlot.details.Name}', Label '{itemInSlot.details.itemLabel}', Qty {itemInSlot.quantity}. Expected: '{expectedItemDetails.Name}', Label '{ItemLabel.PrescriptionPrepared}'. No match.", this);
                 }
            }


            // 5. Handle success or failure.
            if (foundCorrectItem)
            {
                Debug.Log($"{gameObject.name}: Correct item found. Completing delivery!", this);

                // Consume 1 of the crafted item from the player's inventory.
                // Use TryRemoveQuantity for stackable items, or TryRemoveInstance for non-stackable if needed.
                // Assuming PrescriptionPrepared items are stackable for now, so TryRemoveQuantity is appropriate.
                int removedCount = playerInventory.Combiner.TryRemoveQuantity(expectedItemDetails, 1);

                if (removedCount == 1)
                {
                     Debug.Log($"{gameObject.name}: Successfully consumed 1 '{expectedItemDetails.Name}' from player inventory.", this);

                     // Clear the active prescription order from the PlayerPrescriptionTracker.
                     // This is also handled in WaitingForDeliverySO.OnExit, but clearing it here immediately
                     // upon successful delivery is also correct. It won't cause issues if called twice.
                     playerTracker.ClearActiveOrder();

                     // Notify the PrescriptionManager to clear the assigned order from its tracking.
                     if (prescriptionManager != null && runner != null)
                     {
                          if (runner.IsTrueIdentityNpc && runner.TiData != null)
                          {
                               prescriptionManager.RemoveAssignedTiOrder(runner.TiData.Id);
                               Debug.Log($"{gameObject.name}: Notified PrescriptionManager to remove assigned TI order for '{runner.TiData.Id}'.", this);
                          }
                          else if (!runner.IsTrueIdentityNpc)
                          {
                               // Use this.gameObject to reference the NPC's GameObject
                               prescriptionManager.RemoveAssignedTransientOrder(this.gameObject);
                               Debug.Log($"{gameObject.name}: Notified PrescriptionManager to remove assigned Transient order for '{this.gameObject.name}'.", this);
                          }
                          else
                          {
                               Debug.LogError($"{gameObject.name}: Runner is TI but TiData is null, or Runner is null. Cannot notify PrescriptionManager to remove assigned order.", this);
                          }
                     }
                     else
                     {
                          Debug.LogError($"{gameObject.name}: PrescriptionManager or Runner is null! Cannot notify manager to remove assigned order.", this);
                     }


                     // Publish an event to signal delivery completion (for NPC state transition).
                     Debug.Log($"{gameObject.name}: Publishing NpcPrescriptionDeliveredEvent.", this);
                     EventManager.Publish(new NpcPrescriptionDeliveredEvent(this.gameObject, activeOrder)); // Pass NPC object and order details


                     // Provide positive player feedback.
                     // TODO: Implement a proper success popup/message
                     Debug.Log("DELIVERY SUCCESS! (Placeholder UI Feedback)");
                     // PlayerUIPopups.Instance?.ShowDeliverySuccessMessage("Prescription Delivered!"); // Placeholder


                     // Return a success InteractionResponse.
                     // Define a new response type if needed, or return null for simple action.
                     // Let's return null for now, as the event triggers the state change.
                     return null; // Indicates successful interaction, event handles state change

                }
                else
                {
                     // This should not happen if foundCorrectItem was true and quantity was >= 1, but defensive check.
                     Debug.LogError($"{gameObject.name}: Failed to consume 1 item from player inventory after finding it! Removed count: {removedCount}. Logic error.", this);
                     // Provide player feedback
                     PlayerUIPopups.Instance?.SetInvalidItemActive(true); // Generic error feedback for system issue
                     return null; // Indicate failure
                }
            }
            else
            {
                Debug.LogWarning($"{gameObject.name}: Correct delivery item not found in player inventory.", this);
                // Provide negative player feedback.
                PlayerUIPopups.Instance?.ShowWrongPrescriptionMessage("I don't have the right prescription yet!"); // Specific feedback

                // Return a failure InteractionResponse (null indicates no state change or complex action)
                return null;
            }
            // --- END PHASE 5 LOGIC ---
        }

        // --- OnDisable/OnDestroy cleanup ---
        private void OnDisable()
        {
            // Ensure prompt is deactivated if component is disabled
            DeactivatePrompt();
        }

        private void OnDestroy()
        {
            // Ensure prompt is deactivated if GameObject is destroyed
            DeactivatePrompt();
        }
    }
}
// --- END OF FILE DeliverPrescription.cs ---