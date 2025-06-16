// --- START OF FILE CashRegisterInteractable.cs ---

using UnityEngine;
using Systems.Interaction; // Needed for IInteractable and InteractionResponse, and the new InteractionManager
using Game.NPC;
using Game.NPC.States;
using System.Collections.Generic; // Needed for List
using Systems.Inventory; // Needed for ItemDetails (for item list)
using System.Linq; // Needed for Sum
using Systems.Economy;
using Game.Events;
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
    [SerializeField] private bool enableOnStart = false;


    [Header("Prompt Settings")]
    [Tooltip("The text to display in the interaction prompt.")]
    [SerializeField] private string interactionPrompt = "Use Cash Register (E)";
    public Vector3 registerTextPromptOffset = Vector3.zero;
    public Vector3 registerTextPromptRotationOffset = Vector3.zero;


    // --- Customer Management ---
    private Game.NPC.NpcStateMachineRunner currentWaitingCustomerRunner = null; // Reference to the NPC currently waiting at the register
    // ---------------------------

    public EconomyManager economyManager;


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
         // This logic should remain here as it's about the *trigger collider*, not the IInteractable component itself.
         if (interactionTriggerCollider != null)
         {
              interactionTriggerCollider.enabled = false; // Collider is off until a customer arrives
              Debug.Log($"CashRegisterInteractable ({gameObject.name}): Interaction trigger initially disabled.", this);
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


    /// <summary>
    /// Activates the interaction prompt.
    /// </summary>
    public void ActivatePrompt()
    {
         if (PromptEditor.Instance != null)
         {
             PromptEditor.Instance.DisplayPrompt(transform, InteractionPrompt, registerTextPromptOffset, registerTextPromptRotationOffset); // Use default offsets or add fields if needed
         }
         else Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): PromptEditor.Instance is null. Cannot display prompt.");
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
    /// </summary>
    public InteractionResponse Interact()
    {
        if (isInteracting)
        {
            Debug.Log($"CashRegisterInteractable ({gameObject.name}): Already interacting with this cash register.");
            return null;
        }

        // --- Check if there is a customer RUNNER waiting ---
        if (currentWaitingCustomerRunner == null) // Use the Runner reference
        {
             Debug.Log($"CashRegisterInteractable ({gameObject.name}): No customer waiting at the register. Interaction not starting minigame.");
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
             currentWaitingCustomerRunner = null; // Clear waiting customer Runner reference
             CustomerDeparted(); // This method is on CashRegisterInteractable, call itself
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
    /// <param name="customerRunner">The NpcStateMachineRunner component of the arriving customer.</param> // <-- CHANGE PARAMETER TYPE
    public void CustomerArrived(Game.NPC.NpcStateMachineRunner customerRunner) // <-- CHANGE PARAMETER TYPE
    {
        if (customerRunner == null)
        {
            Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): CustomerArrived called with null customerRunner.", this);
            return;
        }

        // Check if there is a customer RUNNER already waiting
        if (currentWaitingCustomerRunner != null && currentWaitingCustomerRunner != customerRunner) // Use the Runner reference
        {
            Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): Customer '{customerRunner.gameObject.name}' (Runner) arrived, but customer '{currentWaitingCustomerRunner.gameObject.name}' (Runner) is already waiting! Ignoring arrival.", this);
            // This scenario should be handled by the CustomerManager/Queue system.
            // The Register should only receive CustomerArrived calls when it's free.
            return;
        }

        // Assign the arriving Runner as the current waiting customer Runner
        currentWaitingCustomerRunner = customerRunner; // Assign the Runner reference
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Customer '{currentWaitingCustomerRunner.gameObject.name}' (Runner) arrived at the register.", this);

        // Activate the interaction trigger collider
        if (interactionTriggerCollider != null)
        {
            interactionTriggerCollider.enabled = true;
            Debug.Log($"CashRegisterInteractable ({gameObject.name}): Interaction trigger enabled.", this);
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
        if (interactionTriggerCollider != null)
        {
            interactionTriggerCollider.enabled = false;
            Debug.Log($"CashRegisterInteractable ({gameObject.name}): Interaction trigger disabled.", this);
        }

        currentWaitingCustomerRunner = null; // Clear the Runner reference
        isInteracting = false;

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