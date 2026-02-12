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

        [Tooltip("If the camera needs to move less than this distance, the transition is instant.")]
        [SerializeField] private float movementThreshold = 0.05f;
        [Tooltip("If the camera needs to rotate less than this angle, the transition is instant.")]
        [SerializeField] private float rotationThreshold = 1.0f;


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
            if ((oldState == Systems.GameStates.MenuManager.GameState.InComputer ||
                 oldState == Systems.GameStates.MenuManager.GameState.InMinigame ||
                 oldState == Systems.GameStates.MenuManager.GameState.InCrafting) &&
                newState == Systems.GameStates.MenuManager.GameState.Playing &&
                hasStoredPlayerView && // Check the flag
                playerBodyTransform != null) // Ensure player body reference is valid for calculation
            {
                Debug.Log($"CameraManager: >>> Initiating Automatic Return Journey <<<");

                // Set mode directly to MouseLook here.
                currentMode = CameraMode.MouseLook;

                // Stop any existing movement
                if (cameraMoveCoroutine != null)
                {
                    StopCoroutine(cameraMoveCoroutine);
                    cameraMoveCoroutine = null;
                }

                // --- Calculate the target world position/rotation ---
                
                // 1. Calculate the target rotations first
                Quaternion playerBodyTargetRotation = Quaternion.Euler(0f, storedPlayerBodyYRotation, 0f);
                Quaternion cameraLocalTargetRotation = Quaternion.Euler(storedPlayerCamXRotation, 0f, 0f);
                Quaternion returnTargetRotation = playerBodyTargetRotation * cameraLocalTargetRotation;

                // 2. [FIX] Use the playerBodyTargetRotation (the intended rotation) to calculate the position offset.
                // Previously, this used playerBodyTransform.rotation (current rotation), which caused an offset bug
                // if the player had rotated to face the computer/screen.
                Vector3 returnTargetPosition = playerBodyTransform.position + playerBodyTargetRotation * storedPlayerCamLocalPosition;

                // --- NEW: Check if movement is negligible ---
                float dist = Vector3.Distance(playerCameraTransform.position, returnTargetPosition);
                float angle = Quaternion.Angle(playerCameraTransform.rotation, returnTargetRotation);

                if (dist < movementThreshold && angle < rotationThreshold)
                {
                    // Snap immediately and DO NOT start coroutine
                    playerBodyTransform.rotation = playerBodyTargetRotation;
                    playerCameraTransform.position = returnTargetPosition;
                    playerCameraTransform.rotation = returnTargetRotation;

                    Debug.Log($"CameraManager: Return journey skipped (Distance: {dist:F4}, Angle: {angle:F4}). Snapped immediately.");

                    // Reset flags
                    hasStoredPlayerView = false;
                    isStoringView = false;

                    // Since coroutine is null and mode is MouseLook, control is instant
                    return;
                }

                // If we are here, significant movement is needed.
                Debug.Log($"CameraManager: Starting return coroutine. Distance: {dist:F4}, Angle: {angle:F4}");

                playerBodyTransform.rotation = playerBodyTargetRotation;
                Debug.Log($"CameraManager: Snapped player body Y rotation back to stored value: {storedPlayerBodyYRotation}");

                cameraMoveCoroutine = StartCoroutine(
                    MoveCameraCoroutine(
                        playerCameraTransform.position,
                        playerCameraTransform.rotation,
                        returnTargetPosition,
                        returnTargetRotation,
                        returnMoveDuration,
                        true
                    )
                );

                hasStoredPlayerView = false;
                isStoringView = false;
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

            // This prevents input while transitioning. 
            // By keeping cameraMoveCoroutine null for negligible moves, we bypass this lock.
            if (cameraMoveCoroutine != null) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, verticalRotationLimits.x, verticalRotationLimits.y);
            playerCameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

            playerBodyTransform.Rotate(Vector3.up * mouseX);
        }


        public void SetCameraMode(CameraMode mode, Transform targetView = null, float duration = 0.5f)
        {
            Debug.Log($"CameraManager.SetCameraMode called: currentMode = {currentMode}, requested mode = {mode}.");

            CameraMode oldMode = currentMode;

            if (mode == CameraMode.MouseLook && cameraMoveCoroutine != null && currentMode == CameraMode.MouseLook)
            {
                Debug.LogWarning("CameraManager: Ignoring SetCameraMode(MouseLook) call because return coroutine is active.");
                return;
            }

            if (currentMode == mode && mode != CameraMode.CinematicView)
            {
                return;
            }

            if (currentMode == CameraMode.CinematicView && cameraMoveCoroutine != null)
            {
                StopCoroutine(cameraMoveCoroutine);
                cameraMoveCoroutine = null;
            }
            else if (mode == CameraMode.CinematicView && cameraMoveCoroutine != null)
            {
                StopCoroutine(cameraMoveCoroutine);
                cameraMoveCoroutine = null;
            }

            // Store view logic
            if (oldMode == CameraMode.MouseLook && mode != CameraMode.MouseLook && playerCameraTransform != null && playerBodyTransform != null && !hasStoredPlayerView && !isStoringView)
            {
                isStoringView = true;
                storedPlayerCamLocalPosition = playerCameraTransform.localPosition;
                storedPlayerCamXRotation = xRotation;
                storedPlayerBodyYRotation = playerBodyTransform.rotation.eulerAngles.y;
                hasStoredPlayerView = true;
                Debug.Log($"CameraManager: Stored player view.");
            }

            currentMode = mode;

            switch (currentMode)
            {
                case CameraMode.MouseLook:
                    break;

                case CameraMode.CinematicView:
                    if (targetView == null)
                    {
                        if (oldMode == CameraMode.MouseLook && hasStoredPlayerView && playerBodyTransform != null)
                        {
                            // Fallback logic for return (using checks for instant snap if needed)
                            
                            // [FIX] Calculate rotation first, then use it for position
                            Quaternion returnBodyRot = Quaternion.Euler(0f, storedPlayerBodyYRotation, 0f);
                            Vector3 returnPos = playerBodyTransform.position + returnBodyRot * storedPlayerCamLocalPosition;
                            Quaternion returnRot = returnBodyRot * Quaternion.Euler(storedPlayerCamXRotation, 0f, 0f);

                            cameraMoveCoroutine = StartCoroutine(MoveCameraCoroutine(playerCameraTransform.position, playerCameraTransform.rotation, returnPos, returnRot, returnMoveDuration, true));

                            hasStoredPlayerView = false;
                            isStoringView = false;
                            currentMode = CameraMode.MouseLook;
                        }
                        else
                        {
                            SetCameraMode(CameraMode.Locked);
                        }
                        return;
                    }

                    // --- NEW: Check if movement is negligible for Forward Cinematic ---
                    float dist = Vector3.Distance(playerCameraTransform.position, targetView.position);
                    float angle = Quaternion.Angle(playerCameraTransform.rotation, targetView.rotation);

                    if (dist < movementThreshold && angle < rotationThreshold)
                    {
                        // Already at target (or close enough)
                        playerCameraTransform.position = targetView.position;
                        playerCameraTransform.rotation = targetView.rotation;
                        cameraMoveCoroutine = null; // Ensure no coroutine
                        Debug.Log($"CameraManager: Cinematic move skipped (already at target). Mode is {currentMode}.");
                    }
                    else
                    {
                        cameraMoveCoroutine = StartCoroutine(
                            MoveCameraCoroutine(
                                playerCameraTransform.position,
                                playerCameraTransform.rotation,
                                targetView.position,
                                targetView.rotation,
                                duration,
                                false
                            )
                        );
                    }
                    break;

                case CameraMode.Locked:
                    if (cameraMoveCoroutine != null)
                    {
                        StopCoroutine(cameraMoveCoroutine);
                        cameraMoveCoroutine = null;
                    }
                    break;
            }

            if (oldMode == CameraMode.MouseLook && mode != CameraMode.MouseLook)
            {
                isStoringView = false;
            }
        }

        private IEnumerator MoveCameraCoroutine(Vector3 startPosition, Quaternion startRotation, Vector3 targetPosition, Quaternion targetRotation, float duration, bool isReturnJourney)
        {
            if (duration <= 0)
            {
                if (playerCameraTransform != null)
                {
                    playerCameraTransform.position = targetPosition;
                    playerCameraTransform.rotation = targetRotation;
                }
                cameraMoveCoroutine = null;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (playerCameraTransform == null)
                {
                    cameraMoveCoroutine = null;
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Optional: Use a smoothstep or ease for nicer movement
                // t = t * t * (3f - 2f * t); 

                playerCameraTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
                playerCameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

                yield return null;
            }

            if (playerCameraTransform != null)
            {
                playerCameraTransform.position = targetPosition;
                playerCameraTransform.rotation = targetRotation;
            }

            cameraMoveCoroutine = null;
        }
    }
}