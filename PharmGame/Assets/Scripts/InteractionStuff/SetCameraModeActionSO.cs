using UnityEngine;
using Systems.Interaction; // Needed for InteractionResponse
using Systems.CameraControl; // Assuming CameraManager is here

namespace Systems.GameStates
{
    /// <summary>
    /// State action to set the CameraManager's mode.
    /// Can optionally provide a target view transform and duration for Cinematic mode.
    /// </summary>
    [CreateAssetMenu(menuName = "Game State/Actions/Set Camera Mode")]
    public class SetCameraModeActionSO : StateAction
    {
        [Tooltip("The target camera mode.")]
        public CameraManager.CameraMode targetMode;

        [Tooltip("The target view transform (used for CinematicView mode). Can be null for other modes.")]
        public Transform cinematicTargetView;

        [Tooltip("The duration for cinematic camera movement (used for CinematicView mode).")]
        public float cinematicMoveDuration = 0.5f;

        public override void Execute(InteractionResponse response, MenuManager manager)
        {
            if (CameraManager.Instance != null)
            {
                // Pass parameters based on the target mode
                if (targetMode == CameraManager.CameraMode.CinematicView)
                {
                    // For CinematicView, try to get the target view and duration from the response
                    // if available (e.g., from EnterComputerResponse, StartMinigameResponse)
                    // Otherwise, use the values configured on the SO.

                    Transform viewFromResponse = null;
                    float durationFromResponse = cinematicMoveDuration; // Default to SO value

                    if (response is EnterComputerResponse computerResponse)
                    {
                        viewFromResponse = computerResponse.CameraTargetView;
                        durationFromResponse = computerResponse.CameraMoveDuration;
                    }
                    else if (response is StartMinigameResponse minigameResponse)
                    {
                        viewFromResponse = minigameResponse.CameraTargetView;
                        durationFromResponse = minigameResponse.CameraMoveDuration;
                    }

                    // Use the response value if found, otherwise use the SO value
                    Transform finalTargetView = viewFromResponse != null ? viewFromResponse : cinematicTargetView;
                    float finalDuration = durationFromResponse; // Use duration from response if available

                    if (finalTargetView == null)
                    {
                         Debug.LogWarning($"SetCameraModeActionSO: CinematicView mode requires a target view, but none found from response or SO configuration.");
                         // Consider setting to Locked or MouseLook if target is null?
                         CameraManager.Instance.SetCameraMode(CameraManager.CameraMode.Locked);
                    }
                    else
                    {
                         CameraManager.Instance.SetCameraMode(targetMode, finalTargetView, finalDuration);
                         // Debug.Log($"SetCameraModeAction: Camera mode set to {targetMode} targeting {finalTargetView.name} over {finalDuration}s."); // Optional debug
                    }
                }
                else
                {
                    // Other modes (MouseLook, Locked) don't need target view or duration
                    CameraManager.Instance.SetCameraMode(targetMode);
                    // Debug.Log($"SetCameraModeAction: Camera mode set to {targetMode}."); // Optional debug
                }
            }
            else
            {
                Debug.LogError("SetCameraModeActionSO: CameraManager Instance is null! Cannot set camera mode.");
            }
        }
    }
}