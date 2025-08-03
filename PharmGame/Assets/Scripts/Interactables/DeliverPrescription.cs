// --- START OF FILE DeliverPrescription.cs ---

// --- START OF FILE DeliverPrescription.cs ---

using UnityEngine;
using Systems.Interaction; // Needed for IInteractable and InteractionResponse, and the new InteractionManager
using Systems.GameStates; // Needed for PromptEditor, MenuManager, PlayerUIPopups
using Systems.Player; // Needed for PlayerPrescriptionTracker
using Game.Prescriptions; // Needed for PrescriptionOrder, PrescriptionManager
using Systems.Inventory; // Needed for Inventory, Item, ItemDetails, ItemLabel, Combiner
using Game.NPC; // Needed for NpcStateMachineRunner, CustomerState
using Game.Events; // Needed for EventManager and new event
using Systems.Economy; // Needed for EconomyManager
using System;

namespace Game.Interaction // Place in a suitable namespace, e.Interaction
{
    /// <summary>
    /// IInteractable component for NPCs in the WaitingForDelivery state,
    /// allowing the player to deliver the crafted item.
    /// --- MODIFIED: Added check for patient name tag on delivered item. ---
    /// --- MODIFIED: Added logic to handle orders pre-marked as ready. --- // <-- ADDED NOTE
    /// </summary>
    [RequireComponent(typeof(NpcStateMachineRunner))] // Ensure Runner is present
    public class DeliverPrescription : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [Tooltip("The message displayed when the player looks at this interactable.")]
        [SerializeField] private string interactionPromptMessage = "Deliver prescription (E)"; // Default message

        [Tooltip("Should this interactable be enabled by default when registered? (Usually false for multi-interactable objects like NPCs)")]
        [SerializeField] private bool enableOnStart = false;
        public bool EnableOnStart => enableOnStart;

        [Header("Delivery Payout Settings")] // <-- NEW HEADER
        [Tooltip("The percentage (0-1) of the moneyWorth to pay out for an imperfect delivery.")] // <-- NEW TOOLTIP
        [Range(0f, 1f)]
        [SerializeField] private float imperfectDeliveryPayoutMultiplier = 0.30f; // 30% payout for imperfect match (70% cut)


        private NpcStateMachineRunner runner;

