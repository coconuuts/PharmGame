// --- START OF FILE PlayerInteractionManager.cs ---

// Systems/Player/PlayerInteractionManager.cs
// Keep existing usings and namespace

using UnityEngine;
using TMPro;
using Systems.Interaction; // Needed for IInteractable, InteractionResponse
using Systems.GameStates; // Needed for MenuManager and GameState enum
using Systems.Player; // Moved PlayerInteractionManager to Player namespace
using System.Linq; // Needed for OfType if you modify Awake/Start

namespace Systems.Player // Place in Systems.Player namespace for consistency
{
    public class PlayerInteractionManager : MonoBehaviour
    {
        [Header("Raycast Settings")]
        public float rayDistance = 2f;
        public Transform cameraTransform;
        private bool isRaycastActive = true; // Flag controlled by state changes

        // promptText is now managed by PromptEditor via IInteractable methods,
        // so this field can be removed or kept for debugging if needed, but isn't used for display here.
        // [Header("TMP Prompt Settings")]
        // [Tooltip("Assign the TMP Text object used for interaction prompts.")]
        // [SerializeField] private TMP_Text promptText; // Made private and serialized


        private IInteractable currentInteractable; // The IInteractable component currently hovered over and enabled

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
                     // Don't disable, still need state change handling
                     // enabled = false; // Keep enabled for state handling
                     // return; // Don't return, allow state handling to potentially fix things
                 }
            }
        }

        private void OnEnable()
        {
            // Subscribe to the state change event
            MenuManager.OnStateChanged += HandleGameStateChanged;
             // Ensure raycast state is correct on enable based on current game state
            if (MenuManager.Instance != null)
            {
                // Call HandleGameStateChanged with the current state to initialize isRaycastActive
                HandleGameStateChanged(MenuManager.Instance.currentState, MenuManager.Instance.currentState, null); // Simulate state change for current state
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from the state change event
            MenuManager.OnStateChanged -= HandleGameStateChanged;

            // Ensure prompt is deactivated and current interactable cleared when disabled
             if (currentInteractable != null)
             {
                  if (PromptEditor.HasInstance)
                  {
                      currentInteractable.DeactivatePrompt();
                  }
                  currentInteractable = null;
             }
        }

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


        private void Update()
        {
            // HandleInteractionRaycast only runs if isRaycastActive is true
            HandleInteractionRaycast();

            // When the player presses E and there's an interactable object
            // currentInteractable is only set if raycast is active AND it finds an enabled interactable.
             if (Input.GetKeyDown(KeyCode.E) && currentInteractable != null)
             {
                  // --- Ensure interaction is only allowed in the Playing state ---
                  // This check is technically redundant if DisableRaycast works correctly
                  // on state change, but it adds robustness.
                  if (MenuManager.Instance != null && MenuManager.Instance.currentState == MenuManager.GameState.Playing)
                  {
                       // --- Call Interact and capture the response ---
                       Debug.Log("PlayerInteractionManager: 'E' pressed. Calling Interact().");
                       InteractionResponse response = currentInteractable.Interact();
                       Debug.Log($"PlayerInteractionManager: Interact() returned a response of type {response?.GetType().Name ?? "null"}.");
                       // ---------------------------------------------

                       // Deactivate prompt and clear the current interactable reference
                       // This ensures the prompt goes away immediately after interaction,
                       // even if the raycast is still hitting the object for a frame or two
                       // before the state potentially changes.
                       currentInteractable.DeactivatePrompt();
                       currentInteractable = null;

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
                       // This case should ideally not happen if DisableRaycast works,
                       // as currentInteractable should be null outside Playing state.
                       // But log defensively.
                       Debug.LogWarning("PlayerInteractionManager: 'E' pressed, but not in the Playing state. Ignoring interaction.", this);
                       // Ensure prompt is deactivated just in case
                       currentInteractable?.DeactivatePrompt();
                       currentInteractable = null;
                  }
             }
        }

        /// <summary>
        /// Casts a ray from the camera to detect interactable objects and manages prompts.
        /// Only runs if isRaycastActive is true.
        /// Relies on InteractionManager singleton to enable/disable IInteractable components.
        /// </summary>
        private void HandleInteractionRaycast()
        {
            if (!isRaycastActive || cameraTransform == null)
            {
                 // If raycast is inactive or camera is null, ensure prompt is hidden and interactable is null
                 if (currentInteractable != null)
                 {
                      currentInteractable.DeactivatePrompt();
                      currentInteractable = null;
                 }
                 return;
            }

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            RaycastHit hit;
            IInteractable hitInteractable = null; // This will hold the found *enabled* interactable component

            if (Physics.Raycast(ray, out hit, rayDistance))
            {
                 GameObject hitObject = hit.collider.gameObject;

                 // --- NEW CORRECTED LOGIC: Find all IInteractable components and check their enabled state ---
                 IInteractable[] potentialInteractables = hitObject.GetComponentsInParent<IInteractable>();

                 foreach (IInteractable interactable in potentialInteractables)
                 {
                     // Check if the interactable is a MonoBehaviour and if it is currently enabled
                     if (interactable is MonoBehaviour monoBehaviour && monoBehaviour.enabled)
                     {
                         // Found the enabled interactable!
                         hitInteractable = interactable;
                         // Optional: Add a check for multiple enabled interactables (shouldn't happen with InteractionManager)
                         // if (foundEnabledInteractable != null) Debug.LogWarning($"PlayerInteractionManager: Found multiple enabled IInteractable components on {hitObject.name} or its parents!", hitObject);
                         // foundEnabledInteractable = interactable;
                         break; // Stop searching once the first enabled one is found
                     }
                 }
                 // --- END NEW CORRECTED LOGIC ---


                // The rest of the logic remains the same:
                // Manage prompt and currentInteractable based on raycast hit (which is now the *enabled* one)
                 if (hitInteractable != null)
                 {
                      // If we hit a NEW interactable (or null -> interactable)
                      if (currentInteractable != hitInteractable)
                      {
                           currentInteractable?.DeactivatePrompt(); // Deactivate prompt on the old one (if any)
                           currentInteractable = hitInteractable; // Set the new one
                           currentInteractable.ActivatePrompt(); // Activate prompt on the new one
                      }
                      // If we hit the SAME interactable, do nothing (prompt is already active)
                 }
                 else // If raycast did NOT hit a valid (enabled) interactable
                 {
                      // If we were looking at an interactable, deactivate its prompt and clear the reference
                      if (currentInteractable != null)
                      {
                           currentInteractable.DeactivatePrompt();
                           currentInteractable = null;
                      }
                 }
            }
             else // If raycast hit nothing
             {
                  // If we were looking at an interactable, deactivate its prompt and clear the reference
                  if (currentInteractable != null)
                  {
                       currentInteractable.DeactivatePrompt();
                       currentInteractable = null;
                  }
             }
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
            Debug.Log("PlayerInteractionManager: Interaction raycast disabled.");
        }
    }
}
// --- END OF FILE PlayerInteractionManager.cs ---