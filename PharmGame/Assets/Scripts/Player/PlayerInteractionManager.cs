using UnityEngine;
using TMPro;

public class PlayerInteractionManager : MonoBehaviour
{
    [Header("Raycast Settings")]
    public float rayDistance = 2f;
    public Transform cameraTransform;
    private bool isRaycastActive = true; // Added flag to control raycast

    [Header("TMP Prompt Settings")]
    // The shared TextMeshPro UI element reused across objects.
    public TMP_Text promptText;

    // Holds the current interactable object in view.
    private IInteractable currentInteractable;

    private void Awake()
    {
        // Use the main camera if no cameraTransform is explicitly provided.
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }

        // Hide the prompt text initially.
        if (promptText != null)
        {
            promptText.text = "";
            promptText.enabled = false;
        }
    }


    private void Update()
    {
        if (isRaycastActive) // Only perform raycast if it's active
        {
            HandleInteractionRaycast();
        }

        // When the player presses E and there's an interactable object, trigger the interaction.
        if (Input.GetKeyDown(KeyCode.E) && currentInteractable != null)
        {
            currentInteractable.Interact();
            currentInteractable.DeactivatePrompt();
            currentInteractable = null;
        }
    }

    /// <summary>
    /// Casts a ray from the camera to detect interactable objects.
    /// </summary>
    private void HandleInteractionRaycast()
    {
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        RaycastHit hit;

        // If an interactable object is already active, clear the previous prompt.
        if (currentInteractable != null)
        {
            currentInteractable.DeactivatePrompt();
            currentInteractable = null;
        }

        // Check if the ray hits a collider within the specified distance.
        if (Physics.Raycast(ray, out hit, rayDistance))
        {
            // Process only if the hit collider is a trigger.
            if (hit.collider.isTrigger)
            {
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    if (currentInteractable != interactable)
                    {
                        // Deactivate the previous prompt if any.
                        currentInteractable?.DeactivatePrompt();
                        currentInteractable = interactable;
                        // Activate the prompt at the object's origin.
                        currentInteractable.ActivatePrompt();
                    }
                    return; // Exit early if an interactable object is found.
                }
            }
        }

        // If no interactable object is hit, ensure the prompt is hidden.
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
