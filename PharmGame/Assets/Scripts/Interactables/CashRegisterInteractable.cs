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
    [SerializeField] private bool enableOnStart = false; 
    public bool EnableOnStart => enableOnStart;


    [Header("Prompt Settings")]
    [Tooltip("The text to display in the interaction prompt.")]
    [SerializeField] private string interactionPrompt = "Use Cash Register (E)";
    public Vector3 registerTextPromptOffset = Vector3.zero;
    public Vector3 registerTextPromptRotationOffset = Vector3.zero;


    // --- Customer Management ---
    private Game.NPC.NpcStateMachineRunner currentWaitingCustomerRunner = null; // Reference to the NPC currently waiting at the register
    // ---------------------------

    public EconomyManager economyManager;

    // --- REMOVED: Flag to control player interaction ---
    // private bool isPlayerInteractionEnabled = false; // This is now controlled by the component's `enabled` property
    // --- END REMOVED ---

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
             Debug.LogError($"CashRegisterInteractable on {gameObject.name}: InteractionManager.Instance is null in Awake! Cannot register.", this);
         }
         // --- END NEW ---

         if (interactionTriggerCollider == null)
         {
             interactionTriggerCollider = GetComponent<Collider>();
             if (interactionTriggerCollider == null)
             {
                 Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Interaction Trigger Collider is not assigned and no Collider found on GameObject!", this);
             }
         }

            if (economyManager == null)
         {
             economyManager = GetComponent<EconomyManager>();
             if (economyManager == null)
             {
                 Debug.LogError($"EconomyManager ({gameObject.name}): EconomyManager is not assigned!", this);;
             }
         }
         if (interactionTriggerCollider != null && !interactionTriggerCollider.isTrigger)
         {
              Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Assigned Interaction Trigger Collider is not marked as a trigger!", this);
         }
    }

    private void Start()
    {
        if (minigameCameraViewPoint == null) Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Minigame Camera View Point is not assigned!", this);
        if (minigameUIRoot == null) Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Minigame UI Root is not assigned!", this);

         if(minigameUIRoot != null) minigameUIRoot.SetActive(false);
    }
    
    // --- NEW: OnEnable and OnDisable to control the collider ---
    private void OnEnable()
    {
        // When this component is enabled by the InteractionManager, enable its collider.
        if (interactionTriggerCollider != null)
        {
            interactionTriggerCollider.enabled = true;
        }
    }

    private void OnDisable()
    {
        // When this component is disabled, also disable its collider and hide its prompt.
        if (interactionTriggerCollider != null)
        {
            interactionTriggerCollider.enabled = false;
        }
        DeactivatePrompt();
    }
    // --- END NEW ---


    private void OnDestroy()
    {
         if (Systems.Interaction.InteractionManager.Instance != null)
         {
             Systems.Interaction.InteractionManager.Instance.UnregisterInteractable(this);
         }
    }

    // --- REMOVED: Method to control player interaction ---
    // This functionality is now handled by InteractionManager enabling/disabling this component,
    // which in turn triggers OnEnable/OnDisable.
    // public void SetPlayerInteractionEnabled(bool enabled) { ... }
    // --- END REMOVED ---

    public void SignalCashierArrived()
    {
        isStaffedByCashier = true;
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Cashier arrived. Register is now staffed.", this);
        // When Cashier arrives, player interaction is always disabled.
        // The InteractionManager will handle disabling this component.
        Systems.Interaction.InteractionManager.Instance.DisableInteractableComponent<CashRegisterInteractable>(gameObject);
    }

    public void SignalCashierDeparted()
    {
        isStaffedByCashier = false;
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Cashier departed. Register is no longer staffed.", this);

        if (currentWaitingCustomerRunner != null)
        {
             Debug.Log($"CashRegisterInteractable ({gameObject.name}): Customer '{currentWaitingCustomerRunner.gameObject.name}' is still waiting. Re-enabling player interaction.", this);
             // Re-enable this component for player interaction
             Systems.Interaction.InteractionManager.Instance.EnableOnlyInteractableComponent<CashRegisterInteractable>(gameObject);
        }
        else
        {
             Debug.Log($"CashRegisterInteractable ({gameObject.name}): No customer waiting. Player interaction remains disabled.", this);
             // Ensure this component is disabled
             Systems.Interaction.InteractionManager.Instance.DisableInteractableComponent<CashRegisterInteractable>(gameObject);
        }
    }


    public void ActivatePrompt()
    {
         // The prompt can only be activated if this component is enabled, so the check is implicit.
         if (PromptEditor.Instance != null)
         {
             PromptEditor.Instance.DisplayPrompt(transform, InteractionPrompt, registerTextPromptOffset, registerTextPromptRotationOffset);
         }
    }

    public void DeactivatePrompt()
    {
         if (PromptEditor.Instance != null)
         {
             PromptEditor.Instance.HidePrompt();
         }
    }

    public InteractionResponse Interact()
    {
        // The check for being enabled is now implicit, as Interact() cannot be called on a disabled component.
        if (isInteracting)
        {
            Debug.Log($"CashRegisterInteractable ({gameObject.name}): Already interacting with this cash register.");
            return null;
        }

        if (currentWaitingCustomerRunner == null)
        {
             Debug.Log($"CashRegisterInteractable ({gameObject.name}): No customer waiting at the register. Interaction not starting minigame.");
             return null;
        }

        List<(ItemDetails details, int quantity)> itemsToScan = currentWaitingCustomerRunner.GetItemsToBuy();

        if (itemsToScan == null || itemsToScan.Sum(item => item.quantity) <= 0)
        {
             Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): Customer '{currentWaitingCustomerRunner.gameObject.name}' (Runner) has no items or zero total quantity to buy. Cancelling minigame.", currentWaitingCustomerRunner.gameObject);
             currentWaitingCustomerRunner.OnTransactionCompleted(0);
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

        EventManager.Publish(new NpcStartedTransactionEvent(currentWaitingCustomerRunner.gameObject));

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

    public void ResetInteraction()
    {
        isInteracting = false;
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): ResetInteraction called. isInteracting is now false.", this);
    }

    public void CustomerArrived(Game.NPC.NpcStateMachineRunner customerRunner)
    {
        if (customerRunner == null)
        {
            Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): CustomerArrived called with null customerRunner.", this);
            return;
        }

        if (currentWaitingCustomerRunner != null && currentWaitingCustomerRunner != customerRunner)
        {
            Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): Customer '{customerRunner.gameObject.name}' (Runner) arrived, but customer '{currentWaitingCustomerRunner.gameObject.name}' (Runner) is already waiting! Ignoring arrival.", this);
            return;
        }

        currentWaitingCustomerRunner = customerRunner;
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Customer '{currentWaitingCustomerRunner.gameObject.name}' (Runner) arrived at the register.", this);

        // This logic is simplified to just check for a cashier. The rest is handled by calling InteractionManager.
        NpcStateMachineRunner cashierRunner = null;
        GameObject cashierGO = GameObject.FindGameObjectWithTag("Cashier");
        if (cashierGO != null)
        {
             cashierRunner = cashierGO.GetComponent<NpcStateMachineRunner>();
             if (cashierRunner != null && cashierRunner.GetCurrentState() != null && cashierRunner.GetCurrentState().HandledState.Equals(CashierState.CashierWaitingForCustomer))
             {
                  Debug.Log($"CashRegisterInteractable ({gameObject.name}): Found Cashier '{cashierGO.name}' in CashierWaitingForCustomer state.", this);
             } else {
                  cashierRunner = null;
             }
        }
        
        if (cashierRunner == null) // If no Cashier is handling it
        {
             // *** THE FIX ***
             // Tell the InteractionManager to enable this component for player interaction.
             Debug.Log($"CashRegisterInteractable ({gameObject.name}): No Cashier present or ready. Enabling this component for player checkout via InteractionManager.", this);
             Systems.Interaction.InteractionManager.Instance.EnableOnlyInteractableComponent<CashRegisterInteractable>(gameObject);
        }
        else // Cashier IS present and ready
        {
             // Ensure player interaction is off
             Systems.Interaction.InteractionManager.Instance.DisableInteractableComponent<CashRegisterInteractable>(gameObject);
             Debug.Log($"CashRegisterInteractable ({gameObject.name}): Cashier is present and ready. Component remains disabled. Cashier will handle checkout.", this);

             Debug.Log($"CashRegisterInteractable ({gameObject.name}): Publishing CustomerReadyForCashierEvent for Cashier '{cashierRunner.gameObject.name}' and Customer '{customerRunner.gameObject.name}'.", this);
             EventManager.Publish(new CustomerReadyForCashierEvent(cashierRunner.gameObject, customerRunner.gameObject));
        }
    }

    public void CustomerDeparted()
    {
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Customer departed from the register. Publishing CashRegisterFreeEvent.");
        
        // *** THE FIX ***
        // Tell the InteractionManager to disable this component.
        Systems.Interaction.InteractionManager.Instance.DisableInteractableComponent<CashRegisterInteractable>(gameObject);

        currentWaitingCustomerRunner = null;
        isInteracting = false;

        EventManager.Publish(new CashRegisterFreeEvent());
    }

    public void OnMinigameCompleted(float totalPaymentAmount)
    {
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Minigame completed. Total payment calculated: {totalPaymentAmount}.");

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddCurrency(totalPaymentAmount);
        }
        else
        {
            Debug.LogError($"CashRegisterInteractable ({gameObject.name}): EconomyManager instance not found! Cannot process payment.", this);
        }

        if (currentWaitingCustomerRunner != null)
        {
             EventManager.Publish(new NpcTransactionCompletedEvent(currentWaitingCustomerRunner.gameObject, totalPaymentAmount));
        }
        else
        {
            Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): Minigame completed, but no currentWaitingCustomerRunner reference!", this);
        }

        isInteracting = false;
    }
}