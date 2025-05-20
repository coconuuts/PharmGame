// Systems/CameraControl/CameraManager.cs
using UnityEngine;
using System.Collections;
using Systems.Interaction; // Needed if state actions pass response directly
using Systems.GameStates; // Needed for MenuManager and GameState enum


namespace Systems.CameraControl
{
    public class CameraManager : MonoBehaviour
    {
        public static CameraManager Instance { get; private set; }

        public enum CameraMode
        {
            MouseLook,
            CinematicView,
            Locked
        }

        [Header("References")]
        [Tooltip("The player camera's Transform.")]
        [SerializeField] private Transform playerCameraTransform;

        [Tooltip("The player body's Transform (for horizontal rotation in mouse look).")]
        [SerializeField] private Transform playerBodyTransform; // Assume player body rotates horizontally

        [Header("Mouse Look Settings")]
        [Tooltip("Mouse sensitivity for camera rotation.")]
        [SerializeField] public float mouseSensitivity = 200f;
        [Tooltip("Vertical rotation limits (min X, max X).")]
        [SerializeField] private Vector2 verticalRotationLimits = new Vector2(-75f, 60f);

         [Header("Cinematic Settings")]
         [Tooltip("The duration for the automatic camera return movement after exiting a cinematic state.")]
         [SerializeField] private float returnMoveDuration = 0.25f; // Configurable return duration


        private CameraMode currentMode = CameraMode.Locked; // Start in Locked mode

        private float xRotation = 0f; // Current vertical rotation

        // The currently running camera movement coroutine
        private Coroutine cameraMoveCoroutine;

        // Fields to store player camera's view before cinematic
        // Stored when entering CinematicView from MouseLook
        private Vector3 storedPlayerCamPosition;
        private Quaternion storedPlayerCamRotation;
        // Flag to indicate if stored view is valid and a return journey is intended
        private bool hasStoredPlayerView = false;
        // Flag to prevent storing the view multiple times if SetCameraMode is called repeatedly
        private bool isStoringView = false;


        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Debug.LogWarning("CameraManager: Duplicate instance found. Destroying this one.", this); Destroy(gameObject); return; }
            Debug.Log("CameraManager: Awake completed.");

