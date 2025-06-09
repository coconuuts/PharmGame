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

        private float xRotation = 0f; // Current vertical rotation (local X rotation)

        // The currently running camera movement coroutine
        private Coroutine cameraMoveCoroutine;

        // --- Fields to store player camera's view RELATIVE to the player body ---
        private Vector3 storedPlayerCamLocalPosition; // Stored when entering CinematicView from MouseLook
        private float storedPlayerCamXRotation;       // Stored local vertical angle (xRotation)
        private float storedPlayerBodyYRotation;      // Stored player body Y rotation (horizontal)

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
                // Initialize xRotation from the camera's current local Euler angle X
                Vector3 eulerRotation = playerCameraTransform.localEulerAngles;
                xRotation = eulerRotation.x;
                if (xRotation > 180) xRotation -= 360; // Adjust for Euler angles > 180
                 Debug.Log($"CameraManager: Initial xRotation set to {xRotation} from localEulerAngles.x.", this);

                 // Store the initial local position and rotations as the default "player view"
                 storedPlayerCamLocalPosition = playerCameraTransform.localPosition;
                 storedPlayerCamXRotation = xRotation;
                 storedPlayerBodyYRotation = playerBodyTransform.rotation.eulerAngles.y;
                 Debug.Log($"CameraManager: Stored initial player camera local position {storedPlayerCamLocalPosition}, vertical rotation {storedPlayerCamXRotation}, body Y rotation {storedPlayerBodyYRotation} as default view.", this);
            }
            else
            {
                Debug.LogError("CameraManager: Essential camera references (Camera/Body) are null. Camera control disabled.", this);
                enabled = false;
            }

            currentMode = CameraMode.Locked; // Start in Locked mode
             Cursor.lockState = CursorLockMode.None; // Ensure cursor is visible initially
             Debug.Log("CameraManager: Initial mode set to Locked. Cursor unlocked.");
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
                  Debug.Log("CameraManager: Stopped ongoing coroutine in OnDestroy.");
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
            // ALSO check if playerBodyTransform is valid for calculating the return position
            if ((oldState == Systems.GameStates.MenuManager.GameState.InComputer ||
                 oldState == Systems.GameStates.MenuManager.GameState.InMinigame ||
                 oldState == Systems.GameStates.MenuManager.GameState.InCrafting) && 
                newState == Systems.GameStates.MenuManager.GameState.Playing &&
                hasStoredPlayerView && // Check the flag
                playerBodyTransform != null) // Ensure player body reference is valid for calculation
            {
                Debug.Log($"CameraManager: >>> Initiating Automatic Return Journey <<<");
                Debug.Log($"CameraManager: hasStoredPlayerView = {hasStoredPlayerView}");
                Debug.Log($"CameraManager: Stored Local Position: {storedPlayerCamLocalPosition}, Stored Vertical Rotation: {storedPlayerCamXRotation}, Stored Body Y Rotation: {storedPlayerBodyYRotation}");

                // Set mode directly to MouseLook here. MouseLook input handling will resume in Update
                // (handled by SetPlayerControl/SetMouseLook actions typically attached to Playing entry).
                // Setting mode early prevents input flickering if the return is fast.
                currentMode = CameraMode.MouseLook;
                Debug.Log("CameraManager: Setting mode directly to MouseLook for return transition.");

                // Stop any existing movement before starting the new one (this check is also in SetCameraMode, but redundant here is fine)
                if (cameraMoveCoroutine != null)
                {
                    StopCoroutine(cameraMoveCoroutine);
                    cameraMoveCoroutine = null;
                    Debug.Log("CameraManager: Stopped any ongoing movement before starting return.");
                }

                // --- Calculate the target world position/rotation based on current player body ---
                // Apply the stored local position offset relative to the current player body rotation
                Vector3 returnTargetPosition = playerBodyTransform.position + playerBodyTransform.rotation * storedPlayerCamLocalPosition;
                // Apply the stored vertical camera rotation relative to the current player body rotation
                // We need to also restore the player body's Y rotation to what it was, otherwise the camera
                // will return facing the wrong direction horizontally if the player body was rotated
                // while the minigame was active.
                Quaternion playerBodyTargetRotation = Quaternion.Euler(0f, storedPlayerBodyYRotation, 0f);
                Quaternion cameraLocalTargetRotation = Quaternion.Euler(storedPlayerCamXRotation, 0f, 0f);
                Quaternion returnTargetRotation = playerBodyTargetRotation * cameraLocalTargetRotation; // Body rotation * Camera local rotation

                Debug.Log($"CameraManager: Calculated return target based on current player body pos/rot. Target Position: {returnTargetPosition}, Target Rotation: {returnTargetRotation}");

                // --- Optional: Immediately set the player body's Y rotation back ---
                // This snaps the body rotation instantly, which feels okay.
                // An alternative is to animate the body rotation too, but that's more complex.
                // Snapping the body rotation here ensures the camera's *vertical* rotation
                // remains correct relative to the body's final orientation after the camera lerp finishes.
                playerBodyTransform.rotation = playerBodyTargetRotation;
                Debug.Log($"CameraManager: Snapped player body Y rotation back to stored value: {storedPlayerBodyYRotation}");

                // Start the return movement coroutine
                cameraMoveCoroutine = StartCoroutine(
                    MoveCameraCoroutine(
                        playerCameraTransform.position, // Start from the camera's current world position (the cinematic view)
                        playerCameraTransform.rotation, // Start from the camera's current world rotation
                        returnTargetPosition,           // Target world position calculated relative to the current player body
                        returnTargetRotation,           // Target world rotation calculated relative to the current player body
                        returnMoveDuration,
                        true // Indicate this is a return journey
                    )
                );

                // Reset the stored player view flag immediately after initiating the return
                hasStoredPlayerView = false;
                isStoringView = false; // Also reset the storing flag
                Debug.Log($"CameraManager: hasStoredPlayerView set to {hasStoredPlayerView}, isStoringView set to {isStoringView} after initiating return.");

                // IMPORTANT: We are NOT calling SetCameraMode(MouseLook) here. The mode is already set,
                // and the return movement is initiated. The SetCameraModeActionSO for Playing entry
                // will likely run shortly after this event handler finishes, but the logic inside
                // SetCameraMode will detect the active coroutine and the already-set mode and ignore the call.
            }
            else
            {
                // Debug log updated for clarity
                Debug.Log($"CameraManager: No automatic return needed for {oldState} -> {newState} (hasStoredPlayerView: {hasStoredPlayerView}, playerBodyTransform valid: {playerBodyTransform != null}).");
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

            // Update the stored xRotation value as the player looks around in MouseLook
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, verticalRotationLimits.x, verticalRotationLimits.y);
            playerCameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f); // Apply as local rotation to the camera

            playerBodyTransform.Rotate(Vector3.up * mouseX); // Apply horizontal rotation to the body
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

             // --- Capture oldMode before changing currentMode ---
             CameraMode oldMode = currentMode;
             // -------------------------------------------------------

             // Ignore calls to MouseLook if a return coroutine is currently active
             // This prevents the PlayingConfig entry action from interrupting the return.
             // HandleGameStateChanged already sets the mode to MouseLook when starting a return.
             // This check also prevents redundant logging from SetCameraModeActionSO if it's configured to run
             // when a return is already in progress.
             if (mode == CameraMode.MouseLook && cameraMoveCoroutine != null && currentMode == CameraMode.MouseLook)
             {
                 // If the current mode is already MouseLook and a coroutine is active, it MUST be a return journey initiated by HandleGameStateChanged.
                 // Just log and return, don't interrupt or change anything.
                 Debug.LogWarning("CameraManager: Ignoring SetCameraMode(MouseLook) call because camera is already in MouseLook and a return coroutine is active.", this);
                 return;
             }
             // If mode is DIFFERENT from MouseLook but a coroutine is active, it's a new cinematic overriding an old one or a return.
             // Let the standard logic below handle stopping the old coroutine.


             // Allow setting to the same mode if it's CinematicView, as target/duration might change.
             // Otherwise, ignore same mode calls.
             if (currentMode == mode && mode != CameraMode.CinematicView)
             {
                  Debug.Log($"CameraManager: Already in mode {mode} and not requesting CinematicView. Ignoring SetCameraMode call.");
                  return;
             }

            // --- Handle Exit Logic for the current mode ---
            // Stop the coroutine ONLY if transitioning FROM CinematicView
            // or if transitioning TO CinematicView (overwriting a previous move).
            // Do NOT stop it if transitioning from MouseLook/Locked to MouseLook/Locked,
            // as the active coroutine might be a paused return that we want to resume if going back to MouseLook.
             if (currentMode == CameraMode.CinematicView && cameraMoveCoroutine != null)
             {
                 StopCoroutine(cameraMoveCoroutine);
                 cameraMoveCoroutine = null;
                 Debug.Log("CameraManager: Stopped ongoing cinematic coroutine (leaving CinematicView).");
             }
             else if (mode == CameraMode.CinematicView && cameraMoveCoroutine != null) // Transitioning TO CinematicView while a move is active (overwriting)
             {
                  // This stops a *return* journey coroutine if a new cinematic is initiated while returning.
                  // Example: Player hits Escape from minigame, return starts. Before return finishes,
                  // player interacts with something else that starts a *different* cinematic.
                  StopCoroutine(cameraMoveCoroutine);
                  cameraMoveCoroutine = null;
                  Debug.Log("CameraManager: Stopped ongoing movement (including potential return) transitioning to new CinematicView.");
             }


            // --- Store player view when transitioning from MouseLook to any other mode ---
            // Only store if oldMode IS MouseLook AND requested mode IS NOT MouseLook
            // AND we are NOT already in the process of storing (debounce multiple calls in one frame)
            // AND playerBodyTransform is valid.
             if (oldMode == CameraMode.MouseLook && mode != CameraMode.MouseLook && playerCameraTransform != null && playerBodyTransform != null && !hasStoredPlayerView && !isStoringView)
             {
                  isStoringView = true; // Set storing flag
                  storedPlayerCamLocalPosition = playerCameraTransform.localPosition; // Store camera's position LOCAL to its parent (player body)
                  storedPlayerCamXRotation = xRotation; // Store local vertical angle
                  storedPlayerBodyYRotation = playerBodyTransform.rotation.eulerAngles.y; // Store player body's Y rotation (horizontal)
                  hasStoredPlayerView = true; // Now we have a valid stored view
                  Debug.Log($"CameraManager: Stored player camera local position {storedPlayerCamLocalPosition}, vertical rotation {storedPlayerCamXRotation}, body Y rotation {storedPlayerBodyYRotation}. hasStoredPlayerView = {hasStoredPlayerView}, isStoringView = {isStoringView}");
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
                     // If coming from a state that didn't store a view (e.g., Locked) or if the
                     // return journey just finished and reset hasStoredPlayerView,
                     // we are just setting the mode here. No movement needed.
                     Debug.Log("CameraManager: Entry logic for MouseLook mode (normal entry).");
                    break;

                case CameraMode.CinematicView:
                    if (targetView == null)
                    {
                        Debug.LogError("CameraManager: SetCameraMode(CinematicView) called but targetView is null!", this);
                        // Fallback: Go back to the mode we came from (if not MouseLook -> Cinematic)
                        // or to MouseLook if we came from MouseLook and have a stored view.
                        // If came from MouseLook and target was null, implies an error initiating the cinematic,
                        // so attempt to return to stored player view instead of locking.
                         if (oldMode == CameraMode.MouseLook && hasStoredPlayerView && playerBodyTransform != null)
                         {
                              Debug.LogWarning("CameraManager: Falling back to return to stored player view.", this);
                              // Recalculate and start return journey immediately
                              Vector3 returnTargetPosition = playerBodyTransform.position + playerBodyTransform.rotation * storedPlayerCamLocalPosition;
                              Quaternion playerBodyTargetRotation = Quaternion.Euler(0f, storedPlayerBodyYRotation, 0f);
                              Quaternion cameraLocalTargetRotation = Quaternion.Euler(storedPlayerCamXRotation, 0f, 0f);
                              Quaternion returnTargetRotation = playerBodyTargetRotation * cameraLocalTargetRotation;

                              playerBodyTransform.rotation = playerBodyTargetRotation; // Snap body rotation

                              cameraMoveCoroutine = StartCoroutine(
                                 MoveCameraCoroutine(
                                     playerCameraTransform.position,
                                     playerCameraTransform.rotation,
                                     returnTargetPosition,
                                     returnTargetRotation,
                                     returnMoveDuration, // Use return duration for this fallback
                                     true // Treat this error fallback as a return journey
                                 )
                              );
                             // Reset the flags immediately as the 'return' is initiated
                             hasStoredPlayerView = false;
                             isStoringView = false;
                             currentMode = CameraMode.MouseLook; // Set mode to MouseLook as we are returning
                         }
                         else
                         {
                              Debug.LogError("CameraManager: Falling back to Locked mode.", this);
                              SetCameraMode(CameraMode.Locked); // If no stored view or not from MouseLook, just lock
                         }
                        return; // Exit this SetCameraMode call
                    }
                    // Start movement coroutine FORWARD to the cinematic target
                    // If we reached here, it means we successfully validated targetView and duration.
                    // Any existing coroutine (including potential return) was stopped above.
                    cameraMoveCoroutine = StartCoroutine(
                        MoveCameraCoroutine(
                            playerCameraTransform.position, // Use camera's current world position as start
                            playerCameraTransform.rotation, // Use camera's current world rotation as start
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
                     // This handles cases where SetCameraMode(Locked) is called directly, e.g., by a debug command.
                     if (cameraMoveCoroutine != null)
                     {
                          StopCoroutine(cameraMoveCoroutine);
                          cameraMoveCoroutine = null;
                          Debug.Log("CameraManager: Stopped any ongoing movement due to entering Locked mode.");
                     }
                    // Reset stored view if explicitly locked? Depends on desired behavior.
                    // Let's keep it simple: only reset when a return journey is *initiated* by state change,
                    // not by explicitly locking.
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
        /// Handles potential destruction of the camera object mid-coroutine.
        /// </summary>
        private IEnumerator MoveCameraCoroutine(Vector3 startPosition, Quaternion startRotation, Vector3 targetPosition, Quaternion targetRotation, float duration, bool isReturnJourney)
        {
             // Capture references needed *inside* the loop in case the main references are cleared later.
             // Note: playerCameraTransform might become null if the camera object is destroyed.
             // Let's rely on the null check *inside* the loop.

            if (duration <= 0)
            {
                 Debug.LogWarning("CameraManager: Camera movement coroutine exiting early due to zero or negative duration.", this);
                 // Ensure the camera snaps to the target if duration is effectively zero
                 if (playerCameraTransform != null)
                 {
                      playerCameraTransform.position = targetPosition;
                      playerCameraTransform.rotation = targetRotation;
                      Debug.Log("CameraManager: Camera snapped to target due to zero duration.");
                 }
                 cameraMoveCoroutine = null; // Ensure coroutine reference is cleared
                 yield break;
            }


             Debug.Log($"CameraManager: Starting MoveCameraCoroutine. Duration: {duration}s, Return: {isReturnJourney}. From: {startPosition} to: {targetPosition}");


            float elapsed = 0f;

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

            // The camera mode was already set by HandleGameStateChanged (for return) or SetCameraMode (for forward)
             if (isReturnJourney)
             {
                 Debug.Log("CameraManager: Return journey coroutine finished. Camera mode is already MouseLook. Player body Y rotation was snapped back.");
                 // No additional action needed here as the mode and body rotation were handled before the coroutine started.
             }
             else
             {
                 Debug.Log("CameraManager: Forward cinematic coroutine finished. Camera mode is already CinematicView or Locked.");
             }
        }
    }
}