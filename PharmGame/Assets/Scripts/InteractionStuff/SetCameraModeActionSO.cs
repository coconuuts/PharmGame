// Systems/GameStates/SetCameraModeActionSO.cs
using UnityEngine;
using Systems.Interaction; // Needed for InteractionResponse types
using Systems.CameraControl; // Assuming CameraManager is here
using Systems.CraftingMinigames; // Needed for StartCraftingMinigameResponse
using Systems.Minigame; // ADDED: Needed for StartMinigameResponse


namespace Systems.GameStates
{
    /// <summary>
    /// State action to set the CameraManager's mode.
    /// Can optionally provide a target view transform and duration for Cinematic mode.
    /// Prioritizes data from specific InteractionResponse types.
    /// </summary>
    [CreateAssetMenu(menuName = "Game State/Actions/Set Camera Mode")]
    public class SetCameraModeActionSO : StateAction
    {
        [Tooltip("The target camera mode.")]
        public CameraManager.CameraMode targetMode;

        [Tooltip("The target view transform (used for CinematicView mode if no response provides one).")]
        public Transform cinematicTargetView; // Default for the SO

        [Tooltip("The duration for cinematic camera movement (used for CinematicView mode if no response provides one).")]
        public float cinematicMoveDuration = 0.5f; // Default for the SO

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

                    // Check specific response types for camera data
                    if (response is EnterComputerResponse computerResponse)
                    {
                        viewFromResponse = computerResponse.CameraTargetView;
                        durationFromResponse = computerResponse.CameraMoveDuration;
                        Debug.Log($"SetCameraModeActionSO: Found Computer response data.");
                    }
                    // --- MODIFIED: Use new property names and type for general minigame response ---
                    else if (response is StartMinigameResponse generalMinigameResponse)
                    {
                        viewFromResponse = generalMinigameResponse.InitialCameraTarget;
                        durationFromResponse = generalMinigameResponse.InitialCameraDuration;
                        Debug.Log($"SetCameraModeActionSO: Found General Minigame response data.");
                    }
                    // --- Use consistent property names for crafting minigame response ---
                    else if (response is StartCraftingMinigameResponse craftingMinigameResponse)
                    {
                        viewFromResponse = craftingMinigameResponse.InitialCameraTarget;
                        durationFromResponse = craftingMinigameResponse.InitialCameraDuration;
                        Debug.Log($"SetCameraModeActionSO: Found Crafting Minigame response data.");
                    }
                    // ----------------------------------------------------------------------


                    // Use the response value if found, otherwise use the SO value
                    Transform finalTargetView = viewFromResponse != null ? viewFromResponse : cinematicTargetView;
                    // Ensure duration is also taken from response if view is
                    float finalDuration = viewFromResponse != null ? durationFromResponse : cinematicMoveDuration;

                    if (finalTargetView == null)
                    {
                         Debug.LogWarning($"SetCameraModeActionSO: CinematicView mode requires a target view, but none found from response or SO configuration. Setting camera mode to Locked.", this);
                         // Fallback: Set to Locked mode if no target view is provided via response or SO
                         CameraManager.Instance.SetCameraMode(CameraManager.CameraMode.Locked);
                    }
                    else
                    {
                         CameraManager.Instance.SetCameraMode(targetMode, finalTargetView, finalDuration);
                         Debug.Log($"SetCameraModeAction: Camera mode set to {targetMode} targeting '{finalTargetView.name}' over {finalDuration}s.", this);
                    }
                }
                else
                {
                    // Other modes (MouseLook, Locked) don't need target view or duration
                    CameraManager.Instance.SetCameraMode(targetMode);
                    Debug.Log($"SetCameraModeAction: Camera mode set to {targetMode}.");
                }
            }
            else
            {
                Debug.LogError("SetCameraModeActionSO: CameraManager Instance is null! Cannot set camera mode.", this);
            }
        }
    }
}