            if (playerCameraTransform == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                     playerCameraTransform = mainCamera.transform;
                }
                 else
                 {
                     Debug.LogError("CameraManager: Player Camera Transform not assigned and no object with 'MainCamera' tag found!", this);
                     enabled = false;
                     return;
                 }
            }

            if (playerBodyTransform == null && playerCameraTransform != null && playerCameraTransform.parent != null)
            {
                playerBodyTransform = playerCameraTransform.parent;
                Debug.LogWarning("CameraManager: Player Body Transform not assigned. Assuming camera parent is the body.", this);
            }

            if (playerCameraTransform != null && playerBodyTransform != null)
            {
                Vector3 eulerRotation = playerCameraTransform.localEulerAngles;
                xRotation = eulerRotation.x;
                if (xRotation > 180) xRotation -= 360; // Adjust for Euler angles > 180
            }
            else
            {
                Debug.LogError("CameraManager: Essential camera references (Camera/Body) are null. Camera control disabled.", this);
                enabled = false;
            }

            currentMode = CameraMode.Locked; // Start in Locked mode
             Cursor.lockState = CursorLockMode.None; // Ensure cursor is visible initially
        }

        private void OnEnable()
        {
            Systems.GameStates.MenuManager.OnStateChanged += HandleGameStateChanged;
            Debug.Log("CameraManager: Subscribed to MenuManager.OnStateChanged.");
        }

        private void OnDisable()
        {
            Systems.GameStates.MenuManager.OnStateChanged -= HandleGameStateChanged;
            Debug.Log("CameraManager: Unsubscribed from MenuManager.OnStateChanged.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
             if (cameraMoveCoroutine != null)
             {
                  StopCoroutine(cameraMoveCoroutine);
                  cameraMoveCoroutine = null;
             }
        }

        /// <summary>
        /// Event handler for MenuManager.OnStateChanged.
        /// Automatically triggers camera return movement and sets mode when exiting cinematic states
        /// (including now exiting Crafting state).
        /// </summary>
        private void HandleGameStateChanged(Systems.GameStates.MenuManager.GameState newState, Systems.GameStates.MenuManager.GameState oldState, InteractionResponse response)
        {
             Debug.Log($"CameraManager: Handling state change from {oldState} to {newState}.");

            // --- Check if we are exiting a cinematic-related state and returning to Playing ---
            // Check if the OLD state was InComputer OR InMinigame OR InCrafting
            // AND the NEW state is Playing
            // AND we have stored a player view to return to (`hasStoredPlayerView` should be true if we entered one of these states from MouseLook)
            if ((oldState == Systems.GameStates.MenuManager.GameState.InComputer ||
                 oldState == Systems.GameStates.MenuManager.GameState.InMinigame ||
                 oldState == Systems.GameStates.MenuManager.GameState.InCrafting) && // ADDED check for InCrafting
                newState == Systems.GameStates.MenuManager.GameState.Playing &&
                hasStoredPlayerView) // Check the flag
            {
                 Debug.Log($"CameraManager: >>> Initiating Automatic Return Journey <<<");
                 Debug.Log($"CameraManager: hasStoredPlayerView = {hasStoredPlayerView}");
                 Debug.Log($"CameraManager: Stored Player Position: {storedPlayerCamPosition}, Stored Player Rotation: {storedPlayerCamRotation}");

                 // Set mode directly to MouseLook here. MouseLook input handling will resume in Update
                 // (handled by SetPlayerControl/SetMouseLook actions typically attached to Playing entry).
                 currentMode = CameraMode.MouseLook; // Setting mode directly
                 Debug.Log("CameraManager: Setting mode directly to MouseLook for return transition.");

                 // Stop any existing movement before starting the new one
                 if (cameraMoveCoroutine != null)
                 {
                     StopCoroutine(cameraMoveCoroutine);
                     cameraMoveCoroutine = null;
                     Debug.Log("CameraManager: Stopped any ongoing movement before starting return.");
                 }

                 // Start the return movement coroutine
                 cameraMoveCoroutine = StartCoroutine(
                     MoveCameraCoroutine(
                         playerCameraTransform.position,
                         playerCameraTransform.rotation,
                         storedPlayerCamPosition,
                         storedPlayerCamRotation,
                         returnMoveDuration,
                         true // Indicate this is a return journey
                     )
                 );

                 // Reset the stored player view flag immediately after initiating the return
                 hasStoredPlayerView = false;
                 isStoringView = false; // Also reset the storing flag
                 Debug.Log($"CameraManager: hasStoredPlayerView set to {hasStoredPlayerView}, isStoringView set to {isStoringView} after initiating return.");

                 // IMPORTANT: We are NOT calling SetCameraMode(MouseLook) here. The mode is already set.
            }
             else
             {
                  Debug.Log($"CameraManager: No automatic return needed for {oldState} -> {newState} (hasStoredPlayerView: {hasStoredPlayerView}).");
                  // If we transition to a state that is NOT Playing, the camera might stay cinematic or locked.
                  // E.g., InMinigame -> InPauseMenu -> InMinigame. We don't want to return to player view just to go back to the cinematic view.
                  // The logic above correctly handles this by only returning when the new state is Playing.
             }
        }


        private void Update()
        {
            if (playerCameraTransform == null || playerBodyTransform == null || !enabled) return;

            switch (currentMode)
            {
                case CameraMode.MouseLook:
                    HandleMouseLookInput();
                    break;
                case CameraMode.CinematicView:
                    // Camera movement is handled by the coroutine (if active)
                    break;
                case CameraMode.Locked:
                    // Camera is static
                    break;
            }
        }

        private void HandleMouseLookInput()
        {
             if (Cursor.lockState != CursorLockMode.Locked) return;
             if (playerCameraTransform == null) { Debug.LogError("CameraManager: playerCameraTransform is null in HandleMouseLookInput!"); return; }
             if (playerBodyTransform == null) { Debug.LogError("CameraManager: playerBodyTransform is null in HandleMouseLookInput!"); return; }

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, verticalRotationLimits.x, verticalRotationLimits.y);
            playerCameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

            playerBodyTransform.Rotate(Vector3.up * mouseX);
        }


        /// <summary>
        /// Sets the current camera behavior mode.
        /// This method is called by StateAction Scriptable Objects for specific mode transitions.
        /// The automatic return from cinematic states is handled by the state change event handler.
        /// </summary>
        /// <param name="mode">The CameraMode to switch to.</param>
        /// <param name="targetView">Optional: The Transform for CinematicView mode (passed by Action SO).</param>
        /// <param name="duration">Optional: The duration for CinematicView movement (passed by Action SO).</param>
        public void SetCameraMode(CameraMode mode, Transform targetView = null, float duration = 0.5f)
        {
             Debug.Log($"CameraManager.SetCameraMode called: currentMode = {currentMode}, requested mode = {mode}. Target view: {targetView?.name ?? "none"}, Duration: {duration}.");

             // --- FIX: Capture oldMode before changing currentMode ---
             CameraMode oldMode = currentMode;
             // -------------------------------------------------------

             // Ignore calls to MouseLook if a return coroutine is currently active
             // This prevents the PlayingConfig entry action from interrupting the return.
             // HandleGameStateChanged already sets the mode to MouseLook when starting a return.
             if (mode == CameraMode.MouseLook && cameraMoveCoroutine != null)
             {
                 // If the current mode is already MouseLook and a coroutine is active, it MUST be a return journey.
                 // Just log and return, don't interrupt.
                 if (currentMode == CameraMode.MouseLook)
                 {
                      Debug.LogWarning("CameraManager: Ignoring SetCameraMode(MouseLook) call because a camera movement coroutine is active (likely a return journey).", this);
                      return;
                 }
                 // If mode is DIFFERENT from MouseLook but a coroutine is active, it's a new cinematic overriding an old one or a return.
                 // Let the standard logic below handle stopping the old coroutine.
             }


             if (currentMode == mode && mode != CameraMode.CinematicView)
             {
                  Debug.Log($"CameraManager: Already in mode {mode} and not requesting CinematicView. Ignoring SetCameraMode call.");
                  return;
             }

            // --- Handle Exit Logic for the current mode ---
            // Stop the coroutine ONLY if transitioning FROM CinematicView
            // or if transitioning TO CinematicView (overwriting a previous move).
            // Do NOT stop it if transitioning from MouseLook/Locked to MouseLook/Locked,
            // as the active coroutine might be a paused return that we want to resume.
             if (currentMode == CameraMode.CinematicView && cameraMoveCoroutine != null)
             {
                 StopCoroutine(cameraMoveCoroutine);
                 cameraMoveCoroutine = null;
                 Debug.Log("CameraManager: Stopped ongoing cinematic coroutine (leaving CinematicView).");
             }
             else if (mode == CameraMode.CinematicView && cameraMoveCoroutine != null) // Transitioning TO CinematicView while a move is active (overwriting)
             {
                  StopCoroutine(cameraMoveCoroutine);
                  cameraMoveCoroutine = null;
                  Debug.Log("CameraManager: Stopped ongoing movement (transitioning to new CinematicView).");
             }


            // --- Store player view when transitioning from MouseLook to any other mode ---
            // Only store if currentMode IS MouseLook AND requested mode IS NOT MouseLook
            // AND we are NOT already in the process of storing (debounce multiple calls in one frame)
            // Using the captured 'oldMode' here.
             if (oldMode == CameraMode.MouseLook && mode != CameraMode.MouseLook && playerCameraTransform != null && !hasStoredPlayerView && !isStoringView)
             {
                  isStoringView = true; // Set storing flag
                  storedPlayerCamPosition = playerCameraTransform.position;
                  storedPlayerCamRotation = playerCameraTransform.rotation;
                  hasStoredPlayerView = true; // Now we have a valid stored view
                  Debug.Log($"CameraManager: Stored player camera position {storedPlayerCamPosition}/rotation {storedPlayerCamRotation}. hasStoredPlayerView = {hasStoredPlayerView}, isStoringView = {isStoringView}");
             }


            // Set the new mode
            currentMode = mode; // Set current mode AFTER capturing oldMode
            Debug.Log($"CameraManager: Mode set to {currentMode}.");


            // --- Handle Entry Logic for the new mode ---
            switch (currentMode)
            {
                case CameraMode.MouseLook:
                     // This case is now primarily for transitions not from a cinematic return
                     // (e.g., initial state, from Locked via a direct SO action).
                     Debug.Log("CameraManager: Entry logic for MouseLook mode (normal entry).");
                     // No movement logic needed here.
                    break;

                case CameraMode.CinematicView:
                    if (targetView == null)
                    {
                        Debug.LogError("CameraManager: SetCameraMode(CinematicView) called but targetView is null!", this);
                        // Fallback: Go back to the mode we came from (if not MouseLook -> Cinematic)
                        // or to MouseLook if we came from MouseLook.
                        SetCameraMode(hasStoredPlayerView ? CameraMode.MouseLook : CameraMode.Locked); // Try returning if view was stored, otherwise lock
                        return;
                    }
                    // Start movement coroutine FORWARD to the cinematic target
                    // Stop any potential return coroutine here as we are initiating a new cinematic view.
                    if (cameraMoveCoroutine != null)
                    {
                        StopCoroutine(cameraMoveCoroutine);
                        cameraMoveCoroutine = null;
                        Debug.Log("CameraManager: Stopped any existing movement (including potential return) for new CinematicView.");
                    }

                    cameraMoveCoroutine = StartCoroutine(
                        MoveCameraCoroutine(
                            playerCameraTransform.position, // Use current position as start
                            playerCameraTransform.rotation, // Use current rotation as start
                            targetView.position,
                            targetView.rotation,
                            duration,
                            false // Not a return journey
                        )
                    );
                    Debug.Log($"CameraManager: Entered CinematicView mode. Moving to {targetView.gameObject.name} over {duration}s.", targetView.gameObject);
                    break;

                case CameraMode.Locked:
                    Debug.Log("CameraManager: Entered Locked mode.");
                     // Ensure any ongoing coroutine (cinematic or return) is stopped if explicitly locked.
                     if (cameraMoveCoroutine != null)
                     {
                          StopCoroutine(cameraMoveCoroutine);
                          cameraMoveCoroutine = null;
                          Debug.Log("CameraManager: Stopped any ongoing movement due to entering Locked mode.");
                     }
                    break;
            }

             // If we transitioned from MouseLook and successfully entered another mode (Cinematic/Locked),
             // the storing process is complete.
             // Using the captured 'oldMode' here.
             if (oldMode == CameraMode.MouseLook && mode != CameraMode.MouseLook)
             {
                 isStoringView = false; // Reset the storing flag
                 Debug.Log($"CameraManager: Finished storing view process. isStoringView = {isStoringView}.");
             }
        }

        /// <summary>
        /// Moves the player camera smoothly between two points.
        /// Uses unscaledDeltaTime for duration calculation.
        /// Sets cameraMoveCoroutine to null upon completion.
        /// </summary>
        private IEnumerator MoveCameraCoroutine(Vector3 startPosition, Quaternion startRotation, Vector3 targetPosition, Quaternion targetRotation, float duration, bool isReturnJourney)
        {
            if (playerCameraTransform == null || duration <= 0)
            {
                 cameraMoveCoroutine = null; // Ensure coroutine reference is cleared even on early exit
                 Debug.LogWarning("CameraManager: Camera movement coroutine exiting early due to invalid parameters.", this);
                 yield break;
            }

            float elapsed = 0f;

            // Use the *actual* current position/rotation of the camera at the start of the coroutine
            // This handles cases where SetCameraMode is called while a previous move is in progress.
            // We already have the start position and rotation passed in from the caller (SetCameraMode)
            // based on the camera's state *just before* this coroutine starts.
            // No need to capture them again here.

             Debug.Log($"CameraManager: Starting MoveCameraCoroutine. Duration: {duration}s, Return: {isReturnJourney}. From: {startPosition} to: {targetPosition}");


            while (elapsed < duration)
            {
                // Check if the camera object has been destroyed during the move
                if (playerCameraTransform == null)
                {
                     Debug.LogWarning("CameraManager: Player Camera Transform is null mid-coroutine, aborting movement.", this);
                     cameraMoveCoroutine = null; // Clear coroutine reference
                     yield break; // Exit coroutine
                }

                // Use unscaledDeltaTime for duration calculation so it works during pauses
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                playerCameraTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
                playerCameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

                // Yield using null to pause when timeScale is 0
                yield return null;
            }

            // Ensure the camera reaches the exact target
            if (playerCameraTransform != null) // Check if camera object still exists
            {
                 playerCameraTransform.position = targetPosition;
                 playerCameraTransform.rotation = targetRotation;
                 Debug.Log("CameraManager: Camera movement coroutine reached target.");
            }
            else
            {
                 Debug.LogWarning("CameraManager: Player Camera Transform is null after movement loop.", this);
            }

            cameraMoveCoroutine = null; // Clear coroutine reference when finished
            Debug.Log("CameraManager: Camera movement coroutine finished.");

            // The camera mode was already set by HandleGameStateChanged or SetCameraMode
             if (isReturnJourney)
             {
                 Debug.Log("CameraManager: Return journey coroutine finished. Camera mode is already MouseLook.");
             }
             else
             {
                 Debug.Log("CameraManager: Forward cinematic coroutine finished. Camera mode is already CinematicView or Locked.");
             }
        }
    }
}