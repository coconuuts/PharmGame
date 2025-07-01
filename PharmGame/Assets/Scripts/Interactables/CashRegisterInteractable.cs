// --- START OF FILE CashRegisterInteractable.cs ---

using UnityEngine;
using Systems.Interaction; // Needed for IInteractable and InteractionResponse, and the new InteractionManager
using Game.NPC; // Needed for NpcStateMachineRunner, CashierState enum
using Game.NPC.States; // Needed for NpcStateSO (though not strictly needed in this file)
using System.Collections.Generic; // Needed for List
using Systems.Inventory; // Needed for ItemDetails (for item list)
using System.Linq; // Needed for Sum
using Systems.Economy;
using Game.Events; // Needed for EventManager and CustomerReadyForCashierEvent
using Systems.GameStates; // Needed for PromptEditor

public class CashRegisterInteractable : MonoBehaviour, IInteractable
{
    [Header("Minigame Settings")]
    [Tooltip("The camera transform the player should move to for the minigame.")]
    [SerializeField] private Transform minigameCameraViewPoint;

    [Tooltip("The duration of the camera movement animation to the minigame view.")]
    [SerializeField] private float cameraMoveDuration = 0.5f;

    [Tooltip("The root GameObject containing the minigame UI elements.")]
    [SerializeField] private GameObject minigameUIRoot;

    [Tooltip("The collider component that acts as the trigger for player interaction.")]
    [SerializeField] private Collider interactionTriggerCollider; // Assign your trigger collider here

    [Tooltip("Should this interactable be enabled by default when registered?")]
    [SerializeField] private bool enableOnStart = false; // This field is now less relevant, controlled by Customer/Cashier logic


    [Header("Prompt Settings")]
    [Tooltip("The text to display in the interaction prompt.")]
    [SerializeField] private string interactionPrompt = "Use Cash Register (E)";
    public Vector3 registerTextPromptOffset = Vector3.zero;
    public Vector3 registerTextPromptRotationOffset = Vector3.zero;


    // --- Customer Management ---
    private Game.NPC.NpcStateMachineRunner currentWaitingCustomerRunner = null; // Reference to the NPC currently waiting at the register
    // ---------------------------

    public EconomyManager economyManager;

    // --- Flag to control player interaction ---
    private bool isPlayerInteractionEnabled = false; // Default to false
    // --- END NEW ---

    // --- NEW: Flag to track if a Cashier is staffing the register ---
    private bool isStaffedByCashier = false;
    public bool IsStaffedByCashier => isStaffedByCashier; // Public getter for CustomerManager
    // --- END NEW ---


    public string InteractionPrompt => interactionPrompt;

    private bool isInteracting = false; // To prevent re-interacting while minigame is active


    private void Awake()
    {
         // --- NEW: Register with the singleton InteractionManager ---
         if (Systems.Interaction.InteractionManager.Instance != null) // Use full namespace if needed
         {
             Systems.Interaction.InteractionManager.Instance.RegisterInteractable(this);
         }
         else
         {
             // This error is critical as the component won't be managed
             Debug.LogError($"CashRegisterInteractable on {gameObject.name}: InteractionManager.Instance is null in Awake! Cannot register.", this);
             // Optionally disable here if registration is absolutely required for function
             // enabled = false;
         }
         // --- END NEW ---

         // Ensure trigger collider is assigned
         if (interactionTriggerCollider == null)
         {
             // Try to get a collider on this object if not assigned
             interactionTriggerCollider = GetComponent<Collider>();
             if (interactionTriggerCollider == null)
             {
                 Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Interaction Trigger Collider is not assigned and no Collider found on GameObject!", this);
                 // REMOVED: enabled = false; // InteractionManager handles initial enabled state
                 // return; // Don't return, allow registration to happen
             }
         }

            if (economyManager == null)
         {
             economyManager = GetComponent<EconomyManager>();
             if (economyManager == null)
             {
                 Debug.LogError($"EconomyManager ({gameObject.name}): EconomyManager is not assigned!", this);;
                 // return; // Don't return, allow registration to happen
             }
         }
         // Ensure the assigned collider is a trigger
         if (interactionTriggerCollider != null && !interactionTriggerCollider.isTrigger) // Added null check
         {
              Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Assigned Interaction Trigger Collider is not marked as a trigger!", this);
              // REMOVED: enabled = false; // InteractionManager handles initial enabled state
         }
    }

