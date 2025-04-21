using UnityEngine;
using TMPro;
using Systems.Interaction; // ADD THIS USING

public class PlayerInteractionManager : MonoBehaviour
{
    [Header("Raycast Settings")]
    public float rayDistance = 2f;
    public Transform cameraTransform;
    private bool isRaycastActive = true;

    [Header("TMP Prompt Settings")]
    public TMP_Text promptText;

    private IInteractable currentInteractable;

    private void Awake()
    {
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (promptText != null)
        {
            promptText.text = "";
            promptText.enabled = false;
        }
    }

    private void Update()
    {
        if (isRaycastActive)
        {
            HandleInteractionRaycast();
        }

        // When the player presses E and there's an interactable object
        if (Input.GetKeyDown(KeyCode.E) && currentInteractable != null)
        {
            // --- Call Interact and capture the response ---
            Debug.Log("PlayerInteractionManager: 'E' pressed. Calling Interact().");
            InteractionResponse response = currentInteractable.Interact();
            Debug.Log($"PlayerInteractionManager: Interact() returned a response of type {response?.GetType().Name ?? "null"}.");
            // ---------------------------------------------

            currentInteractable.DeactivatePrompt(); // Deactivate prompt immediately after interaction
            currentInteractable = null; // Clear the current interactable reference

            // --- Pass the response to the MenuManager for handling ---
            // This part will be fully implemented in Step 4
            if (response != null && MenuManager.Instance != null)
            {
                Debug.Log("PlayerInteractionManager: Passing interaction response to MenuManager.");
                MenuManager.Instance.HandleInteractionResponse(response); // This method will be added in MenuManager (Step 4)
            }
            else
            {
                if (response == null) Debug.LogWarning("PlayerInteractionManager: Interactable returned a null response.");
                if (MenuManager.Instance == null) Debug.LogError("PlayerInteractionManager: MenuManager Instance is null! Cannot handle interaction response.");
                // Handle cases where there's no response or no MenuManager if necessary
            }
        }
    }

    // ... (HandleInteractionRaycast, DisableRaycast, EnableRaycast methods remain the same)
    /// <summary>
    /// Casts a ray from the camera to detect interactable objects.
    /// </summary>
    private void HandleInteractionRaycast()
    {
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        RaycastHit hit;

        if (currentInteractable != null)
        {
            currentInteractable.DeactivatePrompt();
            currentInteractable = null;
        }

        if (Physics.Raycast(ray, out hit, rayDistance))
        {
            if (hit.collider.isTrigger)
            {
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    if (currentInteractable != interactable)
                    {
                        currentInteractable?.DeactivatePrompt();
                        currentInteractable = interactable;
                        currentInteractable.ActivatePrompt();
                    }
                    return;
                }
            }
        }

        if (currentInteractable != null)
        {
            currentInteractable.DeactivatePrompt();
            currentInteractable = null;
        }
    }

    public void DisableRaycast()
    {
        isRaycastActive = false;
    }

    public void EnableRaycast()
    {
        isRaycastActive = true;
    }
}