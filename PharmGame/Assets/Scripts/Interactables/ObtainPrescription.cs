// --- START OF FILE ObtainPrescription.cs ---

using UnityEngine;
using TMPro; // Needed for TMP_Text
using Systems.Interaction; // Needed for IInteractable and InteractionResponse
using Game.Prescriptions; // Needed for PrescriptionOrder
using Game.NPC; // Needed for NpcStateMachineRunner
using Game.NPC.TI; // Needed for TiNpcData
using Game.Events; // Needed for EventManager and new event
using Systems.GameStates; // Needed for PromptEditor
using Systems.Player; // Needed for PlayerPrescriptionTracker // <-- Added using directive

namespace Game.Interaction // Place in a suitable namespace, e.g., Game.Interaction
{
    /// <summary>
    /// IInteractable component for NPCs in the WaitingForPrescription state,
    /// allowing the player to obtain their prescription.
    /// </summary>
    public class ObtainPrescription : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [Tooltip("The message displayed when the player looks at this interactable.")]
        [SerializeField] private string interactionPromptMessage = "Take prescription order (E)";

        /// <summary>
        /// Flag indicating if this NPC's prescription order is currently active for the player.
        /// </summary>
        public bool activePrescriptionOrder { get; private set; } = false;

        public string InteractionPrompt => interactionPrompt;

        // --- IInteractable Implementation ---

        public string interactionPrompt => interactionPromptMessage;

        public void ActivatePrompt()
        {
            // Ask the singleton to activate the screen-space prompt and SET ITS TEXT
            if (PromptEditor.Instance != null)
            {
                Debug.Log($"{gameObject.name}: Activating screen-space NPC prompt with message: '{interactionPromptMessage}'.", this);
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
                Debug.Log($"{gameObject.name}: Deactivating screen-space NPC prompt.", this);
                PromptEditor.Instance.SetScreenSpaceNPCPromptActive(false, ""); // MODIFIED: Clear the message when hiding
            }
            else
            {
                Debug.LogError("PromptEditor.Instance is null! Cannot deactivate NPC prompt.");
            }
        }

        /// <summary>
        /// Handles the player interacting with this component.
        /// Retrieves the prescription order, sets the active flag, updates UI, and returns the response.
        /// </summary>
        /// <returns>An ObtainPrescriptionResponse containing the order details.</returns>
        public InteractionResponse Interact()
        {
            Debug.Log($"{gameObject.name}: Interact called on ObtainPrescription.", this);

            PrescriptionOrder orderToDisplay = new PrescriptionOrder(); // Default empty struct
            bool orderFound = false;

            // Get the Runner to access NPC data
            NpcStateMachineRunner runner = GetComponent<NpcStateMachineRunner>();

            if (runner != null)
            {
                if (runner.IsTrueIdentityNpc && runner.TiData != null)
                {
                    // TI NPC: Check TiData
                    if (runner.TiData.pendingPrescription)
                    {
                        orderToDisplay = runner.TiData.assignedOrder;
                        orderFound = true;
                        Debug.Log($"{gameObject.name}: Retrieved TI prescription order.", this);
                    } else {
                         Debug.LogWarning($"{gameObject.name}: TI NPC flagged as IsTrueIdentityNpc but TiData.pendingPrescription is false when Interact called!", this);
                    }
                }
                else if (!runner.IsTrueIdentityNpc)
                {
                    // Transient NPC: Check Runner's transient fields
                    if (runner.hasPendingPrescriptionTransient)
                    {
                        orderToDisplay = runner.assignedOrderTransient;
                        orderFound = true;
                        Debug.Log($"{gameObject.name}: Retrieved Transient prescription order.", this);
                    } else {
                         Debug.LogWarning($"{gameObject.name}: Transient NPC flagged as hasPendingPrescriptionTransient is false when Interact called!", this);
                    }
                } else {
                     Debug.LogWarning($"{gameObject.name}: Runner isTrueIdentityNpc is true but TiData is null!", this);
                }
            }
            else
            {
                Debug.LogError($"{gameObject.name}: NpcStateMachineRunner component not found on GameObject! Cannot retrieve prescription data.", this);
            }


            if (orderFound)
            {
                // Set the flag on this interactable
                activePrescriptionOrder = true;
                Debug.Log($"{gameObject.name}: activePrescriptionOrder set to true on ObtainPrescription component.", this);

                // --- NEW: Assign the order to the PlayerPrescriptionTracker ---
                GameObject playerGO = GameObject.FindGameObjectWithTag("Player"); // Assuming player has the "Player" tag
                if (playerGO != null)
                {
                    PlayerPrescriptionTracker playerTracker = playerGO.GetComponent<PlayerPrescriptionTracker>();
                    if (playerTracker != null)
                    {
                        playerTracker.SetActiveOrder(orderToDisplay);
                        Debug.Log($"{gameObject.name}: Assigned prescription order to PlayerPrescriptionTracker.", this);
                    }
                    else
                    {
                        Debug.LogError($"{gameObject.name}: Player GameObject found but missing PlayerPrescriptionTracker component!", playerGO);
                    }
                }
                else
                {
                    Debug.LogError($"{gameObject.name}: Player GameObject with tag 'Player' not found! Cannot assign prescription order to player tracker.", this);
                }
                // --- END NEW ---

                // Publish event to signal order obtained (for NPC state transition)
                Debug.Log($"{gameObject.name}: Publishing NpcPrescriptionOrderObtainedEvent.", this);
                EventManager.Publish(new NpcPrescriptionOrderObtainedEvent(this.gameObject));

                // Return the response containing the order details (for UI display via SimpleActionDispatcher)
                return new ObtainPrescriptionResponse(orderToDisplay);
            }
            else
            {
                // Order not found or Runner/data issue
                activePrescriptionOrder = false; // Ensure flag is false
                Debug.LogWarning($"{gameObject.name}: Interact called, but no active prescription order was found or data was invalid. Returning null response.", this);
                return null; // Or return a specific "NoOrderFoundResponse" if needed, but null is simpler for now.
            }
        }

        /// <summary>
        /// Resets the active prescription order flag and clears the UI text.
        /// Called when the NPC leaves the WaitingForPrescription state.
        /// </summary>
        public void ResetInteraction()
        {
            activePrescriptionOrder = false;
            Debug.Log($"{gameObject.name}: ObtainPrescription interaction state reset.", this);
        }

        // --- Optional: OnDisable/OnDestroy cleanup ---
        private void OnDisable()
        {
            // Ensure prompt is deactivated if component is disabled
            DeactivatePrompt();
            // Reset interaction state on disable
            ResetInteraction();
            // Note: PlayerUIPopups.Instance?.HidePrescriptionOrder() should be called by WaitingForPrescriptionSO.OnExit
        }

        private void OnDestroy()
        {
            // Ensure prompt is deactivated if GameObject is destroyed
            DeactivatePrompt();
            // Reset interaction state on destroy
            ResetInteraction();
            // Note: PlayerUIPopups.Instance?.HidePrescriptionOrder() should be called by WaitingForPrescriptionSO.OnExit
         }
    }
}
// --- END OF FILE ObtainPrescription.cs ---