    private void Start()
    {
        if (minigameCameraViewPoint == null) Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Minigame Camera View Point is not assigned!", this);
        if (minigameUIRoot == null) Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Minigame UI Root is not assigned!", this);

        // Ensure the minigame UI is initially hidden
         if(minigameUIRoot != null) minigameUIRoot.SetActive(false);

         // --- Ensure the trigger collider is initially deactivated ---
         // The initial state of isPlayerInteractionEnabled will determine if it starts enabled or not.
         // Let's explicitly set the collider state based on the flag here.
         if (interactionTriggerCollider != null)
         {
              // Collider state is now controlled by SetPlayerInteractionEnabled
              // It will be set by CustomerArrived or CashierWaitingForCustomerSO.OnEnter
              // For now, let's ensure it's off by default unless enableOnStart is true
              SetPlayerInteractionEnabled(enableOnStart); // Use the inspector flag for initial state
              Debug.Log($"CashRegisterInteractable ({gameObject.name}): Interaction trigger initially set to enabled={enableOnStart}.", this);
         }
        // ---------------------------------------------------------
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


    // --- NEW: Method to control player interaction ---
    /// <summary>
    /// Enables or disables player interaction with this cash register.
    /// Also controls the state of the interaction trigger collider.
    /// </summary>
    /// <param name="enabled">True to enable player interaction, false to disable.</param>
    public void SetPlayerInteractionEnabled(bool enabled)
    {
        isPlayerInteractionEnabled = enabled;
        if (interactionTriggerCollider != null)
        {
            // The collider should only be enabled if player interaction is enabled AND there's a customer waiting.
            // This method just sets the flag. The CustomerArrived/Departed methods handle the collider state based on this flag AND customer presence.
            // Let's simplify: This method controls the flag. CustomerArrived/Departed control the collider based on the flag and customer state.
            // Reverting to original plan: This method controls the flag AND the collider.
            // The logic for *when* to call this method lives in CustomerArrived/Departed and Cashier states.
            interactionTriggerCollider.enabled = enabled; // Directly control collider state
            Debug.Log($"CashRegisterInteractable ({gameObject.name}): Player interaction enabled set to {enabled}. Collider enabled: {interactionTriggerCollider.enabled}", this);
        }
    }
    // --- END NEW ---

    // --- NEW: Methods to signal Cashier presence ---
    /// <summary>
    /// Called by the Cashier NPC when they arrive at the register spot to start their shift.
    /// </summary>
    public void SignalCashierArrived()
    {
        isStaffedByCashier = true;
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Cashier arrived. Register is now staffed.", this);
        // When Cashier arrives, player interaction is always disabled.
        SetPlayerInteractionEnabled(false);
    }

    /// <summary>
    /// Called by the Cashier NPC when they leave the register spot (e.g., going home, interrupted).
    /// </summary>
    public void SignalCashierDeparted()
    {
        isStaffedByCashier = false;
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Cashier departed. Register is no longer staffed.", this);

        // When Cashier leaves, check if a customer is waiting.
        // If a customer is waiting, re-enable player interaction so the player can check them out.
        // If no customer is waiting, player interaction remains off until the next customer arrives.
        if (currentWaitingCustomerRunner != null)
        {
             Debug.Log($"CashRegisterInteractable ({gameObject.name}): Customer '{currentWaitingCustomerRunner.gameObject.name}' is still waiting. Re-enabling player interaction.", this);
             SetPlayerInteractionEnabled(true); // Allow player to check out the waiting customer
        }
        else
        {
             Debug.Log($"CashRegisterInteractable ({gameObject.name}): No customer waiting. Player interaction remains disabled.", this);
             SetPlayerInteractionEnabled(false); // Keep off until next customer arrives
        }
    }
    // --- END NEW ---


    /// <summary>
    /// Activates the interaction prompt.
    /// </summary>
    public void ActivatePrompt()
    {
         // Only display prompt if player interaction is enabled
         if (isPlayerInteractionEnabled && PromptEditor.Instance != null) // <-- Check flag
         {
             PromptEditor.Instance.DisplayPrompt(transform, InteractionPrompt, registerTextPromptOffset, registerTextPromptRotationOffset); // Use default offsets or add fields if needed
         }
         else if (PromptEditor.Instance != null) // Hide if prompt was somehow active but interaction is disabled
         {
              PromptEditor.Instance.HidePrompt();
         }
         // else Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): PromptEditor.Instance is null. Cannot display prompt."); // Too noisy
    }

    /// <summary>
    /// Deactivates (hides) the interaction prompt.
    /// </summary>
    public void DeactivatePrompt()
    {
         if (PromptEditor.Instance != null)
         {
             PromptEditor.Instance.HidePrompt();
         }
    }

    /// <summary>
    /// Runs the object's specific interaction logic and returns a response describing the outcome.
    /// Called by the Player Interaction system when the player interacts.
    /// MODIFIED: Creates a StartMinigameResponse including the MinigameType.
    /// ADDED: Check if player interaction is currently enabled.
    /// </summary>
    public InteractionResponse Interact()
    {
        // --- NEW: Check if player interaction is enabled ---
        if (!isPlayerInteractionEnabled)
        {
             // Debug.Log($"CashRegisterInteractable ({gameObject.name}): Player interaction is currently disabled.", this); // Too noisy
             return null; // Cannot interact if disabled
        }
        // --- END NEW ---

        if (isInteracting)
        {
            Debug.Log($"CashRegisterInteractable ({gameObject.name}): Already interacting with this cash register.");
            return null;
        }

        // --- Check if there is a customer RUNNER waiting ---
        if (currentWaitingCustomerRunner == null) // Use the Runner reference
        {
             Debug.Log($"CashRegisterInteractable ({gameObject.name}): No customer waiting at the register. Interaction not starting minigame.");
             // This case should ideally not happen if isPlayerInteractionEnabled is true,
             // as CustomerArrived should set isPlayerInteractionEnabled(true) and currentWaitingCustomerRunner.
             // But defensive check is fine.
             return null;
        }
        // -------------------------------------------------

        // --- Get the customer's purchase list and validate ---
        // Call GetItemsToBuy on the Runner
        List<(ItemDetails details, int quantity)> itemsToScan = currentWaitingCustomerRunner.GetItemsToBuy(); // Call on the Runner

        if (itemsToScan == null || itemsToScan.Sum(item => item.quantity) <= 0)
        {
             Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): Customer '{currentWaitingCustomerRunner.gameObject.name}' (Runner) has no items or zero total quantity to buy. Cancelling minigame.", currentWaitingCustomerRunner.gameObject);
             // Customer leaves empty-handed? Tell the customer RUNNER to exit.
             // Call OnTransactionCompleted on the Runner
             currentWaitingCustomerRunner.OnTransactionCompleted(0); // Call on the Runner (Fix 1)
             // currentWaitingCustomerRunner = null; // This is cleared in CustomerDeparted, which is called by the customer's Exiting state
             // CustomerDeparted(); // This method is on CashRegisterInteractable, call itself - No, the customer's Exiting state calls this.
             return null;
        }

        int actualTargetClickCount = itemsToScan.Sum(item => item.quantity);
         Debug.Log($"CashRegisterInteractable ({gameObject.name}): Customer '{currentWaitingCustomerRunner.gameObject.name}' (Runner) wants to buy {actualTargetClickCount} items.", currentWaitingCustomerRunner.gameObject);

        if (minigameCameraViewPoint == null)
        {
             Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Cannot create StartMinigameResponse - Minigame Camera View Point not assigned.", this);
             return null;
        }

        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Interact called. Preparing to start minigame with {actualTargetClickCount} items.", this);

        // --- Inform the customer RUNNER that the transaction is starting ---
        // Call StartTransaction on the Runner
        currentWaitingCustomerRunner.StartTransaction(/* itemsToScan */); // Call on the Runner (Fix 2)
        // -----------------------------------------------------------------

        // Publish event - the Runner is subscribed to this
        EventManager.Publish(new NpcStartedTransactionEvent(currentWaitingCustomerRunner.gameObject));

        // --- Create and return the response ---
        StartMinigameResponse response = new StartMinigameResponse(
            Systems.Minigame.MinigameType.BarcodeScanning,
            minigameCameraViewPoint,
            cameraMoveDuration,
            itemsToScan,
            this
        );

        isInteracting = true;

        return response;
    }

