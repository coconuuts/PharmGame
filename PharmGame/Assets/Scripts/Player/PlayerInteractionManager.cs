using UnityEngine;
using TMPro;
using Systems.Interaction; // Needed for IInteractable and InteractionResponse
using Systems.GameStates; // Needed for MenuManager and GameState enum
using Systems.Player; // Moved PlayerInteractionManager to Player namespace

namespace Systems.Player // Place in Systems.Player namespace for consistency
{
    public class PlayerInteractionManager : MonoBehaviour
    {
        [Header("Raycast Settings")]
        public float rayDistance = 2f;
        public Transform cameraTransform;
        private bool isRaycastActive = true; // Flag controlled by state changes

        [Header("TMP Prompt Settings")]
        [Tooltip("Assign the TMP Text object used for interaction prompts.")]
        [SerializeField] private TMP_Text promptText; // Made private and serialized

         // Assuming you have a PromptManager or similar handling prompt UI
         // If PromptEditor.Instance exists and manages this, you might not need promptText here.
         // Let's assume for now that PromptEditor handles the actual text display,
         // and Activate/DeactivatePrompt methods on IInteractable call PromptEditor.

        private IInteractable currentInteractable;

        private void Awake()
        {
            if (cameraTransform == null)
            {
                // Attempt to find the MainCamera if not assigned
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                     cameraTransform = mainCamera.transform;
                }
                 else
                 {
                     Debug.LogError("PlayerInteractionManager: Camera Transform not assigned and no object with 'MainCamera' tag found!", this);
                     enabled = false; // Disable if no camera
                     return;
                 }
            }

            // If PromptEditor manages prompts, we don't need to initialize promptText here.
            // If this script manages the text directly, initialize it:
            /*
             if (promptText != null)
             {
                  promptText.text = "";
                  promptText.enabled = false; // Or promptText.gameObject.SetActive(false);
             }
             else
             {
                  Debug.LogWarning("PlayerInteractionManager: Prompt Text is not assigned. Interaction prompts will not be displayed.", this);
             }
            */
        }

        private void OnEnable()
        {
            // Subscribe to the state change event
            MenuManager.OnStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            // Unsubscribe from the state change event
            MenuManager.OnStateChanged -= HandleGameStateChanged;

            // Ensure prompt is deactivated and current interactable cleared when disabled
             if (currentInteractable != null)
             {
                  currentInteractable.DeactivatePrompt();
                  currentInteractable = null;
             }
             // If this script manages the prompt text directly:
             /*
             if (promptText != null)
             {
                  promptText.enabled = false; // Or promptText.gameObject.SetActive(false);
             }
             */
        }

        // --- ADDED: Handler for state changes ---
        private void HandleGameStateChanged(MenuManager.GameState newState, MenuManager.GameState oldState, InteractionResponse response)
        {
            // Enable raycast only in the Playing state
            if (newState == MenuManager.GameState.Playing)
            {
                EnableRaycast();
            }
            else
            {
                DisableRaycast(); // Disable raycast in all other states
            }
        }
        // ----------------------------------------


        private void Update()
        {
            // --- MODIFIED: isRaycastActive is controlled by the state handler now ---
            if (isRaycastActive)
            {
                HandleInteractionRaycast(); // Only perform raycast if active
            }
             else // If raycast is NOT active, ensure prompt is hidden and interactable is null
             {
                 if (currentInteractable != null)
                 {
                      currentInteractable.DeactivatePrompt(); // Hide prompt if it was visible
                      currentInteractable = null; // Clear the reference
                 }
                  // If this script manages the prompt text directly:
                  /*
                  if (promptText != null && promptText.enabled)
                  {
                       promptText.enabled = false; // Or promptText.gameObject.SetActive(false);
                  }
                  */
             }
            // -----------------------------------------------------------------------


            // When the player presses E and there's an interactable object AND raycast is active (implicit check via currentInteractable != null)
            // Also consider if interaction should ONLY be allowed in the Playing state.
            // Since MenuManager controls disabling the raycast in other states, the check currentInteractable != null is sufficient
            // *after* HandleInteractionRaycast runs (which it only does if isRaycastActive is true).
             if (Input.GetKeyDown(KeyCode.E) && currentInteractable != null)
             {
                  // --- Ensure interaction is only allowed in the Playing state ---
                  if (MenuManager.Instance != null && MenuManager.Instance.currentState == MenuManager.GameState.Playing)
                  {
                       // --- Call Interact and capture the response ---
                       Debug.Log("PlayerInteractionManager: 'E' pressed. Calling Interact().");
                       InteractionResponse response = currentInteractable.Interact();
                       Debug.Log($"PlayerInteractionManager: Interact() returned a response of type {response?.GetType().Name ?? "null"}.");
                       // ---------------------------------------------

                       currentInteractable.DeactivatePrompt(); // Deactivate prompt immediately after interaction
                       currentInteractable = null; // Clear the current interactable reference

                       // --- Pass the response to the MenuManager for handling ---
                       if (MenuManager.Instance != null)
                       {
                            Debug.Log("PlayerInteractionManager: Passing interaction response to MenuManager.");
                            // MenuManager.HandleInteractionResponse will determine if it causes a state change or simple action
                            MenuManager.Instance.HandleInteractionResponse(response);
                       }
                       else
                       {
                            Debug.LogError("PlayerInteractionManager: MenuManager Instance is null! Cannot handle interaction response.");
                       }
                  }
                  else
                  {
                       Debug.Log("PlayerInteractionManager: 'E' pressed, but not in the Playing state. Ignoring interaction.");
                       // Optionally deactivate the prompt if it was somehow still visible outside Playing
                       currentInteractable?.DeactivatePrompt();
                       currentInteractable = null;
                  }
             }
        }

