using UnityEngine;
using Systems.Interaction; // Needed for IInteractable and InteractionResponse
using Game.NPC; // Needed for CustomerAI
using System.Collections.Generic; // Needed for List
using Systems.Inventory; // Needed for ItemDetails (for item list)
using System.Linq; // Needed for Sum
using Systems.Economy;

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


    [Header("Prompt Settings")]
    [Tooltip("The text to display in the interaction prompt.")]
    [SerializeField] private string interactionPrompt = "Use Cash Register (E)";
    public Vector3 registerTextPromptOffset = Vector3.zero;
    public Vector3 registerTextPromptRotationOffset = Vector3.zero;


    // --- Customer Management ---
    private Game.NPC.CustomerAI currentWaitingCustomer = null; // Reference to the NPC currently waiting at the register
    // ---------------------------

    public EconomyManager economyManager;


    public string InteractionPrompt => interactionPrompt;

    private bool isInteracting = false; // To prevent re-interacting while minigame is active


    private void Awake()
    {
         // Ensure trigger collider is assigned
         if (interactionTriggerCollider == null)
         {
             // Try to get a collider on this object if not assigned
             interactionTriggerCollider = GetComponent<Collider>();
             if (interactionTriggerCollider == null)
             {
                 Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Interaction Trigger Collider is not assigned and no Collider found on GameObject!", this);
                 enabled = false;
                 return;
             }
         }

            if (economyManager == null)
         {
             economyManager = GetComponent<EconomyManager>();
             if (economyManager == null)
             {
                 Debug.LogError($"EconomyManager ({gameObject.name}): EconomyManager is not assigned!", this);;
                 return;
             }
         }
         // Ensure the assigned collider is a trigger
         if (!interactionTriggerCollider.isTrigger)
         {
              Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Assigned Interaction Trigger Collider is not marked as a trigger!", this);
              enabled = false;
              return;
         }
    }

    private void Start()
    {
        if (minigameCameraViewPoint == null) Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Minigame Camera View Point is not assigned!", this);
        if (minigameUIRoot == null) Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Minigame UI Root is not assigned!", this);

        // Ensure the minigame UI is initially hidden
         if(minigameUIRoot != null) minigameUIRoot.SetActive(false);

         // --- Ensure the trigger collider is initially deactivated ---
         if (interactionTriggerCollider != null)
         {
              interactionTriggerCollider.enabled = false; // Collider is off until a customer arrives
              Debug.Log($"CashRegisterInteractable ({gameObject.name}): Interaction trigger initially disabled.", this);
         }
        // ---------------------------------------------------------
    }

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
    // Prevent interaction if already interacting with this cash register
    if (isInteracting)
    {
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Already interacting with this cash register.");
        return null;
    }

    // --- Check if there is a customer waiting ---
    if (currentWaitingCustomer == null)
    {
         Debug.Log($"CashRegisterInteractable ({gameObject.name}): No customer waiting at the register. Interaction not starting minigame.");
         // Optionally return a different response here (e.g., a message like "The register is empty")
         // For now, just return null.
         return null;
    }
    // -----------------------------------------

    // --- Get the customer's purchase list and validate ---
    List<(ItemDetails details, int quantity)> itemsToScan = currentWaitingCustomer.Shopper.GetItemsToBuy();

    if (itemsToScan == null || itemsToScan.Sum(item => item.quantity) <= 0)
    {
         Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): Customer '{currentWaitingCustomer.gameObject.name}' has no items or zero total quantity to buy. Cancelling minigame.", currentWaitingCustomer.gameObject);
         // Customer leaves empty-handed? Tell the customer to exit.
         currentWaitingCustomer.OnTransactionCompleted(0); // Signal transaction complete with 0 payment
         currentWaitingCustomer = null; // Clear waiting customer reference
         CustomerDeparted(); // Deactivate trigger etc.
         return null;
    }

    // The total clicks needed for the minigame is the total quantity of items the customer is buying
    int actualTargetClickCount = itemsToScan.Sum(item => item.quantity);
     Debug.Log($"CashRegisterInteractable ({gameObject.name}): Customer '{currentWaitingCustomer.gameObject.name}' wants to buy {actualTargetClickCount} items.", currentWaitingCustomer.gameObject);


    // --- Removed Minigame UI Root check here, UIManager handles UI activation based on state ---
    if (minigameCameraViewPoint == null /* || minigameUIRoot == null */) // minigameUIRoot check is no longer strictly needed for response creation
    {
         // Added null check for camera view point as it's still in the response
         Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Cannot create StartMinigameResponse - Minigame Camera View Point not assigned.", this);
         return null;
    }

    Debug.Log($"CashRegisterInteractable ({gameObject.name}): Interact called. Preparing to start minigame with {actualTargetClickCount} items.", this);

    // --- Inform the customer that the transaction is starting ---
    currentWaitingCustomer.StartTransaction(/* itemsToScan */); // Pass the list if needed by CustomerAI
    // ---------------------------------------------------------

    // --- Create and return the response ---
    // Pass the actual list of items to the response, not just the count.
    // MODIFIED: Include the MinigameType and remove the minigameUIRoot parameter.
    StartMinigameResponse response = new StartMinigameResponse(
        Systems.Minigame.MinigameType.BarcodeScanning, // Specify the type of minigame
        minigameCameraViewPoint,
        cameraMoveDuration,
        // Removed minigameUIRoot parameter
        itemsToScan, // Pass the list of items here
        this // PASS THIS INSTANCE
    );

    isInteracting = true; // Mark as interacting

    return response; // Returns StartMinigameResponse to the system
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
    /// Called by a CustomerAI script when it reaches the register point.
    /// Signals that a customer is ready to interact.
    /// </summary>
    /// <param name="customer">The CustomerAI component of the arriving customer.</param>
    public void CustomerArrived(Game.NPC.CustomerAI customer)
    {
        if (customer == null)
        {
            Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): CustomerArrived called with null customer.", this);
            return;
        }
        if (currentWaitingCustomer != null && currentWaitingCustomer != customer)
        {
            Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): Customer '{customer.gameObject.name}' arrived, but customer '{currentWaitingCustomer.gameObject.name}' is already waiting! Ignoring arrival or handling queue (not implemented).", this);
            // TODO: Implement queueing or handle multiple arrivals if needed
            return;
        }

        currentWaitingCustomer = customer;
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Customer '{customer.gameObject.name}' arrived at the register.", this);

        // --- Activate the interaction trigger collider ---
        if (interactionTriggerCollider != null)
        {
            interactionTriggerCollider.enabled = true;
            Debug.Log($"CashRegisterInteractable ({gameObject.name}): Interaction trigger enabled.", this);
        }
        // -------------------------------------------------

        // The customer's state is already WaitingAtRegister when this is called.
        // They will wait for the player to Interact().
    }

    /// <summary>
    /// Called by a CustomerAI script when it is leaving the register area.
    /// Cleans up the waiting state.
    /// </summary>
    public void CustomerDeparted()
    {
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Customer departed from the register.");

        // --- Deactivate the interaction trigger collider ---
         if (interactionTriggerCollider != null)
         {
             interactionTriggerCollider.enabled = false;
              Debug.Log($"CashRegisterInteractable ({gameObject.name}): Interaction trigger disabled.", this);
         }
        // -------------------------------------------------

        currentWaitingCustomer = null; // Clear the reference
        isInteracting = false; // Ensure interaction state is reset

        // TODO: Handle next customer in queue if queueing is implemented
    }

    /// <summary>
    /// Called by the MinigameManager when the minigame associated with this register interaction is completed (e.g., won).
    /// Handles payment and signals the customer.
    /// </summary>
    /// <param name="totalPaymentAmount">The total currency value of the scanned items.</param>
    public void OnMinigameCompleted(float totalPaymentAmount)
    {
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Minigame completed. Total payment calculated: {totalPaymentAmount}.");

        // --- Process Payment ---
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddCurrency(totalPaymentAmount); // Add money to player
        }
        else
        {
            Debug.LogError($"CashRegisterInteractable ({gameObject.name}): EconomyManager instance not found! Cannot process payment.", this);
        }
        // ---------------------

        // --- Signal the waiting customer that transaction is completed ---
        if (currentWaitingCustomer != null)
        {
            currentWaitingCustomer.OnTransactionCompleted(totalPaymentAmount); // Tell NPC transaction is done
            // The NPC will transition to Exiting state via OnTransactionCompleted.
        }
        else
        {
            Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): Minigame completed, but no currentWaitingCustomer reference!", this);
        }
        // -----------------------------------------------------------------

        // --- Customer Departure is handled now that transaction is complete ---
        // The customer will call CustomerDeparted() when they reach their exit point
        // or potentially when they transition out of WaitingAtRegister/TransactionActive if desired.
        // For now, CustomerAI calls CustomerDeparted() implicitly via its state flow after OnTransactionCompleted.

         // Ensure the interaction state is reset
         isInteracting = false; // Already set in ResetInteraction, but defensive
    }

    // TODO: Implement failure conditions if the minigame is lost
    // public void OnMinigameFailed(...) { ... }
}