    /// <summary>
    /// Public method to reset the interacting state (Called by MenuManager state exit action).
    /// </summary>
    public void ResetInteraction()
    {
        isInteracting = false;
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): ResetInteraction called. isInteracting is now false.", this);

        // Note: Customer departure (disabling trigger etc.) is handled by OnMinigameCompleted
    }

    /// <summary>
    /// Called by a state SO (e.g., WaitingAtRegisterStateSO) when a customer reaches the register point.
    /// Signals that a customer is ready to interact.
    /// </summary>
    /// <param name="customerRunner">The NpcStateMachineRunner component of the arriving customer.</param>
    public void CustomerArrived(Game.NPC.NpcStateMachineRunner customerRunner)
    {
        if (customerRunner == null)
        {
            Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): CustomerArrived called with null customerRunner.", this);
            return;
        }

        // Check if there is a customer RUNNER already waiting
        if (currentWaitingCustomerRunner != null && currentWaitingCustomerRunner != customerRunner)
        {
            Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): Customer '{customerRunner.gameObject.name}' (Runner) arrived, but customer '{currentWaitingCustomerRunner.gameObject.name}' (Runner) is already waiting! Ignoring arrival.", this);
            // This scenario should be handled by the CustomerManager/Queue system.
            // The Register should only receive CustomerArrived calls when it's free.
            return;
        }

        // Assign the arriving Runner as the current waiting customer Runner
        currentWaitingCustomerRunner = customerRunner;
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Customer '{currentWaitingCustomerRunner.gameObject.name}' (Runner) arrived at the register.", this);

        // --- NEW: Check if a Cashier is present and ready ---
        // Find the active Cashier Runner, if any.
        // Assumption: There is only one active Cashier at a time, and they have the "Cashier" tag.
        NpcStateMachineRunner cashierRunner = null;
        GameObject cashierGO = GameObject.FindGameObjectWithTag("Cashier"); // Find the Cashier GameObject
        if (cashierGO != null)
        {
             cashierRunner = cashierGO.GetComponent<NpcStateMachineRunner>();
             if (cashierRunner != null)
             {
                  // Check if the Cashier Runner is currently in the WaitingForCustomer state
                  if (cashierRunner.GetCurrentState() != null && cashierRunner.GetCurrentState().HandledState.Equals(CashierState.CashierWaitingForCustomer))
                  {
                       Debug.Log($"CashRegisterInteractable ({gameObject.name}): Found Cashier '{cashierGO.name}' in CashierWaitingForCustomer state.", this);
                       // Cashier is present and ready
                       // isCashierPresentAndReady will be true effectively
                  } else {
                      Debug.Log($"CashRegisterInteractable ({gameObject.name}): Found Cashier '{cashierGO.name}' but not in CashierWaitingForCustomer state ({cashierRunner.GetCurrentState()?.HandledState.ToString() ?? "NULL"}). Treating as not ready.", this);
                       cashierRunner = null; // Treat as not ready if not in correct state
                  }
             } else {
                  Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): GameObject with tag 'Cashier' found, but missing NpcStateMachineRunner component!", cashierGO);
             }
        }
        // If cashierRunner is still null, no ready cashier was found


        if (cashierRunner == null) // If no Cashier is handling it (no runner found or not in correct state)
        {
             // Activate the interaction trigger collider for player interaction
             SetPlayerInteractionEnabled(true); // Use the new method
             Debug.Log($"CashRegisterInteractable ({gameObject.name}): No Cashier present or ready. Enabling player interaction trigger for player checkout.", this);
        }
        else // Cashier IS present and ready
        {
             // Player interaction remains disabled.
             SetPlayerInteractionEnabled(false); // Ensure player interaction is off
             Debug.Log($"CashRegisterInteractable ({gameObject.name}): Cashier is present and ready. Player interaction trigger remains disabled. Cashier will handle checkout.", this);

             // --- NEW: Signal the Cashier that a customer is ready ---
             Debug.Log($"CashRegisterInteractable ({gameObject.name}): Publishing CustomerReadyForCashierEvent for Cashier '{cashierRunner.gameObject.name}' and Customer '{customerRunner.gameObject.name}'.", this);
             EventManager.Publish(new CustomerReadyForCashierEvent(cashierRunner.gameObject, customerRunner.gameObject));
             // --- END NEW ---
        }
    }

    /// <summary>
    /// Called by a state SO (ExitingStateSO) when it is leaving the register area.
    /// Cleans up the waiting state.
    /// </summary>
    public void CustomerDeparted() // This method is called by the ExitingStateSO OnEnter
    {
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Customer departed from the register. Publishing CashRegisterFreeEvent.");

        // Deactivate the interaction trigger collider
        // Note: Player interaction is disabled when a Cashier arrives.
        // It is enabled when the Cashier leaves (Phase 2.6).
        // If a customer departs when *no* Cashier is present (player checkout),
        // this should also disable player interaction again until the next customer arrives.
        // The SetPlayerInteractionEnabled(false) handles this.
        SetPlayerInteractionEnabled(false); // Use the new method to disable player interaction

        currentWaitingCustomerRunner = null; // Clear the Runner reference
        isInteracting = false; // Should be false anyway after minigame completion or impatience

        // Publish CashRegisterFreeEvent (as done in Substep 3)
        EventManager.Publish(new CashRegisterFreeEvent());
    }

    /// <summary>
    /// Called by the MinigameManager when the minigame is completed.
    /// Handles payment and signals the customer RUNNER.
    /// </summary>
    public void OnMinigameCompleted(float totalPaymentAmount)
    {
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Minigame completed. Total payment calculated: {totalPaymentAmount}.");

        // Process Payment
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddCurrency(totalPaymentAmount);
        }
        else
        {
            Debug.LogError($"CashRegisterInteractable ({gameObject.name}): EconomyManager instance not found! Cannot process payment.", this);
        }

        // --- Signal the waiting customer RUNNER that transaction is completed ---
        if (currentWaitingCustomerRunner != null) // Check the Runner reference
        {
            // Call OnTransactionCompleted on the Runner
            currentWaitingCustomerRunner.OnTransactionCompleted(totalPaymentAmount); // Call on the Runner (Fix 4)
            // The NPC (Runner) will transition to Exiting state via OnTransactionCompleted.
            // The ExitingStateSO OnEnter calls CustomerDeparted() on *this* Register instance.
        }
        else
        {
            Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): Minigame completed, but no currentWaitingCustomerRunner reference!", this);
        }
        // -----------------------------------------------------------------

        // Publish NpcTransactionCompletedEvent (as done in Substep 3)
        if (currentWaitingCustomerRunner != null) // Publish if we had a valid Runner reference
        {
             EventManager.Publish(new NpcTransactionCompletedEvent(currentWaitingCustomerRunner.gameObject, totalPaymentAmount));
        }

        isInteracting = false;
    }

    // TODO: Implement failure conditions if the minigame is lost
    // public void OnMinigameFailed(...) { ... }
}
// --- END OF FILE CashRegisterInteractable.cs ---