// --- START OF FILE DeliverPrescription.cs ---

using UnityEngine;
using Systems.Interaction; // Needed for IInteractable and InteractionResponse, and the new InteractionManager
using Systems.GameStates; // Needed for PromptEditor, MenuManager, PlayerUIPopups
using Systems.Player; // Needed for PlayerPrescriptionTracker
using Game.Prescriptions; // Needed for PrescriptionOrder, PrescriptionManager
using Systems.Inventory; // Needed for Inventory, Item, ItemDetails, ItemLabel, Combiner
using Game.NPC; // Needed for NpcStateMachineRunner, CustomerState
using Game.Events; // Needed for EventManager and new event

namespace Game.Interaction // Place in a suitable namespace, e.Interaction
{
    /// <summary>
    /// IInteractable component for NPCs in the WaitingForDelivery state,
    /// allowing the player to deliver the crafted item.
    /// --- MODIFIED: Added Registration with InteractionManager singleton. ---
    /// --- MODIFIED: Added check for exact health match during delivery. ---
    /// </summary>
    [RequireComponent(typeof(NpcStateMachineRunner))] // Ensure Runner is present
    public class DeliverPrescription : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [Tooltip("The message displayed when the player looks at this interactable.")]
        [SerializeField] private string interactionPromptMessage = "Deliver prescription (E)"; // Default message

        [Tooltip("Should this interactable be enabled by default when registered? (Usually false for multi-interactable objects like NPCs)")]
        [SerializeField] private bool enableOnStart = false;

        private NpcStateMachineRunner runner; // Cache the Runner reference

        private void Awake()
        {
            runner = GetComponent<NpcStateMachineRunner>();
            if (runner == null)
            {
                Debug.LogError($"DeliverPrescription on {gameObject.name}: NpcStateMachineRunner component not found! This interactable requires it.", this);
                // REMOVED: enabled = false; // InteractionManager will handle initial enabled state
            }

            // --- NEW: Register with the singleton InteractionManager ---
            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.RegisterInteractable(this);
            }
            else
            {
                // This error is critical as the component won't be managed
                Debug.LogError($"DeliverPrescription on {gameObject.name}: InteractionManager.Instance is null in Awake! Cannot register.", this);
                // Optionally disable here if registration is absolutely required for function
                // enabled = false;
            }
            // --- END NEW ---
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
        /// Checks if the health of the delivered item exactly matches the required amount.
        /// </summary>
        /// <returns>An InteractionResponse object describing the result of the interaction.</returns>
        public InteractionResponse Interact()
        {
            Debug.Log($"{gameObject.name}: Interact called on DeliverPrescription.", this);

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
                 // Provide feedback? PlayerUIPopups.Instance?.ShowPopup("Cannot Deliver", "You don't have an active order.");
                 return null; // No active order, interaction fails
            }

            PrescriptionOrder activeOrder = playerTracker.ActivePrescriptionOrder.Value;
            int requiredTotalUnits = activeOrder.dosePerDay * activeOrder.lengthOfTreatmentDays; // Calculate the required total units
            Debug.Log($"{gameObject.name}: Player has active order for '{activeOrder.prescribedDrug}'. Required total units: {requiredTotalUnits}. Checking inventory.", this);


            // 2. Get the expected ItemDetails using the PrescriptionManager.
            PrescriptionManager prescriptionManager = PrescriptionManager.Instance;
            if (prescriptionManager == null)
            {
                 Debug.LogError($"{gameObject.name}: PrescriptionManager.Instance is null! Cannot validate delivery item.", this);
                 return null; // Cannot validate, interaction fails
            }

            ItemDetails expectedItemDetails = prescriptionManager.GetExpectedOutputItemDetails(activeOrder);
            if (expectedItemDetails == null)
            {
                 Debug.LogError($"{gameObject.name}: Could not get expected ItemDetails from PrescriptionManager for drug '{activeOrder.prescribedDrug}'. Mapping missing or invalid?", this);
                 return null; // Cannot validate, interaction fails
            }
            Debug.Log($"{gameObject.name}: Expected delivery item: '{expectedItemDetails.Name}' with label '{expectedItemDetails.itemLabel}'.", this);


            // 3. Find the player's toolbar inventory.
            // Assuming the player's toolbar inventory has the tag "PlayerToolbarInventory"
            GameObject playerToolbarGO = GameObject.FindGameObjectWithTag("PlayerToolbarInventory");
            Inventory playerInventory = null;
            if (playerToolbarGO != null)
            {
                 playerInventory = playerToolbarGO.GetComponent<Inventory>();
            }

            if (playerInventory?.Combiner?.InventoryState == null)
            {
                 Debug.LogError($"{gameObject.name}: Player Toolbar Inventory or its Combiner/State not found! Cannot check inventory.", this);
                 return null; // Cannot check inventory, interaction fails
            }
            Debug.Log($"{gameObject.name}: Found Player Toolbar Inventory.", this);


            // 4. Check the player's toolbar inventory for the expected item.
            bool foundCorrectItem = false;
            bool isDeliveredItemPerfectMatch = false; // <-- NEW: Flag for health match

