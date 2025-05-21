using UnityEngine;
using System; // Needed for Action event

namespace Systems.Minigame.Clicking // Or a more specific sub-namespace
{
    /// <summary>
    /// General component to handle player click input and raycasting.
    /// Fires an event when a clickable object on a specific layer is hit.
    /// </summary>
    public class ClickInteractor : MonoBehaviour
    {
        [Tooltip("The Camera used for raycasting.")]
        private Camera interactionCamera; // Set this via Initialize

        [Tooltip("The LayerMask for objects that are considered clickable.")]
        private LayerMask clickableLayer; // Set this via Initialize

        [Tooltip("Is interaction currently enabled?")]
        private bool isInteractionEnabled = false;

        /// <summary>
        /// Event triggered when a clickable object is clicked.
        /// The parameter is the GameObject that was hit by the raycast.
        /// </summary>
        public event Action<GameObject> OnObjectClicked;

        /// <summary>
        /// Initializes the click interactor with necessary references.
        /// </summary>
        public void Initialize(Camera cam, LayerMask layer)
        {
            if (cam == null)
            {
                Debug.LogError("ClickInteractor: Initialization failed. Interaction Camera is null.");
                // Attempt to use Camera.main as a fallback if null, though explicit assignment is better
                 interactionCamera = Camera.main;
                 if (interactionCamera == null)
                 {
                      Debug.LogError("ClickInteractor: Initialization failed. Could not find a valid camera.");
                      return; // Cannot function without a camera
                 }
                 else Debug.LogWarning("ClickInteractor: Initialized with Camera.main as fallback.", this);
            }
             else interactionCamera = cam;

            clickableLayer = layer;

            Debug.Log($"ClickInteractor: Initialized with Camera '{interactionCamera.name}' and LayerMask '{LayerMask.LayerToName(GetSingleLayerFromMask(clickableLayer))}'...", this);
        }

        // Helper to get a single layer index from a mask for logging (handles simple masks)
        private int GetSingleLayerFromMask(LayerMask mask)
        {
            int layer = 0;
            while (((1 << layer) & mask) == 0 && layer < 32)
            {
                layer++;
            }
            return layer < 32 ? layer : -1;
        }


        /// <summary>
        /// Enables the click interactor to start processing input.
        /// </summary>
        public void EnableInteraction()
        {
            isInteractionEnabled = true;
            Debug.Log("ClickInteractor: Interaction enabled.", this);
        }

        /// <summary>
        /// Disables the click interactor, preventing further input processing.
        /// </summary>
        public void DisableInteraction()
        {
            isInteractionEnabled = false;
            Debug.Log("ClickInteractor: Interaction disabled.", this);
        }

        void Update()
        {
            if (!isInteractionEnabled || interactionCamera == null)
            {
                return;
            }

            // Check for left mouse button click
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = interactionCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                // Perform the raycast using the specified layer mask
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, clickableLayer))
                {
                    // An object on the clickable layer was hit, trigger the event
                    // Debug.Log($"ClickInteractor: Raycast hit {hit.collider.gameObject.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}", hit.collider.gameObject);
                    OnObjectClicked?.Invoke(hit.collider.gameObject);
                }
                 else
                 {
                     // Debug.Log("ClickInteractor: Raycast hit nothing on the clickable layer.");
                 }
            }
        }

        /// <summary>
        /// Cleanup method to ensure event subscriptions are cleared.
        /// </summary>
        public void PerformCleanup()
        {
            DisableInteraction(); // Ensure interaction is off
            // Null out the event to prevent stale subscriptions on pooled/reused interactors
            if (OnObjectClicked != null)
            {
                 foreach (Delegate d in OnObjectClicked.GetInvocationList())
                 {
                     OnObjectClicked -= (Action<GameObject>)d;
                 }
            }
            OnObjectClicked = null; // Ensure it's completely null after clearing
            Debug.Log("ClickInteractor: Performed cleanup (disabled, cleared event).", this);
        }
    }
}