        private void Awake()
        {
            runner = GetComponent<NpcStateMachineRunner>();
            if (runner == null)
            {
                Debug.LogError($"DeliverPrescription on {gameObject.name}: NpcStateMachineRunner component not found! This interactable requires it.", this);
            }

            // --- Register with the singleton InteractionManager ---
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
        /// Checks player inventory for the correct item and completes the delivery if found,
        /// OR handles delivery if the order was pre-marked as ready.
        /// Checks if the health of the delivered item exactly matches the required amount (for non-ready orders).
        /// Transfers the item instance to the NPC's inventory upon successful delivery (for non-ready orders).
        /// Adds the order's moneyWorth (potentially reduced for imperfect match) to the player's economy on successful delivery.
        /// MODIFIED: Added check for patient name tag on delivered item.
        /// MODIFIED: Added logic to handle orders pre-marked as ready.
        /// </summary>
        /// <returns>An InteractionResponse object describing the result of the interaction.</returns>
        public InteractionResponse Interact()
        {
            Debug.Log($"{gameObject.name}: Interact called on DeliverPrescription.", this);

            // --- Get NPC's Assigned Order ---
            PrescriptionOrder npcAssignedOrder;
            bool npcHasOrder = false;

            if (runner != null)
            {
                 if (runner.IsTrueIdentityNpc && runner.TiData != null)
                 {
                      npcAssignedOrder = runner.TiData.assignedOrder; // Struct copy
                      npcHasOrder = runner.TiData.pendingPrescription; // Check the flag
                 }
                 else if (!runner.IsTrueIdentityNpc)
                 {
                      npcAssignedOrder = runner.assignedOrderTransient; // Struct copy
                      npcHasOrder = runner.hasPendingPrescriptionTransient; // Check the flag
                 } else {
                     Debug.LogWarning($"{gameObject.name}: Interact: Runner is TI but TiData is null or Runner is null! Cannot access assigned order data.", this);
                     PlayerUIPopups.Instance?.ShowPopup("Delivery Failed", "NPC data error."); // Provide feedback
                     return null;
                 }
            } else {
                 Debug.LogError($"{gameObject.name}: Interact: Runner component is null! Cannot access assigned order data.", this);
                 PlayerUIPopups.Instance?.ShowPopup("Delivery Failed", "NPC data error."); // Provide feedback
                 return null;
            }

            if (!npcHasOrder)
            {
                 Debug.LogWarning($"{gameObject.name}: Interact called, but NPC does not have a pending prescription flag! Cannot deliver.", this);
                 PlayerUIPopups.Instance?.ShowPopup("Delivery Failed", "This customer has no order."); // Provide feedback
                 return null;
            }
            Debug.Log($"{gameObject.name}: NPC has assigned order for '{npcAssignedOrder.prescribedDrug}' (Patient: '{npcAssignedOrder.patientName}').", this);


            // --- Get PlayerPrescriptionTracker and the player's active order. ---
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player"); // Assuming player has the "Player" tag
            PlayerPrescriptionTracker playerTracker = null;
            if (playerGO != null)
            {
                playerTracker = playerGO.GetComponent<PlayerPrescriptionTracker>();
            }

            if (playerTracker == null)
            {
                 Debug.LogError($"{gameObject.name}: PlayerPrescriptionTracker not found! Cannot check player's active order.", this);
                 PlayerUIPopups.Instance?.ShowPopup("Delivery Failed", "Player data error."); // Provide feedback
                 return null;
            }

            PrescriptionOrder? playerActiveOrder = playerTracker.ActivePrescriptionOrder;

            // --- Validate Player's Active Order Matches NPC's Assigned Order ---
            // The player MUST have the *same* order active to deliver it.
            if (!playerActiveOrder.HasValue || !playerActiveOrder.Value.Equals(npcAssignedOrder)) // Assuming PrescriptionOrder.Equals or == override
            {
                 Debug.LogWarning($"{gameObject.name}: Player's active order does not match NPC's assigned order. Player Active: {(playerActiveOrder.HasValue ? playerActiveOrder.Value.patientName : "None")}, NPC Assigned: {npcAssignedOrder.patientName}.", this);
                 PlayerUIPopups.Instance?.ShowPopup("Cannot Deliver", $"This isn't the order I asked for!"); // Provide feedback
                 return null;
            }
            Debug.Log($"{gameObject.name}: Player's active order matches NPC's assigned order ({npcAssignedOrder.patientName}). Proceeding with delivery check.", this);


            // --- Get PrescriptionManager Reference ---
            PrescriptionManager prescriptionManager = PrescriptionManager.Instance;
            if (prescriptionManager == null)
            {
                 Debug.LogError($"{gameObject.name}: PrescriptionManager.Instance is null! Cannot validate delivery item or check ready status.", this);
                 PlayerUIPopups.Instance?.ShowPopup("Error", "Prescription manager not found."); // Provide feedback
                 return null;
            }

            // --- NEW: Check if the order was pre-marked as ready ---
            bool isOrderPreMarkedReady = prescriptionManager.IsOrderReady(npcAssignedOrder.patientName);
            // --- END NEW ---


            // --- Handle Delivery ---
            float payout = npcAssignedOrder.moneyWorth; // Start with full payout
            bool isDeliveredItemPerfectMatch = true; // Assume perfect unless checked otherwise

            if (isOrderPreMarkedReady)
            {
                // --- Scenario: Order was pre-marked ready via computer ---
                Debug.Log($"{gameObject.name}: Order for '{npcAssignedOrder.patientName}' was pre-marked ready. Bypassing inventory check and item transfer.", this);

                // The item was already validated and conceptually "transferred" during the Mark Ready step.
                // No need to check player inventory or transfer the item here.
                // Payout is the full amount. isDeliveredItemPerfectMatch is true.

                // --- NEW: Unmark the order as ready in the PrescriptionManager ---
                prescriptionManager.UnmarkOrderReady(npcAssignedOrder.patientName);
                Debug.Log($"DeliverPrescription ({gameObject.name}): Unmarked order for '{npcAssignedOrder.patientName}' as ready.", this);
                // --- END NEW ---

                PlayerUIPopups.Instance?.ShowPopup("Delivery Complete", $"Order for {npcAssignedOrder.patientName} delivered!"); // Provide feedback

            }
            else // <--- Scenario: Order was NOT pre-marked ready (standard delivery flow) ---
            {
                Debug.Log($"{gameObject.name}: Order for '{npcAssignedOrder.patientName}' was NOT pre-marked ready. Proceeding with standard inventory check and item transfer.", this);

                // --- Get NPC Inventory Reference ---
                Inventory npcInventory = GetComponent<Inventory>();
                if (npcInventory == null)
                {
                    Debug.LogError($"DeliverPrescription ({gameObject.name}): NPC GameObject is missing an Inventory component! Cannot transfer item.", this);
                    PlayerUIPopups.Instance?.ShowPopup("Delivery Failed", "NPC cannot receive items."); // Provide feedback
                    return null; // Cannot proceed without NPC inventory
                }
                Debug.Log($"DeliverPrescription ({gameObject.name}): Found NPC Inventory component.", this);


                // --- Get the expected ItemDetails using the PrescriptionManager. ---
                ItemDetails expectedItemDetails = prescriptionManager.GetExpectedOutputItemDetails(npcAssignedOrder);
                if (expectedItemDetails == null)
                {
                     Debug.LogError($"{gameObject.name}: Could not get expected ItemDetails from PrescriptionManager for drug '{npcAssignedOrder.prescribedDrug}'. Mapping missing or invalid?", this);
                     PlayerUIPopups.Instance?.ShowPopup("Delivery Failed", "Could not validate item."); // Provide feedback
                     return null; // Cannot validate, interaction fails
                }
                Debug.Log($"{gameObject.name}: Expected delivery item: '{expectedItemDetails.Name}' with label '{expectedItemDetails.itemLabel}'.");


                // --- Find the player's toolbar inventory. ---
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
                     PlayerUIPopups.Instance?.ShowPopup("Delivery Failed", "Player inventory not accessible."); // Provide feedback
                     return null; // Cannot check inventory, interaction fails
                }
                Debug.Log($"{gameObject.name}: Found Player Toolbar Inventory.");


                // --- Find the Specific Item Instance in Player Inventory ---
                Item deliveredItemInstance = null; // Variable to hold the found item instance
                int requiredTotalUnits = npcAssignedOrder.dosePerDay * npcAssignedOrder.lengthOfTreatmentDays; // Calculate the required total units
                string requiredPatientName = npcAssignedOrder.patientName; // CAPTURE REQUIRED PATIENT NAME

                Item[] inventoryItems = playerInventory.Combiner.InventoryState.GetCurrentArrayState();
                // Only check physical slots
                for (int i = 0; i < playerInventory.Combiner.PhysicalSlotCount; i++)
                {
                     Item itemInSlot = inventoryItems[i];
                     // Debug.Log($"DeliverPrescription ({gameObject.name}): Checking player inventory slot {i}. Item: {(itemInSlot != null ? itemInSlot.details?.Name ?? "NULL Details" : "Empty")}.", this); // Too noisy

                     if (itemInSlot != null && itemInSlot.details != null)
                     {
                          bool detailsMatch = (itemInSlot.details == expectedItemDetails); // Use ItemDetails == operator
                          bool labelMatches = (itemInSlot.details.itemLabel == ItemLabel.PrescriptionPrepared);
                          // --- NEW: Check for patient name tag match --- // <-- ADDED
                          bool patientNameTagMatches = (!string.IsNullOrEmpty(itemInSlot.patientNameTag) && itemInSlot.patientNameTag.Equals(requiredPatientName, StringComparison.OrdinalIgnoreCase)); // Case-insensitive comparison
                          // --- END NEW ---


                          // Check if the slot has the correct type, label, AND patient name tag
                          if (detailsMatch && labelMatches && patientNameTagMatches) // <-- ADDED patientNameTagMatches TO CONDITION
                          {
                               // --- Perform the health check for perfect match ---
                               Debug.Log($"  - Details Match Expected ('{expectedItemDetails.Name}'): {detailsMatch}", itemInSlot.details);
                               Debug.Log($"  - Label Is PrescriptionPrepared ({ItemLabel.PrescriptionPrepared}): {labelMatches}", itemInSlot.details);
                               Debug.Log($"  - Patient Name Tag Matches Required ('{itemInSlot.patientNameTag ?? "NULL"}' vs '{requiredPatientName}'): {patientNameTagMatches}"); // <-- MODIFIED LOG
                               Debug.Log($"  - Item is non-stackable durable ({itemInSlot.details.maxStack == 1 && itemInSlot.details.maxHealth > 0}): {itemInSlot.details.maxStack == 1 && itemInSlot.details.maxHealth > 0}", itemInSlot.details);
                               Debug.Log($"  - Delivered Item Health ({itemInSlot.health}): {itemInSlot.health}", itemInSlot.details);
                               Debug.Log($"  - Required Total Units ({requiredTotalUnits}): {requiredTotalUnits}");

                               // Check if the delivered item's health exactly matches the required total units
                               isDeliveredItemPerfectMatch = (itemInSlot.health == requiredTotalUnits);
                               Debug.Log($"  - Health Matches Required Units: {isDeliveredItemPerfectMatch}");

                               // We found the correct item instance based on type, label, AND patient name tag
                               Debug.Log($"{gameObject.name}: Found correct delivery item type '{itemInSlot.details.Name}' with patient tag '{itemInSlot.patientNameTag}' in player inventory slot {i}. Storing instance reference.", this); // <-- MODIFIED LOG
                               deliveredItemInstance = itemInSlot; // Store the reference to the item instance
                               // No break here yet, in case future logic needs to iterate all slots,
                               // but for simple delivery of one item, break is fine. Let's keep the break.
                               break; // Break the loop as we only need to find one instance to deliver
                          }
                           else
                           {
                                // Log why this item didn't match, if it was close (e.g., correct type/label but wrong tag)
                                if (detailsMatch && labelMatches && !patientNameTagMatches)
                                {
                                    Debug.Log($"  - Item type/label match, but patient tag mismatch ('{itemInSlot.patientNameTag ?? "NULL"}' vs '{requiredPatientName}'). Skipping.", itemInSlot.details); // <-- ADDED LOG
                                }
                                // else: Item didn't match type or label, no need for specific log here.
                           }
                     }
                }

                // --- Handle success or failure for standard delivery ---
                // --- Check if deliveredItemInstance was found instead of just a boolean flag ---
                if (deliveredItemInstance != null)
                {
                    // If we found the correct item instance (matching type, label, AND patient tag)
                    Debug.Log($"{gameObject.name}: Correct item instance found in player inventory. Attempting transfer to NPC.", this);

                    // --- Pre-Transfer Validation (NPC Inventory & Filtering) ---
                    // Check NPC Inventory Combiner/State
                    if (npcInventory.Combiner?.InventoryState == null)
                    {
                        Debug.LogError($"DeliverPrescription ({gameObject.name}): NPC Inventory Combiner or State is null! Cannot transfer item.", this);
                        PlayerUIPopups.Instance?.ShowPopup("Delivery Failed", "NPC cannot receive items."); // Provide feedback
                        return null; // Cannot proceed
                    }

                    // --- Remove Specific Instance from Player ---
                    Debug.Log($"DeliverPrescription ({gameObject.name}): Attempting to remove specific item instance (ID: {deliveredItemInstance.Id}, Tag: '{deliveredItemInstance.patientNameTag ?? "NULL"}') from player inventory.", this); // <-- MODIFIED LOG
                    // Replace TryRemoveQuantity with TryRemove on the ObservableArray
                    bool removedFromPlayer = playerInventory.Combiner.InventoryState.TryRemove(deliveredItemInstance);

                    if (!removedFromPlayer)
                    {
                        // This should ideally not happen if we just found the item, but defensive check.
                        Debug.LogError($"{gameObject.name}: CRITICAL ERROR: Failed to remove specific item instance (ID: {deliveredItemInstance.Id}, Tag: '{deliveredItemInstance.patientNameTag ?? "NULL"}') from player inventory after finding it! Item may be duplicated or state corrupted.", this); // <-- MODIFIED LOG
                        PlayerUIPopups.Instance?.ShowPopup("Delivery Failed", "Inventory error."); // Provide feedback
                        return null; // Indicate critical failure
                    }

                    // --- Add Specific Instance to NPC ---
                    Debug.Log($"DeliverPrescription ({gameObject.name}): Attempting to add specific item instance (ID: {deliveredItemInstance.Id}, Tag: '{deliveredItemInstance.patientNameTag ?? "NULL"}') to NPC inventory '{npcInventory.Id}'.", this); // <-- MODIFIED LOG
                    // Use AddItem on the NPC inventory. For non-stackable, this calls AddSingleInstance.
                    bool addedToNPC = npcInventory.AddItem(deliveredItemInstance);

                    // --- Handle Add to NPC Outcome ---
                    if (!addedToNPC) // <--- Handle the case where addedToNPC is FALSE
                    {
                        // Handle the critical failure: item removed from player but not added to NPC
                        Debug.LogError($"DeliverPrescription ({gameObject.name}): Failed to add item instance (ID: {deliveredItemInstance.Id}, Tag: '{deliveredItemInstance.patientNameTag ?? "NULL"}') to NPC inventory '{npcInventory.Id}' after removing it from player! Item might be lost.", this); // <-- MODIFIED LOG
                        PlayerUIPopups.Instance?.ShowPopup("Delivery Failed", "NPC inventory full."); // Provide feedback
                        // Optionally re-add item to player here if possible? Or trigger a more serious error state.
                        return null;
                    }

                    // --- Apply penalty and set feedback based on perfect match ---
                    if (isDeliveredItemPerfectMatch)
                    {
                        Debug.Log($"Perfect delivery to {npcAssignedOrder.patientName}. Payout: ${payout:F2}");
                        PlayerUIPopups.Instance?.ShowPopup("Delivery Complete", $"Order for {npcAssignedOrder.patientName} delivered!"); // Provide feedback
                    }
                    else
                    {
                        // Apply penalty
                        payout *= imperfectDeliveryPayoutMultiplier;
                        PlayerUIPopups.Instance?.ShowPopup("Delivery Complete", $"Prescription not accurate! Payout reduced."); // Provide feedback
                        Debug.Log($"Imperfect delivery to {npcAssignedOrder.patientName}. Payout: ${payout:F2} (Reduced).");
                    }
                }
                else
                {
                    // Item type/label/patient tag not found in inventory (deliveredItemInstance is null)
                    Debug.LogWarning($"{gameObject.name}: Correct delivery item type, label, OR patient tag not found in player inventory for patient '{requiredPatientName}'.", this); // <-- MODIFIED LOG
                    // Provide negative player feedback.
                    PlayerUIPopups.Instance?.ShowPopup("Cannot Transfer", $"I don't have the prescription yet!"); // <-- MODIFIED POPUP MESSAGE

                    // Return a failure InteractionResponse (null indicates no state change or complex action)
                    return null; // Interaction failed, do not proceed with delivery completion steps
                }
            } // <--- End of Standard Delivery Flow ---


            // --- Common Delivery Completion Steps (Executed for both Ready and Standard Delivery Success) ---

            // --- Add money to the player ---
            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.AddCurrency(payout); // Add the calculated payout amount
                Debug.Log($"DeliverPrescription ({gameObject.name}): Added ${payout:F2} to player's cash.", this);
            }
            else
            {
                 Debug.LogError($"DeliverPrescription ({gameObject.name}): EconomyManager.Instance is null! Cannot add money.", this);
            }

