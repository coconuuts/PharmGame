using UnityEngine;
using System.Collections;
using Systems.Interaction; // Needed if state actions pass response directly

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
        [SerializeField] private Transform playerBodyTransform;

        [Header("Mouse Look Settings")]
        [Tooltip("Mouse sensitivity for camera rotation.")]
        [SerializeField] public float mouseSensitivity = 200f;
        [Tooltip("Vertical rotation limits (min X, max X).")]
        [SerializeField] private Vector2 verticalRotationLimits = new Vector2(-75f, 60f);

        private CameraMode currentMode = CameraMode.Locked;

        private float xRotation = 0f; // Current vertical rotation
        // Horizontal rotation is handled by rotating the player body

        // For CinematicView mode
        private Vector3 cinematicTargetPosition;
        private Quaternion cinematicTargetRotation;
        private float cinematicMoveDuration;
        private Coroutine cameraMoveCoroutine;

        // --- ADDED FIELDS TO STORE PLAYER CAMERA'S VIEW BEFORE CINEMATIC ---
        private Vector3 storedPlayerCamPosition;
        private Quaternion storedPlayerCamRotation;
        // --------------------------------------------------------------------


        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Debug.LogWarning("CameraManager: Duplicate instance found. Destroying this one.", this); Destroy(gameObject); return; }
            Debug.Log("CameraManager: Awake completed.");

            if (playerCameraTransform == null)
            {
                 GameObject cameraObject = GameObject.FindGameObjectWithTag("MainCamera");
                 if (cameraObject != null) playerCameraTransform = cameraObject.transform;
                 else Debug.LogError("CameraManager: Player Camera Transform not assigned and 'MainCamera' tag not found!", this);
            }

            if (playerBodyTransform == null && playerCameraTransform != null && playerCameraTransform.parent != null)
            {
                playerBodyTransform = playerCameraTransform.parent;
                 Debug.LogWarning("CameraManager: Player Body Transform not assigned. Assuming camera parent is the body.", this);
            }

            if (playerCameraTransform != null && playerBodyTransform != null)
            {
                // Initialize rotation based on current camera orientation relative to body
                 Vector3 eulerRotation = playerCameraTransform.localEulerAngles;
                xRotation = eulerRotation.x;
                if (xRotation > 180) xRotation -= 360;

                // storedPlayerCamPosition and Rotation will be set just before first cinematic view
                 // No need to initialize here explicitly unless you need the initial state immediately accessible.

            }
            else
            {
                Debug.LogError("CameraManager: Essential camera references (Camera/Body) are null. Camera control disabled.", this);
                enabled = false;
            }

            // Start in Locked mode by default until MenuManager sets the initial state
             currentMode = CameraMode.Locked;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
                    // Camera movement is handled by the coroutine
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

            // Apply vertical rotation to the camera (local X axis)
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, verticalRotationLimits.x, verticalRotationLimits.y);
            playerCameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

            // Apply horizontal rotation to the player body (Y axis)
            playerBodyTransform.Rotate(Vector3.up * mouseX);
        }


        // --- Public Method to Set Camera Mode ---
        /// <summary>
        /// Sets the current camera behavior mode.
        /// Handles transitions between modes (e.g., stopping mouse look for cinematic).
        /// Initiates movement coroutines for CinematicView and transitions back to MouseLook.
        /// </summary>
        /// <param name="mode">The CameraMode to switch to.</param>
        /// <param name="targetView">Optional: The Transform for CinematicView mode.</param>
        /// <param name="duration">Optional: The duration for CinematicView movement.</param>
        public void SetCameraMode(CameraMode mode, Transform targetView = null, float duration = 0.5f)
        {
            if (currentMode == mode)
            {
                // Debug.Log($"CameraManager: Already in mode {mode}. Ignoring SetCameraMode call."); // Optional: less verbose
                return; // Already in the desired mode
            }

            Debug.Log($"CameraManager: Switching camera mode from {currentMode} to {mode}.");

            // --- Handle Exit Logic for the current mode ---
            switch (currentMode)
            {
                case CameraMode.MouseLook:
                    // When exiting MouseLook to go to Cinematic, store the current player camera view
                    if (mode == CameraMode.CinematicView && playerCameraTransform != null)
                    {
                         storedPlayerCamPosition = playerCameraTransform.position;
                         storedPlayerCamRotation = playerCameraTransform.rotation;
                         Debug.Log("CameraManager: Stored player camera position/rotation for return.");
                    }
                     // Input handling stops automatically in Update when mode changes
                    break;
                case CameraMode.CinematicView:
                    // Stop any ongoing cinematic movement
                    if (cameraMoveCoroutine != null)
                    {
                        StopCoroutine(cameraMoveCoroutine);
                        Debug.Log("CameraManager: Stopped ongoing cinematic coroutine.");
                        cameraMoveCoroutine = null;
                    }
                     // When exiting cinematic, the camera is left at the cinematic target position/rotation.
                     // The entry logic for the new mode will take over from there.
                    break;
                case CameraMode.Locked:
                    // Nothing specific to do when exiting locked mode
                    break;
            }

            // Set the new mode (temporarily Locked during return transition)
             CameraMode nextMode = mode; // Store the intended next mode
             if (currentMode == CameraMode.CinematicView && mode == CameraMode.MouseLook)
             {
                  // If transitioning from Cinematic back to MouseLook, temporarily set mode to Locked during the return movement
                  currentMode = CameraMode.Locked; // Camera is static during interpolation
                  Debug.Log("CameraManager: Temporarily setting mode to Locked for return transition.");
             }
             else
             {
                  currentMode = mode; // For other transitions, set mode immediately
             }


            // --- Handle Entry Logic for the new mode ---
            switch (nextMode) // Check against the intended next mode
            {
                case CameraMode.MouseLook:
                     // If transitioning back from Cinematic, start the return movement
                     if (currentMode == CameraMode.Locked && previousMode == CameraMode.CinematicView) // Check if the temporary Locked mode was set for a return
                     {
                          Debug.Log("CameraManager: Entering MouseLook from Cinematic exit. Starting return movement.");
                          // Start the return movement coroutine back to the stored player view
                          cameraMoveCoroutine = StartCoroutine(
                              MoveCameraCoroutine(
                                  playerCameraTransform.position,     // Start from current camera position (cinematic target)
                                  playerCameraTransform.rotation,     // Start from current camera rotation
                                  storedPlayerCamPosition,            // Target position (stored player view)
                                  storedPlayerCamRotation,            // Target rotation (stored player view)
                                  duration,                           // Duration for return movement
                                  true                                // Indicate this is a return journey
                              )
                          );
                          // The actual mode switch to MouseLook will happen at the end of the coroutine
                     }
                     else
                     {
                         // Normal entry to MouseLook (e.g., from Locked or Playing initial)
                         Debug.Log("CameraManager: Entered MouseLook mode (normal).");
                         // Mouse input handling starts automatically in Update.
                         // Camera is already at the position/rotation where the previous mode left it.
                     }
                    break;
                case CameraMode.CinematicView:
                     if (targetView == null)
                     {
                         Debug.LogError("CameraManager: SetCameraMode(CinematicView) called but targetView is null!", this);
                         SetCameraMode(CameraMode.MouseLook); // Revert to MouseLook on error
                         return;
                     }
                    // Store target data and start movement coroutine FORWARD
                    cinematicTargetPosition = targetView.position;
                    cinematicTargetRotation = targetView.rotation;
                    cinematicMoveDuration = duration;

                    cameraMoveCoroutine = StartCoroutine(
                         MoveCameraCoroutine(
                            playerCameraTransform.position,     // Start from current camera position
                            playerCameraTransform.rotation,     // Start from current camera rotation
                            cinematicTargetPosition,
                            cinematicTargetRotation,
                            cinematicMoveDuration,
                            false                               // Indicate this is a forward journey (to cinematic target)
                         )
                     );
                     Debug.Log($"CameraManager: Entered CinematicView mode. Moving to {targetView.gameObject.name} over {duration}s.", targetView.gameObject);
                    break;
                case CameraMode.Locked:
                    // Camera Update loop will do nothing.
                     Debug.Log("CameraManager: Entered Locked mode.");
                    break;
            }
             // Need to store the previous mode to check for Cinematic->MouseLook transition
             previousMode = currentMode; // Store mode AFTER setting it potentially to Locked
        }

        // --- Camera Movement Coroutine ---
        /// <summary>
        /// Moves the player camera smoothly between two points.
        /// </summary>
        /// <param name="startPosition">The world position to start from.</param>
        /// <param name="startRotation">The world rotation to start from.</param>
        /// <param name="targetPosition">The world position to move to.</param>
        /// <param name="targetRotation">The world rotation to move to.</param>
        /// <param name="duration">The duration of the movement.</param>
        /// <param name="isReturnJourney">True if this is a movement back to the player's original view.</param> // ADDED PARAM
        private IEnumerator MoveCameraCoroutine(Vector3 startPosition, Quaternion startRotation, Vector3 targetPosition, Quaternion targetRotation, float duration, bool isReturnJourney) // ADDED PARAM
        {
            if (playerCameraTransform == null || duration <= 0) yield break;

            float elapsed = 0f;

            // Ensure we start from the *actual* current position/rotation of the camera, not just the stored start
            // (in case the camera was manually moved or snap-changed)
            Vector3 actualStartPosition = playerCameraTransform.position;
            Quaternion actualStartRotation = playerCameraTransform.rotation;


            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                playerCameraTransform.position = Vector3.Lerp(actualStartPosition, targetPosition, t);
                playerCameraTransform.rotation = Quaternion.Slerp(actualStartRotation, targetRotation, t);

                yield return null;
            }

            // Ensure the camera reaches the exact target position and rotation
            playerCameraTransform.position = targetPosition;
            playerCameraTransform.rotation = targetRotation;

            cameraMoveCoroutine = null;
             Debug.Log("CameraManager: Camera movement coroutine finished.");

            // --- Handle completion logic ---
             if (isReturnJourney) // If this was a return journey
             {
                 Debug.Log("CameraManager: Return journey finished. Setting mode back to MouseLook.");
                 currentMode = CameraMode.MouseLook; // Set mode to MouseLook after returning
                 // Mouse input handling will resume in Update
             }
             // For non-return journeys (to cinematic target), the mode is already CinematicView, which is correct.
            // -----------------------------
        }

        // Need to store the previous mode to handle the MouseLook transition from Cinematic
        private CameraMode previousMode; // ADDED FIELD


        // TODO: Add methods for transitioning back to MouseLook from Locked mode (e.g., if locking was temporary)

    }
}