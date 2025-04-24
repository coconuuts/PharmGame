using UnityEngine;
using Systems.Interaction; // Needed for InteractionResponse

public class CashRegisterInteractable : MonoBehaviour, IInteractable
{
    [Header("Minigame Settings")]
    [Tooltip("The camera transform the player should move to for the minigame.")]
    [SerializeField] private Transform minigameCameraViewPoint;

    [Tooltip("The duration of the camera movement animation to the minigame view.")]
    [SerializeField] private float cameraMoveDuration = 0.5f;

    [Tooltip("The root GameObject containing the minigame UI elements.")]
    [SerializeField] private GameObject minigameUIRoot;

    [Tooltip("The text to display in the interaction prompt.")]
    [SerializeField] private string interactionPrompt = "Use Cash Register (E)";

    // --- How the CashRegister knows the customer's purchase count ---
    // This is a placeholder. You'll need a system for customers and their purchases.
    [Header("Testing/Placeholder")]
    [Tooltip("Placeholder for the number of items the customer is buying.")]
    [SerializeField] private int testTargetClickCount = 5; // For testing purposes

    [Header("Prompt Settings")] // Assuming prompt settings are common, move these up if needed
     public Vector3 registerTextPromptOffset = Vector3.zero; // Consider renaming or removing
     public Vector3 registerTextPromptRotationOffset = Vector3.zero; // Consider renaming or removing


    public string InteractionPrompt => interactionPrompt;

    private bool isInteracting = false; // To prevent re-interacting while minigame is active

    private void Start()
    {
        if (minigameCameraViewPoint == null) Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Minigame Camera View Point is not assigned!", this);
        if (minigameUIRoot == null) Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Minigame UI Root is not assigned!", this);

        // Ensure the minigame UI is initially hidden
         if(minigameUIRoot != null) minigameUIRoot.SetActive(false);
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
    /// Returns StartMinigameResponse with minigame data and this instance.
    /// </summary>
    public InteractionResponse Interact()
    {
        // Prevent interaction if already interacting with this cash register
        if (isInteracting)
        {
            Debug.Log($"CashRegisterInteractable ({gameObject.name}): Already interacting with this cash register.");
            return null;
        }

        int actualTargetClickCount = testTargetClickCount;
        if (actualTargetClickCount <= 0)
        {
             Debug.LogWarning($"CashRegisterInteractable ({gameObject.name}): Target click count is zero or less. No minigame started.");
             return null;
        }

        if (minigameCameraViewPoint == null || minigameUIRoot == null)
        {
             Debug.LogError($"CashRegisterInteractable ({gameObject.name}): Cannot create StartMinigameResponse - Camera View Point or Minigame UI Root not assigned.", this);
             return null;
        }

        Debug.Log($"CashRegisterInteractable ({gameObject.name}): Interact called. Returning StartMinigameResponse with target clicks: {actualTargetClickCount}.", this);

        // --- Create and return the response ---
        StartMinigameResponse response = new StartMinigameResponse(
            minigameCameraViewPoint,
            cameraMoveDuration,
            minigameUIRoot,
            actualTargetClickCount,
            this // PASS THIS INSTANCE
        );

        isInteracting = true; // Mark as interacting

        return response;
    }

    // --- Public method to reset the interacting state (Called by MenuManager state exit action) ---
    public void ResetInteraction()
    {
        isInteracting = false;
        Debug.Log($"CashRegisterInteractable ({gameObject.name}): ResetInteraction called. isInteracting is now false.", this);
    }

    // TODO: Add a method here that receives the customer/purchase data
    // For example, a public method like SetCustomerPurchase(int itemCount)
    // That method would update testTargetClickCount or a real purchase count field.
}