            // --- Update Success Logic ---
            // Clear the active prescription order from the PlayerPrescriptionTracker.
            playerTracker.ClearActiveOrder();
            // PlayerUIPopups.Instance?.HidePopup("Prescription Order"); // Hide popup is now handled by PlayerPrescriptionTracker.OnActiveOrderChangedHandler

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

            // --- Publish event to free the claim spot on successful delivery (Phase 3, Substep 3) ---
            Debug.Log($"{gameObject.name}: Successful delivery. Publishing FreePrescriptionClaimSpotEvent.", this);
            EventManager.Publish(new FreePrescriptionClaimSpotEvent(this.gameObject));

            // Publish an event to signal delivery completion (for NPC state transition).
            // Pass the perfect match flag in the event.
            Debug.Log($"DeliverPrescription: Publishing NpcPrescriptionDeliveredEvent with IsPerfectDelivery = {isDeliveredItemPerfectMatch}.", this);
            EventManager.Publish(new NpcPrescriptionDeliveredEvent(this.gameObject, npcAssignedOrder, isDeliveredItemPerfectMatch)); // <-- Pass the flag and order details


            return null; // Indicates successful interaction, event handles state change
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

            // --- Unregister from the singleton InteractionManager ---
            if (InteractionManager.Instance != null)
            {
                 InteractionManager.Instance.UnregisterInteractable(this);
            }
            // Note: If the manager is destroyed first, this might log an error.
            // The manager's OnDestroy includes logic to handle prompts for registered interactables.
        }
    }
}