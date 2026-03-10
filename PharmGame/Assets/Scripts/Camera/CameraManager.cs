using UnityEngine;
using System.Collections;
using Systems.Interaction; 
using Systems.GameStates; 

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

        [Header("Cinematic Settings")]
        [Tooltip("The duration for the automatic camera return movement after exiting a cinematic state.")]
        [SerializeField] private float returnMoveDuration = 0.25f; 

        [Tooltip("If the camera needs to move less than this distance, the transition is instant.")]
        [SerializeField] private float movementThreshold = 0.05f;
        [Tooltip("If the camera needs to rotate less than this angle, the transition is instant.")]
        [SerializeField] private float rotationThreshold = 1.0f;

        [Header("Head Bobbing Settings")]
        [Tooltip("Enable or disable camera bobbing.")]
        public bool enableHeadBob = true;
        [Tooltip("Reference to the player's movement script to read speed and state.")]
        [SerializeField] private Systems.Player.PlayerMovement playerMovement;
        public float walkBobFrequency = 14f;
        public float walkBobAmplitude = 0.05f;
        public float sprintBobFrequency = 18f;
        public float sprintBobAmplitude = 0.1f;
        public float bobSmoothing = 10f;


        private CameraMode currentMode = CameraMode.Locked; 
        private float xRotation = 0f; 
        private Coroutine cameraMoveCoroutine;

        // --- Fields to store player camera's view ---
        private Vector3 storedPlayerCamLocalPosition; 
        private float storedPlayerCamXRotation;       
        private float storedPlayerBodyYRotation;      
        private bool hasStoredPlayerView = false;
        private bool isStoringView = false;
        private float bobTimer = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            if (playerCameraTransform == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null) playerCameraTransform = mainCamera.transform;
            }

            if (playerBodyTransform == null && playerCameraTransform != null && playerCameraTransform.parent != null)
            {
                playerBodyTransform = playerCameraTransform.parent;
            }

            if (playerCameraTransform != null && playerBodyTransform != null)
            {
                Vector3 eulerRotation = playerCameraTransform.localEulerAngles;
                xRotation = eulerRotation.x;
                if (xRotation > 180) xRotation -= 360; 

                storedPlayerCamLocalPosition = playerCameraTransform.localPosition;
                storedPlayerCamXRotation = xRotation;
                storedPlayerBodyYRotation = playerBodyTransform.rotation.eulerAngles.y;
            }

            currentMode = CameraMode.Locked; 
            Cursor.lockState = CursorLockMode.None; 
        }

        private void OnEnable()
        {
            Systems.GameStates.MenuManager.OnStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            Systems.GameStates.MenuManager.OnStateChanged -= HandleGameStateChanged;
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

        public float GetPitch() { return xRotation; }

        public void SetPitch(float pitch)
        {
            xRotation = Mathf.Clamp(pitch, verticalRotationLimits.x, verticalRotationLimits.y);
            if (playerCameraTransform != null)
            {
                playerCameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            }
        }

        private void HandleGameStateChanged(Systems.GameStates.MenuManager.GameState newState, Systems.GameStates.MenuManager.GameState oldState, InteractionResponse response)
        {
            if ((oldState == Systems.GameStates.MenuManager.GameState.InComputer ||
                 oldState == Systems.GameStates.MenuManager.GameState.InMinigame ||
                 oldState == Systems.GameStates.MenuManager.GameState.InCrafting) &&
                newState == Systems.GameStates.MenuManager.GameState.Playing &&
                hasStoredPlayerView && 
                playerBodyTransform != null) 
            {
                currentMode = CameraMode.MouseLook;

                if (cameraMoveCoroutine != null)
                {
                    StopCoroutine(cameraMoveCoroutine);
                    cameraMoveCoroutine = null;
                }

                Quaternion playerBodyTargetRotation = Quaternion.Euler(0f, storedPlayerBodyYRotation, 0f);
                Quaternion cameraLocalTargetRotation = Quaternion.Euler(storedPlayerCamXRotation, 0f, 0f);
                Quaternion returnTargetRotation = playerBodyTargetRotation * cameraLocalTargetRotation;
                Vector3 returnTargetPosition = playerBodyTransform.position + playerBodyTargetRotation * storedPlayerCamLocalPosition;

                float dist = Vector3.Distance(playerCameraTransform.position, returnTargetPosition);
                float angle = Quaternion.Angle(playerCameraTransform.rotation, returnTargetRotation);

                if (dist < movementThreshold && angle < rotationThreshold)
                {
                    playerBodyTransform.rotation = playerBodyTargetRotation;
                    playerCameraTransform.position = returnTargetPosition;
                    playerCameraTransform.rotation = returnTargetRotation;

                    hasStoredPlayerView = false;
                    isStoringView = false;
                    return;
                }

                playerBodyTransform.rotation = playerBodyTargetRotation;

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
                    HandleHeadBob(); // Triggers bobbing logic
                    break;
                case CameraMode.CinematicView:
                case CameraMode.Locked:
                    break;
            }
        }

        private void HandleMouseLookInput()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (cameraMoveCoroutine != null) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, verticalRotationLimits.x, verticalRotationLimits.y);
            playerCameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

            playerBodyTransform.Rotate(Vector3.up * mouseX);
        }

        private void HandleHeadBob()
        {
            if (!enableHeadBob || playerCameraTransform == null || playerMovement == null) return;
            if (cameraMoveCoroutine != null) return; // Prevent bobbing during camera transitions

            // Calculate the player's lateral speed
            Vector3 planarVelocity = new Vector3(playerMovement.controller.velocity.x, 0, playerMovement.controller.velocity.z);
            bool isMoving = planarVelocity.magnitude > 0.1f;

            if (isMoving && playerMovement.isGrounded)
            {
                float frequency = playerMovement.isSprinting ? sprintBobFrequency : walkBobFrequency;
                float amplitude = playerMovement.isSprinting ? sprintBobAmplitude : walkBobAmplitude;

                bobTimer += Time.deltaTime * frequency;
                
                // Creates a figure-8 motion using Cosine for X and Sine for Y
                float targetY = storedPlayerCamLocalPosition.y + Mathf.Sin(bobTimer) * amplitude;
                float targetX = storedPlayerCamLocalPosition.x + Mathf.Cos(bobTimer / 2f) * amplitude * 0.5f;

                Vector3 targetPos = new Vector3(targetX, targetY, storedPlayerCamLocalPosition.z);
                playerCameraTransform.localPosition = Vector3.Lerp(playerCameraTransform.localPosition, targetPos, Time.deltaTime * bobSmoothing);
            }
            else
            {
                // Smoothly return to the default stored view when the player stops
                bobTimer = 0f;
                playerCameraTransform.localPosition = Vector3.Lerp(playerCameraTransform.localPosition, storedPlayerCamLocalPosition, Time.deltaTime * bobSmoothing);
            }
        }

        public void SetCameraMode(CameraMode mode, Transform targetView = null, float duration = 0.5f)
        {
            CameraMode oldMode = currentMode;

            if (mode == CameraMode.MouseLook && cameraMoveCoroutine != null && currentMode == CameraMode.MouseLook) return;
            if (currentMode == mode && mode != CameraMode.CinematicView) return;

            if (cameraMoveCoroutine != null)
            {
                StopCoroutine(cameraMoveCoroutine);
                cameraMoveCoroutine = null;
            }

            if (oldMode == CameraMode.MouseLook && mode != CameraMode.MouseLook && playerCameraTransform != null && playerBodyTransform != null && !hasStoredPlayerView && !isStoringView)
            {
                isStoringView = true;
                storedPlayerCamLocalPosition = playerCameraTransform.localPosition;
                storedPlayerCamXRotation = xRotation;
                storedPlayerBodyYRotation = playerBodyTransform.rotation.eulerAngles.y;
                hasStoredPlayerView = true;
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
                            Quaternion returnBodyRot = Quaternion.Euler(0f, storedPlayerBodyYRotation, 0f);
                            Vector3 returnPos = playerBodyTransform.position + returnBodyRot * storedPlayerCamLocalPosition;
                            Quaternion returnRot = returnBodyRot * Quaternion.Euler(storedPlayerCamXRotation, 0f, 0f);

                            cameraMoveCoroutine = StartCoroutine(MoveCameraCoroutine(playerCameraTransform.position, playerCameraTransform.rotation, returnPos, returnRot, returnMoveDuration, true));

                            hasStoredPlayerView = false;
                            isStoringView = false;
                            currentMode = CameraMode.MouseLook;
                        }
                        else { SetCameraMode(CameraMode.Locked); }
                        return;
                    }

                    float dist = Vector3.Distance(playerCameraTransform.position, targetView.position);
                    float angle = Quaternion.Angle(playerCameraTransform.rotation, targetView.rotation);

                    if (dist < movementThreshold && angle < rotationThreshold)
                    {
                        playerCameraTransform.position = targetView.position;
                        playerCameraTransform.rotation = targetView.rotation;
                        cameraMoveCoroutine = null; 
                    }
                    else
                    {
                        cameraMoveCoroutine = StartCoroutine(MoveCameraCoroutine(playerCameraTransform.position, playerCameraTransform.rotation, targetView.position, targetView.rotation, duration, false));
                    }
                    break;

                case CameraMode.Locked:
                    break;
            }

            if (oldMode == CameraMode.MouseLook && mode != CameraMode.MouseLook) isStoringView = false;
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