            Item[] inventoryItems = playerInventory.Combiner.InventoryState.GetCurrentArrayState();
            // Only check physical slots
            for (int i = 0; i < playerInventory.Combiner.PhysicalSlotCount; i++)
            {
                 Item itemInSlot = inventoryItems[i];

                 // --- ADD DEBUG LOGS HERE ---
                 Debug.Log($"DeliverPrescription ({gameObject.name}): Checking player inventory slot {i}. Item: {(itemInSlot != null ? itemInSlot.details?.Name ?? "NULL Details" : "Empty")}.", this);

                 if (itemInSlot != null && itemInSlot.details != null)
                 {
                      bool detailsMatch = (itemInSlot.details == expectedItemDetails); // Use ItemDetails == operator
                      bool labelMatches = (itemInSlot.details.itemLabel == ItemLabel.PrescriptionPrepared);

                      // Check if the slot has the correct type and label
                      if (detailsMatch && labelMatches)
                      {
                           // --- NEW: Perform the health check for perfect match ---
                           Debug.Log($"  - Details Match Expected ('{expectedItemDetails.Name}'): {detailsMatch}", itemInSlot.details);
                           Debug.Log($"  - Label Is PrescriptionPrepared ({ItemLabel.PrescriptionPrepared}): {labelMatches}", itemInSlot.details);
                           Debug.Log($"  - Item is non-stackable durable ({itemInSlot.details.maxStack == 1 && itemInSlot.details.maxHealth > 0}): {itemInSlot.details.maxStack == 1 && itemInSlot.details.maxHealth > 0}", itemInSlot.details);
                           Debug.Log($"  - Delivered Item Health ({itemInSlot.health}): {itemInSlot.health}", itemInSlot.details);
                           Debug.Log($"  - Required Total Units ({requiredTotalUnits}): {requiredTotalUnits}");

                           // Check if the delivered item's health exactly matches the required total units
                           isDeliveredItemPerfectMatch = (itemInSlot.health == requiredTotalUnits);
                           Debug.Log($"  - Health Matches Required Units: {isDeliveredItemPerfectMatch}");
                           // --- END NEW ---


                           // We found the correct item type (regardless of health match)
                           Debug.Log($"{gameObject.name}: Found correct delivery item type '{itemInSlot.details.Name}' in player inventory slot {i}.", this);
                           foundCorrectItem = true;
                           // Break the loop as we only need to find one instance to deliver
                           break; 
                      }
                 }
                 // --- END DEBUG LOGS ---
            }


            // 5. Handle success or failure.
            if (foundCorrectItem)
            {
                // If we found the correct item type (even if health didn't match exactly)
                Debug.Log($"{gameObject.name}: Correct item type found. Completing delivery!", this);

                // Consume 1 of the crafted item from the player's inventory.
                // We consume by ItemDetails here, assuming the inventory system will find *an* instance.
                // A more robust system might consume the specific item instance found in the loop.
                // For now, let's rely on TryRemoveQuantity finding one.
                int removedCount = playerInventory.Combiner.TryRemoveQuantity(expectedItemDetails, 1);

                if (removedCount == 1)
                {
                    Debug.Log($"{gameObject.name}: Successfully consumed 1 '{expectedItemDetails.Name}' from player inventory.", this);

                    // Clear the active prescription order from the PlayerPrescriptionTracker.
                    playerTracker.ClearActiveOrder();
                    PlayerUIPopups.Instance?.HidePopup("Prescription Order"); // Hide the UI if it's still open


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
                    // --- NEW: Pass the perfect match flag in the event ---
                    Debug.Log($"PrescriptionManager: Publishing NpcPrescriptionDeliveredEvent with IsPerfectDelivery = {isDeliveredItemPerfectMatch}.", this);
                    EventManager.Publish(new NpcPrescriptionDeliveredEvent(this.gameObject, activeOrder, isDeliveredItemPerfectMatch)); // <-- Pass the flag
                    // --- END NEW ---


                    // Provide positive player feedback.
                    // TODO: Implement a proper success popup/message that includes perfect/imperfect feedback
                    Debug.Log($"DELIVERY COMPLETE! Perfect Match: {isDeliveredItemPerfectMatch} (Placeholder UI Feedback)");
                    return null; // Indicates successful interaction, event handles state change

                }
                else
                {
                    // This should not happen if foundCorrectItem was true, but defensive check.
                    Debug.LogError($"{gameObject.name}: Failed to consume 1 item from player inventory after finding it! Removed count: {removedCount}. Logic error.", this);
                    // Provide failure feedback?
                    return null; // Indicate failure
                }
            }
            else
            {
                // Item type not found in inventory
                Debug.LogWarning($"{gameObject.name}: Correct delivery item type not found in player inventory.", this);
                // Provide negative player feedback.
                PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", "I don't have the right prescription yet!"); // Use the configured name and pass message

                // Return a failure InteractionResponse (null indicates no state change or complex action)
                return null;
            }
        }

        // --- OnDisable/OnDestroy cleanup ---
        private void OnDisable()
        {
             // Deactivate prompt if disabled (e.g., by InteractionManager)
             DeactivatePrompt();
        }

        private void OnDestroy()
        {
            // Ensure prompt is deactivated if GameObject is destroyed
            DeactivatePrompt();

            // --- NEW: Unregister from the singleton InteractionManager ---
            if (InteractionManager.Instance != null)
            {
                 InteractionManager.Instance.UnregisterInteractable(this);
            }
            // Note: If the manager is destroyed first, this might log an error.
            // The manager's OnDestroy includes logic to handle prompts for registered interactables.
            // --- END NEW ---
        }
    }
}