        /// <summary>
        /// Casts a ray from the camera to detect interactable objects and manages prompts.
        /// Only runs if isRaycastActive is true.
        /// </summary>
        private void HandleInteractionRaycast()
        {
            if (cameraTransform == null)
            {
                 Debug.LogError("PlayerInteractionManager: cameraTransform is null in HandleInteractionRaycast!");
                 return;
            }

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            RaycastHit hit;
            IInteractable hitInteractable = null;

            if (Physics.Raycast(ray, out hit, rayDistance))
            {
                 // Check the collider first to see if it's a trigger or has an IInteractable
                 // Using GetComponent<IInteractable>() is generally more direct than checking isTrigger if you're specifically looking for the interface.
                 // However, keeping the isTrigger check might be intentional if only triggers are interactable. Let's stick to the original logic flow.
                 if (hit.collider.isTrigger)
                 {
                      hitInteractable = hit.collider.GetComponent<IInteractable>();
                 }
                 // If not a trigger, check the collider's GameObject for IInteractable anyway (in case interactable is on a non-trigger collider)
                 // This adds robustness.
                 if (hitInteractable == null)
                 {
                      hitInteractable = hit.collider.GetComponentInParent<IInteractable>(); // Check parent hierarchy too
                 }
            }

             // --- Manage prompt and currentInteractable based on raycast hit ---
             if (hitInteractable != null)
             {
                  // If we hit a NEW interactable
                  if (currentInteractable != hitInteractable)
                  {
                       currentInteractable?.DeactivatePrompt(); // Deactivate prompt on the old one (if any)
                       currentInteractable = hitInteractable; // Set the new one
                       currentInteractable.ActivatePrompt(); // Activate prompt on the new one
                  }
                  // If we hit the SAME interactable, do nothing (prompt is already active)
             }
             else // If raycast did NOT hit an interactable
             {
                  // If we were looking at an interactable, deactivate its prompt and clear the reference
                  if (currentInteractable != null)
                  {
                       currentInteractable.DeactivatePrompt();
                       currentInteractable = null;
                  }
             }
             // -------------------------------------------------------------------
        }


        // --- Public methods to enable/disable raycast (called by StateAction SO or event handler) ---
        /// <summary>
        /// Enables the interaction raycast.
        /// Called by StateAction Scriptable Objects or event handlers.
        /// </summary>
        public void EnableRaycast()
        {
            isRaycastActive = true;
             Debug.Log("PlayerInteractionManager: Interaction raycast enabled.");
        }

        /// <summary>
        /// Disables the interaction raycast and hides any active prompt.
        /// Called by StateAction Scriptable Objects or event handlers.
        /// </summary>
        public void DisableRaycast()
        {
            isRaycastActive = false;
             // Ensure prompt is hidden and current interactable is cleared immediately when disabled
             if (currentInteractable != null)
             {
                  currentInteractable.DeactivatePrompt();
                  currentInteractable = null;
             }
              // If this script manages the prompt text directly:
              /*
              if (promptText != null && promptText.enabled)
              {
                   promptText.enabled = false; // Or promptText.gameObject.SetActive(false);
              }
              */
            Debug.Log("PlayerInteractionManager: Interaction raycast disabled.");
        }
        // ---------------------------------------------------------------------------------------
    }
}